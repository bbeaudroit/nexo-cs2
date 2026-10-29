$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$settingsFile=Join-Path $repoRoot 'esp-settings.json'
if(!(Test-Path -LiteralPath $settingsFile)){
 $defaults=Get-Content -LiteralPath (Join-Path $repoRoot 'desktop-ui\defaults.json') -Raw|ConvertFrom-Json
 $defaults.settings|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $settingsFile -Encoding UTF8
}
dotnet publish (Join-Path $repoRoot 'esp\Esp.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $repoRoot 'engine-v7') --nologo
if($LASTEXITCODE-ne0){throw 'Engine build failed'}
npm run sync:menu --prefix (Join-Path $repoRoot 'sites\nexo-vercel')
if($LASTEXITCODE-ne0){throw 'Menu synchronization failed'}
npm run build --prefix (Join-Path $repoRoot 'desktop-ui')
if($LASTEXITCODE-ne0){throw 'Desktop build failed'}
npm start --prefix (Join-Path $repoRoot 'desktop-ui')
