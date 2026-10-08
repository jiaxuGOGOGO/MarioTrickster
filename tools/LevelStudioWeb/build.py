#!/usr/bin/env python3
"""关卡设计台（网页）构建脚本：从项目 C# 源码读元素表与样板，生成单文件 index.html。
元素/样板/规则改了就重跑：python3 tools/LevelStudioWeb/build.py  → tools/LevelStudioWeb/index.html"""
import json, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(HERE, '..', '..', 'Assets', 'Scripts')
rd = lambda p: open(os.path.join(S, p), encoding='utf-8').read()
reg, cat = rd('LevelDesign/AsciiElementRegistry.cs'), rd('LevelDesign/ElementCatalog.cs')
phys = {}
for m in re.finditer(r"asciiChar = '(.)', elementName = \"(\w+)\", isSolid = (\w+), isHazard = (\w+), jumpBoost = ([\d.]+)f.*?visualColor = new Color\(([\d.f, ]+)\)", reg, re.S):
    rgb = [float(v.strip().rstrip('f')) for v in m.group(6).split(',')][:3]
    phys[m.group(1)] = dict(so=m.group(3) == 'true', hz=m.group(4) == 'true', jb=float(m.group(5)) > 0, rgb=rgb)
fixed = {'M': [0.9, 0.2, 0.2], 'T': [0.2, 0.4, 0.9], 'G': [0.98, 0.8, 0.25], '.': [0.11, 0.1, 0.17]}
els = []
for m in re.finditer(r"I\('(.)', \"(\w+)\", \"([^\"]+)\", \"([^\"]+)\", Role\.(\w+), \"((?:[^\"\\]|\\.)*)\", \"((?:[^\"\\]|\\.)*)\"([^)]*)\)", cat):
    c, rest = m.group(1), m.group(8)
    if c == ' ': continue
    p = phys.get(c, {})
    els.append(dict(c=c, k=m.group(2), zh=m.group(3), en=m.group(4), r=m.group(5), w=m.group(6).replace('**', ''), p=m.group(7),
                    u='unique: true' in rest, s='needsSupport: true' in rest, s1='step1: true' in rest,
                    m=1 if 'muzzle: 1' in rest else -1 if 'muzzle: -1' in rest else 0,
                    so=p.get('so', False), hz=p.get('hz', False), jb=p.get('jb', False),
                    rgb=[round(v, 2) for v in fixed.get(c, p.get('rgb', [0.5, 0.5, 0.5]))]))
def arr(path, name):
    s = rd(path); i = s.index(name); j = s.index('};', i)
    return re.findall(r'"([^"]+)"', s[i:j])
samples = {'默认恶作剧房间': arr('Editor/Step1PrankRoomBuilder.cs', 'public static readonly string[] Room =')}
wm = rd('Editor/LevelWorkshopModel.cs')
names = {'PrisonSample': '两层监狱', 'LureSample': '诱捕走廊', 'HakoniwaSample': '箱庭监狱（四层）', 'LongHallSample': '长廊远征（大房间）'}
for f in re.findall(r'public static readonly string\[\] (\w+Sample) =', wm):
    samples[names.get(f, f)] = arr('Editor/LevelWorkshopModel.cs', f'public static readonly string[] {f} =')
# 像素图导入/导出靠颜色区分元素：颜色完全相同的（如左右两门大炮）在网页里错开一点点
seen = set()
for e in els:
    k = tuple(round(v * 255) for v in e['rgb'])
    while k in seen:
        e['rgb'] = [round(min(1, v + 0.04), 2) for v in e['rgb']]; k = tuple(round(v * 255) for v in e['rgb'])
    seen.add(k)
# S210：大地图格子表（OverworldCatalog.cs）+ 样板小镇（OverworldPack.cs）+ 门可连的房间名（LevelWorkshopModel.SampleRooms）
owc = rd('Overworld/OverworldCatalog.cs')
ow_tiles = []
for m in re.finditer(r"T\('(.)', \"(\w+)\", \"([^\"]+)\", \"([^\"]+)\", \"(\w+)\", (\w+), (\w+), (\w+), (\d+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, \"((?:[^\"\\]|\\.)*)\", \"((?:[^\"\\]|\\.)*)\"\)", owc):
    c = m.group(1)
    ow_tiles.append(dict(c=c, k=m.group(2), zh=m.group(3), en=m.group(4), r=m.group(5), solid=m.group(6) == 'true', sight=m.group(7) == 'true', hides=m.group(8) == 'true', cost=int(m.group(9)),
                         rgb=[float(m.group(10)), float(m.group(11)), float(m.group(12))], w=m.group(13).replace('\\"', '"'), p=m.group(14).replace('\\"', '"')))
