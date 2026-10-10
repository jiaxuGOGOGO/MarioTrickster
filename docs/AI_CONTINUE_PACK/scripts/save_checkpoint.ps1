[CmdletBinding()]
param(
    [ValidateSet('Status', 'Save', 'Export')]
    [string]$Mode = 'Status',
    [string]$Message = '',
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$ExpectedBranch = 'genspark_ai_developer'
$GitArgs = @('-c', "safe.directory=$RepoRoot")
$PackRoot = Join-Path $RepoRoot 'docs\AI_CONTINUE_PACK'
$CheckpointFile = Join-Path $PackRoot 'LATEST_CHECKPOINT.md'
$QuickStartFiles = @('SAVE_MARIOTRICKSTER.cmd', 'CHECK_MARIOTRICKSTER.cmd', 'OPEN_MARIOTRICKSTER_UNITY.cmd', 'START_HERE.md', 'CONTINUE_WITH_MANUS.txt')

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & git @GitArgs @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Get-TestSummary {
    $report = Join-Path $RepoRoot 'TestReport.txt'
    if (-not (Test-Path $report)) { return 'No TestReport.txt found in this working copy.' }
    $lines = Get-Content -LiteralPath $report -TotalCount 20 |
        Where-Object { $_ -match 'EditMode|PlayMode|\d{4}-\d{2}-\d{2}|:\s*\d+' } |
        ForEach-Object { $_.Trim() }
    if ($lines.Count -eq 0) { return 'TestReport.txt exists but no summary was recognized.' }
    return ($lines -join "`n")
}

function Get-WorkingStatus {
    return @(& git @GitArgs status --porcelain)
}

function Get-UntrackedPaths([string[]]$Status) {
    return @($Status | Where-Object { $_ -match '^\?\? ' } | ForEach-Object { $_.Substring(3) })
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is not on PATH.' }
Push-Location $RepoRoot
try {
    $inside = ((& git @GitArgs rev-parse --is-inside-work-tree 2>$null) -eq 'true')
    if (-not $inside) { throw "Not a Git working tree: $RepoRoot" }

    $branch = (& git @GitArgs branch --show-current).Trim()
    if ($branch -ne $ExpectedBranch) { throw "Expected branch $ExpectedBranch, found $branch." }
    $head = (& git @GitArgs rev-parse HEAD).Trim()
    $statusBefore = Get-WorkingStatus
    $untrackedBefore = Get-UntrackedPaths $statusBefore
    $metaCount = @($untrackedBefore | Where-Object { $_ -match '\.meta$' }).Count
    $otherUntracked = @($untrackedBefore | Where-Object { $_ -notmatch '\.meta$' -and $_ -notmatch '^docs/AI_CONTINUE_PACK/' -and $_ -notin $QuickStartFiles })

    Write-Host "Repository: $RepoRoot"
    Write-Host "Branch: $branch"
    Write-Host "HEAD: $head"
    Write-Host "Untracked Unity meta files: $metaCount"
    if ($otherUntracked.Count -gt 0) {
        Write-Host 'Unexpected untracked files (not included automatically):'
        $otherUntracked | ForEach-Object { Write-Host "  $_" }
    }
    Write-Host '--- Current Git status ---'
    $visibleStatus = @($statusBefore | Where-Object { $_ -notmatch '^\?\?.*\.meta$' })
    if ($visibleStatus.Count -eq 0) { Write-Host 'Clean except for excluded Unity .meta files.' }
    else { $visibleStatus | ForEach-Object { Write-Host $_ } }
    if ($metaCount -gt 0) { Write-Host "Excluded Unity .meta files: $metaCount" }
    Write-Host '--- Latest Unity test summary ---'
    Write-Host (Get-TestSummary)

    if ($Mode -eq 'Status') {
        Write-Host 'Status mode only. No files were staged, committed, pushed, or deleted.'
        exit 0
    }

    if ($Mode -eq 'Export') {
        $exportRoot = Join-Path $RepoRoot '.handoff'
        New-Item -ItemType Directory -Force -Path $exportRoot | Out-Null
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $patch = Join-Path $exportRoot "MarioTrickster-$stamp.patch"
        $manifest = Join-Path $exportRoot "MarioTrickster-$stamp.txt"
        Invoke-Git diff --binary HEAD "--output=$patch"
        @(
            "branch=$branch",
            "head=$head",
            "created=$(Get-Date -Format o)",
            "untracked_meta_count=$metaCount",
            "warning=The patch contains tracked-file changes only. Stage or separately copy new untracked assets before migration.",
            'test_summary=',
            (Get-TestSummary)
        ) | Set-Content -LiteralPath $manifest -Encoding UTF8
        Write-Host "Portable patch: $patch"
        Write-Host "Manifest: $manifest"
        Write-Host 'Attach both files to the new-computer or new-account handoff if GitHub push is unavailable.'
        exit 0
    }

    if ($otherUntracked.Count -gt 0) {
        throw 'Save stopped because there are unexpected untracked files. Review and add them explicitly, or remove them, before creating a checkpoint.'
    }

    $userName = (& git @GitArgs config user.name).Trim()
    $userEmail = (& git @GitArgs config user.email).Trim()
    if ([string]::IsNullOrWhiteSpace($userName) -or [string]::IsNullOrWhiteSpace($userEmail)) {
        throw 'Git user.name or user.email is missing in this Windows user profile. Configure them before Save mode.'
    }

    $testSummary = Get-TestSummary
    @(
        '# Latest portable checkpoint',
        '',
        "- Created: $(Get-Date -Format o)",
        "- Branch: $branch",
        "- Base HEAD before commit: $head",
        "- Untracked Unity .meta excluded from checkpoint: $metaCount",
        '- All meaningful tracked changes and the AI_CONTINUE_PACK are staged by this save operation.',
        '- New non-meta files outside the AI_CONTINUE_PACK and approved quick-start files are intentionally refused and require explicit review.',
        '',
        '## Latest Unity test summary',
        '',
        '```text',
        $testSummary,
        '```',
        '',
        '## Restore rule',
        '',
        'Clone the branch, read HANDOFF_STATE.md and this file, run bootstrap_windows.ps1, then open Unity and rerun affected tests.'
    ) | Set-Content -LiteralPath $CheckpointFile -Encoding UTF8

    Invoke-Git add -u
    Invoke-Git add -- docs/AI_CONTINUE_PACK
    Invoke-Git add -- @QuickStartFiles
    Invoke-Git diff --cached --check

    & git @GitArgs diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'Nothing staged for checkpoint. No commit was created.'
        exit 0
    }
    if ($LASTEXITCODE -ne 1) { throw 'Could not inspect staged changes.' }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        $Message = "checkpoint: portable handoff $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    }
    Invoke-Git commit -m $Message
    $newHead = (& git @GitArgs rev-parse HEAD).Trim()
    Write-Host "Checkpoint commit created: $newHead"

    if ($Push) {
        Invoke-Git push origin $branch
        Write-Host "Checkpoint pushed: origin/$branch"
    }
    else {
        Write-Host 'Checkpoint is committed locally but not pushed. Run the same command with -Push before changing computer or Manus account.'
    }
}
finally {
    Pop-Location
}
