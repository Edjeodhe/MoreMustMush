# -*- coding: utf-8 -*-
"""verify 8회차 — 이론_8 8-R2(T8-1 반영 2차안) 검증.
① 보스 34·44·54: q = Φ((ln(Hp/Hp_B) − mu + ln M − ln R)/s) 를 내 맞춤(v8_boss.fit_logn)으로 다시, M·R 격자 민감도
② P-9b 가격: 지금 db.json(Ver.07) + final_prices_r2.json → 노드 안 ×4 · 화면 사다리(STEP_FIX 1.8) · 효율 넘김
다시 돌리기: python -X utf8 BalanceData/r8/verify/v8_r2.py → v8_r2_out.txt"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from v8_early import load_nodes
from v8_price import check
from v8_boss import fit_logn, B, N

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
HP_B = {'angel': 65, 'phantom': 400, 'mover': 200}           # 지표 B 때(6회차, 0.0.8)
R2 = {'angel': 37, 'phantom': 160, 'mover': 220}
V07 = {'angel': 30, 'phantom': 105, 'mover': 100}
TGT = {'angel': 0.40, 'phantom': 0.50, 'mover': 0.60}


def q(st, hp, M, R):
    k = B[st][0]; _, mu, s = fit_logn(B[st][1], B[st][2])
    return N.cdf((math.log(hp / HP_B[k]) - mu + math.log(M) - math.log(R)) / s)


def need(st, qt, M, R):
    k = B[st][0]; _, mu, s = fit_logn(B[st][1], B[st][2])
    return HP_B[k] * math.exp(mu + s * N.inv_cdf(qt) - math.log(M) + math.log(R))


if __name__ == '__main__':
    out = ['[① 보스] 사람 판당 실패율 — 이론 가운데(M 실측 1.85·1.88·1.91, R 1.0·1.25·1.9) 재현과 격자']
    Mm = {30: 1.85, 40: 1.88, 50: 1.91}
    Rm = {30: 1.0, 40: 1.25, 50: 1.9}
    for st in (30, 40, 50):
        k = B[st][0]
        out.append(f'  {st + 4} {k}: r2 {R2[k]} → 가운데 {q(st, R2[k], Mm[st], Rm[st]):.2f} · Ver.07 {V07[k]} → {q(st, V07[k], Mm[st], Rm[st]):.2f}')
    out.append('  격자 (r2 체력): M × R')
    grid = {30: (1.0, 1.15, 1.3), 40: (1.0, 1.1, 1.25, 1.5), 50: (1.0, 1.25, 1.5, 1.9, 2.4)}
    for st in (30, 40, 50):
        k = B[st][0]
        for M in (1.4, 1.6, 1.85 if st == 30 else 1.88 if st == 40 else 1.91, 2.2):
            out.append(f'   {st + 4} M {M}: ' + ' · '.join(f'R {R} {q(st, R2[k], M, R):.2f}' for R in grid[st]))
    out.append('  곡선 (M 실측, R 짝): 34 → 44 → 54')
    for r34, r44, r54 in ((1.0, 1.25, 1.9), (1.0, 1.0, 1.0), (1.0, 1.25, 1.5), (1.0, 1.25, 1.2), (1.0, 1.1, 1.5), (1.15, 1.5, 2.4), (1.0, 1.5, 1.0)):
        a, b, c = q(30, 37, 1.85, r34), q(40, 160, 1.88, r44), q(50, 220, 1.91, r54)
        out.append(f'   R {r34}/{r44}/{r54}: {a:.2f} → {b:.2f} → {c:.2f}' + ('  ← 54 > 0.65' if c > 0.65 else '') + ('  ← 역전' if not a <= b <= c else ''))
    out.append('  목표 실패율에 맞는 Hp (M 실측): ' + ' · '.join(
        f'{B[st][0]} {TGT[B[st][0]]:.2f}: ' + '/'.join(f'R{R}→{need(st, TGT[B[st][0]], Mm[st], R):.0f}' for R in grid[st]) for st in (30, 40, 50)))
    # 54 R=1.9의 근거: 봇 공격력 비 98 → 173(×1.77). 사람이 P-9 노드를 못 사면 1.0
    out.append(f'  54 mover 220, R 1.0(사람이 P-9 노드 못 삼)·M 1.91: {q(50, 220, 1.91, 1.0):.2f} · M 1.6: {q(50, 220, 1.6, 1.0):.2f} · 평균 도전 {1 / (1 - q(50, 220, 1.91, 1.0)):.1f}')
    # ② P-9b
    now = load_nodes()   # 지금 db.json = Ver.07
    fp = json.load(open(os.path.join(ROOT, 'BalanceData', 'r8', 'theory', 'final_prices_r2.json'), encoding='utf-8'))
    r2 = load_nodes()
    for k, c in fp.items():
        r2[k]['costs'] = list(c) + r2[k]['costs'][len(c):]
    out.append('\n[② P-9b] 바뀌는 노드 ' + str(len(fp)) + ': ' + ' · '.join(f'{k} {[f"{x:.3g}" for x in now[k]["costs"][:now[k]["max"]]]}→{[f"{x:.3g}" for x in fp[k][:now[k]["max"]]]}' for k in fp))
    out += check(now, 'Ver.07 지금')
    out += check(r2, 'Ver.07 + P-9b')
    nodes = r2
    lad = sorted((math.ceil(n['costs'][L] * 0.9), k, L + 1) for k, n in nodes.items() if not n['gem'] and not n['inf'] for L in range(n['max']))
    over = [(b[0] / a[0], a, b) for a, b in zip(lad, lad[1:]) if b[0] / a[0] > 1.8]
    out.append('  STEP_FIX 1.8 넘는 뜀: ' + (' · '.join(f'×{j:.2f} {a[1]}{a[2]}→{b[1]}{b[2]}' for j, a, b in over) or '없음'))
    hi = [x for x in lad if x[0] >= 1e9] + [(math.ceil(3.63e12 * 0.9), 'inf', 1)]
    j = max(((b[0] / a[0], a, b) for a, b in zip(hi, hi[1:])), key=lambda x: x[0])
    out.append(f'  1e9 이상 + 무한 시작가 최대 ×{j[0]:.2f} ({j[1][1]}{j[1][2]} {j[1][0]:.3g} → {j[2][1]}{j[2][2]} {j[2][0]:.3g})')
    out.append(f'  무한 부모: ps_atk4 L3 {math.ceil(nodes["ps_atk4"]["costs"][2] * 0.9):.3g} · ed_score3p L3 {math.ceil(nodes["ed_score3p"]["costs"][2] * 0.9):.3g} · 무한 L1 {math.ceil(3.63e12 * 0.9):.3g}')
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'v8_r2_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
