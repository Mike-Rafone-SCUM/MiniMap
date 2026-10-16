param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string[]]$Languages=@('en','es-AR','fr','de','nl','ru','zh-CN','tr','ar','pt-BR','ko','pl','pt-PT','th','zh-TW','ja','cs','it','uk')
)
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).ProviderPath
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$previewLanguages=$Languages
if (($previewLanguages | Select-Object -Unique).Count -ne $previewLanguages.Length) { throw 'Preview language codes must be unique.' }
foreach ($language in $previewLanguages) {
    # Separate STA hosts avoid type/assembly reuse and isolate temporary profiles.
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Render-SpawnCartPreview.ps1') `
        -AssemblyPath $assembly -OutputPath (Join-Path $output "$language-selector.png") `
        -ModifierOutputPath (Join-Path $output "$language-modifiers.png") `
        -KitOutputPath (Join-Path $output "$language-kits.png") -Language $language
    if ($LASTEXITCODE -ne 0) { throw "Localization preview failed: $language" }
}
@{
    assembly = $assembly
    sha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    languages = $previewLanguages.Length
    previews = $previewLanguages.Length * 3
    nativeSpeakerApproval = 'pending'
    dpiCoverage = 'current Windows environment only'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'preview-manifest.json') -Encoding UTF8
