$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -Path @(Get-ChildItem (Join-Path $root 'src/Localization*.cs') | ForEach-Object FullName)
$tables=[ordered]@{}
foreach($name in @('StringsEn','SectionNamesPt','CategoryNamesPt','MapObjectsEsAr')) {
    $field=[ScumMiniMap.Localization].GetField($name,[Reflection.BindingFlags]'Public,NonPublic,Static')
    $table=$field.GetValue($null)
    $result=[ordered]@{}
    foreach($key in $table.Keys){$result[$key]=if($name -eq 'StringsEn'){$table[$key]}else{$key}}
    $tables[$name]=$result
}
$folder=Join-Path $root 'release/checks/baltic-swahili'
New-Item -ItemType Directory -Force $folder | Out-Null
$tables | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $folder 'reference.json') -Encoding UTF8
