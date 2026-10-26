# 옛 트리(T9-0, 8회차 값) 봇 기준 곡선 추출: 표시 스테이지별 도달 분·공격력·핀볼·판 수입(일반 판 중앙)·판 시간
import csv, glob, json, os, statistics as S, sys
TUT = 2
src = sys.argv[1] if len(sys.argv) > 1 else 'BalanceData/r9/test/T9-0'
out = sys.argv[2] if len(sys.argv) > 2 else 'BalanceData/r9m/theory/ref_curve.json'
res = {}
for f in sorted(glob.glob(os.path.join(src, 'rounds_*.csv'))):
    tag = os.path.basename(f)[7:-4]
    rows = list(csv.DictReader(open(f, encoding='utf-8')))
    d = {}
    for r in rows:
        st = int(r['stage']) - TUT
        e = d.setdefault(st, dict(t=float(r['playMin']), atk=float(r['atk']), balls=int(r['balls']), nets=[], dur=float(r['dur']), buys=0, n=0, boss=0, bossFail=0))
        e['n'] += 1; e['buys'] += int(r['buys'])
        if r['boss']:
            e['boss'] += 1; e['bossFail'] += r['bossRes'] != '처치'
        else:
            e['nets'].append(float(r['net']))
    res[tag] = {st: dict(t=e['t'], atk=e['atk'], balls=e['balls'], net=S.median(e['nets']) if e['nets'] else 0, dur=e['dur'], buys=e['buys'], n=e['n'], boss=e['boss'], bossFail=e['bossFail']) for st, e in d.items()}
json.dump(res, open(out, 'w', encoding='utf-8'), ensure_ascii=False)
for k in ['k06', 'k10', 'k6']:
    tags = [t for t in res if t.startswith(k + '_')]
    if not tags: continue
    print(f'== {k} ({len(tags)} seeds) 표시 | 도달분 | 공격력 | 핀볼 | 판수입 | 판시간 | 판수 | 보스도전')
    for st in [-1, 1, 5, 9, 10, 11, 15, 19, 20, 21, 25, 29, 30, 31, 35, 40, 43, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100]:
        v = [res[t][st] for t in tags if st in res[t]]
        if not v: continue
        m = lambda key: S.median([x[key] for x in v])
        print(f'{st:4d} | {m("t"):6.1f} | {m("atk"):8.1f} | {m("balls"):3.0f} | {m("net"):10.3g} | {m("dur"):4.0f} | {m("n"):3.0f} | {m("boss"):3.0f}')
