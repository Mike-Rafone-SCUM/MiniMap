param(
    [string]$CataloguePath,
    [string]$OutputPath,
    [ValidateRange(1,3)][int]$RequestAttempts = 2
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $CataloguePath) { $CataloguePath = Join-Path $projectRoot 'resources\spawn-items.tsv' }
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot 'resources\spawn-compatibility.tsv' }
$catalogue = [IO.Path]::GetFullPath($CataloguePath)
$output = [IO.Path]::GetFullPath($OutputPath)
if ([string]::Equals($catalogue,$output,[StringComparison]::OrdinalIgnoreCase)) { throw 'Compatibility output must be separate from the item catalogue.' }
$items = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in [IO.File]::ReadAllLines($catalogue,[Text.Encoding]::UTF8)) {
    if (-not $line -or $line.StartsWith('#')) { continue }
    $fields = $line.Split("`t")
    if (($fields.Length -ne 4 -and $fields.Length -ne 5) -or $fields[0] -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_-]{0,127}$' -or
        $items.ContainsKey($fields[0]) -or $fields[3] -cne ('https://scum-db.com/items/' + $fields[0])) { throw 'Invalid source item catalogue; compatibility was not replaced.' }
    $items.Add($fields[0],[pscustomobject]@{ Id=$fields[0]; Category=$fields[2] })
}
if ($items.Count -lt 1000) { throw 'The item catalogue is unexpectedly small; compatibility was not replaced.' }
$weapons = @($items.Values | Where-Object Category -eq 'weapon_ranged' | Sort-Object Id)
if ($weapons.Count -lt 1) { throw 'The item catalogue has no ranged weapons; compatibility was not replaced.' }
$edges = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
$ignored = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$unresolved = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$negative = [Collections.Generic.List[string]]::new()
$empty = [Collections.Generic.List[string]]::new()
$covered = 0

function Get-Compatibility([string]$Url,[string]$Id) {
    for ($attempt=1; $attempt -le $RequestAttempts; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $Url -Method Get -TimeoutSec 30
            if ($null -eq $response) { throw 'SCUM Database returned a null compatibility response; compatibility was not replaced.' }
            return $response
        }
        catch {
            $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            if ($status -eq 400 -or $status -eq 404) { $negative.Add($Id + ':' + $status); return $null }
            if ($attempt -eq $RequestAttempts) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}

function Assert-CompatibilityRecord([object]$Record,[string]$Context) {
    if ($Record -isnot [pscustomobject] -or -not $Record.PSObject.Properties['game_id'] -or
        $Record.game_id -isnot [string] -or $Record.game_id -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_-]{0,127}$') {
        throw ('SCUM Database returned an invalid ' + $Context + ' record; compatibility was not replaced.')
    }
}

function Assert-CompatibilitySchema([object]$Data) {
    if ($Data -isnot [pscustomobject]) { throw 'SCUM Database compatibility response must be an object; compatibility was not replaced.' }
    foreach ($property in $Data.PSObject.Properties) {
        if (@('magazines','attachments','ammunition','ammo_stat_ranges') -cnotcontains $property.Name) {
            throw ('SCUM Database returned an unknown compatibility field: ' + $property.Name + '; review the API before replacing compatibility.')
        }
    }
    foreach ($field in @('magazines','attachments','ammunition')) {
        if (-not $Data.PSObject.Properties[$field]) { throw ('SCUM Database compatibility field is missing: ' + $field + '; compatibility was not replaced.') }
    }
    foreach ($field in @('magazines','ammunition')) {
        if ($Data.$field -isnot [array]) { throw ('SCUM Database compatibility field must be an array: ' + $field + '; compatibility was not replaced.') }
        foreach ($record in $Data.$field) { Assert-CompatibilityRecord $record $field }
    }
    if ($Data.attachments -isnot [pscustomobject]) { throw 'SCUM Database attachments must be an object of arrays; compatibility was not replaced.' }
    foreach ($group in $Data.attachments.PSObject.Properties) {
        if ($group.Value -isnot [array]) { throw ('SCUM Database attachment group must be an array: ' + $group.Name + '; compatibility was not replaced.') }
        foreach ($record in $group.Value) {
            Assert-CompatibilityRecord $record ('attachment/' + $group.Name)
            if (-not $record.PSObject.Properties['match_method'] -or $record.match_method -isnot [string] -or
                ($record.match_method -cne 'direct' -and $record.match_method -cne 'requires_adapter')) {
                throw 'SCUM Database attachment matching schema changed; compatibility was not replaced.'
            }
            $adapterProperty = $record.PSObject.Properties['required_adapters']
            if ($record.match_method -ceq 'requires_adapter' -and
                (-not $adapterProperty -or $adapterProperty.Value -isnot [array] -or $adapterProperty.Value.Count -eq 0)) {
                throw 'SCUM Database adapter-dependent attachment has no adapter array; compatibility was not replaced.'
            }
            if ($adapterProperty) {
                if ($adapterProperty.Value -isnot [array]) { throw 'SCUM Database required_adapters must be an array; compatibility was not replaced.' }
                foreach ($adapter in $adapterProperty.Value) {
                    if ($adapter -isnot [string] -or $adapter -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_-]{0,127}$') {
                        throw 'SCUM Database returned an invalid required adapter ID; compatibility was not replaced.'
                    }
                }
            }
        }
    }
}

