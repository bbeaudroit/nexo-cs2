$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Get-Process -Name cs2 -ErrorAction SilentlyContinue)) {
    throw 'Abri CS2 y dejalo en el menu principal antes de continuar.'
}
$dumpFolder = Join-Path $PSScriptRoot ('dumps\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $dumpFolder -Force | Out-Null
Write-Host "Salida: $dumpFolder"
& (Join-Path $PSScriptRoot 'cs2-dumper.exe') -o $dumpFolder -f json -vv
if ($LASTEXITCODE -ne 0) {
    throw "El dumper fallo con codigo $LASTEXITCODE. Conservar cs2-dumper.log para el diagnostico."
}
foreach ($requiredFile in @('offsets.json', 'client_dll.json')) {
    $requiredPath = Join-Path $dumpFolder $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Falta $requiredFile" }
    Get-Content -LiteralPath $requiredPath -Raw | ConvertFrom-Json | Out-Null
}
Write-Host 'Volcado generado y JSON validado. Avisa en el chat para revisar los offsets.'
