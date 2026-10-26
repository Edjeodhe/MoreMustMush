# 옛 트리(cf1abf52)와 새 트리(현재 db.json)의 효과 총량을 같은 구매 순위(골드 가격 순 분위수)에서 비교한다.
# 사용: python compare.py  → 표 출력 + compare.json
import json, subprocess, os, math
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
OLD_COST_MUL = 0.9
old = json.loads(subprocess.check_output(['git', 'show', 'cf1abf52:Assets/Resources/DB/db.json'], cwd=ROOT).decode('utf-8'))
new = json.load(open(os.path.join(ROOT, 'Assets/Resources/DB/db.json'), encoding='utf-8'))

# 능력치 → (옛 키 목록, 새 줄 목록, 기본값)
OLD_MAP = {
    'atk': ['ps_atk', 'ps_atk2', 'ps_atk2p', 'ps_atk3', 'ps_atk3p', 'ps_atk4'],
    'crit': ['ps_crit'], 'critMul': ['ps_crit2'], 'size': ['ps_size'], 'spd': ['ps_spd'],
    'slots': ['ps_ball', 'ps_ball2', 'ps_ball3'], 'time': ['ps_dur', 'ps_dur2'],
    'scoreF': ['ed_score', 'ed_score1p', 'ed_score2', 'ed_score2p', 'ed_score3', 'ed_score3p'],
    'harvF': ['ed_bonus', 'ed_bonus1p', 'ed_bonus2', 'ed_bonus2p', 'ed_bonus3', 'ed_bonus3p'],
    'settle': ['ed_price', 'ed_price2', 'ed_trade'], 'cols': ['ed_cols', 'ed_cols2'], 'colMax': ['ed_size'],
}
NEW_MAP = {
    'atk': ['ps_blade'], 'crit': ['ps_crit'], 'critMul': ['ps_critdmg'], 'size': ['ps_size'], 'spd': ['ps_wind'],
    'slots': ['ps_tools'], 'time': ['ps_stamina'], 'scoreF': ['ed_hunt'],
    'harvF': ['ed_hand1', 'ed_hand2', 'ed_hand3', 'ed_hand4'], 'settle': ['ed_haggle', 'ed_regular'],
    'cols': ['ed_view', 'ed_sky', 'ed_god'], 'colMax': ['ed_more'],
}
# 시작값(아무것도 안 샀을 때 식에 들어가는 값)
OLD_BASE = {'atk': 1, 'crit': 0, 'critMul': 3, 'size': 1, 'spd': 1, 'slots': 1, 'time': 10, 'scoreF': 0, 'harvF': 1, 'settle': 0.1, 'cols': 4, 'colMax': 5}
NEW_BASE = {'atk': 1, 'crit': 0, 'critMul': 1.5, 'size': 1, 'spd': 1, 'slots': 1, 'time': 10, 'scoreF': 1, 'harvF': 1, 'settle': 0.1, 'cols': 3, 'colMax': 3}

def efx(u, i): return u.get(f'Efx_Lv{i}', 0.0) or 0.0

def old_levels():
    C = {c['Cost_ID']: c for c in old['Upgrade_Cost']}
    key2stat = {k: s for s, ks in OLD_MAP.items() for k in ks}
    out = []
    for u in old['Upgrade']:
        if u['Branch'] not in ('ed', 'md', 'ps') or u['Efx_MaxLv'] <= 0: continue
        c = C[u['Cost_ID']]
        for i in range(1, u['Efx_MaxLv'] + 1):
            g = c.get(f'Cost_Lv{i}', 0) or 0; gem = c.get(f'Gem_Lv{i}', 0) or 0
            if g <= 0 or gem > 0: continue
            st = key2stat.get(u['Upg_Key']); d = efx(u, i) - efx(u, i - 1) if st else 0
            out.append((math.ceil(g * OLD_COST_MUL), u['Upg_Key'], i, st, d))
    return sorted(out, key=lambda t: t[0])

def new_levels():
    C = {c['Cost_ID']: c for c in new['Upgrade_Cost']}
    line2stat = {k: s for s, ks in NEW_MAP.items() for k in ks}
    out = []
    for u in new['Upgrade']:
        if u['Branch'] not in ('ed', 'md', 'ps') or u['Efx_MaxLv'] <= 0: continue
        c = C[u['Cost_ID']]
        for i in range(1, u['Efx_MaxLv'] + 1):
            g = c.get(f'Cost_Lv{i}', 0) or 0; gem = c.get(f'Gem_Lv{i}', 0) or 0
            if g <= 0 or gem > 0: continue
            st = line2stat.get(u.get('Line', '')); d = efx(u, i) - efx(u, i - 1) if st else 0
            out.append((g, u['Upg_Key'], i, st, d))
    return sorted(out, key=lambda t: t[0])

def cum(levels, base, hand=False):
    """구매 순서대로 능력치 누적. 반환: 분위수별 상태 목록"""
    s = dict(base); rows = []
    hands = [0.0] * 4
    for r, (p, k, i, st, d) in enumerate(levels):
        if st == 'harvF' and hand:
            hands[int(k[7]) - 1] += d; s['harvF'] = 1 + hands[0]   # 일반 버섯 손길(대부분 수확)
        elif st: s[st] += d
        rows.append((r / max(1, len(levels) - 1), p, k, dict(s)))
    return rows

def at(rows, q):
    best = rows[0]
    for row in rows:
        if row[0] <= q + 1e-9: best = row
    return best

OL, NL = old_levels(), new_levels()
orows, nrows = cum(OL, OLD_BASE), cum(NL, NEW_BASE, hand=True)
QS = [0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
res = []
for q in QS:
    o, n = at(orows, q), at(nrows, q)
    res.append({'q': q, 'price_old': o[1], 'price_new': n[1], 'old': o[3], 'new': n[3]})
if __name__ == '__main__':
    print('옛 골드 칸', len(OL), '새 골드 칸', len(NL))
    stats = list(OLD_BASE)
    print('q     가격     ' + ' '.join(f'{s:>14}' for s in stats))
    for r in res:
        print(f"{r['q']:.2f} {r['price_new']:9.3g} " + ' '.join(f"{r['old'][s]:6.3g}/{r['new'][s]:<7.3g}" for s in stats))
    # 새 줄의 각 행이 놓인 분위수
    print('\n새 줄 행의 분위수(구매 순위)')
    for (q, p, k, s) in nrows:
        pass
    pos = {}
    for (q, p, k, s) in nrows: pos.setdefault(k, []).append(round(q, 3))
    for k in sorted(pos):
        if any(k.startswith(x) for x in ('ps_blade', 'ed_hunt', 'ed_haggle', 'ed_regular', 'ed_hand', 'ps_stamina', 'ps_crit', 'ps_size', 'ps_wind', 'ed_view', 'ed_sky', 'ed_god', 'ed_more')):
            print(k, pos[k])
    json.dump(res, open(os.path.join(os.path.dirname(__file__), 'compare.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
