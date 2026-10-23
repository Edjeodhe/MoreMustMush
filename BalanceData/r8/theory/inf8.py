# -*- coding: utf-8 -*-
"""8회차 U3: 무한 노드(inf_atk 11032 · inf_gold 13032) 곱 → 덧셈.
지금: 효과 = infMul^min(L, infCap) (infMul = DB Efx_Lv1: 1.07 · 1.04), 가격 = ceil(3.63e12 × 1.076^L × 0.9).
안: 효과 = 1 + step × min(L, infCap), step = Efx_Lv1 − 1 (DB 칼럼 그대로, 값만 바꿈).
레벨 L까지 누적 가격과 레벨별 효과, '한 레벨 더'의 효과 증가율 / 가격 증가율을 비교한다.
다시 돌리기: python -X utf8 BalanceData/r8/theory/inf8.py → inf8_out.txt"""
import math, os
HERE = os.path.dirname(os.path.abspath(__file__))
C0, K, CM = 3.63e12, 1.076, 0.9
LV = [1, 10, 30, 50, 95, 100, 129, 130]
OPTS = [('지금 atk 곱 1.07', 'mul', 1.07), ('지금 gold 곱 1.04', 'mul', 1.04),
        ('atk 덧셈 0.07 (DB 그대로)', 'add', 0.07), ('atk 덧셈 0.10 (권장)', 'add', 0.10), ('atk 덧셈 0.15', 'add', 0.15),
        ('gold 덧셈 0.04 (DB 그대로)', 'add', 0.04), ('gold 덧셈 0.05 (권장)', 'add', 0.05), ('gold 덧셈 0.08', 'add', 0.08)]
cap = math.floor(math.log(1.7976931348623157e308 / 4 / (C0 * CM)) / math.log(K))


def eff(kind, x, L):
    return x ** L if kind == 'mul' else 1 + x * L


out = [f'infCap = {cap} (가격식이 그대로면 같음). 레벨 L까지 누적 가격 = Σ ceil(3.63e12·1.076^i·0.9), i<L',
       '레벨 | ' + ' | '.join(str(L) for L in LV),
       '누적 가격 | ' + ' | '.join(f'{C0 * CM * (K ** L - 1) / (K - 1):.2g}' for L in LV),
       '그 레벨 1개 가격 | ' + ' | '.join(f'{C0 * CM * K ** (L - 1):.2g}' for L in LV)]
for name, kind, x in OPTS:
    out.append(f'{name} | ' + ' | '.join(f'×{eff(kind, x, L):.3g}' for L in LV))
out.append('\n한 레벨 더의 효과 증가율 (가격은 늘 +7.6%)')
for name, kind, x in OPTS:
    out.append(f'{name} | ' + ' | '.join(f'+{(eff(kind, x, L + 1) / eff(kind, x, L) - 1) * 100:.1f}%' for L in LV))
# 골드 G를 다 부으면 닿는 레벨 L(G) = ln(1 + G(K−1)/(C0·CM)) / ln K
out.append('\n쓴 골드(누적) → 레벨 → 효과 (덧셈 0.10 / 0.05)')
for G in (1e13, 1e14, 1e15, 1e16, 1e17, 1e18):
    L = math.floor(math.log(1 + G * (K - 1) / (C0 * CM)) / math.log(K))
    out.append(f'{G:.0e} | L {L} | 곱 atk ×{1.07 ** L:.3g} · gold ×{1.04 ** L:.3g} | 덧셈 atk ×{1 + 0.1 * L:.3g} · gold ×{1 + 0.05 * L:.3g}')
txt = '\n'.join(out); print(txt)
open(os.path.join(HERE, 'inf8_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
