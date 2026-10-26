# 2차 보정(실측): it2 판 기록으로 수입 줄·공격력 줄·장인의 도구 Efx를 다시 정한다 → efx_draft2.json
# 근거·가정: 밸런스설계/이론_9m.md "2차 보정(실측)". 다시 돌리면 같은 결과.
# 입력: oldcurve.json(옛 T9-0: 스테이지별 산 최고가·net·공격력·공 수), hcurve.json(새 it2: 최고가별 H), efx_draft.json(1차)
# 식: 일반 판 net ≈ H(P) × G × zone(s) × 공 수 × 판 시간,  G = (1+ed_hunt) × (1.1+ed_haggle+ed_regular)
#     보스 dps ≈ K_DPS × st.atk × 공 수 (it2 실측 6.3~7.8),  st.atk = ps_blade × 1.1 (별·세계수 몫 빼고)
import json, math, os
HERE = os.path.dirname(os.path.abspath(__file__))
oc = {int(k): v for k, v in json.load(open(os.path.join(HERE, 'oldcurve.json'))).items()}
hc = {float(k): v for k, v in json.load(open(os.path.join(HERE, 'hcurve.json'))).items()}
d1 = json.load(open(os.path.join(HERE, 'efx_draft.json'), encoding='utf-8'))

K_DPS, T_KILL = 7.0, 15.0          # 보스 dps / (st.atk × 공 수), 첫 도전에서 잡을 목표 시간(초)
BOSS_HP = {10: 43.7, 20: 198.1, 30: 19750.7, 40: 94343.8, 50: 977009.0, 60: 1.13e6, 70: 4.66e6}   # 30·40·50은 실측(it2·T9-0 bossMaxHp), 60·70은 식 ×0.4(실측 비율)
T_KILL70 = 25.0                   # 보스 70: 판 시간(약 34초) 안에 첫 도전 처치
H_MIN_N = 10                      # H 구간 표본이 이보다 적으면 버린다
AB_DUR = 3.0                      # 별·세계수 판 시간(it2: 노장 17초일 때 판 30초)

# 새 트리 골드 칸 가격 (compare.py new_levels, oldmap)
P = {'hunt1': 45, 'haggle1': 230, 'haggle2': 4.0e3, 'haggle3': 2.5e4, 'hunt2': 8.9e6, 'regular1': 1.2e7,
     'regular2': 2.5e10, 'hunt3': 6.9e11, 'regular3': 3.7e12,
     'blade1': 26, 'blade2': 360, 'blade3': 2.3e6, 'blade4': 9.0e7, 'blade5': 2.1e10,
     'tools1': 5.0e9, 'tools2': 3.0e10, 'tools3': 3.0e11, 'tools4': 2.7e12,
     'stam1': 2.3e4, 'stam2': 3.3e5, 'stam3': 8.5e6, 'stam4': 2.2e8, 'stam5': 8.9e11}
TOOLS = [0, 1, 3, 5, 7]           # 장인의 도구 Efx 안: 레벨마다 영구 수확기 +1·+2·+2·+2 (끝 8개 = 옛과 같음)
STAM = [0, 5, 9, 13, 17, 21]      # 노장의 체력(1차 그대로)

def lerp_log(xs, ys, x):
    """log-log 선형 보간 (끝 밖은 끝값)"""
    lx = math.log10(x)
    if lx <= xs[0]: return ys[0]
    if lx >= xs[-1]: return ys[-1]
    for i in range(len(xs) - 1):
        if xs[i] <= lx <= xs[i + 1]:
            t = (lx - xs[i]) / (xs[i + 1] - xs[i])
            return 10 ** (math.log10(ys[i]) * (1 - t) + math.log10(ys[i + 1]) * t)

HX = [b for b in sorted(hc) if hc[b]['n'] >= H_MIN_N]; HY = [hc[b]['H'] for b in HX]
def H(p): return lerp_log(HX, HY, p)   # 1e10 넘으면 끝값(가정: 측정 밖은 평평)

STG = [s for s in sorted(oc) if s >= 1]
def stage_of(p):   # 옛 봇이 가격 p 칸을 산 스테이지
    for s in STG:
        if oc[s]['p'] >= p: return s
    return 70
NS = [s for s in STG if oc[s]['net']]
def net_old(s):    # 옛 일반 판 net (스테이지 보간)
    xs = [x for x in NS];
    if s <= xs[0]: return oc[xs[0]]['net']
    for a, b in zip(xs, xs[1:]):
        if a <= s <= b:
            t = (s - a) / (b - a); return 10 ** (math.log10(oc[a]['net']) * (1 - t) + math.log10(oc[b]['net']) * t)
    return oc[xs[-1]]['net']
