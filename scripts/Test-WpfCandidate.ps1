param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory
)
$ErrorActionPreference='Stop'
$candidate=[IO.Path]::GetFullPath($Executable)
if(-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw 'Explicit candidate executable is missing' }
$name=Split-Path $candidate -Leaf
if($name -notin @('SkynettMiniMap.exe','SkynettMiniMap-Test.exe')) { throw 'Use the canonical executable name in its package directory' }
$alternate=if($name -eq 'SkynettMiniMap.exe') {'SkynettMiniMap-Test.exe'} else {'SkynettMiniMap.exe'}
if(Test-Path -LiteralPath (Join-Path (Split-Path $candidate -Parent) $alternate)) { throw 'Package contains both executable variants; choose an isolated canonical package' }
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $evidence) { throw 'Choose a fresh evidence directory' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$root=Split-Path $PSScriptRoot -Parent
$results=@()
$checks=@(
    @{Name='package'; Script='scripts/Test-PackagedMiniMap.ps1'; Args=@('-AssemblyPath',$candidate)},
    @{Name='audit-polish'; Script='tests/Test-SquadUi.ps1'; Args=@('-Executable',$candidate,'-OutputDirectory',(Join-Path $evidence 'audit-polish'))},
    @{Name='member-polish'; Script='tests/Test-SquadUi.ps1'; Args=@('-Executable',$candidate,'-OutputDirectory',(Join-Path $evidence 'member-polish'))},
    @{Name='settings-polish'; Script='tests/Test-SquadUi.ps1'; Args=@('-Executable',$candidate,'-OutputDirectory',(Join-Path $evidence 'settings-polish'))},
    @{Name='ui'; Script='tests/Test-SquadUi.ps1'; Args=@('-Executable',$candidate,'-OutputDirectory',(Join-Path $evidence 'ui'))},
    @{Name='wpf'; Script='tests/Test-WpfPerformance.ps1'; Args=@('-PackageDirectory',(Split-Path $candidate -Parent),'-OutputDirectory',(Join-Path $evidence 'wpf'))},
    @{Name='opacity-setup'; Script='tests/Test-OpacitySetup.ps1'; Args=@('-Executable',$candidate,'-OutputDirectory',(Join-Path $evidence 'opacity-setup'))}
)
$identity=Get-FileHash -LiteralPath $candidate -Algorithm SHA256
$savedPolish=$env:MINIMAP_AUDIT_POLISH_ONLY
$savedMemberPolish=$env:MINIMAP_MEMBER_POLISH_ONLY
$savedSettingsPolish=$env:MINIMAP_SETTINGS_POLISH_ONLY
try {
    # Focus-sensitive fixtures run serially, each in a fresh STA process.
    foreach($check in $checks) {
        $env:MINIMAP_AUDIT_POLISH_ONLY=if($check.Name -eq 'audit-polish') {'1'} else {$null}
        $env:MINIMAP_MEMBER_POLISH_ONLY=if($check.Name -eq 'member-polish') {'1'} else {$null}
        $env:MINIMAP_SETTINGS_POLISH_ONLY=if($check.Name -eq 'settings-polish') {'1'} else {$null}
        $log=Join-Path $evidence ($check.Name+'.log')
        $arguments=@('-NoProfile','-STA','-ExecutionPolicy','Bypass','-File',(Join-Path $root $check.Script))+$check.Args
        # Windows PowerShell wraps native stderr as ErrorRecords. Preserve it in
        # the log and inspect the actual child exit code before failing.
        $priorErrorAction=$ErrorActionPreference
        try {
            $ErrorActionPreference='Continue'
            & powershell.exe @arguments *> $log
            $code=$LASTEXITCODE
        } finally { $ErrorActionPreference=$priorErrorAction }
        $results+=@{check=$check.Name;exitCode=$code;log=$log}
        Write-Host ($check.Name+': exit '+$code)
        if($code -ne 0) {
            Get-Content -LiteralPath $log -Tail 80 | ForEach-Object { Write-Host $_ }
            throw ('Candidate check failed: '+$check.Name+'. See '+$log)
        }
    }
} finally {
    $env:MINIMAP_AUDIT_POLISH_ONLY=$savedPolish
    $env:MINIMAP_MEMBER_POLISH_ONLY=$savedMemberPolish
    $env:MINIMAP_SETTINGS_POLISH_ONLY=$savedSettingsPolish
    @{executable=$candidate;sha256=$identity.Hash;checks=$results;scope='Local synthetic acceptance; no production relay checks';manual=@('Real-game session','Mixed-DPI and disconnected monitors','Narrator and high contrast','OBS','Native-language review')} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'verification.json') -Encoding UTF8
}