owp = rd('Overworld/OverworldPack.cs'); i = owp.index('SampleText = string.Join'); j = owp.index('}) + ', i)
ow_sample = '\n'.join(bytes(x, 'utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') for x in re.findall(r'^        "((?:[^"\\]|\\.)*)",$', owp[i:j], re.M)) + '\n'
i2 = owp.index('BigSampleText = string.Join'); j2 = owp.index('}) + ', i2)
ow_big = '\n'.join(bytes(x, 'utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') for x in re.findall(r'^        "((?:[^"\\]|\\.)*)",$', owp[i2:j2], re.M)) + '\n'
i3 = owp.index('MountainSampleText = string.Join'); j3 = owp.index('}) + ', i3)
ow_mtn = '\n'.join(bytes(x, 'utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') for x in re.findall(r'^        "((?:[^"\\]|\\.)*)",$', owp[i3:j3], re.M)) + '\n'
i4 = owp.index('StormSampleText = string.Join'); j4 = owp.index('}) + ', i4)
ow_storm = '\n'.join(bytes(x, 'utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') for x in re.findall(r'^        "((?:[^"\\]|\\.)*)",$', owp[i4:j4], re.M)) + '\n'
# S220：伤害说明表（OverworldCatalog.Harm）
ow_harm = {m.group(1): m.group(2) for m in re.finditer(r"\{ '(.)', \"((?:[^\"\\]|\\.)*)\" \}", owc)}
# S220：像素图标（OverworldArt.cs，和游戏里同一套）
art = rd('Overworld/OverworldArt.cs')
ow_pal = {m.group(1): [float(v) for v in m.group(2).replace('f', '').split(',')] for m in re.finditer(r"\{ '(.)', new\[\] \{ ([\d.f, ]+) \} \}", art)}
ow_tileic = {m.group(1): m.group(2) for m in re.finditer(r"\{ '(.)', \"(\w+)\" \}", art)}
ow_icons = {}
for m in re.finditer(r'\{ "(\w+)", new\[\] \{(.*?)\} \}', art, re.S):
    rows = re.findall(r'"([^"]+)"', m.group(2))
    if len(rows) == 16 and all(len(r) == 16 for r in rows): ow_icons[m.group(1)] = rows
ow_art = dict(pal=ow_pal, tile=ow_tileic, icons=ow_icons)
room_names = [re.search(r'DefaultRoomName = "([^"]+)"', wm).group(1)] + re.findall(r'\("([^"]+)", \w+Sample\)', wm)
sample_room = {n: names.get(f, f) for n, f in re.findall(r'\("([^"]+)", (\w+Sample)\)', wm)}
sample_room[room_names[0]] = '默认恶作剧房间'
data = 'const OW_TILES=' + json.dumps(ow_tiles, ensure_ascii=False, separators=(',', ':')) + ';\nconst OW_SAMPLE=' + json.dumps(ow_sample, ensure_ascii=False) + ';\nconst OW_BIG_SAMPLE=' + json.dumps(ow_big, ensure_ascii=False) + ';\nconst OW_MTN_SAMPLE=' + json.dumps(ow_mtn, ensure_ascii=False) + ';\nconst OW_STORM_SAMPLE=' + json.dumps(ow_storm, ensure_ascii=False) + ';\nconst OW_HARM=' + json.dumps(ow_harm, ensure_ascii=False) + ';\nconst OW_ART=' + json.dumps(ow_art, separators=(',', ':')) + ';\nconst OW_ROOMS=' + json.dumps(sample_room, ensure_ascii=False, separators=(',', ':')) + ';\n'
# S232：小镇居民台词表（Assets/Resources/TownStories.json，和 Unity 读同一个文件）+ 调参默认值（MarioMindTuningSO.cs，网页只读、不另存一份）
stories = json.load(open(os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'TownStories.json'), encoding='utf-8'))['lines']
for l in stories:
    l.setdefault('needs', []); l.setdefault('once', False); l.setdefault('coolDays', 3); l.setdefault('tier', 2); l.setdefault('tone', 'comic'); l.setdefault('who', 'door'); l.setdefault('when', 'back')
