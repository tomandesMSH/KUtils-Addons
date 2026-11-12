# Builds the instrument pack zips and catalog.json into <Work>\release.
# <Work>\dl: the FreePats downloads; <Work>\built: the VSCO SoundFonts made by tools\PackTool
# (dotnet run -- build <tree.tsv> <cache> <Work>\built).
param([string]$Work = (Join-Path $PSScriptRoot "..\work"))
$ErrorActionPreference = 'Stop'
$p = $Work
$out = "$p\release"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $out | Out-Null
$cc0 = Get-Content "$p\dl\guitar\SpanishClassicalGuitar-SF2-20190618\cc0.txt" -Raw
$base = 'https://github.com/tomandesMSH/KUtils-Addons/releases/download/instruments-v1'

$vsco = 'Samples: VSCO 2 Community Edition by Versilian Studios (https://github.com/sgossner/VSCO-2-CE), CC0 1.0. Built into a SoundFont for Keyboard Utils (one dynamic layer, mono, looped sustain).'
$packs = @(
  @{ id = 'guitar';   sf2 = "$p\dl\guitar\SpanishClassicalGuitar-SF2-20190618\SpanishClassicalGuitar-20190618.sf2"; readme = "$p\dl\guitar\SpanishClassicalGuitar-SF2-20190618\readme.txt"; credits = 'Spanish classical guitar - FreePats (https://freepats.zenvoid.org), CC0 1.0.'; presets = @{ main = 0 } },
  @{ id = 'violin';   sf2 = "$p\built\violin.sf2"; readme = $null; credits = "Solo violin, arco and pizzicato. $vsco"; presets = @{ main = 0; pizzicato = 1 } },
  @{ id = 'viola';    sf2 = "$p\built\viola.sf2"; readme = $null; credits = "Viola section, arco and pizzicato. $vsco"; presets = @{ main = 0; pizzicato = 1 } },
  @{ id = 'cello';    sf2 = "$p\built\cello.sf2"; readme = $null; credits = "Cello section, arco and pizzicato. $vsco"; presets = @{ main = 0; pizzicato = 1 } },
  @{ id = 'flute';    sf2 = "$p\built\flute.sf2"; readme = $null; credits = "Flute. $vsco"; presets = @{ main = 0 } },
  @{ id = 'recorder'; sf2 = "$p\dl\recorder\Recorder-SF2-20201205\Recorder-20201205.sf2"; readme = "$p\dl\recorder\Recorder-SF2-20201205\readme.txt"; credits = 'Wooden recorder - FreePats (https://freepats.zenvoid.org), performed by Eugene Vlaskin, CC0 1.0.'; presets = @{ main = 0 } },
  @{ id = 'clarinet'; sf2 = "$p\dl\clarinet\Clarinet-SF2-20190818\Clarinet-20190818.sf2"; readme = "$p\dl\clarinet\Clarinet-SF2-20190818\readme.txt"; credits = 'Clarinet - FreePats (https://freepats.zenvoid.org), samples by Tyler and Kaili Dence, CC0 1.0.'; presets = @{ main = 0 } },
  @{ id = 'trumpet';  sf2 = "$p\built\trumpet.sf2"; readme = $null; credits = "Trumpet. $vsco"; presets = @{ main = 0 } },
  @{ id = 'altosax';  sf2 = "$p\dl\tenorsax\TenorSaxophone-small-SF2-20200717\TenorSaxophone-small-20200717.sf2"; readme = "$p\dl\tenorsax\TenorSaxophone-small-SF2-20200717\readme.txt"; credits = 'Saxophone (a tenor saxophone, played in the alto''s range) - FreePats (https://freepats.zenvoid.org) from the Versilian Community Sample Library, CC0 1.0.'; presets = @{ main = 0 } }
)

$catalog = @()
foreach ($pack in $packs) {
  $dir = "$p\stage\$($pack.id)"
  Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
  New-Item -ItemType Directory $dir | Out-Null
  Copy-Item $pack.sf2 "$dir\sound.sf2"
  [IO.File]::WriteAllText("$dir\LICENSE.txt", $cc0)
  $readme = "Keyboard Utils instrument pack: $($pack.id)`r`n`r`n$($pack.credits)`r`n"
  if ($pack.readme) { $readme += "`r`n--- Original readme ---`r`n" + (Get-Content $pack.readme -Raw) }
  [IO.File]::WriteAllText("$dir\readme.txt", $readme)
  $manifest = [ordered]@{ id = $pack.id; version = 1; sound = 'sound.sf2'; presets = $pack.presets; credits = $pack.credits; license = 'CC0-1.0' }
  [IO.File]::WriteAllText("$dir\pack.json", ($manifest | ConvertTo-Json -Depth 4))
  $zip = "$out\$($pack.id)-pack-v1.zip"
  Compress-Archive -Path "$dir\*" -DestinationPath $zip -CompressionLevel Optimal
  $item = Get-Item $zip
  $catalog += [ordered]@{
    id = $pack.id; version = 1
    url = "$base/$($item.Name)"
    bytes = $item.Length
    sha256 = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    credits = $pack.credits
  }
  "{0,-9} {1,6:N1} MB" -f $pack.id, ($item.Length / 1MB)
}
[IO.File]::WriteAllText("$out\catalog.json", ([ordered]@{ version = 1; packs = $catalog } | ConvertTo-Json -Depth 5))
Get-ChildItem $out | Select-Object Name, Length
