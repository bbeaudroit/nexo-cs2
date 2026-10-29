param([string]$VpkPath)
$ErrorActionPreference='Stop'
$nexoRoot=$PSScriptRoot
if(!$VpkPath){
    $nexoLibraries=@('C:\SteamLibrary')
    $nexoSteam=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if($nexoSteam){$nexoLibraries+=$nexoSteam;$nexoVdf=Join-Path $nexoSteam 'steamapps\libraryfolders.vdf';if(Test-Path -LiteralPath $nexoVdf){$nexoContent=Get-Content -Raw -LiteralPath $nexoVdf;foreach($nexoMatch in [regex]::Matches($nexoContent,'"path"\s+"([^"]+)"')){$nexoLibraries+=$nexoMatch.Groups[1].Value.Replace('\\','\')}}}
    foreach($nexoLibrary in $nexoLibraries){$nexoCandidate=Join-Path $nexoLibrary 'steamapps\common\Counter-Strike Global Offensive\game\csgo\pak01_dir.vpk';if(Test-Path -LiteralPath $nexoCandidate){$VpkPath=$nexoCandidate;break}}
}
if(!$VpkPath -or !(Test-Path -LiteralPath $VpkPath)){throw 'No se encontró CS2. Ejecutá Preparar-Modelo.ps1 -VpkPath con la ruta de pak01_dir.vpk.'}
$nexoTools=Join-Path $nexoRoot 'tools\source2viewer-20.1';New-Item -ItemType Directory -Path $nexoTools -Force | Out-Null
$nexoZip=Join-Path $nexoRoot 'tools\source2viewer-20.1.zip'
$nexoExpected='67AAF8A67A85CA60FD09FF343E62103A4CDF3A2D177FC656231D7D734C5B3AA3'
if(!(Test-Path -LiteralPath $nexoZip)){Invoke-WebRequest -Uri 'https://github.com/ValveResourceFormat/ValveResourceFormat/releases/download/20.1/cli-windows-x64.zip' -OutFile $nexoZip}
if((Get-FileHash -LiteralPath $nexoZip -Algorithm SHA256).Hash -ne $nexoExpected){throw 'La descarga de Source 2 Viewer no pasó la verificación SHA-256.'}
Expand-Archive -LiteralPath $nexoZip -DestinationPath $nexoTools -Force
$nexoAssets=Join-Path $nexoRoot 'local-assets';New-Item -ItemType Directory -Path $nexoAssets -Force | Out-Null
& (Join-Path $nexoTools 'Source2Viewer-CLI.exe') -i $VpkPath --vpk_filepath 'agents/models/ctm_sas/ctm_sas.vmdl_c' -d --gltf_export_format glb --gltf_export_materials --gltf_export_animations --gltf_animation_list '__preview_bind_pose__' -o (Join-Path $nexoAssets 'sas.glb')
if($LASTEXITCODE -ne 0){throw 'No se pudo exportar el agente de CS2.'}
$nexoBytes=[IO.File]::ReadAllBytes((Join-Path $nexoAssets 'sas.glb'));$nexoJsonLength=[BitConverter]::ToInt32($nexoBytes,12);$nexoModel=[Text.Encoding]::UTF8.GetString($nexoBytes,20,$nexoJsonLength)|ConvertFrom-Json
$nexoFiles=@('sas.glb')
Add-Type -AssemblyName System.Drawing
foreach($nexoImage in $nexoModel.images){$nexoName=[string]$nexoImage.uri;if($nexoName -notmatch '^[a-zA-Z0-9_.-]+\.png$'){throw 'El exportador devolvió una ruta no válida.'};if($nexoFiles -contains $nexoName){continue};$nexoFiles+=$nexoName;$nexoImagePath=Join-Path $nexoAssets $nexoName
    $nexoSource=[Drawing.Image]::FromFile($nexoImagePath);$nexoResized=$null
    try{if($nexoSource.Width -gt 1024 -or $nexoSource.Height -gt 1024){$nexoRatio=[Math]::Min(1024.0/$nexoSource.Width,1024.0/$nexoSource.Height);$nexoResized=New-Object Drawing.Bitmap ([Math]::Max(1,[int]($nexoSource.Width*$nexoRatio))),([Math]::Max(1,[int]($nexoSource.Height*$nexoRatio)));$nexoGraphics=[Drawing.Graphics]::FromImage($nexoResized);try{$nexoGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$nexoGraphics.DrawImage($nexoSource,0,0,$nexoResized.Width,$nexoResized.Height)}finally{$nexoGraphics.Dispose()}}}finally{$nexoSource.Dispose()}
    if($nexoResized){try{$nexoResized.Save($nexoImagePath,[Drawing.Imaging.ImageFormat]::Png)}finally{$nexoResized.Dispose()}}
}
@{model='sas.glb';source='Local CS2 installation / SAS';redistribution=$false;files=$nexoFiles}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $nexoAssets 'manifest.json') -Encoding UTF8
Write-Output 'Modelo de CS2 preparado localmente. No se modificó la instalación del juego.'
if(Test-Path -LiteralPath (Join-Path $nexoRoot 'Preparar-Mapas.ps1')){& (Join-Path $nexoRoot 'Preparar-Mapas.ps1') -VpkPath $VpkPath}
