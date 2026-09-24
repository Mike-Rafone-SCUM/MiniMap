[CmdletBinding(SupportsShouldProcess=$true)]
param()
$ErrorActionPreference = 'Stop'
$cleanRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
# Explicit generated directories only. Versioned releases, source and user profiles are excluded.
foreach ($relative in @('release\checks','release\tests','release\.staging','release\performance-test')) {
    $target = [IO.Path]::GetFullPath((Join-Path $cleanRoot $relative))
    if (-not $target.StartsWith($cleanRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target escaped project root.' }
    if (Test-Path -LiteralPath $target) {
        $item = Get-Item -LiteralPath $target -Force
        $links = @($item) + @(Get-ChildItem -LiteralPath $target -Force -Recurse)
        if (@($links | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) { throw "Refusing cleanup through a reparse point: $target" }
        if ($PSCmdlet.ShouldProcess($target,'Remove generated MiniMap output')) { Remove-Item -LiteralPath $target -Recurse -Force }
    }
}
