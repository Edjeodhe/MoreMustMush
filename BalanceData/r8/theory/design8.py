# -*- coding: utf-8 -*-
"""8회차 초반(내부 1~23) 수치안 비교. 안마다 DB Upgrade(효과)·Upgrade_Cost(가격) 덮어쓰기 → model8 진행(봇·사람).
다시 돌리기: python -X utf8 BalanceData/r8/theory/design8.py → design8_out.txt · design8_out.json"""
import json, math, os, sys
from tree import load
from model8 import fit_fe, smooth_fe, simulate, t80_rows, table, fmt
from human15 import a_fn_make

HERE = os.path.dirname(os.path.abspath(__file__))
HP = json.load(open(os.path.join(HERE, 'human_params.json')))
HUMAN = a_fn_make(HP['a0'], HP['slope'], HP['d'])

# 안: {키: {'costs': [Cost_Lv1..], 'efx': [Efx_Lv0..4]}} — DB 값(화면 가격 = ceil(×0.9))
PLANS = {
    '지금': {},
    'E1': {
        'power':    {'costs': [6, 40, 150, 480]},
        'ed_score': {'costs': [9, 30, 110]},
        'ed_cols':  {'costs': [18, 55, 170, 520], 'efx': [4, 7, 9, 10, 11]},
        'ps_dur':   {'costs': [24, 75, 220, 650]},
        'ed_price': {'costs': [32, 95, 260]},
        'md_bar':   {'costs': [50, 160, 400]},
        'ps_atk':   {'costs': [45, 130, 600, 2400]},
        'ps_ball':  {'costs': [200, 800]},
    },
    'E2': {
        'power':    {'costs': [6, 110, 260, 560]},
        'ed_score': {'costs': [8, 30, 110]},
        'ed_cols':  {'costs': [18, 55, 170, 520], 'efx': [4, 7, 9, 10, 11]},
        'ps_dur':   {'costs': [24, 75, 220, 650]},
        'ed_price': {'costs': [32, 95, 260]},
        'md_bar':   {'costs': [50, 160, 400]},
        'ps_atk':   {'costs': [45, 130, 600, 2400]},
        'ps_ball':  {'costs': [200, 800]},
        'md_kid':   {'costs': [500, 1500, 4000]},
        '_loose':   [10, 10, 10, 10, 9, 8],
    },
    'E3': {
        'power':    {'costs': [6, 160, 380, 700]},
        'ed_score': {'costs': [8, 30, 110]},
        'ed_cols':  {'costs': [18, 55, 150, 520], 'efx': [4, 7, 9, 11, 12]},
        'ps_dur':   {'costs': [24, 75, 220, 650]},
        'ed_price': {'costs': [32, 95, 260]},
        'md_bar':   {'costs': [50, 160, 400]},
        'ps_atk':   {'costs': [45, 240, 700, 2400]},
        'md_map':   {'costs': [260, 450, 750, 1700]},
        'ps_ball':  {'costs': [200, 800]},
        'md_kid':   {'costs': [500, 1500, 4000]},
        '_loose':   [10, 10, 10, 10, 9, 8],
    },
    'E4': {
        'power':    {'costs': [6, 24, 96, 380]},
        'ed_score': {'costs': [8, 30, 110]},
        'ed_cols':  {'costs': [16, 55, 150, 520], 'efx': [4, 7, 9, 11, 12]},
        'ps_dur':   {'costs': [24, 75, 220, 650]},
        'ed_price': {'costs': [32, 95, 260]},
        'md_bar':   {'costs': [50, 160, 400]},
        'ps_atk':   {'costs': [45, 180, 700, 2400]},
        'md_map':   {'costs': [260, 450, 750, 1700]},
        'ps_ball':  {'costs': [200, 800]},
        'md_kid':   {'costs': [500, 1500, 4000]},
        '_loose':   [10, 10, 10, 10, 9, 8],
    },
}
PLANS['E5'] = json.loads(json.dumps(PLANS['E4']))   # 검증_8 반영: ed_score L1 7 · ed_cols 13·50 · md_kid 500·1800·6800 (화면 사다리 뜀 ≤ 1.8)
PLANS['E5']['ed_score']['costs'] = [7, 28, 110]
PLANS['E5']['ed_cols']['costs'] = [13, 50, 150, 520]
PLANS['E5']['md_kid']['costs'] = [500, 1800, 6800]
PLANS['E5']['power']['costs'] = [6, 22, 88, 350]
FINAL = 'E5'   # 최종안 (8-R-5)
LOOSE_NOW = [10, 10, 9, 8, 6, 4]   # Defs.cs EARLY_LOOSE (내부 판 1~6, [0]은 튜토리얼이면 안 씀 → TUTORIAL_LOOSE 8)
LOOSE_EL = 0.7                       # 낱개 버섯 수 → 판 수입 탄력 (군락 탄력과 같게, 가정)


