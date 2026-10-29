param([Parameter(Mandatory=$true)][string]$VpkPath)
$ErrorActionPreference='Stop'
$nexoRoot=$PSScriptRoot
$nexoCli=Join-Path $nexoRoot 'tools\source2viewer-20.1\Source2Viewer-CLI.exe'
if(!(Test-Path -LiteralPath $nexoCli -PathType Leaf)){throw 'Prepare el modelo primero para descargar Source 2 Viewer.'}
$nexoExtract=Join-Path $nexoRoot 'work\map-extract'
& $nexoCli -i $VpkPath -o $nexoExtract -f 'resource/overviews/,panorama/images/overheadmaps/' -d
if($LASTEXITCODE -ne 0){throw 'No se pudieron importar los planos de CS2.'}
$nexoOutput=Join-Path $nexoRoot 'local-assets\maps';New-Item -ItemType Directory -Path $nexoOutput -Force|Out-Null
$nexoMaps=@()
function Copy-RadarImage([string]$base){foreach($suffix in @('_radar_psd.png','_radar_tga.png')){$source=Join-Path $nexoExtract ('panorama\images\overheadmaps\'+$base+$suffix);if(Test-Path -LiteralPath $source){$name=$base+'.png';Copy-Item -LiteralPath $source -Destination (Join-Path $nexoOutput $name) -Force;return $name}};return $null}
foreach($nexoFile in Get-ChildItem -LiteralPath (Join-Path $nexoExtract 'resource\overviews') -Filter '*.txt'){
 $name=$nexoFile.BaseName;if($name -notmatch '^(de|cs|ar)_[a-z0-9_]+$'){continue}
 $text=([IO.File]::ReadAllText($nexoFile.FullName))-replace '//[^\r\n]*',''
 function Read-Overview([string]$key){$match=[regex]::Match($text,'"'+$key+'"\s+"([^"\r\n]+)"',[Text.RegularExpressions.RegexOptions]::IgnoreCase);if($match.Success){return $match.Groups[1].Value};return $null}
 $style=[Globalization.NumberStyles]::Float;$culture=[Globalization.CultureInfo]::InvariantCulture
 [float]$x=0;[float]$y=0;[float]$scale=0
 if(![float]::TryParse((Read-Overview 'pos_x'),$style,$culture,[ref]$x)-or ![float]::TryParse((Read-Overview 'pos_y'),$style,$culture,[ref]$y)-or ![float]::TryParse((Read-Overview 'scale'),$style,$culture,[ref]$scale)-or $scale -le 0){continue}
 $image=Copy-RadarImage $name
 if(!$image){$material=(Read-Overview 'material')-replace '^.*/','' -replace '_radar$','';if($material -match '^[a-z0-9_]+$'){$image=Copy-RadarImage $material}}
 if(!$image){continue}
 $layers=@()
 foreach($section in [regex]::Matches($text,'"(default|lower|[a-z0-9_]+)"\s*\{([^{}]*"AltitudeM[^{}]+)\}',[Text.RegularExpressions.RegexOptions]::IgnoreCase)){
  [float]$min=0;[float]$max=0
  $minText=[regex]::Match($section.Groups[2].Value,'"AltitudeMin"\s*"([^"]+)"').Groups[1].Value;$maxText=[regex]::Match($section.Groups[2].Value,'"AltitudeMax"\s*"([^"]+)"').Groups[1].Value
  $layerName=$section.Groups[1].Value;$layerImage=if($layerName -eq 'default'){$image}else{Copy-RadarImage ($name+'_'+$layerName)}
  if($layerImage-and [float]::TryParse($minText,$style,$culture,[ref]$min)-and [float]::TryParse($maxText,$style,$culture,[ref]$max)-and $max -gt $min){$layers+=@{name=$layerName;min=$min;max=$max;image=$layerImage}}
 }
 $nexoMaps+=@{name=$name;x=$x;y=$y;scale=$scale;width=1024;height=1024;image=$image;layers=@($layers)}
}
if($nexoMaps.Count -eq 0){throw 'CS2 no devolvió planos compatibles.'}
$json=ConvertTo-Json -InputObject @($nexoMaps) -Depth 6
[IO.File]::WriteAllText((Join-Path $nexoOutput 'maps.json'),$json,(New-Object Text.UTF8Encoding($false)))
Write-Output "Planos importados desde la instalación local: $($nexoMaps.Count)."