def zone(s): i = 0 if s < 20 else 1 if s < 45 else 2 if s < 75 else 3; return 10 ** i * 1.01 ** (s - 1)
def step(p, keys, vals):   # 가격 p까지 산 마지막 값
    v = vals[0]
    for k, x in zip(keys, vals[1:]):
        if P[k] <= p: v = x
    return v
def balls(p): return 1 + step(p, ['tools1', 'tools2', 'tools3', 'tools4'], TOOLS)
def dur(p): return 10 + AB_DUR + step(p, ['stam1', 'stam2', 'stam3', 'stam4', 'stam5'], STAM)
def g_req(p):
    s = stage_of(p); return net_old(s) / (H(p) * zone(s) * balls(p) * dur(p))
def g_mean(p0, p1, n=12):   # 구간 [p0, p1) 의 G_req 기하평균
    xs = [10 ** (math.log10(p0) + (math.log10(p1) - math.log10(p0)) * i / n) for i in range(n)]
    return math.exp(sum(math.log(g_req(x)) for x in xs) / n)

def nice(x):   # 유효숫자 2자리
    if x <= 0: return 0
    e = math.floor(math.log10(x)) - 1; return round(x / 10 ** e) * 10 ** e if e >= 0 else round(x, -e)

# ---- 수입 줄: 사는 순서대로 G 목표 = 그 줄이 마지막 수입 칸인 가격 구간의 G_req 기하평균
# 초반 흥정(가격 230~2.5e4, 표시 5~14)은 H 표본이 적고(구간 n 3~27) G_req가 들쭉날쭉해 기하평균이 옛 net의 2~4배로 넘친다.
# 넘치면 봇이 앞당겨 사서 뒤가 더 넘치므로(되먹임) 옛 net의 1~2배 안으로 낮춘 값을 쓴다
HAG_FIX = {'haggle1': 6, 'haggle2': 15, 'haggle3': 28}
ORDER = ['hunt1', 'haggle1', 'haggle2', 'haggle3', 'hunt2', 'regular1', 'regular2', 'hunt3', 'regular3']
hunt, hag, reg = 0.0, 0.0, 0.0
inc = {}
for i, k in enumerate(ORDER):
    p0 = P[k]; p1 = P[ORDER[i + 1]] if i + 1 < len(ORDER) else p0 * 3
    g = g_mean(p0, p1)
    if k.startswith('hunt'): hunt = nice(max(hunt, g / (1.1 + hag + reg) - 1))
    elif k in HAG_FIX: hag = HAG_FIX[k]
    elif k.startswith('haggle'): hag = nice(max(hag, g / (1 + hunt) - 1.1 - reg))
    else: reg = nice(max(reg, g / (1 + hunt) - 1.1 - hag))
    inc[k] = {'G_req': round(g, 1), 'hunt': hunt, 'haggle': hag, 'regular': reg, 'G': round((1 + hunt) * (1.1 + hag + reg), 1)}

# ---- 공격력 줄: 보스 s를 T_KILL초에 잡는 ps_blade (그때 옛 봇이 산 최고가로 산 줄 기준)
need = {}
for s, hp in BOSS_HP.items():
    p = oc[s]['p'] if s in oc else oc[max(oc)]['p']
    need[s] = {'pmax': p, 'balls': balls(p), 'blade_need': hp / (K_DPS * T_KILL * balls(p) * 1.1)}
BLADE = {'blade1': 3, 'blade2': 8, 'blade3': 50,
         'blade4': nice(max(need[30]['blade_need'], need[40]['blade_need']) * 1.1),
         'blade5': nice(need[70]['blade_need'] * T_KILL / T_KILL70)}   # 보스 70(트리 끝)을 T_KILL70초 안에. 50·60은 넉넉히 넘는다

