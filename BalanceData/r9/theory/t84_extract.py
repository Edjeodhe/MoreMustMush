# -*- coding: utf-8 -*-
"""9회차 theory: T8-4(PickZone, 튜토리얼 2판) 원자료에서 표시 스테이지별 봇 상태를 뽑는다.
표시 = 내부 - 2. 출력 t84_state.json (시드별 판 행), t84_extract_out.txt (요약)
다시 돌리기: python -X utf8 BalanceData/r9/theory/t84_extract.py"""
import csv, json, os, statistics as S
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, '..', '..', 'r8', 'test', 'T8-4', 'late')
TUT = 2
rows = {}
for s in range(1, 7):
    inf = {'inf_atk': 0, 'inf_gold': 0}
    out = []
    with open(os.path.join(SRC, f'rounds_s{s}.csv'), encoding='utf-8') as f:
        for r in csv.DictReader(f):
            disp = int(r['stage']) - TUT
            rec = dict(seed=s, disp=disp, boss=int(r['bossFlag']), net=float(r['net']), atk=float(r['atk']), balls=int(r['balls']),
                       buys=int(r['buys']), playMin=float(r['playMin']), dur=float(r['dur']), bossLeft=float(r['bossLeft'] or 0),
                       bossRes=r['bossRes'], bossKey=r['boss'], nextCost=float(r['nextCost'] or 0), nextKey=r['nextKey'],
                       infA0=inf['inf_atk'], infG0=inf['inf_gold'], bought=r['bought'])
            gold_buys = 0
            for b in filter(None, r['bought'].split(';')):
                k, lv = b.split(':'); a, c = lv.split('>')
                if k in inf: inf[k] = int(c)
                else: gold_buys += int(c) - int(a)
            rec['goldBuys'] = gold_buys; rec['infA'] = inf['inf_atk']; rec['infG'] = inf['inf_gold']
            out.append(rec)
    rows[s] = out
json.dump(rows, open(os.path.join(HERE, 't84_state.json'), 'w', encoding='utf-8'))
lines = ['표시 | 봇 공격력(시드 중앙) | 수확기 | inf_atk Lv(시작) | 판 net 중앙 | 다음가/net 중앙 | 분']
for d in [20, 45, 50, 55, 60, 65, 70, 72, 75, 80, 85, 90, 95, 100]:
    rs = [x for s in rows for x in rows[s] if x['disp'] == d]
    if not rs: continue
    med = lambda k: S.median([x[k] for x in rs])
    nn = [x for x in rs if not x['boss']]
    ratio = S.median([x['nextCost'] / x['net'] for x in nn]) if nn else float('nan')
    lines.append(f"{d} | {med('atk'):.0f} | {med('balls'):.0f} | {med('infA0'):.0f} | {S.median([x['net'] for x in rs]):.3g} | {ratio:.2f} | {med('playMin'):.1f}")
txt = '\n'.join(lines); print(txt)
open(os.path.join(HERE, 't84_extract_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
