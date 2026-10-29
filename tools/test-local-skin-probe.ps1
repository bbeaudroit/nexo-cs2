[CmdletBinding()]
param([ValidateSet('Preflight','Start','Poll','Restore','Shutdown')][string]$Action='Preflight',[ValidateSet('Original','VisualRestore','Current14193')][string]$Revision='Original',[switch]$ExecuteLocalPractice)
$ErrorActionPreference='Stop'
if([IntPtr]::Size-ne8){throw 'Use a fresh 64-bit PowerShell host.'}
$taskRoot=Split-Path -Parent $PSScriptRoot
$probeFolder=switch($Revision){'Original'{'skin-bridge/local-test'}'VisualRestore'{'skin-bridge/local-test-v2'}'Current14193'{'skin-bridge/local-test-14193'}}
$probeFile=switch($Revision){'Original'{'Nexo.SkinBridge.LocalTest.dll'}'VisualRestore'{'Nexo.SkinBridge.LocalTestV2.dll'}'Current14193'{'Nexo.SkinBridge.Local14193.dll'}}
$artifact=Join-Path $taskRoot ($probeFolder+'/build/'+$probeFile)
$manifest=Get-Content -LiteralPath (Join-Path $taskRoot ($probeFolder+'/reviewed-manifest.json')) -Raw|ConvertFrom-Json
if($manifest.format-ne1-or$manifest.artifact-ne$probeFile-or$manifest.abiVersion-ne1-or$manifest.snapshotSize-ne80-or$manifest.kit-ne1171-or-not$manifest.experimentalLocalOnly){throw 'Unreviewed local probe manifest.'}
$hash=(Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if($hash-ne$manifest.sha256){throw 'Local probe changed after review; execution is blocked.'}
$games=@(Get-Process -Name cs2 -ErrorAction SilentlyContinue);if($games.Count-ne1){throw 'Exactly one CS2 instance is required.'}
$gameProcess=$games[0];$created=$gameProcess.StartTime.ToUniversalTime().ToFileTimeUtc()
$info=Get-CimInstance Win32_Process -Filter ('ProcessId='+$gameProcess.Id)
$insecure=[bool]($info.CommandLine-match'(?i)(?:^|\s)-insecure(?:\s|$)')
$modules=@($gameProcess.Modules);$client=$modules|Where-Object ModuleName -eq client.dll;$engine=$modules|Where-Object ModuleName -eq engine2.dll
if(-not$client-or-not$engine){throw 'Expected game modules unavailable.'}
$expectedClient=if($Revision-eq'Current14193'){'08a741030c1e569d8b854c5886a87fcafc21d0d0e2f05dfd380e8a351171501d'}else{'ccaf4e4926babc052994502b19ec61198a11008a39dce712921b93dd993be9f5'}
$expectedBuild=if($Revision-eq'Current14193'){14193}else{14192}
$buildRva=if($Revision-eq'Current14193'){0x6ba028}else{0x6ba02c}
if((Get-FileHash -LiteralPath $client.FileName -Algorithm SHA256).Hash.ToLowerInvariant()-ne$expectedClient){throw 'Audited client hash mismatch.'}
if(-not('SkinBridgeObservationLoader'-as[type])){Add-Type -Path @((Join-Path $PSScriptRoot 'SkinBridgeObservationLoader.cs'),(Join-Path $PSScriptRoot 'LocalSkinProbeLoader.cs'))}
if(-not[SkinBridgeObservationLoader].GetMethod('RunLocalProbe')){throw 'Use a fresh PowerShell host for this local test.'}
$build=[SkinBridgeObservationLoader]::ReadBuild($gameProcess.Id,$created,$engine.BaseAddress.ToInt64(),$buildRva)
if($build-ne$expectedBuild-or$manifest.expectedClientBuild-ne$expectedBuild){throw 'Audited build/manifest mismatch.'}
$localServer=[bool]($modules.ModuleName-contains'server.dll')
[ordered]@{preflight=$true;action=$Action;revision=$Revision;executionRequested=[bool]$ExecuteLocalPractice;experimentalLocalOnly=$true;visualChangeVerified=$false;artifactSHA256=$hash;build=$build;insecureLaunch=$insecure;serverModuleLoaded=$localServer;canExecute=($insecure-and$localServer)}|ConvertTo-Json
if($Action-eq'Preflight'){return}
if(-not$ExecuteLocalPractice-or-not$insecure-or-not$localServer){throw 'Explicit local-test execution and a local -insecure session are required.'}
$lockedArtifact=[IO.File]::Open($artifact,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try{
 if((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()-ne$hash){throw 'Artifact changed before execution.'}
 [SkinBridgeObservationLoader]::RunLocalProbe($gameProcess.Id,$created,$artifact,$Action)
}finally{$lockedArtifact.Dispose()}
