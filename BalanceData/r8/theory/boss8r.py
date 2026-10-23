# -*- coding: utf-8 -*-
"""8-R 사용자 결정 반영: 44 사람 실패 ≤ 50%, 54는 44보다 쉽지 않게(50~60%), 34 40%와 곡선 확인.
P-9(중반 가격 ×0.35)로 사람이 44·54 전에 무더기 파쇄(ps_heavy, 보스 피해 ×1.5·×2)·공격력 III+를 더 일찍 산다 → 사람 보스 DPS 배율 R_P9
  44: 1.0(못 삼)~1.25(가운데)~1.5(ps_heavy 1) · 54: 1.0~1.3~1.5 (가정, 사람 판 수입 = 봇 T8-0 × 0.2로 35~53 누적 골드 2e9·3e10에서 살 수 있는 것)
다시 돌리기: python -X utf8 BalanceData/r8/theory/boss8r.py → boss8r_out.txt"""
import math, os
from statistics import NormalDist
from boss_fit import fit, B as BDATA, HP_NOW
from boss8 import M
N = NormalDist(); HERE = os.path.dirname(os.path.abspath(__file__))
RP9 = {30: (1.0, 1.0, 1.0), 40: (1.0, 1.25, 1.5), 50: (1.0, 1.3, 1.5)}
CAND = {30: [26, 30], 40: [80, 90, 95, 100, 105, 110], 50: [80, 90, 100]}
out = ['보스 | Hp_Mul | 사람 판당 실패율 R_P9 낮음·가운데·높음 | 평균 도전(가운데)']
for st, key, q, L, rpu, n in BDATA:
    if st not in CAND: continue
    mu, s = fit(q, L)
    for hp in CAND[st]:
        qs = [N.cdf((math.log(hp / HP_NOW[key]) - mu + math.log(M[st]) - math.log(r)) / s) for r in RP9[st]]
        out.append(f'{st} {key} | {hp} | ' + ' · '.join(f'{x:.2f}' for x in qs) + f' | {1 / (1 - qs[1]):.2f}')
txt = '\n'.join(out); print(txt)
open(os.path.join(HERE, 'boss8r_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
