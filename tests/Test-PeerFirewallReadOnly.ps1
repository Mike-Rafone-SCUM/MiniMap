param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
# Explicit optional Windows COM smoke check; creates or changes no firewall rules.
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path (Resolve-Path -LiteralPath $PackageDirectory).Path 'SkynettMiniMap-Test.exe'))
$type=$assembly.GetType('ScumMiniMap.PeerFirewall',$true).GetNestedType('WindowsPolicy',[Reflection.BindingFlags]::NonPublic)
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$policy=[Activator]::CreateInstance($type,$flags,$null,@(),$null)
try {
    $result=$type.GetMethod('Get',$flags).Invoke($policy,@('SCUM MiniMap nonexistent test '+[guid]::NewGuid().ToString('N')))
    if($null -ne $result){throw 'Unexpected firewall rule collision'}
    Write-Output 'Read-only Windows Firewall COM lookup passed; no rules were changed.'
}finally { $type.GetMethod('Dispose',$flags).Invoke($policy,@()) }
