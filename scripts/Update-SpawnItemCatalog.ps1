param(
    [string]$OutputPath,
    [ValidateRange(1,1000)][int]$PageSize = 500
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot 'resources\spawn-items.tsv' }
$output = [IO.Path]::GetFullPath($OutputPath)
$api = 'https://scum-db.com/api/search'
$records = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
$offset = 0
$total = -1
$received = 0
$variants = 0

function ConvertTo-CatalogueText([string]$Value) {
    return [regex]::Replace($Value, '\s+', ' ').Trim()
}

do {
    $url = $api + '?limit=' + $PageSize + '&offset=' + $offset + '&splitVariants=true'
    $page = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 60
    if (-not $page.PSObject.Properties['items'] -or -not $page.pagination) { throw 'SCUM Database search response changed; catalogue was not replaced.' }
    if ([int]$page.pagination.offset -ne $offset -or [int]$page.pagination.limit -ne $PageSize) { throw 'SCUM Database returned unexpected pagination; catalogue was not replaced.' }
    if ($total -lt 0) { $total = [int]$page.pagination.total }
    elseif ($total -ne [int]$page.pagination.total) { throw 'SCUM Database changed while paging; retry without replacing the catalogue.' }
    $received += @($page.items).Count
    foreach ($item in $page.items) {
        # Positive spawnability comes from the source. Craft/build records and hidden or removed items are not spawn cart entries.
        if ($item.is_spawnable -ne $true -or $item.asset_purpose -ne 'item' -or $item.is_buildable -eq $true -or
            $item.admin_spawn_hidden -eq $true -or $item.removed_from_game -eq $true) { continue }
        foreach ($flag in @('is_spawnable','is_buildable','admin_spawn_hidden','removed_from_game')) {
            if (-not $item.PSObject.Properties[$flag] -or $item.$flag -isnot [bool]) { throw 'SCUM Database item flags changed; catalogue was not replaced.' }
        }
        if (-not $item.PSObject.Properties['subcategory']) { throw 'SCUM Database subcategory metadata changed; catalogue was not replaced.' }
        $id = [string]$item.game_id
        if ($id -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_-]{0,127}$') { throw "Unsafe source item ID; catalogue was not replaced: $id" }
        $name = ConvertTo-CatalogueText ([string]$item.name)
        $category = ConvertTo-CatalogueText ([string]$item.category)
        $subcategory = ConvertTo-CatalogueText ([string]$item.subcategory)
        if (-not $category) { $category = 'other' }
        if (-not $name -or $name.Length -gt 256 -or $category.Length -gt 80 -or $subcategory.Length -gt 120) { throw "Invalid source metadata; catalogue was not replaced: $id" }
        foreach ($character in ($name + $category + $subcategory).ToCharArray()) {
            if ([char]::IsControl($character)) { throw "Invalid source metadata; catalogue was not replaced: $id" }
        }
        $record = [pscustomobject]@{ Id = $id; Name = $name; Category = $category; SourceUrl = 'https://scum-db.com/items/' + $id; Subcategory = $subcategory }
        if ($records.ContainsKey($id)) {
            $previous = $records[$id]
            if ($previous.Id -cne $id -or $previous.Name -cne $name -or $previous.Category -cne $category -or $previous.Subcategory -cne $subcategory) { throw "Conflicting source item ID; catalogue was not replaced: $id" }
            continue
        }
        $records.Add($id,$record)
        if ($item.is_skin_variant -or $item.parent_game_id -or $item.variant_group_id) { $variants++ }
    }
    # Search can omit merged aliases from a page. Advance by the reported limit, not returned count.
    $offset += $PageSize
} while ($offset -lt $total)

if ($records.Count -lt 1000) { throw 'SCUM Database returned too few eligible items; catalogue was not replaced.' }
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# SCUM Database spawn item catalogue v2')
$lines.Add('# Source: https://scum-db.com/ (game asset metadata; individual source links below)')
$lines.Add('# Retrieved UTC: ' + [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))
$lines.Add('# Query: ' + $api + '?limit=' + $PageSize + '&splitVariants=true; all pages')
$lines.Add('# Eligibility: is_spawnable=true; asset_purpose=item; is_buildable/admin_spawn_hidden/removed_from_game are not true')
$lines.Add('# Source search total: ' + $total + '; returned records: ' + $received + '; eligible distinct IDs: ' + $records.Count + '; variants: ' + $variants)
$lines.Add('# Columns: ID<TAB>Name<TAB>Category<TAB>SourceUrl<TAB>Subcategory')
foreach ($record in @($records.Values | Sort-Object Name,Id)) {
    $lines.Add($record.Id + "`t" + $record.Name + "`t" + $record.Category + "`t" + $record.SourceUrl + "`t" + $record.Subcategory)
}
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
Write-Output ('Spawn catalogue ready: {0} IDs, {1} variants; {2}' -f $records.Count,$variants,$output)
