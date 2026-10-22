param([switch]$IncludeWorldPark)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out=Join-Path $root '.work\worldpark-verify\compiled'
New-Item -ItemType Directory -Force $out | Out-Null
$sdk=(& dotnet --list-sdks | ForEach-Object { if($_ -match '^(\S+)\s+\[(.+)\]$'){ Join-Path $Matches[2] $Matches[1] } } | Where-Object { Test-Path (Join-Path $_ 'Roslyn\bincore\csc.dll') } | Select-Object -Last 1)
if(!$sdk){throw 'No .NET SDK with Roslyn csc.dll found (install .NET 8 SDK).'}
$csc=Join-Path $sdk 'Roslyn\bincore\csc.dll'
foreach($project in @('MarioTrickster','MarioTrickster.Editor','EditModeTests')){
    [xml]$x=[IO.File]::ReadAllText((Join-Path $root ($project+'.csproj')))
    $args=[Collections.Generic.List[string]]::new()
    $args.Add('-noconfig');$args.Add('-nostdlib+');$args.Add('-target:library');$args.Add('-langversion:9.0');$args.Add('-unsafe+');$args.Add('-nologo')
    $args.Add('-out:"'+(Join-Path $out ($project+'.dll'))+'"')
    $defines=@($x.Project.PropertyGroup | ForEach-Object {$_.DefineConstants} | Where-Object {$_})[0]
    if($defines){$args.Add('-define:'+$defines)}
    foreach($ref in $x.Project.ItemGroup.Reference){
        if(!$ref){continue};$p=[string]$ref.HintPath
        if(![IO.Path]::IsPathRooted($p)){$p=[IO.Path]::GetFullPath((Join-Path $root $p))}
        if([IO.Path]::IsPathRooted([string]$ref.HintPath)){$p=[string]$ref.HintPath}
        if(!(Test-Path -LiteralPath $p)){throw "Missing actual reference: $p"}
        $args.Add('-reference:"'+$p+'"')
    }
    foreach($pr in $x.Project.ItemGroup.ProjectReference){
        if(!$pr){continue};$name=[IO.Path]::GetFileNameWithoutExtension([string]$pr.Include)
        $p=Join-Path $out ($name+'.dll')
        if(!(Test-Path $p)){$p=Join-Path $root ('Library\ScriptAssemblies\'+$name+'.dll')}
        if(!(Test-Path $p)){throw "Missing project reference: $p"};$args.Add('-reference:"'+$p+'"')
    }
    $sources=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($s in $x.Project.ItemGroup.Compile){if($s){[void]$sources.Add([IO.Path]::GetFullPath((Join-Path $root $s.Include)))}}
    if($IncludeWorldPark){
        $extras=switch($project){'MarioTrickster'{@(Get-ChildItem Assets/Scripts/WorldPark -Filter '*.cs' -File)}'MarioTrickster.Editor'{@(Get-ChildItem Assets/Scripts/Editor -Filter 'WorldPark*.cs' -File)}'EditModeTests'{@(Get-ChildItem Assets/Tests/EditMode -Filter 'WorldPark*.cs' -File)}}
        foreach($s in $extras){[void]$sources.Add($s.FullName)}
    }
    foreach($s in $sources){$args.Add('"'+$s+'"')}
    $rsp=Join-Path $out ($project+'.rsp')
    [IO.File]::WriteAllLines($rsp,$args,[Text.UTF8Encoding]::new($false))
    Write-Output "COMPILE $project ($($sources.Count) sources)"
    & dotnet $csc ('@'+$rsp)
    if($LASTEXITCODE -ne 0){throw "Compilation failed: $project"}
}
Write-Output 'Actual Unity-referenced runtime/editor/test compilation passed; no native Unity tests were executed.'
