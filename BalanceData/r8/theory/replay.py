# -*- coding: utf-8 -*-
"""T7-5 봇 기록(phase_k06_s*.json incR)에 tree.bulk_buy를 다시 돌려 판별 구매·능력치를 복원한다.
검증: 복원한 판별 구매 수 = 기록 buysR. 출력 replay_out.txt / replay_rounds.json
다시 돌리기: python -X utf8 BalanceData/r8/theory/replay.py"""
import json, os, math, sys
sys.path.insert(0, os.path.dirname(__file__))
import tree

PH = os.path.join(tree.ROOT, 'BalanceData', 'r7', 'test', 'T7-5', 'phase_k06_s%d.json')
ZOOM_EXP = 0.8


def stats(nodes, lv, stage):
    """Game.ComputeStats의 일부(맑음, 별·세계수 보너스 0). stage = 내부 판"""
    E = lambda k: tree.efx(nodes, lv, k)
    cst = max(1, stage - 4)
    zoom = E('md_map2') if lv.get('md_map2', 0) > 0 else E('md_map')
    area = (1 / zoom ** 2) ** ZOOM_EXP
    cols = max(1, round(max(1, math.ceil(E('ed_cols') + E('ed_cols2'))) * area))
    atk = (1 + sum(E(k) for k in ('ps_atk', 'ps_atk2', 'ps_atk2p', 'ps_atk3', 'ps_atk3p', 'ps_atk4'))) * 1.1
    inf_atk = nodes['inf_atk']['efx'][1] ** lv.get('inf_atk', 0) if 'inf_atk' in nodes else 1
    balls = 1 + lv.get('ps_ball', 0) + lv.get('ps_ball2', 0) + lv.get('ps_ball3', 0)
    base = 10 if stage <= 4 else 10 + min(10, 0.5 * (cst - 1))
    dur = base + E('ps_dur') + E('ps_dur2')
    flat = sum(E(k) for k in ('ed_score', 'ed_score1p', 'ed_score2', 'ed_score2p', 'ed_score3', 'ed_score3p'))
    price = 1 + (E('ed_price') - 1) + E('ed_price2') + (max(E('ed_trade'), 1) - 1) + 0.1
    return dict(zoom=zoom, cols=cols, atk=atk * inf_atk, balls=balls, dur=dur, flat=flat, price=price,
                combo=E('md_combo') + E('md_combo2'), size=E('ed_size'))


def replay(seed, nodes=None):
    nodes = nodes or tree.load()[0]
    ph = json.load(open(PH % seed, encoding='utf-8'))
    lv, gold, rows = {}, 0.0, []
    for i, inc in enumerate(ph['incR']):
        st = stats(nodes, lv, ph['stageR'][i])
        gold += inc
        gold, b = tree.bulk_buy(nodes, lv, gold)
        nxt = tree.cheapest(nodes, lv)
        rows.append(dict(r=i + 1, stage=ph['stageR'][i], boss=ph['bossR'][i], inc=inc, buys=len(b), rec=ph['buysR'][i],
                         bought=[f'{k}{L}' for k, L, c in b], st=st, atkR=ph['atkR'][i], gold=gold,
                         next=(nxt[0]['id'] if nxt[0] else None, nxt[1]), lv=dict(lv),
                         made=sum(ph['madeT'][ph['stageR'][i] - 1][:4]) if ph['stageR'][i] - 1 < len(ph['madeT']) else None))
    return rows


if __name__ == '__main__':
    out, allr = [], {}
    for s in range(1, 7):
        rows = replay(s)
        allr[s] = rows
        bad = [(x['r'], x['buys'], x['rec']) for x in rows if x['buys'] != x['rec']]
        atkbad = [(x['r'], round(x['st']['atk'], 2), round(x['atkR'], 2)) for x in rows if abs(x['st']['atk'] - x['atkR']) > 0.02 * x['atkR'] + 0.01]
        out.append(f'시드 {s}: 판 {len(rows)} · 구매 수 불일치 {len(bad)} {bad[:5]} · 공격력 불일치(파워 버프·세트 제외 판정) {len(atkbad)} {atkbad[:3]}')
    out.append('')
    for x in allr[1][:30]:
        st = x['st']
        out.append(f"R{x['r']:>2} s{x['stage']} b{x['boss']} inc {x['inc']:>12,.0f} 산 {x['buys']} {x['bought']} | 다음 {x['next'][0]} {x['next'][1]:,.0f} (비 {x['next'][1]/max(x['inc'],1):.2f}) | 군락 {st['cols']} 수확기 {st['balls']} 시간 {st['dur']:.1f} 점수+{st['flat']:.0f} 정산x{st['price']:.2f} 공{st['atk']:.2f} zoom {st['zoom']} 만든 {x['made']}")
    txt = '\n'.join(out)
    print(txt)
    open(os.path.join(os.path.dirname(__file__), 'replay_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    json.dump({s: [{k: v for k, v in x.items() if k != 'lv'} | {'lv': x['lv']} for x in r] for s, r in allr.items()},
              open(os.path.join(os.path.dirname(__file__), 'replay_rounds.json'), 'w', encoding='utf-8'), ensure_ascii=False, default=str)
