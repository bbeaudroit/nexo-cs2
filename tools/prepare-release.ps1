$ErrorActionPreference='Stop'
$nexoRoot=Split-Path -Parent $PSScriptRoot
$releaseVersion=(Get-Content -LiteralPath (Join-Path $nexoRoot 'desktop-ui\package.json') -Raw | ConvertFrom-Json).version
$stage=Join-Path $nexoRoot 'release-stage';New-Item -ItemType Directory -Path $stage -Force|Out-Null
$engine=Join-Path $stage 'engine-v7'
dotnet publish (Join-Path $nexoRoot 'esp\Esp.csproj') -c Release -r win-x64 --self-contained true -o $engine --nologo
if($LASTEXITCODE -ne 0){throw 'Native self-contained publish failed'}
$dump=Join-Path $stage 'dumps\manual-01';New-Item -ItemType Directory -Path $dump -Force|Out-Null
Copy-Item -Path (Join-Path $nexoRoot 'dumps\manual-01\*.json') -Destination $dump -Force
$tools=Join-Path $stage 'tools';New-Item -ItemType Directory -Path $tools -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $nexoRoot 'tools\cloudflared.exe') -Destination (Join-Path $tools 'cloudflared.exe') -Force
Copy-Item -LiteralPath (Join-Path $nexoRoot 'Preparar-Modelo.ps1'),(Join-Path $nexoRoot 'Preparar-Mapas.ps1') -Destination $stage -Force
[IO.File]::WriteAllText((Join-Path $stage 'nexo-install.json'),(@{format=1;channel='stable';version=$releaseVersion}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $stage 'FIRST-START.txt'),"Nexo $releaseVersion - Windows x64`r`n`r`n1. Start CS2, then open Nexo and choose your local name. Nexo waits for CS2 before opening.`r`n2. Start CS2 in windowed or borderless mode.`r`n3. Click Import from CS2 if the preview model is missing. Models and map overviews are imported from your own game files.`r`n4. System > Updates checks and downloads updates; Install and restart applies them. Your CFGs remain in %APPDATA%\Nexo.`r`n`r`nNo .NET installation is required for this self-contained release. No personal CFGs, profile names, webhook, license keys or extracted Valve assets are included.`r`n")
if(Test-Path -LiteralPath (Join-Path $stage 'esp-settings.json')){throw 'Release stage contains a mutable user settings file'}
Write-Output 'Release stage prepared without personal settings or extracted game assets.'