rows = {k: list(v) for k, v in d1['rows'].items()}
h1 = inc['hunt1']['hunt']
rows['ed_hunt1'] = [0, nice(h1 / 4), nice(h1 / 2), h1]
rows['ed_hunt2'] = [h1, inc['hunt2']['hunt']]
rows['ed_hunt3'] = [inc['hunt2']['hunt'], inc['hunt3']['hunt']]
rows['ed_haggle1'] = [0, inc['haggle1']['haggle']]
rows['ed_haggle2'] = [inc['haggle1']['haggle'], inc['haggle2']['haggle']]
rows['ed_haggle3'] = [inc['haggle2']['haggle'], inc['haggle3']['haggle']]
rows['ed_regular1'] = [0, inc['regular1']['regular']]
rows['ed_regular2'] = [inc['regular1']['regular'], inc['regular2']['regular']]
rows['ed_regular3'] = [inc['regular2']['regular'], inc['regular3']['regular']]
bl = [1, BLADE['blade1'], BLADE['blade2'], BLADE['blade3'], BLADE['blade4'], BLADE['blade5']]
for i in range(5): rows[f'ps_blade{i + 1}'] = [bl[i], bl[i + 1]]
rows['ps_tools'] = list(TOOLS)

# ---- 예측: 표시 스테이지별 (옛 봇이 그때 산 최고가 기준) net·공격력·보스 처치 시간
def G_at(p):
    hu = step(p, ['hunt1', 'hunt2', 'hunt3'], [0, h1, inc['hunt2']['hunt'], inc['hunt3']['hunt']])
    ha = step(p, ['haggle1', 'haggle2', 'haggle3'], [0, inc['haggle1']['haggle'], inc['haggle2']['haggle'], inc['haggle3']['haggle']])
    re = step(p, ['regular1', 'regular2', 'regular3'], [0, inc['regular1']['regular'], inc['regular2']['regular'], inc['regular3']['regular']])
    return (1 + hu) * (1.1 + ha + re)
def blade_at(p): return step(p, ['blade1', 'blade2', 'blade3', 'blade4', 'blade5'], bl)
pred = []
for s in (5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70):
    p = oc[s]['p']; net = H(p) * G_at(p) * zone(s) * balls(p) * dur(p)
    r = {'stage': s, 'pmax_old': p, 'G': round(G_at(p), 1), 'net_pred': float(f'{net:.3g}'), 'net_old': oc[s]['net'] or float(f'{net_old(s):.3g}'),
         'atk_pred': round(blade_at(p) * 1.1, 1), 'atk_old': oc[s]['atk'], 'balls_pred': balls(p), 'balls_old': oc[s]['balls']}
    if s in BOSS_HP: r['boss_kill_s'] = round(BOSS_HP[s] / (K_DPS * blade_at(p) * 1.1 * balls(p)), 1)
    pred.append(r)

# ---- b̄ 역산: 같은 스테이지 구간에서 공·초·지역을 뺀 수입(옛 H_old / 새 H×G) = 옛 노드 몫 / 새 노드 몫 이라 두면
#      (b̄+F)(1+정산)/b̄ = 비율 × G_new  → b̄ = F / (비율×G_new/(1+정산) − 1)   (측정값: measure2.json)
m2 = json.load(open(os.path.join(HERE, 'measure2.json'), encoding='utf-8'))
binv = []
for band in ('21-30', '31-40'):
    o, n = m2['old_T9_0'][band], m2['new_it2'][band]
    ratio = o['H_per_ball_sec'] / (n['H_per_ball_sec'] * n['G'])
    x = ratio * n['G'] / (1 + o['settle'])
    binv.append({'band': band, 'F_old': o['F'], 'settle_old': o['settle'], 'G_new': n['G'], 'per_ball_sec_ratio': round(ratio, 2), 'b_bar': round(o['F'] / (x - 1), 1) if x > 1 else None})

out = {'_note': '2차 보정(실측) — 키 → Efx_Lv0..N. 구조·가격·칼럼 그대로. 밸런스설계/이론_9m.md "2차 보정(실측)"',
       'rows': rows, 'draft1': {k: d1['rows'].get(k, [0, 1, 2, 3, 4]) for k in rows if d1['rows'].get(k, [0, 1, 2, 3, 4]) != rows[k]},
       'income_steps': inc, 'boss_need': need, 'predict': pred, 'b_bar_inversion': binv,
       'assume': {'K_DPS': K_DPS, 'T_KILL': T_KILL, 'AB_DUR': AB_DUR, 'H_over_1e10': 'flat (측정 밖)', 'ps_tools': TOOLS}}
json.dump(out, open(os.path.join(HERE, 'efx_draft2.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
if __name__ == '__main__':
    for k, v in inc.items(): print(k, v)
    for s, v in need.items(): print('boss', s, {a: (f'{b:.3g}' if isinstance(b, float) else b) for a, b in v.items()})
    print('blade', bl)
    for r in pred: print(r)
    print(binv)
