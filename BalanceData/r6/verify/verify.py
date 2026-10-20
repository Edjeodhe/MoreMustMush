# -*- coding: utf-8 -*-
"""verify 6회차 — 이론_6 독립 재계산.
1) 보스 체력: db.json(Hp_Mul·Const)과 코드 공식(Defs.BossHp = TIERS[3].hp(tuned 40) x BOSS_HP_K x ZoneMul(LatestTheme, 표시 s) x mul,
   ZoneMul = ZONE_STEP^지역 x STAGE_GROWTH^(s-1), LatestTheme = from <= 표시 s인 마지막 지역(Game.cs:66-67), 60~는 BOSS_LOOP_HP_MUL(Defs.Boss.cs:59))
2) 하한 시나리오를 따로 짠 코드로 다시 돌린다(이론 model.py 재사용 안 함, 입력은 같은 측정: T5-2 phase, verify_fit K).
3) 이론이 안 본 변형: (a) 판 시간을 사용자 보고(시간 노드 0, 19.5초) (b) 지연을 비례(x = 0.6 s) (c) 표시 10~20 사이 K 보간 대신 계단(K_king) (d) 재도전 사이 성장 HG 0.25/1.0
출력: verify_out.txt, verify_out.json"""
import json, math, os, glob, random, statistics as S
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
DB = json.load(open(os.path.join(ROOT, 'Assets', 'Resources', 'DB', 'db.json'), encoding='utf-8'))
C = {r['Const_Key']: r['Value'] for r in DB['Const']}
HPMUL = {b['Boss_Key']: b['Hp_Mul'] for b in DB['Boss']}
ORDER = [b['Boss_Key'] for b in DB['Boss']]
THEMES = [('forest', 1), ('night', 20), ('field', 45), ('sea', 75), ('ruins', 105)]   # Defs.World.cs THEMES from
TIER3_HP = 40.0   # Defs.cs:90 tuned hp[3]
L = []
def P(*a): L.append(' '.join(str(x) for x in a))

def latest_idx(s): return max(i for i, (_, f) in enumerate(THEMES) if s >= f)
def zone_mul(s): return C['ZONE_STEP'] ** latest_idx(s) * C['STAGE_GROWTH'] ** (s - 1)
def boss_id(s):
    k = s // int(C['BOSS_EVERY'])
    return 'loop' if k > len(ORDER) else ORDER[(k - 1) % len(ORDER)]
def boss_look(s):
    k = s // int(C['BOSS_EVERY']); return ORDER[(k - 1) % len(ORDER)]
def boss_hp(s, mul=None):
    b = boss_id(s)
    if mul is None: mul = C['BOSS_LOOP_HP_MUL'] if b == 'loop' else HPMUL[b]
    return TIER3_HP * C['BOSS_HP_K'] * zone_mul(s) * mul

PROP = dict(king=1.1, cleaner=0.6, angel=80, phantom=850, mover=220, loop=220)
P('== 1. 보스 체력 (코드 공식, db.json) ==')
P('Const:', {k: C[k] for k in ('BOSS_HP_K', 'ZONE_STEP', 'STAGE_GROWTH', 'BOSS_LOOP_HP_MUL', 'CLEANER_HEAL', 'CLEANER_INT', 'BOSS_EVERY')})
hp_tab = {}
for s in (10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110):
    b = boss_id(s); now = boss_hp(s); prop = boss_hp(s, PROP[b])
    forest = TIER3_HP * C['STAGE_GROWTH'] ** (s - 1) * (C['BOSS_LOOP_HP_MUL'] if b == 'loop' else HPMUL[b])
    hp_tab[s] = dict(boss=b, look=boss_look(s), theme=THEMES[latest_idx(s)][0], now=now, forest_quote=forest, prop=prop)
    heal = f" 회복 {now*C['CLEANER_HEAL']/C['CLEANER_INT']:.4g}/s -> 제안 {prop*0.03/3:.3g}/s" if boss_look(s) == 'cleaner' else ''
    P(f'  {s:3d} {b:8s}({boss_look(s)}) 지역 {THEMES[latest_idx(s)][0]:6s} x{zone_mul(s):.4g}: 지금 {now:.4g} (숲 기준 {forest:.4g}, x{now/forest:.0f}) / 제안 {prop:.4g}{heal}')