def made_median():
    import statistics as S
    rows = t80_rows()
    return {n: S.median([r['made'] for r in rows if r['stage'] == n and r['bossFlag'] == 0]) for n in range(1, 8)}


def fe_with_loose(fe, loose):
    if not loose:
        return fe
    made = made_median()
    fe2 = dict(fe)
    for i in range(1, 6):          # 내부 판 2~6 (판 1은 튜토리얼 TUTORIAL_LOOSE)
        n = i + 1
        fe2[n] = fe[n] + LOOSE_EL * math.log(1 + (loose[i] - LOOSE_NOW[i]) / made[n])
    return fe2


def run(plan, base_fe=None):
    nodes, _, _ = load(drop=('homing',), overrides={k: dict(v, costs=v.get('costs'), efx=v.get('efx')) for k, v in plan.items()} if False else None)
    for k, v in plan.items():
        if k.startswith('_'):
            continue
        if 'costs' in v:
            nodes[k]['costs'] = list(v['costs']) + nodes[k]['costs'][len(v['costs']):]
        if 'efx' in v:
            nodes[k]['efx'] = list(v['efx'])
    return nodes


def summary(sim, lo, hi):
    rs = [r for r in sim if lo <= r['n'] <= hi]
    g = (rs[-1]['net'] / rs[0]['net']) ** (1 / max(1, len(rs) - 1))
    return dict(growth=g, buys_min=min(r['buys'] for r in rs), buys_avg=sum(r['buys'] for r in rs) / len(rs),
                ratio_max=max(r['ratio'] for r in rs), net_hi=rs[-1]['net'])


if __name__ == '__main__':
    base_nodes = run({})
    fe, _ = fit_fe(base_nodes, t80_rows())
    fe = smooth_fe(fe)
    names = sys.argv[1:] or list(PLANS)
    out, js = [], {}
    for name in names:
        nodes = run(PLANS[name])
        fe_p = fe_with_loose(fe, PLANS[name].get('_loose'))
        bot = simulate(nodes, fe_p, nmax=23)
        hum = simulate(nodes, fe_p, HUMAN, nmax=23)
        js[name] = dict(bot=bot, human=hum)
        out.append(f'\n===== 안 {name} =====')
        for who, sim in (('봇', bot), ('사람', hum)):
            for lo, hi in ((1, 8), (8, 13), (15, 23)):
                s = summary(sim, lo, hi)
                out.append(f"{who} 내부 {lo}~{hi}: 판당 ×{s['growth']:.2f} · 구매 최소 {s['buys_min']} 평균 {s['buys_avg']:.1f} · 다음 가격/판 수입 최대 {s['ratio_max']:.2f} · {hi}판 net {fmt(s['net_hi'])}")
        # 가격 조건: 노드 안 다음 레벨 배율, 1단계 가격 사다리(정렬)에서 바로 앞 대비 배율
        within = max((nd['costs'][i + 1] / nd['costs'][i], k) for k, nd in nodes.items() if not nd['inf'] and not nd['gem'] for i in range(min(nd['max'], len(nd['costs'])) - 1) if nd['costs'][i] > 0)
        ladder = sorted(c for nd in nodes.values() if nd['tier'] == 1 and not nd['gem'] for c in nd['costs'][:nd['max']] if c > 0)
        jump = max(b / a for a, b in zip(ladder, ladder[1:]))
        out.append(f'가격 조건: 노드 안 최대 ×{within[0]:.2f} ({within[1]}) · 1단계 사다리 최대 뜀 ×{jump:.2f}')
        out += table(hum, f'[사람 · {name}]')
        out += table(bot, f'[봇 · {name}]')
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'design8_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    json.dump(js, open(os.path.join(HERE, 'design8_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, default=str)
