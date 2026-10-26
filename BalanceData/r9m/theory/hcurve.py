# 새 트리(it2) 판별 H = net / (G × zone × 공 수 × 판 시간) 을 "그때까지 산 가장 비싼 골드 칸 가격" 구간별로 모은다.
# H = 노드 수입 배율(G)·지역·공 수·시간을 뺀 나머지(등급 점수·콤보·별·스킬·황금 …). 사용: python hcurve.py → hcurve.json
import csv, json, os, math, statistics as S
from compare import new
import measure2 as M
C = {c['Cost_ID']: c for c in new['Upgrade_Cost']}; NU = {u['Upg_Key']: u for u in new['Upgrade']}
def price(k, L):
    c = C[NU[k]['Cost_ID']]; g = c.get(f'Cost_Lv{L}', 0) or 0; gem = c.get(f'Gem_Lv{L}', 0) or 0
    return 0 if gem > 0 else g
bins = {}
for path in M.NEW:
    rows = M.load(path, True); raw = list(csv.DictReader(open(path, encoding='utf-8')))
    pmax = 0
    for r, x in zip(rows, raw):
        for b in (x['bought'] or '').split(';'):
            if ':' in b:
                k, ab = b.split(':')
                if k in NU and k != 'ps_tools': pmax = max(pmax, price(k, int(ab.split('>')[1])))
        if r['boss'] or r['ds'] < 1: continue
        z = 10 ** r['zi'] * 1.01 ** (r['ds'] - 1)
        h = r['net'] / (r['G'] * z * r['balls'] * r['dur'])
        bins.setdefault(round(math.log10(max(pmax, 1)) * 2) / 2, []).append(h)
out = {str(b): {'n': len(v), 'H': S.median(v)} for b, v in sorted(bins.items())}
json.dump(out, open(os.path.join(os.path.dirname(__file__), 'hcurve.json'), 'w', encoding='utf-8'), indent=1)
if __name__ == '__main__':
    for b, v in out.items(): print('log10P', b, v['n'], f"{v['H']:.3g}")
