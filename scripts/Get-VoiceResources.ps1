$root=Split-Path $PSScriptRoot -Parent
$voiceRoot=Join-Path $root 'resources\voice-navigation'
if(Test-Path -LiteralPath $voiceRoot) {
    Get-ChildItem -LiteralPath $voiceRoot -Directory | ForEach-Object {
        $voice=$_.Name
        Get-ChildItem -LiteralPath $_.FullName -Filter '*.mp3' -File | ForEach-Object {
            '/resource:'+$_.FullName+',voice-navigation/'+$voice+'/'+$_.Name
        }
    }
}
