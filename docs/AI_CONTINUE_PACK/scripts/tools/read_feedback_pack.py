#!/usr/bin/env python3
"""S242：读用户发来的反馈包（反馈包_MMdd_HHmm.zip），先看自动总结，再按严重度列出每条记录的黑匣子要点。
用法：python3 read_feedback_pack.py <zip> [--all]
输出：总结 + 每条记录（类型 / 时间 / 房间快照 / 最后 8 条面包屑 / 最后 6 行采样）+ Unity 日志错误前 40 行。截图用 gsk understand_images 另看。"""
import sys, zipfile, re, io
SEV={'BadNumber':5,'Error':5,'Stuck':4,'TimeFrozen':4,'Rescue':4,'HeldNoMove':3,'Mash':3,'Hitch':2,'Manual':1}
def main(p, show_all):
    z=zipfile.ZipFile(p); names=z.namelist()
    rd=lambda n: z.read(n).decode('utf-8','replace')
    print('文件：', len(names), '个，共', sum(i.file_size for i in z.infolist())//1024, 'KB（解压后）')
    if '00_给AI的话.md' in names: print(rd('00_给AI的话.md'))
    ev=[]
    if 'events.tsv' in names:
        for l in rd('events.tsv').splitlines():
            c=l.split('\t')
            if len(c)>=4: ev.append(c)
    ev.sort(key=lambda c:(-SEV.get(c[0],0), c[1]))
    seen=set()
    for c in (ev if show_all else ev[:12]):
        f=c[2]
        if f in seen or f not in names: continue
        seen.add(f); md=rd(f)
        print('\n'+'='*70+f'\n[{c[0]}] {c[1]} {f}：{c[3]}')
        m=re.search(r'## 房间快照.*?```\n(.*?)```', md, re.S)
        if m: print(m.group(1))
        m=re.search(r'## 出事前发生了什么.*?\n(.*?)\n## ', md, re.S)
        if m: print('\n'.join(m.group(1).strip().splitlines()[-8:]))
        rows=[l for l in md.splitlines() if l.startswith('| -') or l.startswith('| 0')]
        print('\n'.join(rows[-6:]))
    if 'UnityLog_errors.txt' in names:
        print('\n'+'='*70+'\nUnity 日志错误（前 40 行）'); print('\n'.join(rd('UnityLog_errors.txt').splitlines()[:40]))
    shots=[n for n in names if n.endswith('.jpg') or n.endswith('.png')]
    print('\n截图', len(shots), '张：', ' '.join(shots[:20]))
if __name__=='__main__':
    if len(sys.argv)<2: print(__doc__); sys.exit(1)
    main(sys.argv[1], '--all' in sys.argv)
