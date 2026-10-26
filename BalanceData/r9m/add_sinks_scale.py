# 새 균사 트리 db.json에 (1) 옛 무한 싱크 6개를 다시 넣고 (2) 트리(ed·md·ps) 골드 가격을 일정 배수로 키운다.
# 사용: python add_sinks_scale.py [pow|flat] [값]   (BalanceData/r9m/db.before.json = 병합 직후 원본에서 매번 새로 만든다. 세계수 wt 가격·균사석은 그대로)
#   pow  (기본): 가격 p → p × max(1, p/P0)^a. 싼 가격(초반)은 거의 그대로, 비쌀수록 더 키운다. 값 = 트리 최고가가 닿을 목표(기본 3e12)에서 a를 구한다
#   oldmap: 옛 트리(cf1abf52, KHD 8·9회차 가격 × 옛 COST_MUL 0.9)의 골드 가격 분포를 새 트리의 구매 순서(임시 가격 순)에 분위수로 대응시킨다 (옛 재화 밸런스를 가져간다)
#   flat: 모든 가격 × 값 (초반 가격도 같이 커져서 옛 골드 지표와 어긋난다)
import json, subprocess, sys, os, math
MODE = sys.argv[1] if len(sys.argv) > 1 else 'pow'
VAL = float(sys.argv[2]) if len(sys.argv) > 2 else (3e12 if MODE == 'pow' else 135)
OLD_COST_MUL = 0.9
P0 = 15.0
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DB = os.path.join(ROOT, 'Assets/Resources/DB/db.json')
BEFORE = os.path.join(ROOT, 'BalanceData/r9m/db.before.json')
old = json.loads(subprocess.check_output(['git', 'show', 'cf1abf52:Assets/Resources/DB/db.json'], cwd=ROOT).decode('utf-8'))
db = json.load(open(BEFORE, encoding='utf-8'))
INF = ['inf_atk', 'inf_gold', 'inf_sdmg', 'inf_srate', 'inf_spd', 'inf_ball']
# 부모(트리 끝 노드): 새 트리에서 같은 역할의 끝 노드
PARENT = {'inf_atk': 'ps_blade5', 'inf_spd': 'ps_blade5', 'inf_ball': 'ps_tools', 'inf_gold': 'ed_regular3', 'inf_sdmg': 'md_burst3', 'inf_srate': 'md_burst3'}
# 가격 키우기 (트리 가지만)
C = {r['Cost_ID']: r for r in db['Upgrade_Cost']}
tree = [u for u in db['Upgrade'] if u['Branch'] in ('ed', 'md', 'ps') and u['Efx_MaxLv'] > 0]
if MODE == 'oldmap':
    OCs = {r['Cost_ID']: r for r in old['Upgrade_Cost']}
    oldp = []
    for u in old['Upgrade']:
        if u['Branch'] not in ('ed', 'md', 'ps') or u['Efx_MaxLv'] <= 0: continue
        c = OCs[u['Cost_ID']]
        for i in range(1, u['Efx_MaxLv'] + 1):
            g = c.get(f'Cost_Lv{i}', 0) or 0; ge = c.get(f'Gem_Lv{i}', 0) or 0
            if g > 0 and ge == 0: oldp.append(math.ceil(g * OLD_COST_MUL))
    oldp.sort()
    levels = []   # (placeholder 가격, 행 깊이 순서, 행, 레벨)
    order = {u['Upg_ID']: n for n, u in enumerate(tree)}
    for u in tree:
        c = C[u['Cost_ID']]
        for i in range(1, u['Efx_MaxLv'] + 1):
            g = c[f'Cost_Lv{i}']; ge = c.get(f'Gem_Lv{i}', 0) or 0
            if g > 0 and ge == 0: levels.append((g, i, order[u['Upg_ID']], u, i))
    levels.sort(key=lambda t: (t[0], t[1], t[2]))
    n = len(levels); m = len(oldp)
    for r, (_, _, _, u, i) in enumerate(levels):
        C[u['Cost_ID']][f'Cost_Lv{i}'] = oldp[round(r * (m - 1) / max(1, n - 1))]
    print('oldmap: 새 구매 칸', n, '← 옛 구매 칸', m, '· 최저', oldp[0], '최고 %.3g' % oldp[-1])
    A = 0
