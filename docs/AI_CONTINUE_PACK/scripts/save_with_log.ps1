param([string]$Mode = 'Save', [switch]$Push)
# Runs save_checkpoint.ps1 in a child PowerShell and keeps its full output in .handoff\last-save.log.
# A child process is used on purpose: redirecting git's stderr inside the same PowerShell would turn
# harmless git warnings (for example "LF will be replaced by CRLF") into terminating errors.
$ErrorActionPreference = 'Continue'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$logDir = Join-Path $RepoRoot '.handoff'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'last-save.log'
"=== $(Get-Date -Format o) mode=$Mode push=$Push user=$env:USERNAME ===" | Set-Content -LiteralPath $log -Encoding UTF8

$childArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'save_checkpoint.ps1'), '-Mode', $Mode)
if ($Push) { $childArgs += '-Push' }

$global:LASTEXITCODE = 0
& powershell.exe @childArgs 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath $log -Append
$code = $LASTEXITCODE
"=== exit=$code ===" | Add-Content -LiteralPath $log -Encoding UTF8
exit $code
