# -*- coding: utf-8 -*-
"""9-R5 보조: 끈기 진 판 몫(0 / 0.5 / 1) 비교 + 봇 시드별 허들. theory9r5.py의 모델 부분만 불러 쓴다(결정적).
다시 돌리기: python -X utf8 BalanceData/r9/theory/theory9r5_h.py → theory9r5_h_out.txt"""
import os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'theory9r5.py'), encoding='utf-8').read()
src = src[:src.index("P(chr(10) + '## 0b.")]
g = {'__file__': os.path.join(HERE, 'theory9r5.py'), '__name__': 'r5'}
exec(compile(src, 'theory9r5.py', 'exec'), g)
run, show, tune, P, OUT, TGT, K, S, math, zmul = (g[k] for k in ('run', 'show', 'tune', 'P', 'OUT', 'TGT', 'K', 'S', 'math', 'zmul'))
R5 = g['R5']
del OUT[:]
TGT.update({80: 14})
WT4 = 1 + 0.92 * 0.12 * 3
res = {}
for w in (0.5, 0.3):
    gr = (1.05, 3.0, w)
    hp = tune(R5, grit=gr)
    mul = {n: hp[n] / (40 * zmul(n)) for n in hp}
    P(chr(10) + f'## 진 판 끈기 몫 {w} (파밍 판 1) · 판마다 x1.05 · 상한 x3 — 허들 평균 목표 {TGT}')
    P('  체력: ' + ' · '.join(f'{n}: {v:.3g}' for n, v in hp.items()) + ' | mul ' + ' · '.join(f'{n}: {v:.0f}' for n, v in mul.items()) +
      ' | LOOP_BOSS ' + ' · '.join(f'{n}: {mul[n]/mul[60]:.3g}' for n in mul) + f' | LOOP_LATER 하한 {mul[100]/mul[60]*zmul(100)/zmul(110):.3f}')
    for tg, lab in ((1.0, '세계수 없음'), (WT4, '세계수 4단계')):
        for f in (0, 1, 2, 4):
            show(f'{lab} · 파밍 {f}판', run(R5, hp, farm=f, tg=tg, grit=gr))
    res[w] = hp
P(chr(10) + '## 봇 시드별(k10, 각 시드 1명 = 판 흔들림만) — 진 판 몫 0.5 체력')
for s0 in range(1, 7):
    OPd = g['OP']; Ks = {s: K[s0] for s in K}; Os = {s: OPd[s0] for s in OPd}
    old = dict(K); oldo = dict(OPd); K.clear(); K.update(Ks); OPd.clear(); OPd.update(Os)
    o, tm, _ = run(R5, res[0.5], players=60, bot=True, inc=1.0, rmin=1.16, grit=(1.05, 3.0, 0.5))
    K.clear(); K.update(old); OPd.clear(); OPd.update(oldo)
    P(f'  시드 {s0}: 100 도달 {tm + 52:.0f}분 | 허들 평균 ' + ' · '.join(f'{n}:{v["mean"]:.1f}' for n, v in o.items()) + ' | 판당 실패 ' + ' · '.join(f'{n}:{v["fail"]:.0%}' for n, v in o.items()))
open(os.path.join(HERE, 'theory9r5_h_out.txt'), 'w', encoding='utf-8').write(chr(10).join(OUT) + chr(10))
print(chr(10).join(OUT))
