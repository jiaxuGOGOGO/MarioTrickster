#!/usr/bin/env bash
# 新对话第一步：在沙盒里搭好"无 Unity 验证环境"。约 1–2 分钟。可重复运行。
# 产物：$WS/repo（仓库，分支 genspark_ai_developer）、$WS/unityref（Unity/NUnit 参考 DLL）、
#       $WS/cc（运行时代码编译）、$WS/cc2/full（编辑器+测试编译）、$WS/sim（纯逻辑体检）
set -e
unset version   # 沙盒里 version=N/A 会让 dotnet 报错
WS=${WS:-/home/user/workspace}
PACK=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$WS" && cd "$WS"
if [ ! -d repo/.git ]; then git clone -q -b genspark_ai_developer https://github.com/jiaxuGOGOGO/MarioTrickster.git repo; else git -C repo fetch -q origin && git -C repo checkout -q genspark_ai_developer && git -C repo pull -q --ff-only || true; fi
git -C repo config user.name "MarioTrickster AI"; git -C repo config user.email "ai@mariotrickster.local"
# 接续包里自带"还没上传到 GitHub"的补丁（换账号/用户忘了跑 bat 时也不丢进度）：缺哪个补哪个
if ls "$PACK/pending/"*.patch >/dev/null 2>&1; then
  for p in "$PACK/pending/"*.patch; do
    subj=$(sed -n 's/^Subject: \[PATCH[^]]*\] //p' "$p" | head -1)
    if git -C repo log --format=%s -200 | grep -qF "$subj"; then echo "已有: $subj"; else
      git -C repo am -q --whitespace=nowarn "$p" && echo "补上: $subj" || { git -C repo am --abort; echo "补丁冲突: $p（说明 GitHub 上已有更新的改动，按 git log 判断）"; }
    fi
  done
fi
fetch() { # id version dir
  [ -d "unityref/$3/lib" ] && return 0
  mkdir -p "unityref/$3" && curl -sL "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" -o "unityref/$3.nupkg" && (cd "unityref/$3" && unzip -qo "../$3.nupkg" && chmod -R u+rwX .)
}
fetch unityengine.modules 2021.3.33 mods
fetch unitytechnologies.unityeditor 2020.2.2.1 ue2020
fetch nunit 3.13.3 nunit
chmod -R u+rwX unityref   # nupkg 解压出的文件没有读权限（踩过的坑：编译报 MonoBehaviour 找不到）
REFS=""
for d in unityref/mods/lib/netstandard2.0/*.dll; do n=$(basename "$d" .dll); REFS="$REFS<Reference Include=\"$n\"><HintPath>$WS/$d</HintPath></Reference>"; done
REFS="$REFS<Reference Include=\"UnityEditor\"><HintPath>$WS/unityref/ue2020/lib/UnityEditor.dll</HintPath></Reference>"
NOWARN="CS0618;CS0414;CS0649;CS0169;CS0067;CS0219;CS0162;CS0168;CS8632;CS1701;CS1702;CS0436;CS0108"
mkdir -p cc/stubs cc2/full sim
cp "$PACK/stubs/RuntimeStubs.cs" cc/stubs/Stubs.cs
cp "$PACK/stubs/EditorStubs.cs" cc2/full/stubTR.cs
cat > cc/cc.csproj <<X
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion><Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>cc</AssemblyName><DefineConstants>UNITY_EDITOR;UNITY_2022_2_OR_NEWER</DefineConstants><NoWarn>$NOWARN</NoWarn></PropertyGroup>
<ItemGroup><Compile Include="$WS/repo/Assets/Scripts/**/*.cs" Exclude="$WS/repo/Assets/Scripts/Editor/**" /><Compile Include="$WS/repo/Assets/SpriteEffectFactory/Runtime/**/*.cs" /><Compile Include="stubs/*.cs" /></ItemGroup>
<ItemGroup>$REFS</ItemGroup></Project>
X
cat > cc2/full/full.csproj <<X
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>latest</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>UNITY_EDITOR;UNITY_2022_2_OR_NEWER</DefineConstants><NoWarn>$NOWARN</NoWarn></PropertyGroup>
<ItemGroup><Compile Include="$WS/repo/Assets/Scripts/Editor/**/*.cs" Exclude="$WS/repo/Assets/Scripts/Editor/LevelTemplateValidatorWindow.cs;$WS/repo/Assets/Scripts/Editor/TestReportRunner.cs" /><Compile Include="$WS/repo/Assets/Tests/EditMode/*.cs" /><Compile Include="$WS/repo/Assets/SpriteEffectFactory/Editor/**/*.cs" /><Compile Include="stubTR.cs" /></ItemGroup>
<ItemGroup>$REFS<Reference Include="nunit.framework"><HintPath>$WS/unityref/nunit/lib/netstandard2.0/nunit.framework.dll</HintPath></Reference><Reference Include="rt"><HintPath>$WS/cc/out/cc.dll</HintPath></Reference></ItemGroup></Project>
X
cp "$PACK/sim/FakeUnity.cs" "$PACK/sim/Check.cs" sim/
SIMSRC=""
for f in LevelDesign/LevelReachabilityAnalyzer LevelDesign/AsciiLevelValidator LevelDesign/AsciiElementRegistry LevelDesign/PhysicsMetrics LevelDesign/PhysicsConfigSO Editor/LevelStudioDocument Gameplay/Step1/Step1Layout LevelDesign/ElementCatalog LevelDesign/LevelDeadlockAnalyzer Editor/LevelWorkshopModel Gameplay/Step1/Step1ComboFeel LevelDesign/ComboRouteAnalyzer LevelDesign/LevelPathPlanner LevelDesign/FloorStacker LevelDesign/HakoniwaAnalyzer LevelDesign/StrategySim LevelDesign/DetourPlanner; do SIMSRC="$SIMSRC<Compile Include=\"$WS/repo/Assets/Scripts/$f.cs\" />"; done
cat > sim/sim.csproj <<X
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable><NoWarn>$NOWARN</NoWarn></PropertyGroup>
<ItemGroup><Compile Include="FakeUnity.cs" /><Compile Include="Check.cs" />$SIMSRC</ItemGroup></Project>
X
echo "SETUP OK: repo=$(git -C repo log --oneline -1)"
