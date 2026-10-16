param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory,[string[]]$Languages=@('en'))
$ErrorActionPreference='Stop'
$package=[IO.Path]::GetFullPath($PackageDirectory)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output) { throw 'Choose a fresh showcase directory.' }
$exe=Join-Path $package 'SkynettMiniMap.exe'
if(-not(Test-Path -LiteralPath $exe)) { $exe=Join-Path $package 'SkynettMiniMap-Test.exe' }
if(-not(Test-Path -LiteralPath $exe)) { throw 'Packaged executable is missing.' }
New-Item -ItemType Directory -Path $output | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$harness=Join-Path $output 'WpfShowcaseHarness.exe'
& (Join-Path $framework 'csc.exe') /nologo /codepage:65001 /target:exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll "/reference:$framework/WPF/PresentationCore.dll" "/reference:$framework/WPF/PresentationFramework.dll" "/reference:$framework/WPF/WindowsBase.dll" "/out:$harness" (Join-Path $PSScriptRoot '../tests/WpfShowcaseHarness.cs')
if($LASTEXITCODE -ne 0) { throw 'Showcase harness compilation failed.' }
foreach($language in $Languages) {
    if($language -notmatch '^[a-z]{2}(-[A-Za-z]{2})?$') { throw 'Invalid language code.' }
    $folder=Join-Path $output $language
    New-Item -ItemType Directory -Path $folder | Out-Null
    & $harness $exe $folder $language
    if($LASTEXITCODE -ne 0) { throw "Showcase capture failed: $language" }
    if(@(Get-ChildItem -LiteralPath $folder -Filter '*.png').Count -ne 10) { throw "Incomplete gallery: $language" }
    if((Get-ChildItem -LiteralPath $folder -Filter '*.png' | Measure-Object Length -Sum).Sum -ge 10MB) { throw "Gallery exceeds the conservative Discord upload budget: $language" }
    Compress-Archive -Path (Join-Path $folder '*.png'),(Join-Path $folder 'gallery.json') -DestinationPath (Join-Path $folder 'SCUM-MiniMap-Showcase.zip')
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../packaging/SHOWCASE.md') -Destination (Join-Path $output 'discord-en.md')
@'
# SCUM MiniMap showcase

Ten real captures per language from the packaged WPF executable. Player names, coordinates, waypoints and deaths are synthetic demonstration data, using loopback TCP peers. These are app captures, not live SCUM gameplay or proof of Internet reachability. No router or firewall changes are requested.

Upload the ten PNGs in filename order as a Discord gallery. Each gallery.json supplies matching native-language image descriptions. Keep the app named SCUM MiniMap, made for the Skynett community. The English post copy is in discord-en.md. These files are prepared assets; generating them does not post to Discord.

Only PNGs and gallery.json are included in each gallery ZIP. Do not upload the harness or isolated-profile directories. Sharing keys are session-only; captured password fields are masked.
'@ | Set-Content -LiteralPath (Join-Path $output 'README.md') -Encoding UTF8
Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Select-Object Hash,@{n='Executable';e={Split-Path $_.Path -Leaf}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'BUILD-INFO.json') -Encoding UTF8
Write-Output "Real WPF captures ready: $output"
