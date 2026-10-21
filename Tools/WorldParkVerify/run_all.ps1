<#
  WorldPark 一键验证（不需要 Unity 许可证；不能代替 Unity EditMode/PlayMode 与真人试玩）
  用法（仓库根目录）：
    powershell -ExecutionPolicy Bypass -File Tools\WorldParkVerify\run_all.ps1
    ... -SkipCompile     跳过 Unity 引用编译（没有装 Unity 或 Unity 未生成 .csproj 时）
    ... -SkipWebBuild    不重建网页设计台 index.html
  结果：屏幕汇总 + .work\worldpark-verify\last-run.txt；任一步 FAIL 时退出码为 1。
#>
param([switch]$SkipCompile, [switch]$SkipWebBuild)
$ErrorActionPreference = 'Continue'
$kit  = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $kit '..\..')).Path
$out  = Join-Path $repo '.work\worldpark-verify'
New-Item -ItemType Directory -Force $out | Out-Null
Set-Location $repo
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:PYTHONIOENCODING = 'utf-8'; $env:PYTHONUTF8 = '1'
$results = [Collections.Generic.List[object]]::new()

function Step([string]$name, [scriptblock]$body, [string]$expect) {
    Write-Host "`n=== $name" -ForegroundColor Cyan
    $text = (& $body 2>&1 | ForEach-Object { "$_" }) -join "`n"
    $code = $LASTEXITCODE
    $tail = ($text -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 4) -join ' | '
    $ok = ($code -eq 0) -and (!$expect -or $text -match $expect)
    $status = if ($ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("[{0}] {1}" -f $status, $tail) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    $results.Add([pscustomobject]@{ Step = $name; Status = $status; Detail = $tail })
}
function Skip([string]$name, [string]$why) {
    Write-Host "`n=== $name`n[SKIP] $why" -ForegroundColor Yellow
    $results.Add([pscustomobject]@{ Step = $name; Status = 'SKIP'; Detail = $why })
}
function Find-Python {
    foreach ($c in @('py', 'python', 'python3')) {
        $cmd = Get-Command $c -ErrorAction SilentlyContinue
        if ($cmd -and $cmd.Source -notmatch 'WindowsApps') { return $cmd.Source }
    }
    $local = Join-Path $repo '.work\tools\python-3.12.10\python.exe'
    if (Test-Path $local) { return $local }
    return $null
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-Host '需要 .NET 8 SDK（dotnet）。' -ForegroundColor Red; exit 1 }
if (!(Get-Command node -ErrorAction SilentlyContinue))   { Write-Host '需要 Node.js（node）。' -ForegroundColor Red; exit 1 }

# 1. Unity 真实程序集引用下编译运行时 / 编辑器 / 测试三个程序集
if ($SkipCompile) { Skip 'Unity引用编译' '按参数跳过' }
elseif (!(Test-Path (Join-Path $repo 'MarioTrickster.csproj'))) { Skip 'Unity引用编译' '缺少 Unity 生成的 MarioTrickster.csproj：先用 Unity 打开一次项目' }
else { Step 'Unity引用编译' { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $kit 'compile-unity-code.ps1') -IncludeWorldPark } 'compilation passed' }

# 2. C# 纯逻辑（直接编译项目源码，不用复制品）
Step 'S248核心规则'          { dotnet run --project (Join-Path $kit 'Core\WorldParkCore.csproj') }          'PASS|passed|通过'
Step 'S249探索与真实碰撞'     { dotnet run --project (Join-Path $kit 'Explore\WorldParkExplore.csproj') }    'PASS|passed|通过'
Step 'S250检查点模块'        { dotnet run --project (Join-Path $kit 'Progress\WorldParkProgress.csproj') }  'PASS|passed|通过'
Step 'S250三会场旅程实走'     { dotnet run --project (Join-Path $kit 'Journey\WorldParkJourney.csproj') }    '通过'
Step 'S250 Unity点位画布规则' { dotnet run --project (Join-Path $kit 'Spatial\WorldParkSpatial.csproj') }    'passed'

# 3. 网页与 C# 同包同动作逐状态对比
Step '网页↔C#跨端一致'       { node (Join-Path $kit 'parity.cjs') } '差异: 0'
Step 'S253网页探索内核跨端' { node (Join-Path $kit 'explore-parity.cjs') } '差异: 0'
Step 'S253网页试玩画面跨端' { node (Join-Path $kit 'play-parity.cjs') } '差异: 0'
Step 'S254会场体检'          { node (Join-Path $kit 'venue-check.cjs') } '会场体检: '
# 3b. S251 剧情带入模拟：多人格玩家按公开信息走完主世界，检验作者预期/公平/威胁/张力/检查点
Step 'S251剧情带入模拟'      { dotnet run --project (Join-Path $kit 'Story\WorldParkStory.csproj') } 'story simulation PASS'