P(f'  확인: 표시 19 지역 {THEMES[latest_idx(19)][0]}, 표시 20 지역 {THEMES[latest_idx(20)][0]} -> 20 보스 판이 첫 밤 판. 10->20 체력 비 지금 x{boss_hp(20)/boss_hp(10):.0f}, 제안 x{boss_hp(20,0.6)/boss_hp(10,1.1):.1f}')

# ---- 봇 곡선 (T5-2) 다시 읽기 ----
by = {}
for p in sorted(glob.glob(os.path.join(ROOT, 'BalanceData', 'r5', 'test', 'T5-2', 'phase_k06_s*.json'))):
    ph = json.load(open(p)); prev = 0; seen = set()
    for st, atk, pl in zip(ph['stageR'], ph['atkR'], ph['playR']):
        T = pl - prev - 30; prev = pl; d = st - 4
        if d < 1 or d in seen: continue
        seen.add(d); by.setdefault(d, []).append((atk, T))
ATK = {d: S.median([a for a, _ in v]) for d, v in by.items()}
TB = {d: S.median([t for _, t in v]) for d, v in by.items()}
FIT = json.load(open(os.path.join(HERE, 'verify_fit.json')))
GM = dict(king=1.0, cleaner=1.0, angel=0.8, phantom=1.0, mover=0.6, loop=1.0)
GHP = dict(phantom=1.99)

def make_env(G=None, kstep=False):
    G = G or GM
    anch = {10 * (i + 1): FIT[b]['K'] / G[b] for i, b in enumerate(ORDER)}
    xs = sorted(anch)
    def K(x):
        if x <= xs[0]: return anch[xs[0]]
        if x >= xs[-1]: return anch[xs[-1]]
        if kstep and x < 20: return anch[10]
        for a, b in zip(xs, xs[1:]):
            if a <= x <= b:
                w = (x - a) / (b - a); return math.exp(math.log(anch[a]) * (1 - w) + math.log(anch[b]) * w)
    def atk(x):
        x = max(1.0, min(max(ATK), x)); a = int(x); b = min(max(ATK), a + 1); w = x - a
        return math.exp(math.log(ATK[a]) * (1 - w) + math.log(ATK[b]) * w)
    def dur(x):
        x = max(1, min(max(TB), int(round(x)))); return max(0.0, TB[x] - 10 - min(10, 0.5 * (x - 1)))
    return K, atk, dur, G

def run(s, mul, heal=(0.03, 3.0), lagf=lambda s: 8, hg=0.5, sigma=0.3, Tmode='bot', env=None, runs=4000, seed=1, gap=2, cap=30):
    K, atk, dur, G = env or make_env()
    b = boss_id(s); g = G[b] if b != 'loop' else 1.0
    hp = boss_hp(s, mul); x0 = s - lagf(s)
    h = heal[0] / heal[1] if b == 'cleaner' else 0
    rng = random.Random(seed); ns = []
    base_T = 10 + min(10, 0.5 * (s - 1))
    def T_of(x, cold):
        d = dur(x) if Tmode == 'bot' else Tmode
        return base_T + d - (3 if cold else 0)
    for _ in range(runs):
        n = 0; t = 0
        while True:
            n += 1; x = x0 + hg * t
            T = T_of(x, rng.random() < 1 / 3)
            D = K(x) * atk(x) * g
            need = hp * GHP.get(b, 1) * (1 / max(0.5, T - 2) + h)
            if math.log(D / need) + sigma * rng.gauss(0, 1) >= 0 or n >= cap: break
            t += gap
        ns.append(n)
    ns.sort()
    T0 = T_of(x0, False); D0 = K(x0) * atk(x0) * g
    left = max(0.0, 1 - max(0.0, D0 - h * hp) * (T0 - 2) / (hp * GHP.get(b, 1)))
    return dict(mean=round(sum(ns) / len(ns), 2), p1=round(sum(v <= 1 for v in ns) / len(ns), 2), p3=round(sum(v <= 3 for v in ns) / len(ns), 2),
                p90=ns[int(len(ns) * 0.9)], D0=round(D0, 3), T0=round(T0, 1), left0=round(left, 2), heal=round(h * hp, 3))

def solve(s, target, **kw):
    lo, hi = math.log(1e-3), math.log(1e5)
    for _ in range(36):
        m = (lo + hi) / 2
        if run(s, math.exp(m), runs=1500, seed=s, **kw)['mean'] < target: lo = m
        else: hi = m
    return math.exp((lo + hi) / 2)

