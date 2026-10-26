# Efx 초안(BalanceData/r9m/theory/efx_draft.json)을 db.json에 적용한다. add_sinks_scale.py 다음에 돌린다.
# - rows: Upg_Key → Efx_Lv0..N (기존 값이 있는 칸만 덮는다. 나머지 레벨 칸은 그대로)
# - ps_tools: 골드 전환 (초안 gem_options A: Cost_Lv1~4 = 5e9·3e10·3e11·2.7e12, Gem 0)
import json, os
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DB = os.path.join(ROOT, 'Assets/Resources/DB/db.json')
import sys
d = json.load(open(os.path.join(ROOT, 'BalanceData/r9m/theory', sys.argv[1] if len(sys.argv) > 1 else 'efx_draft.json'), encoding='utf-8'))
db = json.load(open(DB, encoding='utf-8'))
U = {u['Upg_Key']: u for u in db['Upgrade']}
C = {c['Cost_ID']: c for c in db['Upgrade_Cost']}
n = 0
for key, vals in d['rows'].items():
    u = U[key]
    for i, v in enumerate(vals): u[f'Efx_Lv{i}'] = float(v)
    n += 1
tc = C[U['ps_tools']['Cost_ID']]
for i, g in enumerate([5e9, 3e10, 3e11, 2.7e12], 1):
    tc[f'Cost_Lv{i}'] = g; tc[f'Gem_Lv{i}'] = 0.0
db['_source'] += ' + Efx ' + (sys.argv[1] if len(sys.argv) > 1 else 'efx_draft.json') + ' (이론_9m) · ps_tools 골드'
open(DB, 'w', encoding='utf-8', newline='\n').write(json.dumps(db, ensure_ascii=False, indent=2) + '\n')
print('ok rows', n)
