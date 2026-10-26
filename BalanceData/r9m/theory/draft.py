# 새 트리 줄 Efx 초안 → efx_draft.json (키 → [Efx_Lv0..N]). 근거·가정은 밸런스설계/이론_9m.md
# 각 행 값 = 그 행이 놓인 구매 분위수에서 옛 트리 총량(compare.py)에 맞춘 값. 수입은 G = (1+타격점수%) × (1.1+정산%)로 맞춘다(b̄ 가정)
import json, os
from compare import new, at, orows, nrows
LINES = {   # 줄 → 행별 끝값 (첫 행 Lv0 = 줄 시작값)
    'ps_blade':   (1,   [[3], [5], [18], [40], [110]]),
    'ed_hunt':    (0,   [[0.2, 0.4, 0.6], [4.6], [22]]),
    'ed_haggle':  (0,   [[0.9], [1.15], [1.4]]),
    'ed_regular': (0,   [[0.3], [8.0], [9.0]]),
    'ps_crit':    (0,   [[0.02], [0.04], [0.06], [0.10], [0.15]]),
    'ps_critdmg': (2.0, [[2.5], [3.0], [4.0]]),
    'ps_stamina': (0,   [[5], [9], [13], [17], [21]]),
    'ps_size':    (0,   [[0.15], [0.3], [0.45]]),
    'ps_wind':    (0,   [[0.12], [0.24], [0.36]]),
    'ed_hand1':   (0,   [[0.05], [1.0], [26]]),
    'ed_hand2':   (0,   [[36], [46], [166]]),
    'ed_hand3':   (0,   [[266], [366], [666]]),
    'ed_hand4':   (0,   [[466], [566], [666]]),
}
rows = {}
for line, (v0, steps) in LINES.items():
    keys = [u['Upg_Key'] for u in new['Upgrade'] if u.get('Line') == line]
    keys.sort(key=lambda k: int(k[len(line):]) if k[len(line):].isdigit() else 0)
    assert len(keys) == len(steps), (line, keys)
    prev = v0
    for k, s in zip(keys, steps):
        rows[k] = [prev] + s; prev = s[-1]
# 현재 값과 나란히
cur = {u['Upg_Key']: [u.get(f'Efx_Lv{i}', 0) for i in range(u['Efx_MaxLv'] + 1)] for u in new['Upgrade']}
for k in rows: assert len(rows[k]) == len(cur[k]), (k, rows[k], cur[k])
# 수입 배율 확인 (b̄: 분위수 0.4 전 5, 뒤 10)
def g_old(q):
    o = at(orows, q)[3]; b = 5 if q < 0.4 else 10
    return (1 + o['scoreF'] / b) * (1 + o['settle'])
check = []
for q in [0.05, 0.1, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.85, 0.9, 1.0]:
    bought = {}
    for (qq, p, k, s) in nrows:
        if qq <= q + 1e-9: bought[k] = bought.get(k, 0) + 1
    def lv(line):
        v = None
        for k in sorted((k for k in rows if k.startswith(line) and k[len(line):].isdigit()), key=lambda k: int(k[len(line):])):
            if k in bought: v = rows[k][bought[k]]
        return v if v is not None else 0
    gn = (1 + lv('ed_hunt')) * (1.1 + lv('ed_haggle') + lv('ed_regular'))
    gc = (1 + at(nrows, q)[3]['scoreF'] - 1) * (1 + at(nrows, q)[3]['settle'])
    check.append({'q': q, 'G_old': round(g_old(q), 2), 'G_new_now': round(gc, 2), 'G_new_draft': round(gn, 2), 'atk_old': at(orows, q)[3]['atk'], 'atk_draft': lv('ps_blade') or 1})
out = {'_note': '키 → Efx_Lv0..N 후보 (초안, 밸런스설계/이론_9m.md). 구조·가격·칼럼 그대로', 'rows': rows, 'current': {k: cur[k] for k in rows}, 'income_check': check,
       'gem_options': {'ps_tools': 'A: 골드 전환 Cost_Lv1~4 ≈ 5e9·3e10·3e11·2.7e12, Gem 0 (Efx 0~4 그대로)', 'md_grand': '균사석 유지', 'ed_legend': '균사석 유지'}}
json.dump(out, open(os.path.join(os.path.dirname(__file__), 'efx_draft.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
for c in check: print(c)
