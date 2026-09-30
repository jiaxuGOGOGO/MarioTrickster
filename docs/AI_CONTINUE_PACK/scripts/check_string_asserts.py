#!/usr/bin/env python3
"""沙盒里没有 Unity，跑不了 NUnit。本脚本把测试里 StringAssert.Contains/DoesNotContain(字面量, Read("文件")) 对照源码逐条检查，
提前发现"改了代码忘了改测试 / 测试写错字"。用法：python3 check_string_asserts.py <repo>/Assets"""
import re, glob, sys
root = (sys.argv[1] if len(sys.argv) > 1 else '/home/user/workspace/repo/Assets').rstrip('/') + '/'
def code_only(src): return '\n'.join(l.split('//')[0] for l in src.split('\n'))
bad = n = 0
for tf in glob.glob(root + 'Tests/EditMode/*.cs'):
    t = open(tf, encoding='utf-8').read()
    for body in re.split(r'\n    \[Test', t):
        reads = {}
        for m in re.finditer(r'(?:string|var) (\w+) = (CodeOnly\()?(?:File\.ReadAllText\(Path\.Combine\(Application\.dataPath, |Read\()"([^"]+)"', body):
            reads[m.group(1)] = (m.group(3), bool(m.group(2)))
        for m in re.finditer(r'StringAssert\.(Contains|DoesNotContain)\("((?:[^"\\]|\\.)*)",\s*(?:(\w+)\b(?!\()|(CodeOnly\()?Read\("([^"]+)"\))', body):
            kind, lit, var, co, direct = m.groups()
            if direct: f, c = direct, bool(co)
            elif var in reads: f, c = reads[var]
            else: continue
            try: src = open(root + f, encoding='utf-8').read().replace('\r\n', '\n')
            except FileNotFoundError: print('MISSING FILE', f); bad += 1; continue
            except IsADirectoryError: continue  # 路径不是简单字面量（Path.Combine 多段），跳过
            if c: src = code_only(src)
            n += 1
            lit2 = lit.replace('\\"', '"').replace('\\n', '\n').replace('\\\\', '\\')
            if (kind == 'Contains') != (lit2 in src):
                bad += 1; print('FAIL', tf.split('/')[-1], kind, f, lit[:90])
print('checked', n, 'bad', bad)
sys.exit(1 if bad else 0)