# 4. 网页设计台
Step '网页点位画布'          { node (Join-Path $repo 'Tools\LevelStudioWeb\worldpark-spatial.test.js') } 'passed'
Step '网页安全网'            { node (Join-Path $repo 'Tools\LevelStudioWeb\worldpark-history.test.js') } 'assertions passed'
Step '网页博弈彩排冒烟'      { node (Join-Path $kit 'web-game-smoke.cjs') } 'web game smoke PASS'
Step '网页世界脚本语法'      { foreach ($f in 'borderland.js', 'worldpark.js', 'explore.js', 'venue-check.js', 'play-model.js', 'play.js', 'worldpark-history.js', 'worldpark-app.js', 'worldpark-spatial.js', 'app.js') { node --check (Join-Path $repo "Tools\LevelStudioWeb\$f"); if ($LASTEXITCODE) { break } }; if (!$LASTEXITCODE) { 'syntax ok' } } 'syntax ok'
$py = Find-Python
if ($SkipWebBuild) { Skip '网页设计台重建' '按参数跳过' }
elseif (!$py) { Skip '网页设计台重建' '找不到 Python（装 Python 3.12+ 或放便携版到 .work\tools\python-3.12.10）' }
else {
    Step '网页设计台重建' {
        & $py (Join-Path $repo 'Tools\LevelStudioWeb\build.py')
        if ($LASTEXITCODE) { return }
        node -e "const fs=require('fs'),vm=require('vm');const h=fs.readFileSync(process.argv[1],'utf8');const r=/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/gi;let m,n=0;while((m=r.exec(h))){new vm.Script(m[1]);n++;}console.log('inline scripts ok',n)" (Join-Path $repo 'Tools\LevelStudioWeb\index.html')
    } 'inline scripts ok'
}

# 4b. S253 真实浏览器自动试玩：无界面 Edge/Chrome 用真实按键走完主世界（含被鬼抓→重来），收集页面错误并截图到 .work\worldpark-verify\web-play\
#     没有浏览器、或调试端口连不上（受限环境）时 SKIP；游戏本身的问题照常 FAIL。
$smokeOut = (& node (Join-Path $kit 'web-play-smoke.cjs') 2>&1 | ForEach-Object { "$_" }) -join "`n"; $smokeCode = $LASTEXITCODE
if ($smokeOut -match '网页试玩冒烟: SKIP（(.*)）') { Skip 'S253真实浏览器试玩' $Matches[1] }
else { Step 'S253真实浏览器试玩' { $smokeOut; $global:LASTEXITCODE = $smokeCode } '错误: 0' }

# 5. 功能地图文档与 C# 唯一来源一致（只比较，不改文件）
Step '功能地图文档同步' {
    $tmp = Join-Path $out 'FEATURE_MAP.generated.md'
    dotnet run --project (Join-Path $kit 'Snapshot\WorldParkSnapshot.csproj') -- map $tmp | Out-Null
    if ($LASTEXITCODE) { return }
    $a = [IO.File]::ReadAllText($tmp).Replace("`r`n", "`n").TrimEnd()
    $b = [IO.File]::ReadAllText((Join-Path $repo 'docs\FEATURE_MAP.md')).Replace("`r`n", "`n").TrimEnd()
    if ($a -eq $b) { 'feature map in sync' } else { "docs/FEATURE_MAP.md 已过期：运行 dotnet run --project Tools\WorldParkVerify\Snapshot\WorldParkSnapshot.csproj -- map docs\FEATURE_MAP.md"; $global:LASTEXITCODE = 1 }
} 'feature map in sync'

$summary = $results | Format-Table -AutoSize | Out-String -Width 220
Write-Host "`n$summary"
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$head = (git -c safe.directory=* rev-parse --short HEAD 2>$null)
"WorldPark verify  $stamp  HEAD=$head`n$summary`n注意：以上不包含 Unity 原生 EditMode/PlayMode 与真人试玩。" | Set-Content -Encoding UTF8 (Join-Path $out 'last-run.txt')
if ($results.Status -contains 'FAIL') { Write-Host '有失败项，见上方。' -ForegroundColor Red; exit 1 }
Write-Host '全部通过（SKIP 项除外）。仍需在 Unity 中运行 EditMode/PlayMode 并实际试玩。' -ForegroundColor Green
exit 0
