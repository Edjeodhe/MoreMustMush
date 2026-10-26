# 옛 기준선(T9-0 k06 s1~3)에서 표시 스테이지별: 그때까지 산 가장 비싼 골드 칸 가격(옛 ×0.9)·공격력·공 수·판 시간·판 net·보스 dps.
# 새 트리 행 가격을 이 곡선에 대어 "그 가격을 살 즈음 옛은 몇 스테이지였나"를 얻는다. 사용: python oldcurve.py → oldcurve.json
import csv, json, os, statistics as S, subprocess, math
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
odb = json.loads(subprocess.check_output(['git', 'show', 'cf1abf52:Assets/Resources/DB/db.json'], cwd=ROOT).decode('utf-8'))
C = {c['Cost_ID']: c for c in odb['Upgrade_Cost']}; OU = {u['Upg_Key']: u for u in odb['Upgrade']}
def price(k, L):   # L = 산 뒤 레벨
    c = C[OU[k]['Cost_ID']]; g = c.get(f'Cost_Lv{L}', 0) or 0; gem = c.get(f'Gem_Lv{L}', 0) or 0
    return 0 if gem > 0 else math.ceil(g * 0.9)
per = {}
for s in (1, 2, 3):
    pmax = 0
    for r in csv.DictReader(open(os.path.join(ROOT, f'BalanceData/r9/test/T9-0/rounds_k06_s{s}.csv'), encoding='utf-8')):
        for b in (r['bought'] or '').split(';'):
            if ':' in b and not b.startswith('inf_'):
                k, ab = b.split(':'); 
                if k in OU: pmax = max(pmax, price(k, int(ab.split('>')[1])))
        ds = int(r['stage']) - 2
        d = per.setdefault(ds, {'p': [], 'atk': [], 'balls': [], 'dur': [], 'net': [], 'min': [], 'bdps': []})
        d['p'].append(pmax); d['atk'].append(float(r['atk'])); d['balls'].append(int(r['balls'])); d['dur'].append(float(r['dur'])); d['min'].append(float(r['playMin']))
        if r['bossFlag'] in ('0', '2'): d['net'].append(float(r['net']))
        if float(r['bossMaxHp']) > 0: d['bdps'].append(float(r['grossDps']))
out = {}
for ds in sorted(per):
    d = per[ds]; out[ds] = {k: (S.median(v) if v else None) for k, v in d.items()}
json.dump(out, open(os.path.join(os.path.dirname(__file__), 'oldcurve.json'), 'w', encoding='utf-8'), indent=1)
if __name__ == '__main__':
    for ds, v in out.items():
        if ds % 5 == 0: print(ds, {k: (f'{x:.3g}' if isinstance(x, float) else x) for k, x in v.items()})