function Add-Compatibility([string]$WeaponId,[string]$RelatedId,[string]$Relation,[string]$SourceUrl) {
    if ($RelatedId -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_-]{0,127}$') { throw 'SCUM Database returned an unsafe related ID; compatibility was not replaced.' }
    if (-not $items.ContainsKey($RelatedId)) {
        $null = $ignored.Add($WeaponId + "`t" + $RelatedId + "`t" + $Relation)
        $null = $unresolved.Add($RelatedId)
        return
    }
    $related = $items[$RelatedId].Id
    if ([string]::Equals($WeaponId,$related,[StringComparison]::OrdinalIgnoreCase)) { return }
    $key = $WeaponId + "`t" + $related + "`t" + $Relation
    if (-not $edges.ContainsKey($key)) { $edges.Add($key,$key + "`t" + $SourceUrl) }
}

for ($index=0; $index -lt $weapons.Count; $index++) {
    $weapon = $weapons[$index].Id
    $url = 'https://scum-db.com/api/items/' + $weapon + '/compatibility'
    $data = Get-Compatibility $url $weapon
    if ($null -ne $data) {
        Assert-CompatibilitySchema $data
        $before = $edges.Count
        foreach ($record in $data.magazines) { Add-Compatibility $weapon ([string]$record.game_id) 'magazine' $url }
        foreach ($record in $data.ammunition) { Add-Compatibility $weapon ([string]$record.game_id) 'ammunition' $url }
        foreach ($group in $data.attachments.PSObject.Properties) {
            foreach ($record in $group.Value) {
                $method = [string]$record.match_method
                if ($method -ne 'direct' -and $method -ne 'requires_adapter') { throw 'SCUM Database attachment matching schema changed; compatibility was not replaced.' }
                Add-Compatibility $weapon ([string]$record.game_id) ('attachment:' + $method) $url
                foreach ($adapter in $record.required_adapters) { Add-Compatibility $weapon ([string]$adapter) 'required_adapter' $url }
            }
        }
        $covered++
        if ($edges.Count -eq $before) { $empty.Add($weapon) }
    }
    if (($index+1)%20 -eq 0) { Write-Output ('Compatibility lookup: {0}/{1} ranged weapons' -f ($index+1),$weapons.Count) }
}
if ($covered -eq 0 -or $edges.Count -eq 0) { throw 'No verified compatibility records were returned; compatibility was not replaced.' }
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# SCUM Database spawn compatibility v1')
$lines.Add('# Retrieved UTC: ' + [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))
$lines.Add('# Source: https://scum-db.com/api/items/<WeaponID>/compatibility; one explicit query per bundled ranged weapon, including variants')
$lines.Add('# Eligibility: both endpoints are present in the existing item catalogue; no name-based or parent-variant compatibility inference')
$lines.Add('# Relations: magazine; ammunition; attachment:direct; attachment:requires_adapter; required_adapter')
$lines.Add('# Scope: adapter-dependent attachments can require an appropriate rail; required_adapter records are the source-listed possible adapters, not an automatic loadout')
$lines.Add('# Catalogue IDs: ' + $items.Count + '; ranged weapon queries: ' + $weapons.Count + '; valid responses: ' + $covered + '; negative responses: ' + $negative.Count + '; empty eligible responses: ' + $empty.Count + '; distinct links: ' + $edges.Count)
$lines.Add('# Ignored links to absent catalogue IDs: ' + $ignored.Count + '; distinct absent IDs: ' + $unresolved.Count)
$lines.Add('# Negative responses: ' + ($negative -join ','))
$lines.Add('# Empty eligible responses: ' + ($empty -join ','))
$lines.Add('# Absent related IDs: ' + (@($unresolved | Sort-Object) -join ','))
$lines.Add('# Columns: WeaponID<TAB>RelatedID<TAB>Relation<TAB>SourceUrl')
foreach ($edge in @($edges.Values | Sort-Object)) { $lines.Add($edge) }
$parent = Split-Path $output -Parent
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$temporary = $output + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllLines($temporary,$lines,[Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $output) { [IO.File]::Replace($temporary,$output,[NullString]::Value) }
    else { [IO.File]::Move($temporary,$output) }
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Output ('Spawn compatibility ready: {0} links across {1} ranged weapon queries; {2}' -f $edges.Count,$weapons.Count,$output)
