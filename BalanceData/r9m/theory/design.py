# r9m: 새 균사 트리(Test_JS2 A안 15차) 수치를 8·9회차 노드 무관 목표 곡선에 맞춘다.
# 실행(프로젝트 루트): python -X utf8 BalanceData/r9m/theory/design.py <income.json 또는 -> [damp]
#   income.json: summarize.py가 만든 표시 스테이지별 일반 판 수입(봇 k06 중앙). '-'면 초기 추정 곡선.
# 결과: Assets/Resources/DB/db.json 의 Upgrade(Efx_Lv*)·Upgrade_Cost(Cost_Lv*, Gem_Lv*)만 바꾼다. 다른 표·칸은 그대로.
#       BalanceData/r9m/theory/plan_out.json (레벨별 목표 스테이지·가격), 이전 가격은 prices_prev.json 으로 감쇠에 쓴다.
import json, math, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
BASE = os.path.join(ROOT, 'BalanceData', 'r9m', 'db.before.json')
DB = os.path.join(ROOT, 'Assets', 'Resources', 'DB', 'db.json')
OUT = os.path.join(os.path.dirname(__file__), 'plan_out.json')
PREV = os.path.join(os.path.dirname(__file__), 'prices_prev.json')
MAXL = 10

# ── 줄 값 (Lv0..N, 줄 전체 값). 여기 없는 줄은 Test_JS2 값 그대로
EFX = {
    'power': [0, 2, 4, 6, 8, 15, 30, 60, 120, 240, 400],      # 바에 맞으면 3초 공격력 +v (5레벨부터 균사석, 후반 보스 피해 축)
    'ps_blade': [1, 3, 6, 18, 60, 200],                         # 공격력 (옛 봇 공격력 곡선: 5→18→40→95→240)
    'ps_crit': [0, .05, .10, .15, .25, .40],
    'ps_critdmg': [.5, 1.0, 2.0, 3.5],                          # 치명타 피해 150% → 450%
    'ps_stamina': [0, 6, 15, 21, 29, 32],                       # 판 시간 10 → 16 → 25 → 31 → 39 → 42초 (옛 판 시간 곡선)
    'ps_tools': [0, 2, 3, 4, 7],                                # 영구 수확기 1 → 3 → 4 → 5 → 8 (옛 핀볼 곡선)
    'ps_combo': [0, .3, .6, 1.0, 1.5, 2.0],
    'ps_kidatk': [0, .1, .25, .5],
    'ed_hunt': [0, .5, 1.5, 4, 12, 40],                         # 타격 점수 ×41
    'ed_haggle': [0, .5, 1.5, 3],                               # 정산 전환율 (단골 손님과 더함)
    'ed_regular': [0, 4, 12, 30],
    'ed_legend': [0, .3, .6, 1.0, 1.5, 2.0],
    'ed_flex': [0, .05, .10, .15],
    'ed_gold': [0, .02, .05, .10],
}
GEM_PRICE = {   # 균사석 레벨 (보스 처치 균사석 10·15·20…로 산다)
    'power': [0, 0, 0, 0, 10, 15, 20, 30, 45, 70],
    'ed_legend': [5, 10, 15, 20, 25],
    'md_grand': [5, 10, 15, 20, 25],
}
POWER_GOLD = [12, 46, 170, 510]   # 사용자 선호: 옛 사냥 준비 골드 4레벨
# ── 목표 구매 스테이지(표시) — 줄 레벨 순서. 없는 줄은 Test_JS2 깊이 순서로 채운다
WANT = {
    'ps_blade': [3, 8, 18, 28, 45],
    'ps_crit': [5, 12, 30, 55, 80],
    'ps_stamina': [5, 9, 19, 29, 70],
    'ps_tools': [13, 29, 58, 66],
    'ed_hunt': [2, 4, 8, 32, 55],
    'ed_haggle': [3, 15, 22],
    'ed_regular': [30, 45, 65],
    'ps_critdmg': [40, 75, 90],
}
# 누적 구매 레벨 목표 N(t): 1~23 판당 2, 24~50 1.5, 51~100 1.1
def cadence(t):
    if t <= 23: return 2.0 * t
    if t <= 50: return 46 + 1.5 * (t - 23)
    return 86.5 + 1.1 * (t - 50)
def cadence_inv(n):
    if n <= 46: return n / 2.0
    if n <= 86.5: return 23 + (n - 46) / 1.5
    return 50 + (n - 86.5) / 1.1