R = {'hp': hp_tab}
P('\n== 2. 하한 L 재계산 (독립 코드) — 제안 Hp, 청소부 회복 3%/3초 ==')
P('  기준(이론과 같은 가정: lag 8, 판 시간 = 봇 시간 노드, HG 0.5, sigma 0.3)')
base = {}
for s in (10, 20, 30, 40, 50, 60):
    b = boss_id(s); r = run(s, PROP[b], seed=200 + s); base[s] = r
    P(f'    {s} {b}: DPS {r["D0"]} 판 {r["T0"]}s 첫 판 남은 {r["left0"]:.0%} 회복 {r["heal"]}/s / 평균 {r["mean"]} 1번 {r["p1"]:.0%} 3번 안 {r["p3"]:.0%} p90 {r["p90"]}')
R['base'] = base
P('  지금 Hp(회복 5%) 기준 L:')
for s in (10, 20, 30, 40, 50):
    b = boss_id(s); r = run(s, HPMUL[b], heal=(0.05, 3.0), seed=250 + s)
    P(f'    {s} {b}: 평균 {r["mean"]} 3번 안 {r["p3"]:.0%} 첫 판 남은 {r["left0"]:.0%}')

variants = {
    'Tuser(시간노드 0, 19.5s)': dict(Tmode=0),
    'Tuser+ps_dur1(+2s)': dict(Tmode=2),
    'lag 비례 x=0.6s': dict(lagf=lambda s: round(0.4 * s)),
    'lag 비례 + Tuser': dict(lagf=lambda s: round(0.4 * s), Tmode=0),
    'K 계단(10~20 보간 없음)': dict(env=make_env(kstep=True)),
    'K 계단 + Tuser': dict(env=make_env(kstep=True), Tmode=0),
    'HG 0.25 (수입 x0.5 상당)': dict(hg=0.25),
    'HG 1.0 (수입 x2 상당)': dict(hg=1.0),
    'HG 0.25 + Tuser': dict(hg=0.25, Tmode=0),
    '재도전 간격 4판': dict(gap=4),
    '천사 G 0.6 (닻도 다시)': dict(env=make_env(G=dict(GM, angel=0.6))),
    '버섯 G 0.4 (닻도 다시)': dict(env=make_env(G=dict(GM, mover=0.4))),
}
P('\n== 3. 변형 (제안 Hp에서 하한 L 평균 도전 / 3번 안 / 첫 판 남은 체력) ==')
for name, kw in variants.items():
    row = {}
    for s in (10, 20, 30, 40, 50):
        b = boss_id(s); r = run(s, PROP[b], seed=300 + s, **kw); row[s] = r
    R[name] = row
    P(f'  {name:24s} ' + ' | '.join(f'{s}: {r["mean"]}번 {r["p3"]:.0%} 남{r["left0"]:.0%}' for s, r in row.items()))

P('\n== 4. 청소부: 사용자 증상(18초 0%) — 첫 판, 3초 덩어리 회복 그대로 (RoundSim.Boss.cs:74-83) ==')
R['sym'] = []
for D in (3.0, 5.3, 9.2, 15.0):
    for (mul, heal) in ((20, 0.05), (0.6, 0.03), (0.55, 0.05), (0.4, 0.03)):
        hp = boss_hp(20, mul); hr = hp * heal / 3; res = []
        for T in (16.5, 19.5):
            cur = hp; t = 0.0; dt = 0.005; nextH = 3.0; kill = False
            while t < T - 1e-9:
                if t >= 2.0: cur -= D * dt
                t += dt
                if cur <= 0: kill = True; break
                if t >= nextH - 1e-9:
                    nextH += 3.0
                    if cur < hp: cur = min(hp, cur + hp * heal)
            res.append(0.0 if kill else cur / hp)
        R['sym'].append(dict(D=D, mul=mul, heal=heal, left16=res[0], left19=res[1]))
        P(f'  DPS {D:5.1f} / Hp_Mul {mul:5} 회복 {heal:.0%}: 회복/초 {hr:.3g} / 판 끝 남은 체력 16.5s {res[0]:.0%} / 19.5s {res[1]:.0%}')

P('\n== 5. 사용자 판 시간(시간 노드 0) 기준으로 L 평균 2.35번이 되는 Hp_Mul ==')
sol = {}
for s in (10, 20, 30, 40, 50):
    b = boss_id(s)
    a = solve(s, 2.35, Tmode=0)
    c = solve(s, 2.35, Tmode=0, env=make_env(kstep=True)) if s == 20 else None
    d = solve(s, 2.35, Tmode=0, heal=(0.05, 3.0)) if s == 20 else None
    sol[s] = dict(Tuser=a, Tuser_kstep=c, Tuser_heal5=d)
    P(f'  {s} {b}: Tuser {a:.4g}' + (f' / Tuser+K계단 {c:.4g} / Tuser 회복5% {d:.4g}' if c else '') + f' (이론 제안 {PROP[b]})')
