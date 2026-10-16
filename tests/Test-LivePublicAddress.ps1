param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
# Explicit, optional network check. The ordinary regression suite stays offline.
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path (Resolve-Path -LiteralPath $PackageDirectory).Path 'SkynettMiniMap-Test.exe'))
$type=$assembly.GetType('ScumMiniMap.PublicPositionAddress',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$address=$type.GetMethod('Lookup',$flags).Invoke($null,@())
if([string]::IsNullOrWhiteSpace($address) -or $type.GetMethod('Parse',$flags).Invoke($null,@($address)) -ne $address){throw 'Public IPv4 lookup validation failed'}
Write-Output 'Local router public IPv4 lookup succeeded; address value omitted from the test log. No external lookup service was contacted.'
