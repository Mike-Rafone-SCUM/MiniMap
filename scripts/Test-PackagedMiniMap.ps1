param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne [Threading.ApartmentState]::STA) {
    throw 'The packaged self-check requires a fresh Windows PowerShell STA process.'
}
$packagedPath = [IO.Path]::GetFullPath($AssemblyPath)
if (-not (Test-Path -LiteralPath $packagedPath -PathType Leaf)) { throw 'Packaged executable is missing.' }
Add-Type -AssemblyName System.Windows.Forms,System.Drawing

# Loading the compiled assembly does not launch its executable or request elevation.
# The fixed -Check argument returns before the normal app creates windows or hooks.
$assembly = [Reflection.Assembly]::LoadFile($packagedPath)
$assetType=$assembly.GetType('ScumMiniMap.InstallationAssets',$true)
try { $null=$assetType.GetMethod('Validate',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@()) }
catch [Reflection.TargetInvocationException] { throw $_.Exception.InnerException }
$entry = $assembly.EntryPoint
if ($null -eq $entry -or $entry.DeclaringType.FullName -ne 'ScumMiniMap.Program' -or
    $entry.Name -ne 'Main' -or -not $entry.IsStatic -or $entry.GetParameters().Count -ne 1 -or
    $entry.GetParameters()[0].ParameterType -ne [string[]]) {
    throw 'Unexpected packaged self-check entry point.'
}
# Program.Main checks for legacy migration files in its host base directory before
# parsing -Check. A clean PowerShell host must not provide any migration inputs.
foreach ($name in @('settings.ini','automatic.log')) {
    if ([IO.File]::Exists((Join-Path ([AppDomain]::CurrentDomain.BaseDirectory) $name))) {
        throw 'Packaged self-check host has unexpected legacy migration input.'
    }
}
$arguments = New-Object object[] 1
$arguments[0] = [string[]]@('-Check')
try { $null = $entry.Invoke($null,$arguments) }
catch [Reflection.TargetInvocationException] { throw $_.Exception.InnerException }
Write-Output 'Packaged assembly self-check passed without launching the app.'