def guess_income(t):   # 초기 추정 (일반 판 net, 봇)
    g = 14.0
    for s in range(1, int(t) + 1):
        g *= 1.45 if s <= 10 else 1.25 if s <= 23 else 1.12 if s <= 50 else 1.06
        if s in (20, 45, 75): g *= 4
    return g

def nice(v):
    if v < 100: return float(max(1, round(v)))
    e = 10 ** (int(math.floor(math.log10(v))) - 1)
    return float(round(v / e) * e)

def main():
    inc_arg = sys.argv[1] if len(sys.argv) > 1 else '-'
    damp = float(sys.argv[2]) if len(sys.argv) > 2 else 0.5
    db = json.load(open(BASE, encoding='utf-8'))
    up = db['Upgrade']; cost = {c['Cost_ID']: c for c in db['Upgrade_Cost']}
    byid = {u['Upg_ID']: u for u in up}
    tree = [u for u in up if u['Branch'] != 'wt']
    # 줄 → 행 순서 (Upg_Req 사슬)
    lines = {}
    for u in tree: lines.setdefault(u['Line'] or u['Upg_Key'], []).append(u)
    for k, rows in lines.items():
        keys = {r['Upg_ID'] for r in rows}
        first = [r for r in rows if r['Upg_Req'] == 0 or r['Upg_Req'] not in keys]
        assert len(first) == 1, k
        order = [first[0]]
        while True:
            nx = [r for r in rows if r['Upg_Req'] == order[-1]['Upg_ID']]
            if not nx: break
            order.append(nx[0])
        assert len(order) == len(rows), k
        lines[k] = order
    # 효과 쓰기
    for k, vals in EFX.items():
        rows = lines[k]; tot = sum(r['Efx_MaxLv'] for r in rows)
        assert len(vals) == tot + 1, (k, len(vals), tot)
        lv0 = 0
        for r in rows:
            m = r['Efx_MaxLv']
            for i in range(MAXL + 1): r[f'Efx_Lv{i}'] = float(vals[lv0 + i]) if i <= m else 0.0
            lv0 += m
    # 레벨 목록 (골드 레벨만 맞춤 대상)
    levels = []   # dict(key, line, row, k(1..), lineLv, gem)
    for k, rows in lines.items():
        lv0 = 0
        for r in rows:
            for i in range(1, r['Efx_MaxLv'] + 1):
                L = lv0 + i
                gem = (k in GEM_PRICE) and GEM_PRICE[k][L - 1] > 0
                levels.append(dict(key=r['Upg_Key'], id=r['Upg_ID'], line=k, k=i, L=L, gem=gem, temp=cost[r['Cost_ID']][f'Cost_Lv{i}']))
            lv0 += r['Efx_MaxLv']
    lev = {(x['key'], x['k']): x for x in levels}
    gold = [x for x in levels if not x['gem'] and x['line'] != 'power']
    # 기본 목표: Test_JS2 임시 가격(깊이) 순위를 누적 구매 목표에 대응 (균사석이던 장인의 도구는 깊이 순서 맨 뒤 취급)
    gold.sort(key=lambda x: (x['temp'] if x['temp'] > 0 else 1e12, x['id'], x['k']))
    for j, x in enumerate(gold): x['d'] = cadence_inv(j + 1 + 4)   # power 골드 4레벨이 먼저
    for k, ws in WANT.items():
        for L, s in enumerate(ws, 1):
            x = next(y for y in gold if y['line'] == k and y['L'] == L); x['d'] = float(s); x['fixed'] = True
    # 선행: (부모 행 1레벨), (Upg_Req 행 마지막 레벨), (같은 행 앞 레벨)
    def prereqs(x):
        r = byid[x['id']]; out = []
        if x['k'] > 1: out.append((x['key'], x['k'] - 1))
        else:
            if r['Upg_Pre']: p = byid[r['Upg_Pre']]; out.append((p['Upg_Key'], 1))
            if r['Upg_Req']: q = byid[r['Upg_Req']]; out.append((q['Upg_Key'], q['Efx_MaxLv']))
        return [lev[o] for o in out]
    # 고정 목표의 조상은 그보다 먼저 (당김)
    for x in gold:
        if not x.get('fixed'): continue
        stack = [(p, x['d']) for p in prereqs(x)]
        while stack:
            p, s = stack.pop()
            if p['gem'] or p['line'] == 'power': continue
            if p['d'] > s - 0.2:
                p['d'] = s - 0.2; stack += [(q, p['d']) for q in prereqs(p)]
    # 미룸: 자식은 선행보다 뒤
    for x in gold: x['t'] = max(1.0, x['d'])
    for _ in range(300):
        ch = False
        for x in gold:
            for p in prereqs(x):
                if p['gem'] or p['line'] == 'power': continue
                if x['t'] < p['t'] + 0.15: x['t'] = p['t'] + 0.15; ch = True
        if not ch: break
    # 수입 곡선
    if inc_arg != '-':
        inc = {int(k): v for k, v in json.load(open(inc_arg, encoding='utf-8')).items()}
        ks = sorted(inc)
        def I(t):
            if t <= ks[0]: return inc[ks[0]]
            if t >= ks[-1]:
                g = inc[ks[-1]]
                for s in range(ks[-1] + 1, int(round(t)) + 1): g *= 1.06 * (4 if s in (20, 45, 75) else 1)
                return g
            a = max(k for k in ks if k <= t); b = min(k for k in ks if k >= t)
            if a == b: return inc[a]
            f = (t - a) / (b - a); return math.exp(math.log(inc[a]) * (1 - f) + math.log(inc[b]) * f)
    else:
        I = guess_income
    # 가격 = 그 스테이지 판 수입 × 0.9 / 밀도(스테이지당 레벨 수)
    gold.sort(key=lambda x: x['t'])
    ts = [x['t'] for x in gold]
    prev = json.load(open(PREV, encoding='utf-8')) if os.path.exists(PREV) and inc_arg != '-' else {}
    for x in gold:
        n = sum(1 for t in ts if abs(t - x['t']) <= 4) / 8.0
        dens = max(0.6, n)
        p = I(x['t']) * 0.9 / dens
        key = f"{x['key']}#{x['k']}"
        if key in prev: p = math.exp(damp * math.log(p) + (1 - damp) * math.log(prev[key]))
        x['p'] = p
    # 정렬 순서 단조(앞보다 ×1.03 이상), 같은 줄 안 단조(앞 레벨보다 ×1.15 이상)
    last = POWER_GOLD[-1] * 0 + 1
    for x in gold:
        x['p'] = max(x['p'], last * 1.03); last = x['p']
    for _ in range(5):
        for x in gold:
            for p in prereqs(x):
                if p['line'] == x['line'] and not p['gem'] and p['line'] != 'power' and x['p'] < p['p'] * 1.15: x['p'] = p['p'] * 1.15
    for x in gold: x['price'] = nice(x['p'])
    json.dump({f"{x['key']}#{x['k']}": x['p'] for x in gold}, open(PREV, 'w', encoding='utf-8'))
    # 쓰기
    for x in levels:
        r = byid[x['id']]; c = cost[r['Cost_ID']]; i = x['k']
        if x['line'] == 'power':
            c[f'Cost_Lv{i}'] = float(POWER_GOLD[i - 1]) if x['L'] <= 4 else 0.0
            c[f'Gem_Lv{i}'] = float(GEM_PRICE['power'][x['L'] - 1])
        elif x['gem']:
            c[f'Cost_Lv{i}'] = 0.0; c[f'Gem_Lv{i}'] = float(GEM_PRICE[x['line']][x['L'] - 1])
        else:
            c[f'Cost_Lv{i}'] = x['price']; c[f'Gem_Lv{i}'] = 0.0
    with open(DB, 'w', encoding='utf-8', newline='\n') as f:
        f.write(json.dumps(db, ensure_ascii=False, indent=2) + '\n')
    plan = [dict(key=x['key'], line=x['line'], L=x['L'], t=round(x['t'], 2), price=x['price'], income=I(x['t'])) for x in gold]
    json.dump(dict(plan=plan), open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    # 검사: 정렬 가격 앞 대비 배율
    ps = sorted(x['price'] for x in gold)
    jumps = [(ps[i] / ps[i - 1]) for i in range(1, len(ps))]
    print(f'골드 레벨 {len(gold)} · 최대 정렬 배율 ×{max(jumps):.2f} (STEP_FIX 1.8 넘는 곳 {sum(j > 1.8 for j in jumps)}) · 마지막 목표 스테이지 {gold[-1]["t"]:.1f}')
    for x in gold[:12] + gold[-6:]: print(f"  {x['key']}#{x['k']} t {x['t']:.1f} 가격 {x['price']:.3g}")

if __name__ == '__main__':
    main()
