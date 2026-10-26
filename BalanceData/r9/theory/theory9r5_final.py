# -*- coding: utf-8 -*-
"""9-R5 최종안: 끈기 (진 판 +1점 · 파밍 판 +2점, 2점마다 보스 피해 x1.05, 상한 x3) + theory9r5_h.py 진 판 몫 0.5 맞춤 체력.
LOOP_BOSS는 3자리로 반올림하고 체력을 다시 계산해 같은 모델로 확인한다. 사람 시드별 허들도 낸다. final_*_r5.json을 쓴다(결정적).
다시 돌리기(순서): theory9r5.py → theory9r5_h.py → theory9r5_final.py (python -X utf8 BalanceData/r9/theory/...)"""
import os, json, shutil
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'theory9r5.py'), encoding='utf-8').read()
src = src[:src.index("P(chr(10) + '## 0b.")]
g = {'__file__': os.path.join(HERE, 'theory9r5.py'), '__name__': 'r5'}
exec(compile(src, 'theory9r5.py', 'exec'), g)
run, show, P, OUT, K, OP, S, math, zmul = (g[k] for k in ('run', 'show', 'P', 'OUT', 'K', 'OP', 'S', 'math', 'zmul'))
R5 = g['R5']
del OUT[:]
GR = (1.05, 3.0, 0.5)
WT4 = 1 + 0.92 * 0.12 * 3
BASE = 157                                     # BOSS_LOOP_HP_MUL (theory9r5_h 맞춤 mul 60 = 157)
LB = {60: 1.0, 70: 3.75, 80: 7.98, 90: 32.2, 100: 76.7}
LATER = 7.0                                    # 하한 6.941 (BossHp(110) >= BossHp(100))
HP = {n: 40 * zmul(n) * BASE * LB[n] for n in LB}
HP110 = 40 * zmul(110) * BASE * LATER
P('## 9-R5 최종 체력(반올림 뒤): ' + ' · '.join(f'{n}: {v:.3g}' for n, v in HP.items()) + f' · 110: {HP110:.3g} (110/100 x{HP110/HP[100]:.3f})')
J = {'hp': HP, 'hp110': HP110, 'base': BASE, 'loop_boss': LB, 'loop_later': LATER, 'runs': {}}
for tg, lab in ((1.0, '세계수 없음'), (WT4, '세계수 4단계'), (1 + 0.92 * 0.12 * 7, '세계수 8단계')):
    for f in (0, 1, 2, 4):
        r = run(R5, HP, farm=f, tg=tg, grit=GR); show(f'{lab} · 파밍 {f}판', r)
        J['runs'][f'{lab}_{f}'] = {'t100': r[1] + 78, 't90p': r[2] + 78, 'h': {str(n): x for n, x in r[0].items()}}
r = run(R5, HP, grit=(1.0, 1.0, 0)); show('끈기 끔(같은 체력)', r)
P(chr(10) + '## 사람 시드별 허들 평균(봇 시드의 트리·Pb를 쓰는 사람 250명, 파밍 2판, 세계수 없음)')
OPd = g['OP']
for s0 in range(1, 7):
    old, oldo = dict(K), dict(OPd)
    K.clear(); K.update({s: old[s0] for s in old}); OPd.clear(); OPd.update({s: oldo[s0] for s in oldo})
    o, tm, _ = run(R5, HP, players=250, grit=GR)
    P(f'  시드 {s0}: 100 도달 {tm + 78:.0f}분 | 허들 평균 ' + ' · '.join(f'{n}:{v["mean"]:.0f}' for n, v in o.items()) + ' | 막힘 ' + ' · '.join(f'{n}:{v["stuck"]:.1%}' for n, v in o.items()))
    o, tm, _ = run(R5, HP, players=60, bot=True, inc=1.0, rmin=1.16, grit=GR)
    P(f'         봇: 100 도달 {tm + 52:.0f}분 | 허들 평균 ' + ' · '.join(f'{n}:{v["mean"]:.1f}' for n, v in o.items()) + ' | 판당 실패 ' + ' · '.join(f'{n}:{v["fail"]:.0%}' for n, v in o.items()))
    K.clear(); K.update(old); OPd.clear(); OPd.update(oldo)
show('봇 전체', run(R5, HP, players=300, bot=True, inc=1.0, rmin=1.16, grit=GR), t0=52)
# DB 파일: 노드 행·가격은 9-R4 그대로, Const만 바뀐다
for k in ('prices', 'rows'):
    shutil.copyfile(os.path.join(HERE, f'final_{k}_r4.json'), os.path.join(HERE, f'final_{k}_r5.json'))
json.dump({'Upgrade': {'inf_atk': {'Efx_Lv1': 1.15}}, 'Const': {'BOSS_LOOP_HP_MUL': {'Value': BASE}}},
          open(os.path.join(HERE, 'final_set_r5.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
json.dump(J, open(os.path.join(HERE, 'theory9r5_final_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
open(os.path.join(HERE, 'theory9r5_final_out.txt'), 'w', encoding='utf-8').write(chr(10).join(OUT) + chr(10))
print(chr(10).join(OUT))