R['solve_Tuser'] = sol

P('\n== 6. 봇(U, lag 0, HG 1, 봇 판 시간) — 제안 Hp ==')
for s in (10, 20, 30, 40, 50, 60):
    b = boss_id(s); r = run(s, PROP[b], lagf=lambda s: 0, hg=1.0, seed=500 + s)
    P(f'  {s} {b}: 평균 {r["mean"]} 1번 {r["p1"]:.0%} (첫 판 남은 {r["left0"]:.0%})')
P('\n== 7. 곡선: 제안 실효 체력 (앞 보스 대비) ==')
prev = None
for s in (10, 20, 30, 40, 50, 60):
    b = boss_id(s); hp = boss_hp(s, PROP[b]) * GHP.get(b, 1)
    P(f'  {s} {b}: 실효 체력 {hp:.4g}' + (f' (앞 대비 x{hp/prev:.3g})' if prev else '')); prev = hp
json.dump(R, open(os.path.join(HERE, 'verify_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
open(os.path.join(HERE, 'verify_out.txt'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')
print('\n'.join(L))

# ===== 8. 재도전 사이 성장 보정: 봇(U) 측정 도전 수(T5-2)를 맞추는 재도전당 성장 dx =====
L2 = []
MEAS = {10: 1.83, 20: 1.83, 30: 3.0, 40: 5.83, 50: 3.17}   # T5-2 시드 6개 평균 (attempts.json)
L2.append('\n== 8. 재도전당 성장 dx 보정 (U: 지금 Hp, 봇 판 시간, sigma = 보스별 적합값) ==')
cal = {}
for s in (10, 20, 30, 40, 50):
    b = boss_id(s); row = {}
    for dx in (2.0, 1.0, 0.5, 0.25, 0.1):
        r = run(s, HPMUL[b], heal=(0.05, 3.0), lagf=lambda s: 0, hg=dx, gap=1, sigma=FIT[b]['sigma'], seed=600 + s)
        row[dx] = r['mean']
    cal[s] = row
    L2.append(f'  {s} {b}: 측정 {MEAS[s]} / 모델 ' + ' '.join(f'dx{dx}:{m}' for dx, m in row.items()))
L2.append('  L(제안 Hp, lag 8, 봇 판 시간)에서 재도전당 성장 dx를 줄이면:')
for dx in (1.0, 0.5, 0.25, 0.1):
    row = {}
    for s in (10, 20, 30, 40, 50):
        b = boss_id(s); row[s] = run(s, PROP[b], hg=dx, gap=1, seed=700 + s)
    L2.append(f'    dx {dx}: ' + ' | '.join(f'{s}: {r["mean"]}번 {r["p3"]:.0%}' for s, r in row.items()))
    R[f'L_dx{dx}'] = row
open(os.path.join(HERE, 'verify_out.txt'), 'a', encoding='utf-8').write('\n'.join(L2) + '\n')
print('\n'.join(L2))

# ===== 9. 보정 성장(dx 0.25·0.1)에서 L 평균 2.35번이 되는 Hp_Mul =====
L3 = ['\n== 9. 보정 성장에서 L 평균 2.35번 Hp_Mul (lag 8, 청소부 회복 3%) ==']
sol9 = {}
for s in (10, 20, 30, 40, 50):
    b = boss_id(s); row = {}
    for dx in (0.25, 0.1):
        row[dx] = solve(s, 2.35, hg=dx, gap=1)
    if s == 20:
        row['0.25+Tuser'] = solve(s, 2.35, hg=0.25, gap=1, Tmode=0)
    sol9[s] = row
    L3.append(f'  {s} {b}: ' + ' / '.join(f'{k}: {v:.4g}' for k, v in row.items()) + f' (이론 {PROP[b]})')
R['solve_dx'] = sol9
# 권고안 확인
REC = dict(king=0.9, cleaner=0.4, angel=50, phantom=450, mover=150)
L3.append('  권고안 ' + str(REC) + ' 확인 (L 평균 / 3번 안 / 첫 판 남은):')
for name, kw in [('이론 가정 dx1', dict()), ('dx 0.25', dict(hg=0.25, gap=1)), ('dx 0.1', dict(hg=0.1, gap=1)), ('dx 0.25 + Tuser', dict(hg=0.25, gap=1, Tmode=0)), ('lag 6 dx1', dict(lagf=lambda s: 6)), ('lag 10 dx 0.25', dict(lagf=lambda s: 10, hg=0.25, gap=1))]:
    row = {s: run(s, REC[boss_id(s)], seed=800 + s, **kw) for s in (10, 20, 30, 40, 50)}
    R['rec_' + name] = row
    L3.append(f'    {name:16s} ' + ' | '.join(f'{s}: {r["mean"]}번 {r["p3"]:.0%} 남{r["left0"]:.0%}' for s, r in row.items()))
row = {s: run(s, REC[boss_id(s)], lagf=lambda s: 0, hg=0.25, gap=1, sigma=FIT[boss_id(s)]['sigma'], seed=900 + s) for s in (10, 20, 30, 40, 50)}
L3.append('    봇 U(dx 0.25)    ' + ' | '.join(f'{s}: {r["mean"]}번' for s, r in row.items()))
row = {s: run(s, PROP[boss_id(s)], lagf=lambda s: 0, hg=0.25, gap=1, sigma=FIT[boss_id(s)]['sigma'], seed=900 + s) for s in (10, 20, 30, 40, 50)}
L3.append('    (이론안) 봇 U(dx 0.25) ' + ' | '.join(f'{s}: {r["mean"]}번' for s, r in row.items()))
open(os.path.join(HERE, 'verify_out.txt'), 'a', encoding='utf-8').write('\n'.join(L3) + '\n')
json.dump(R, open(os.path.join(HERE, 'verify_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
print('\n'.join(L3))

# ===== 10. 최종 권고안 확인 =====
L4 = ['\n== 10. verify 권고안 확인 (L 평균 / 3번 안 / 1번 / 첫 판 남은) ==']
REC2 = dict(king=1.0, cleaner=0.4, angel=65, phantom=720, mover=200)
L4.append('  ' + str(REC2) + ', CLEANER_HEAL 0.03')
for name, kw in [('이론 가정(dx1, 봇 T)', dict()), ('dx 0.25', dict(hg=0.25, gap=1)), ('dx 0.1', dict(hg=0.1, gap=1)), ('dx 0.25 + Tuser', dict(hg=0.25, gap=1, Tmode=0)),
                 ('lag 6 dx 0.25', dict(lagf=lambda s: 6, hg=0.25, gap=1)), ('lag 10 dx 0.25', dict(lagf=lambda s: 10, hg=0.25, gap=1)), ('K 계단 dx 0.25', dict(env=make_env(kstep=True), hg=0.25, gap=1))]:
    row = {s: run(s, REC2[boss_id(s)], seed=1000 + s, **kw) for s in (10, 20, 30, 40, 50)}
    R['rec2_' + name] = row
    L4.append(f'    {name:20s} ' + ' | '.join(f'{s}: {r["mean"]}번 {r["p3"]:.0%} 1번{r["p1"]:.0%} 남{r["left0"]:.0%}' for s, r in row.items()))
row = {s: run(s, REC2[boss_id(s)], lagf=lambda s: 0, hg=0.25, gap=1, sigma=FIT[boss_id(s)]['sigma'], seed=1100 + s) for s in (10, 20, 30, 40, 50)}
L4.append('    봇 U(dx 0.25)          ' + ' | '.join(f'{s}: {r["mean"]}번' for s, r in row.items()))
row = {s: run(s, PROP[boss_id(s)], seed=1200 + s, hg=0.25, gap=1) for s in (10, 20, 30, 40, 50)}
L4.append('    (이론안) L dx 0.25     ' + ' | '.join(f'{s}: {r["mean"]}번 {r["p3"]:.0%} 1번{r["p1"]:.0%}' for s, r in row.items()))
row = {s: run(s, PROP[boss_id(s)], seed=1300 + s, hg=0.25, gap=1, Tmode=0) for s in (10, 20)}
L4.append('    (이론안) L dx 0.25+Tuser ' + ' | '.join(f'{s}: {r["mean"]}번 {r["p3"]:.0%} 1번{r["p1"]:.0%}' for s, r in row.items()))
open(os.path.join(HERE, 'verify_out.txt'), 'a', encoding='utf-8').write('\n'.join(L4) + '\n')
json.dump(R, open(os.path.join(HERE, 'verify_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
print('\n'.join(L4))
