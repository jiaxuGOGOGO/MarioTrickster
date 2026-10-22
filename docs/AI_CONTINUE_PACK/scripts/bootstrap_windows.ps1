[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$ExpectedBranch = 'genspark_ai_developer'
$ExpectedUnity = '2022.3.61f1'
$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe'
$failed = $false

function Report([string]$Label, [bool]$Ok, [string]$Detail) {
    $prefix = if ($Ok) { '[OK]' } else { '[MISSING]' }
    Write-Host "$prefix $Label - $Detail"
    if (-not $Ok) { $script:failed = $true }
}

function Info([string]$Label, [bool]$Ok, [string]$Detail) {
    $prefix = if ($Ok) { '[OK]' } else { '[OPTIONAL]' }
    Write-Host "$prefix $Label - $Detail"
}

Write-Host "MarioTrickster bootstrap check"
Write-Host "Repository: $RepoRoot"

$git = Get-Command git -ErrorAction SilentlyContinue
Report 'Git' ($null -ne $git) ($(if ($git) { $git.Source } else { 'git is not on PATH' }))
if ($git) {
    $gitSafeArgs = @('-c', "safe.directory=$RepoRoot")
    Push-Location $RepoRoot
    try {
        $inside = ((& git @gitSafeArgs rev-parse --is-inside-work-tree 2>$null) -eq 'true')
        Report 'Git worktree' $inside ($(if ($inside) { 'repository detected' } else { 'not a Git repository' }))
        if ($inside) {
            $branch = (& git @gitSafeArgs branch --show-current).Trim()
            Report 'Branch' ($branch -eq $ExpectedBranch) "current=$branch expected=$ExpectedBranch"
            Write-Host '--- Git status ---'
            & git @gitSafeArgs status --short --branch
            Write-Host '--- Latest commit ---'
            & git @gitSafeArgs log -1 --oneline
        }
    }
    finally {
        Pop-Location
    }
}

Report "Unity $ExpectedUnity" (Test-Path $Unity) ($(if (Test-Path $Unity) { $Unity } else { 'install through Unity Hub with Windows Standalone Support' }))
Info 'Visual Studio' (Test-Path 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe') 'Unity game-development workload recommended'

$node = Get-Command node -ErrorAction SilentlyContinue
Info 'Node.js' ($null -ne $node) ($(if ($node) { (& $node --version) } else { 'optional: needed for web studio work' }))

$python = Get-Command py -ErrorAction SilentlyContinue
if ($null -eq $python) { $python = Get-Command python -ErrorAction SilentlyContinue }
Info 'Python' ($null -ne $python) ($(if ($python) { (& $python --version) } else { 'optional: Python 3.12+ for helper scripts' }))

$versionFile = Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt'
$versionText = if (Test-Path $versionFile) { Get-Content -LiteralPath $versionFile -Raw } else { '' }
Report 'Project version file' ($versionText -match [regex]::Escape($ExpectedUnity)) $versionFile

if ($failed) {
    Write-Host 'Bootstrap check found missing requirements. Install or correct them before opening the project.'
    exit 1
}

Write-Host 'Bootstrap check passed. Open this repository in Unity Hub, then run EditMode before coding.'