tsrc = rd('Gameplay/Step1/MarioMindTuningSO.cs'); tun = {}
for m in re.finditer(r'(?:\[Tooltip\("((?:[^"\\]|\\.)*)"\)\]\s*)?(?:\[Range\(([-\d.f]+), ([-\d.f]+)\)\]\s*)?public (float|int|bool) (\w+) = ([^;]+);', tsrc):
    tip, lo, hi, ty, name, val = m.groups(); val = val.strip().rstrip('f')
    try: v = (val == 'true') if ty == 'bool' else (int(val) if ty == 'int' else float(val))
    except ValueError: continue
    e = {'t': ty, 'v': v}
    if tip: e['tip'] = tip
    if lo is not None: e['min'] = float(lo.rstrip('f')); e['max'] = float(hi.rstrip('f'))
    tun[name] = e
ta = rd('Gameplay/Step1/TuningAudit.cs'); ti = ta.index('public static readonly string[] Rules ='); tj = ta.index('};', ti)
tu_rules = [x.encode('utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') for x in re.findall(r'"((?:[^"\\]|\\.)*)",', ta[ti:tj])]
data += 'const TUNING_RULES=' + json.dumps(tu_rules, ensure_ascii=False) + ';\n'
data += 'const TS_STORIES=' + json.dumps(stories, ensure_ascii=False, separators=(',', ':')) + ';\nconst TUNING=' + json.dumps(tun, ensure_ascii=False, separators=(',', ':')) + ';\n'
data += 'const ELEMENTS=' + json.dumps(els, ensure_ascii=False, separators=(',', ':')) + ';\nconst SAMPLES=' + json.dumps(samples, ensure_ascii=False, separators=(',', ':')) + ';\n'
# S236：功能地图（FeatureMap.cs，和 Unity 开始页同一份）
fm = rd('LevelDesign/FeatureMap.cs')
Q = r'"((?:[^"\\]|\\.)*)"'
un = lambda x: x.replace('\\"', '"')
fm_all = []
for m in re.finditer(r'\n        F\(' + ', '.join([Q] * 7) + r', (\d+), ' + ', '.join([Q] * 6) + r'\),', fm):
    g = [un(x) for x in m.groups()]
    fm_all.append(dict(id=g[0], area=g[1], tier=g[2], name=g[3], how=g[4], what=g[5], create=g[6], since=int(g[7]), docs=g[8], menu=g[9], keys=g[10], web=g[11]))
def fm_list(name):
    i = fm.index(name); j = fm.index('};', i); return [un(x) for x in re.findall(Q, fm[i:j])]
fm_goals = [dict(title=un(a), ids=b.split(' '), steps=un(c)) for a, b, c in re.findall(r'G\(' + Q + ', ' + Q + ', ' + Q + r'\)', fm)]
data += 'const FEATURE_MAP=' + json.dumps(dict(areas=fm_list('Areas ='), tiers=fm_list('Tiers ='), all=fm_all, goals=fm_goals), ensure_ascii=False, separators=(',', ':')) + ';\n'
logic = open(os.path.join(HERE, 'logic.js'), encoding='utf-8').read()
logic = re.sub(r"if \(typeof module[^\n]*\n?", '', logic)
html = open(os.path.join(HERE, 'shell.html'), encoding='utf-8').read()
ow = re.sub(r"if \(typeof module[^\n]*\n?", '', open(os.path.join(HERE, 'overworld.js'), encoding='utf-8').read())
html = html.replace('/*DATA*/', data).replace('/*LOGIC*/', logic + '\n' + ow).replace('/*APP*/', open(os.path.join(HERE, 'app.js'), encoding='utf-8').read())
open(os.path.join(HERE, 'index.html'), 'w', encoding='utf-8').write(html)
print(f'index.html: {len(fm_all)} 项功能地图, {len(stories)} 句居民台词, {len(tun)} 个调参值, {len(els)} 个元素, {len(samples)} 个样板, {len(ow_tiles)} 种小镇格子, {len(ow_harm)} 条伤害说明, {len(ow_icons)} 个像素图标, {len(html)//1024} KB')