else:
    pmax = max(C[u['Cost_ID']][f'Cost_Lv{i}'] for u in tree for i in range(1, u['Efx_MaxLv'] + 1))
    A = math.log(VAL / pmax) / math.log(pmax / P0) if MODE == 'pow' else 0
    def scale(p):
        if p <= 0: return p
        return math.ceil(p * (max(1.0, p / P0) ** A)) if MODE == 'pow' else p * VAL
    for u in tree:
        c = C[u['Cost_ID']]
        for i in range(1, 11):
            c[f'Cost_Lv{i}'] = scale(c[f'Cost_Lv{i}'])
K = f'{MODE} {VAL:g} (a={A:.3f})' if MODE != 'oldmap' else 'oldmap (옛 가격 분포 이식)'
# 사냥 준비(power): 균사석을 모두 빼고 골드로만 산다 (사용자 2026-10-26). 1~4레벨은 옛 가격 12·46·170·510 × 옛 COST_MUL 0.9, 5레벨부터는 ×3씩
pw = next(u for u in db['Upgrade'] if u['Upg_Key'] == 'power')
pc = C[pw['Cost_ID']]
pg = [12, 42, 153, 459]
while len(pg) < 10: pg.append(pg[-1] * 3)
for i in range(1, 11):
    pc[f'Cost_Lv{i}'] = float(pg[i - 1]); pc[f'Gem_Lv{i}'] = 0.0

# 싱크 행
byKey = {u['Upg_Key']: u for u in db['Upgrade']}
occupied = {(u['Pos_X'], u['Pos_Y']) for u in db['Upgrade'] if u['Branch'] != 'wt'}
OC = {r['Cost_ID']: r for r in old['Upgrade_Cost']}
tmpl = next(u for u in db['Upgrade'] if u['Upg_Key'] == 'ps_blade1')
for u in old['Upgrade']:
    if u['Upg_Key'] not in INF: continue
    p = byKey[PARENT[u['Upg_Key']]]
    best = None
    for dx in range(-3, 4):
        for dy in range(-3, 4):
            q = (p['Pos_X'] + dx, p['Pos_Y'] + dy)
            if q in occupied: continue
            d = abs(dx) + abs(dy)
            if best is None or d < best[0]: best = (d, q)
    pos = best[1]; occupied.add(pos)
    row = {k: tmpl[k] for k in tmpl}
    row.update({k: u[k] for k in ['Upg_ID', 'Upg_Key', 'Upg_Name', 'Str_Group_ID', 'Branch', 'Type', 'Efx_MaxLv', 'Cost_ID', 'Tier', 'Sub', 'Skill', 'Icon', 'Color']})
    row.update({'Upg_Pre': p['Upg_ID'], 'Upg_Req': p['Upg_ID'], 'Line': '', 'Pos_X': pos[0], 'Pos_Y': pos[1]})
    for i in range(0, 11): row[f'Efx_Lv{i}'] = u.get(f'Efx_Lv{i}', 0.0)
    db['Upgrade'].append(row)
    oc = OC[u['Cost_ID']]
    cost = {'Cost_ID': u['Cost_ID']}
    for i in range(1, 11): cost[f'Cost_Lv{i}'] = oc.get(f'Cost_Lv{i}', 0.0); cost[f'Gem_Lv{i}'] = oc.get(f'Gem_Lv{i}', 0.0)
    db['Upgrade_Cost'].append(cost)
db['_source'] = db['_source'].split(' + 싱크')[0] + f' + 싱크 6개 복원 · 트리 골드 가격 {K}'
open(DB, 'w', encoding='utf-8', newline='\n').write(json.dumps(db, ensure_ascii=False, indent=2) + '\n')
print('ok', K, 'rows', len(db['Upgrade']))
