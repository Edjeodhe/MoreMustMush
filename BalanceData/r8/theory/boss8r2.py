# -*- coding: utf-8 -*-
"""8-R2: T8-1 실측 유도 배율 M(34 1.85 · 44 1.88 · 54 1.91, SKILL 1.0 짝 시드)과 봇 상태 배율(T8-1/T8-0 보스 직전 공격력·수확기·시간)을
사람 상태 변화 R의 대리로 넣어 34·44·54 Hp_Mul을 다시 낸다. 식은 boss8과 같다: 실패 q = Φ((ln(hp/hp_B) − mu + ln M − ln R)/s).
R: 34 1.0(1.0~1.05, T8-1 공격력 같음) · 44 1.25(1.1~1.5: T8-1 ×1.1 + P-9b) · 54 1.9(1.5~2.4: T8-1 공격력 98→157~173 ×1.7 + P-9b)
다시 돌리기: python -X utf8 BalanceData/r8/theory/boss8r2.py → boss8r2_out.txt"""
import math, os
from statistics import NormalDist
from boss_fit import fit, B as BDATA, HP_NOW
N = NormalDist(); HERE = os.path.dirname(os.path.abspath(__file__))
M = {30: 1.85, 40: 1.88, 50: 1.91}
R = {30: (1.0, 1.0, 1.05), 40: (1.1, 1.25, 1.5), 50: (1.5, 1.9, 2.4)}   # P-9b(추가 ×0.5)로 44·54 앞 무더기 파쇄·공격력 III+가 더 이름
TGT = {30: 0.40, 40: 0.45, 50: 0.55}
CAND = {30: [30, 37], 40: [105, 150, 160, 170], 50: [100, 200, 220, 240]}
out = ['보스 | Hp_Mul | 사람 판당 실패율 쉬운 쪽(R 높음)·가운데·어려운 쪽(R 낮음) | 평균 도전(가운데) | 목표 q에 맞는 값(R 가운데)']
for st, key, q, L, rpu, n in BDATA:
    if st not in M: continue
    mu, s = fit(q, L)
    hfit = HP_NOW[key] * math.exp(mu - math.log(M[st]) + math.log(R[st][1]) + s * N.inv_cdf(TGT[st]))
    for hp in CAND[st]:
        qs = [N.cdf((math.log(hp / HP_NOW[key]) - mu + math.log(M[st]) - math.log(r)) / s) for r in R[st]]
        out.append(f'{st} {key} | {hp} | ' + ' · '.join(f'{x:.2f}' for x in qs[::-1]) + f' | {1 / (1 - qs[1]):.2f} | {hfit:.3g}')
txt = '\n'.join(out); print(txt)
open(os.path.join(HERE, 'boss8r2_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
