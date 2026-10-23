# -*- coding: utf-8 -*-
"""verify 8회차 — DB 지금 값 대조 · 가격 사다리 조건 · 군락 표 · 무한 노드 덧셈 · (P-9) 독립 계산.
BalanceSim.PriceCheck(Assets/Editor/BalanceSim.cs:583~)와 같은 규칙: 골드 노드(gem·싱크 제외) 모든 레벨 화면 가격을 오름차순 → 바로 앞 대비 최대,
노드 안 다음 레벨 최대, TIER_LINES 효율 넘김(t=1 경계 제외, 같은 단계 형제는 ×4).
다시 돌리기: python -X utf8 BalanceData/r8/verify/v8_price.py → v8_price_out.txt"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from v8_early import load_nodes, E4, COST_MUL

HERE = os.path.dirname(os.path.abspath(__file__))
THEORY_NOW = {'power': [12, 46, 170, 510], 'ed_score': [16, 60, 230], 'ed_cols': [22, 84, 250, 650], 'ps_dur': [30, 110, 300, 800],
              'ed_price': [40, 150, 380], 'md_bar': [55, 200, 440], 'ps_atk': [65, 240, 920, 3500], 'md_map': [150, 340, 750, 1700],
              'ps_ball': [888, 2666], 'md_kid': [1300, 3400, 6800]}
TIER_LINES = [(['ps_atk', 'ps_atk2', 'ps_atk2p', 'ps_atk3', 'ps_atk3p', 'ps_atk4'], [1, 2.5, 2.5, 6, 6, 15], 0.53),
              (['ed_score', 'ed_score1p', 'ed_score2', 'ed_score2p', 'ed_score3', 'ed_score3p'], [1, 1, 10, 10, 100, 100], 0.66),
              (['ed_bonus', 'ed_bonus1p', 'ed_bonus2', 'ed_bonus2p', 'ed_bonus3', 'ed_bonus3p'], [1, 1, 10, 10, 100, 100], 0.66)]
P9 = ['ed_score3', 'ed_bonus3', 'ps_heavy', 'ps_atk3p', 'bl', 'ed_giant', 'md_chef', 'split', 'ed_score3p', 'ps_crit2', 'md_stump', 'ed_bonus3p']


def sc(n, L):
    return math.ceil(n['costs'][L] * COST_MUL)


def check(nodes, tag):
    out = []
    pr = lambda n: not n['gem'] and not n['inf']
    lad = sorted((sc(n, L), k, L + 1) for k, n in nodes.items() if pr(n) for L in range(n['max']))
    jumps = sorted(((b[0] / max(1, a[0]), a, b) for a, b in zip(lad, lad[1:])), reverse=True)[:5]
    t1 = sorted((sc(n, L), k, L + 1) for k, n in nodes.items() if pr(n) and n['tier'] == 1 for L in range(n['max']))
    j1 = max(((b[0] / a[0], a, b) for a, b in zip(t1, t1[1:])), key=lambda x: x[0])
    within = sorted(((sc(n, L + 1) / sc(n, L), k, L + 1) for k, n in nodes.items() if pr(n) for L in range(n['max'] - 1)), reverse=True)[:3]
    ok = tot = 0; sib = []; fails = []
    for ids, per, cross in TIER_LINES:
        for t in range(2, len(ids)):
            a, b = nodes[ids[t - 1]], nodes[ids[t]]
            pa, pb = sc(a, a['max'] - 1), sc(b, 0)
            if per[t] == per[t - 1]:
                sib.append((pb / pa, ids[t - 1], ids[t])); continue
            tot += 1
            if pb / per[t] <= pa / per[t - 1] * cross * 1.001: ok += 1
            else: fails.append(f'{ids[t - 1]}→{ids[t]} {pb / per[t] / (pa / per[t - 1]):.2f}')
    out.append(f'[{tag}] 전체 사다리 바로 앞 대비 상위 5: ' + ' · '.join(f'×{j:.2f} ({a[1]}{a[2]} {a[0]:,}→{b[1]}{b[2]} {b[0]:,})' for j, a, b in jumps))
    out.append(f'   1단계만: 최대 ×{j1[0]:.2f} ({j1[1][1]}{j1[1][2]} {j1[1][0]}→{j1[2][1]}{j1[2][2]} {j1[2][0]}) · 노드 안 상위: ' + ' · '.join(f'×{w:.2f} {k}{L}→{L + 1}' for w, k, L in within))
    out.append(f'   효율 넘김 {ok}/{tot} (못 지킴: {", ".join(fails) or "없음"}) · 형제 ×4: ' + ' · '.join(f'{a}→{b} ×{r:.2f}' for r, a, b in sib))
    head = lad[:16]
    out.append('   싼 순서 앞 16칸: ' + ' '.join(f'{k}{L}:{c}' for c, k, L in head))
    return out


def cols_table(efx):
    zs = [1.68, 1.5, 1.35, 1.2, 1.1, 1.0]
    rows = []
    for L, e in enumerate(efx):
        rows.append(f'L{L}({e}): ' + ' '.join(f'z{z}:{max(1, math.floor(e * (1 / (z * z)) ** 0.8 + 0.5))}' for z in zs)
                    + f' · +ed_cols2 5 z1.0: {math.floor((e + 5) + 0.5)}')
    return rows


if __name__ == '__main__':
    out = []
    now = load_nodes()
    bad = [k for k, v in THEORY_NOW.items() if [int(x) for x in now[k]['costs'][:len(v)]] != v]
    out.append(f'[0] db.json 지금 가격 = 이론_8 "지금" 열: 다른 노드 {bad or "없음"} · ed_cols Efx {now["ed_cols"]["efx"]} · '
               f'inf Efx_Lv1 {now["inf_atk"]["efx"][1]}/{now["inf_gold"]["efx"][1]} · inf 가격 {now["inf_atk"]["costs"][:2]}')
    plan = load_nodes(E4)
    out += check(now, '지금')
    out += check(plan, 'E4')
    p9 = load_nodes(E4)
    for k in P9:
        if k in p9:
            p9[k]['costs'] = [c * 0.35 for c in p9[k]['costs']]
    out += check(p9, 'E4 + P-9 ×0.35(이론 6절 목록 전 레벨)')
    out.append('[군락] 지금 4·6·8·10·11'); out += cols_table([4, 6, 8, 10, 11])
    out.append('[군락] E4 4·7·9·11·12'); out += cols_table([4, 7, 9, 11, 12])
    # 무한 노드
    C0, K = 3.63e12, 1.076
    cap = math.floor(math.log(1.7976931348623157e308 / 4 / (C0 * COST_MUL)) / math.log(K))
    out.append(f'[무한] infCap {cap} (Defs.Nodes.cs:265 식)')
    for L in (1, 16, 50, 100, 129, 130):
        cum = sum(math.ceil(C0 * K ** i * COST_MUL) for i in range(L))
        out.append(f'  L{L}: 누적 {cum:.3g} · 곱 1.07^L ×{1.07 ** L:.4g} / 1.04^L ×{1.04 ** L:.4g} · 덧셈 1.07→ ×{1 + 0.07 * L:.3g} 1.04→ ×{1 + 0.04 * L:.3g} · 덧셈 1.10→ ×{1 + 0.1 * L:.3g} 1.05→ ×{1 + 0.05 * L:.3g}')
    out.append(f'  상한 L{cap}: 덧셈 1.07 ×{1 + 0.07 * cap:.4g} (유한) · 1.04 ×{1 + 0.04 * cap:.4g}')
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'v8_price_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
