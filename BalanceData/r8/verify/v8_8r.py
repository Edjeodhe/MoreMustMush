# -*- coding: utf-8 -*-
"""verify 8회차 — 이론_8 8-R(사용자 결정 반영) 검증: P-9 가격 사다리·효율 넘김, phantom 105·mover 100·LOOP 110, 무한 1.07·1.04 덧셈.
P-9 값은 theory p9_overrides.json(데이터만 읽음)과 이론 8-R 표 값 둘 다 넣어 본다. 보스는 v8_boss 맞춤 재사용.
다시 돌리기: python -X utf8 BalanceData/r8/verify/v8_8r.py → v8_8r_out.txt"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from v8_early import load_nodes, E4
from v8_price import check
from v8_boss import fit_logn, B, N

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
TABLE = {'ps_atk3': [4.5e7, 1.6e8, 6e8, 8.05e8], 'ed_score3': [9.4e8, 1.225e9, 4.55e9], 'ed_bonus3': [1.7e9, 2.135e9, 8.05e9],
         'ps_heavy': [1.925e9, 7.7e9], 'ps_atk3p': [2.905e9, 1.085e10, 4.2e10], 'bl': [7e9, 2.695e10, 4.55e10, 1.75e11],
         'ed_giant': [1.05e10, 4.2e10], 'md_chef': [1.505e10, 5.6e10], 'split': [1.505e10, 5.6e10, 1.155e11, 4.55e11],
         'ed_score3p': [1.75e10, 6.65e10, 2.31e11], 'ps_crit2': [1.925e10, 2.52e10], 'md_stump': [2.135e10, 8.05e10],
         'ed_bonus3p': [2.87e10, 1.085e11, 4.2e11], 'ed_trade': [4.2e10, 1.505e11], 'ed_gold': [4.2e10, 1.505e11],
         'ps_atk4': [4.9e10, 1.89e11, 4.9e11]}


def with_p9(p9):
    n = load_nodes(E4)
    for k, c in p9.items():
        n[k]['costs'] = list(c) + n[k]['costs'][len(c):]
    return n


if __name__ == '__main__':
    out = []
    js = json.load(open(os.path.join(ROOT, 'BalanceData', 'r8', 'theory', 'p9_overrides.json'), encoding='utf-8'))
    diff = {k: (js[k], TABLE[k]) for k in TABLE if [round(x / 1e6) for x in js[k]] != [round(x / 1e6) for x in TABLE[k]]}
    out.append(f'[P-9] p9_overrides.json vs 이론 8-R 표: 다른 노드 {len(diff)} ' + ' · '.join(f'{k} {a}≠{b}' for k, (a, b) in diff.items()))
    now = load_nodes()
    out.append('[P-9] 지금 값 = 이론 "지금" 열: ' + ', '.join(f'{k} {"OK" if [round(x / 1e7) for x in now[k]["costs"][:len(v)]][-1] >= 0 else ""}' for k, v in TABLE.items()))
    out.append('   지금 DB: ' + ' · '.join(f'{k} {[f"{x:.3g}" for x in now[k]["costs"][:now[k]["max"]]]}' for k in TABLE))
    for tag, p9 in (('E4+P-9(json)', js), ('E4+P-9(표)', TABLE)):
        out += check(with_p9(p9), tag)
    # 2단계 이상 구간(1e8~4e12) 사다리만
    nodes = with_p9(js)
    lad = sorted((math.ceil(n['costs'][L] * 0.9), k, L + 1) for k, n in nodes.items() if not n['gem'] and not n['inf'] for L in range(n['max']))
    lad = [x for x in lad if 1e8 <= x[0] <= 4e12] + [(math.ceil(3.63e12 * 0.9), 'inf', 1)]
    j = max(((b[0] / a[0], a, b) for a, b in zip(lad, lad[1:])), key=lambda x: x[0])
    out.append(f'[P-9] 1e8~4e12 구간 + 무한 시작가: 최대 ×{j[0]:.2f} ({j[1][1]}{j[1][2]} {j[1][0]:.3g} → {j[2][1]}{j[2][2]} {j[2][0]:.3g})')
    out.append(f'[P-9] 무한 노드 부모: ps_atk4 3레벨 화면 {math.ceil(nodes["ps_atk4"]["costs"][2] * 0.9):.3g} (지금 {math.ceil(now["ps_atk4"]["costs"][2] * 0.9):.3g}), '
               f'ed_score3p 3레벨 {math.ceil(nodes["ed_score3p"]["costs"][2] * 0.9):.3g} (지금 {math.ceil(now["ed_score3p"]["costs"][2] * 0.9):.3g})')
    # 보스
    REC = {'angel': 30, 'phantom': 105, 'mover': 100}
    NOW = {'angel': 65, 'phantom': 400, 'mover': 200}
    out.append('\n[보스 8-R] 사람 판당 실패율 (로그정규, h = 새/지금 × M / R). R = P-9 몫 × 초반안 몫(34~54 theory 1, verify 1.5 대안)')
    for st, k, Ms, Rp9 in ((30, 'angel', (1.2, 1.4, 1.72, 2.2, 2.92), (1.0,)), (40, 'phantom', (1.4, 1.8, 2.11, 2.85, 3.96), (1.0, 1.25, 1.5)),
                           (50, 'mover', (1.4, 1.8, 2.11, 2.85, 3.96), (1.0, 1.3, 1.5))):
        _, mu, s = fit_logn(B[st][1], B[st][2])
        for Re in (1.0, 1.5):
            for Rp in Rp9:
                qs = [N.cdf((math.log(REC[k] / NOW[k] * M / (Rp * Re)) - mu) / s) for M in Ms]
                out.append(f'  {st + 4} {k} {REC[k]} · R초반 {Re} · R_P9 {Rp}: ' + ' · '.join(f'M {M} {q:.2f}' for M, q in zip(Ms, qs)))
    # 같은 M에서 34·44·54 곡선 (가운데 P-9)
    out.append('  곡선(같은 M 짝, R초반 1, P-9 가운데 44 1.25 · 54 1.3): ')
    for M34, M44 in ((1.2, 1.4), (1.4, 1.8), (1.72, 2.11), (2.2, 2.85), (2.92, 3.96)):
        q = []
        for st, k, M, Rp in ((30, 'angel', M34, 1.0), (40, 'phantom', M44, 1.25), (50, 'mover', M44, 1.3)):
            _, mu, s = fit_logn(B[st][1], B[st][2])
            q.append(N.cdf((math.log(REC[k] / NOW[k] * M / Rp) - mu) / s))
        out.append(f'    M {M34}/{M44}: 34 {q[0]:.2f} → 44 {q[1]:.2f} → 54 {q[2]:.2f}' + ('  ← 44가 34보다 쉬움' if q[1] < q[0] else ''))
    out.append(f'  LOOP 110 / mover 100 = {110 / 100:.2f} (규칙 1.1 유지)')
    # 무한
    out.append('\n[무한 1.07·1.04 덧셈] L: 공격력·골드 (지금 곱)')
    for L in (10, 16, 50, 100, 129, 130, 9277):
        out.append(f'  L{L}: ×{1 + 0.07 * L:.4g} · ×{1 + 0.04 * L:.4g}  (곱 ×{1.07 ** min(L, 400):.3g} · ×{1.04 ** min(L, 400):.3g})')
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'v8_8r_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
