$ErrorActionPreference = 'Stop'
$checkRoot = Split-Path $PSScriptRoot -Parent
# Fresh Windows PowerShell STA processes isolate assemblies and UI fixtures.
foreach ($check in @(
    'scripts\Test-UpdateChecker.ps1',
    'scripts\Check-Stability.ps1',
    'scripts\Test-InputVoice.ps1',
    'tests\Test-AuditRegressions.ps1',
    'tests\Test-ChatCopyRegression.ps1'
)) {
    & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $checkRoot $check)
    if ($LASTEXITCODE -ne 0) { throw "Check failed: $check" }
}
