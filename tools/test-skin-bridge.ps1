[CmdletBinding()]
param([switch]$ExecuteLocalPractice)
$ErrorActionPreference='Stop'
if([IntPtr]::Size-ne8){throw 'Use a 64-bit PowerShell host.'}
$taskRoot=Split-Path -Parent $PSScriptRoot
$artifact=Join-Path $taskRoot 'skin-bridge/build/Nexo.SkinBridge.Observation.dll'
$manifest=Get-Content -LiteralPath (Join-Path $taskRoot 'skin-bridge/observation-manifest.json') -Raw|ConvertFrom-Json
if($manifest.format-ne1-or$manifest.artifact-ne'Nexo.SkinBridge.Observation.dll'-or$manifest.abiVersion-ne1-or-not$manifest.observationOnly-or$manifest.skinMutationImplemented-or$manifest.optionsSize-ne16-or$manifest.observationSize-ne24-or$manifest.snapshotSize-ne320){throw 'Unreviewed observation manifest.'}
$artifactHash=(Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
if($artifactHash-ne$manifest.sha256){throw 'Artifact changed after review. Do not regenerate its approved hash automatically.'}
$games=@(Get-Process -Name cs2 -ErrorAction SilentlyContinue)
if($games.Count-ne1){throw 'Exactly one local CS2 instance is required.'}
$gameProcess=$games[0];$created=$gameProcess.StartTime.ToUniversalTime().ToFileTimeUtc()
$processInfo=Get-CimInstance Win32_Process -Filter ('ProcessId='+$gameProcess.Id)
$insecure=[bool]($processInfo.CommandLine-match'(?i)(?:^|\s)-insecure(?:\s|$)')
$modules=@($gameProcess.Modules)
$client=$modules|Where-Object ModuleName -eq 'client.dll'
$engine=$modules|Where-Object ModuleName -eq 'engine2.dll'
if(-not$client-or-not$engine){throw 'Client and engine modules are required.'}
$clientHash=(Get-FileHash -LiteralPath $client.FileName -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedClient='ccaf4e4926babc052994502b19ec61198a11008a39dce712921b93dd993be9f5'
if($clientHash-ne$expectedClient-or$manifest.expectedClientSHA256-ne$expectedClient-or$manifest.expectedClientBuild-ne14192){throw 'Client or reviewed build mismatch.'}
if(-not('SkinBridgeObservationLoader'-as[type])){Add-Type -Path (Join-Path $PSScriptRoot 'SkinBridgeObservationLoader.cs')}
$build=[SkinBridgeObservationLoader]::ReadBuild($gameProcess.Id,$created,$engine.BaseAddress.ToInt64(),0x6ba02c)
if($build-ne14192){throw 'Build14192 is required.'}
$serverLoaded=[bool]($modules.ModuleName-contains'server.dll')
$blocked=@();if(-not$insecure){$blocked+='CS2 was not launched with -insecure.'};if(-not$serverLoaded){$blocked+='No local server module was observed.'}
$alreadyLoaded=[bool]($modules.ModuleName-contains'Nexo.SkinBridge.Observation.dll')
if($alreadyLoaded){$blocked+='Observation DLL already loaded; restart local CS2 before retrying.'}
$preflight=[ordered]@{preflight=$true;executionRequested=[bool]$ExecuteLocalPractice;gameWritesPerformed=$false;skinMutationImplemented=$false;observationOnly=$true;artifactSHA256=$artifactHash;build=$build;insecureLaunch=$insecure;serverModuleLoaded=$serverLoaded;canExecuteLocalPractice=($blocked.Count-eq0);blockedReasons=$blocked}
$preflight|ConvertTo-Json -Depth 4
if(-not$ExecuteLocalPractice){return}
if($blocked.Count){throw ('Local observation test blocked: '+($blocked-join' '))}
# This switch is deliberately separate from preflight. The parent task must get
# approval to restart local practice and load the reviewed observation module.
# It does not grant approval by itself, and never requests an anti-cheat bypass.
$lockedArtifact=[IO.File]::Open($artifact,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try{
 if((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()-ne$artifactHash){throw 'Artifact changed before execution.'}
 [SkinBridgeObservationLoader]::Run($gameProcess.Id,$created,$artifact)
}finally{$lockedArtifact.Dispose()}
