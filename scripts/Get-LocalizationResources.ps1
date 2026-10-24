$ErrorActionPreference = 'Stop'
$resourceRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'resources'
# Polish names already belong to the original canonical resource list.
foreach ($language in @('es-AR','fr','de','nl','ru','zh-CN','tr','ar','pt-BR','pt-PT','ko','th','zh-TW','ja','cs','it','uk','fi','lt','sw')) {
    foreach ($prefix in @('spawn-item-names','spawn-category-names')) {
        $name = "$prefix-$language.tsv"
        $path = Join-Path $resourceRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing localized catalogue: $name" }
        '/resource:' + $path + ',' + $name
    }
}
