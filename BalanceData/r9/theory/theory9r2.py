# -*- coding: utf-8 -*-
"""9-R2: 사용자 결정 2 — 반복 보스 끝없이 증가 + 무한(싱크) 노드 추가 + 세계수 단계 골드 보정. 결정적.
다시 돌리기: python -X utf8 BalanceData/r9/theory/theory9r2.py → theory9r2_out.txt, theory9r2_out.json
바탕: T9-0 봇 SKILL 1.0(k10) 판 수입·보스 D (theory9r.py와 같은 c 0.234), 표시 60~150.
가정(오차 큼, 8절):
  사람 판 수입 = 0.6 × 봇k10 일반 판 중앙(표시 n) × 골드 노드비 × 세계수 골드 보정. 101~ : 100의 값 × 1.01^(n-100), 105 폐허 ×10(바다 진입 ×12 실측과 같은 꼴)
  사람 보스 D = D60 × 공격력비 × (수확기수비)^0.7 × 스킬몫 × 속도^0.7 × (체력60/체력n),  스킬몫 = 1 + W·((1+스킬확률 싱크)(1+스킬피해 싱크) − 1), W 0.5
  판 수입은 공격력에 안 듣는다(EF 0.02, theory9r) → 싱크는 보스에만 듣는다
  허들 = 첫 도전부터 처치까지 (실패 판 + 파밍 2판) × 1.35분. 사람 사이 0.70 · 판마다 0.73 (ln D)
"""
import csv, json, math, os, random, statistics as S
from statistics import NormalDist
HERE = os.path.dirname(os.path.abspath(__file__)); TD = os.path.join(HERE, '..', 'test', 'T9-0')
N = NormalDist(); OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a))

# ---- T9-0 k10: 표시별 일반 판 net 중앙, 보스 D 중앙 ----
net = {}; botD = {}
for s in range(1, 7):
    for r in csv.DictReader(open(os.path.join(TD, f'rounds_k10_s{s}.csv'), encoding='utf-8')):
        n = int(r['stage']) - 2
        if r['bossFlag'] == '1':
            botD.setdefault(n, []).append(float(r['grossDps']) / float(r['bossMaxHp']) * (float(r['dur']) - 2))
        else:
            net.setdefault(n, []).append(float(r['net']))
NET = {n: S.median(v) for n, v in net.items()}
C = 0.234
D60 = C * S.median(botD[60])
def zmul(n): e = 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4; return 10 ** e * 1.01 ** (n - 1)
HP60 = 40 * zmul(60) * 210
def income_base(n):
    if n <= 100:
        m = n if n in NET else n - 1
        return NET[m]
    return NET[99] * 1.01 ** (n - 99) * (10 if n >= 105 else 1)
def lgbot(n):   # T9-0 k10 봇 풍년 레벨(시드 중앙 부근): 70 2 · 80 30 · 90 47 · 100 56, 이후 56(바탕 수입 외삽에 이미 포함)
    pts = [(70, 2), (80, 30), (90, 47), (100, 56)]
    if n <= 70: return 0 if n < 66 else 2
    if n >= 100: return 56
    for (a, x), (b, y) in zip(pts, pts[1:]):
        if a <= n <= b: return x + (y - x) * (n - a) / (b - a)
P(f'사람 D60 = c {C} × 봇 D60 {S.median(botD[60]):.2f} = {D60:.2f} (지금 LOOP 210 기준)')

# ---- 싱크 노드 ----
SINKS = {   # key: (시작가 DB, 가격비, 레벨당 효과, 상한 레벨 or None)
    'inf_atk':   (3.63e12, 1.06, 0.15, None),   # 공격력 ×(1+0.15L)
    'inf_gold':  (3.63e12, 1.06, 0.04, None),   # 정산 골드 ×(1+0.04L)
    'inf_sdmg':  (2.0e11, 1.06, 0.10, None),    # 스킬 피해 +10%p/L (st.skillDmg에 더함)
    'inf_srate': (2.0e11, 1.06, 0.02, 50),      # 스킬 발동률 ×(1+0.02L), 상한 50(×2)
    'inf_spd':   (2.0e11, 1.08, 0.01, 30),      # 수확기 속도 +1%p/L, 상한 30(+30%)
    'inf_ball':  (2.0e13, 4.0, 1.0, 4),         # 영구 수확기 +1/L, 상한 4 (8 → 12개)
}
W = 0.5
def power(L):
    atk = 1 + 0.15 * L['inf_atk']
    balls = ((8 + L['inf_ball']) / 8) ** 0.7
    sk = 1 + W * ((1 + 0.02 * L['inf_srate']) * (1 + 0.10 * L['inf_sdmg']) - 1)
    spd = (1 + 0.01 * L['inf_spd']) ** 0.7
    return atk * balls * sk * spd
OPEN = {'inf_atk': 64, 'inf_gold': 64, 'inf_sdmg': 55, 'inf_srate': 55, 'inf_spd': 55, 'inf_ball': 70}   # 열림 표시(가정)
CUR_N = [0]
def buy(L, gold, keys):
    keys = [k for k in keys if CUR_N[0] >= OPEN[k]]
    while True:
        best, bc = None, float('inf')
        for k in keys:
            p0, kk, _, cap = SINKS[k]
            if cap is not None and L[k] >= cap: continue
            c = math.ceil(p0 * kk ** L[k] * 0.9)
            if c < bc: best, bc = k, c
        if best is None or bc > gold: return gold
        gold -= bc; L[best] += 1

def run(o, players=2000, seed=11, horizon=150):
    rng = random.Random(seed)
    FT = {}
    keys = o['keys']; res = {n: [] for n in range(60, horizon + 1, 10)}; tot = []; grow = {n: [] for n in res}
    for pl in range(players):
        person = rng.gauss(0, 0.70); L = {k: 0 for k in SINKS}; gold = 0.0; t = 0.0
        for n in range(51, horizon + 1):
            CUR_N[0] = n
            def inc():
                return 0.6 * o['tree_gold'] * income_base(n) * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * lgbot(n))
            if n % 10 == 0 and n >= 60:
                hp = 40 * zmul(n) * o['mul'](n); rounds = 0; tries = 0
                pw60 = o['pw60']
                while True:
                    tries += 1; rounds += 1
                    lnD = math.log(D60 * o.get('dmul', 1.0)) + math.log(power(L) / pw60) + math.log(o['tree_atk']) - math.log(hp / HP60) + person + rng.gauss(0, 0.73)
                    if lnD >= 0 or tries > 80: break
                    for _ in range(2):
                        p_before = power(L); gold += inc(); gold = buy(L, gold, keys) ; rounds += 1
                        if pl < 200: grow[n].append(power(L) / p_before)
                gold += 2 * inc(); gold = buy(L, gold, keys) 
                res[n].append((rounds - 1) * 1.35); FT.setdefault(n, [0, 0]); FT[n][0] += tries - 1; FT[n][1] += tries; t += rounds * 1.35
            else:
                gold += inc(); gold = buy(L, gold, keys) ; t += 1.35
            if n == 100: tot.append(t)
    FR.clear() if isinstance(globals().get('FR'), dict) else None
    globals()['FR'] = {n: a / b for n, (a, b) in FT.items()}
    return ({n: (S.median(v), S.mean(v), sorted(v)[int(0.9 * len(v))]) for n, v in res.items()},
            {n: (math.exp(S.mean([math.log(x) for x in v])) if v else float('nan')) for n, v in grow.items()}, (S.median(tot) if tot else float("nan")))

# 사람 표시 60 도착 때 싱크 레벨 0 → pw60 = 1. dmul = 사람 D60 배율(c 모델 1, verify 대안 'lag 8 × 조준 1.65' = 사람이 2.5~8배 셈 → 4)
BASEO = dict(keys=['inf_atk', 'inf_gold'], tree_gold=1.0, tree_atk=1.0, hstart=64, pw60=1.0)
FULL = dict(BASEO, keys=list(SINKS))
# 반복 보스 식(권장): 지역 배율을 쓰지 않고 표시 스테이지 다항식 — 사람 힘이 덧셈 싱크라 지수가 아니라 다항으로 크기 때문
#   hp(n) = HP60 × R60 × (1 + B·(n − 60)/10)^Q    (HP60 = 지금 60 king 체력 1.51e6)
def mkhp(R, B, Q): return lambda n: HP60 * R * (1 + B * (n - 60) / 10) ** Q
def runh(o, hpf, dmul=1.0, **kw):
    return run(dict(o, mul=lambda n: hpf(n) / (40 * zmul(n)), dmul=dmul), **kw)
TGT = 12.0
def fit(o, dmul):
    best = None
    for R in (1.0, 1.5, 2.5, 4.0, 6.0) if dmul == 1 else (4.0, 6.0, 10.0, 16.0, 24.0):
        for B in (0.5, 1.0, 2.0):
            for Q in (0.75, 1.0, 1.5, 2.0):
                h, g, t = runh(o, mkhp(R, B, Q), dmul, players=240, horizon=150)
                err = sum((math.log(max(v[0], 1) / TGT)) ** 2 + 0.5 * (math.log(max(v[1], 1) / 15)) ** 2 for v in h.values())
                if best is None or err < best[0]: best = (err, R, B, Q)
    return best
P('\n## 1. 파밍 1판의 보스 D 증가율 (사람 가운데, 허들 중 파밍 판, LOOP 210)')
for name, o in (('무한 노드 2개(투지 +15%p·풍년 +4%p, ×1.06)', BASEO), ('무한 노드 6개(+스킬 피해·발동률·속도·수확기)', FULL)):
    h, g, t = run(dict(o, mul=lambda n: 210), players=300)
    P(f'  {name}: ' + ' · '.join(f'{n}: ×{v:.3f}' for n, v in g.items() if not math.isnan(v)))
TG = {60: 10, 70: 11, 80: 12, 90: 13, 100: 14, 110: 15, 120: 16, 130: 17, 140: 18, 150: 19}   # 허들 중앙 목표(분): 점점 길게
def tune_seq(o, dmul=1.0):
    hp = {}
    for n in sorted(TG):
        lo, hi = 1e5, 1e12
        for _ in range(26):
            mid = math.sqrt(lo * hi); hp[n] = mid
            f = lambda m, hp=hp: hp.get(m, hp[max(hp)])
            med = runh(o, f, dmul, players=400, horizon=n)[0][n][0]
            if med < TG[n]: lo = mid
            else: hi = mid
        hp[n] = float('%.3g' % math.sqrt(lo * hi))
    return hp
J = {}
HPC = tune_seq(FULL)
fC = lambda n: HPC.get(n, HPC[150])
P(f'\n## 2. 보스별 체력 맞춤 (c 모델, 싱크 6개, 허들 중앙 목표 {TG}분, 파밍 2판/실패)')
P('  체력: ' + ' · '.join(f'{n}: {v:.3g}' for n, v in HPC.items()))
P('  지금 체력 대비: ' + ' · '.join(f'{n}: ×{v/(40*zmul(n)*210):.2f}' for n, v in HPC.items()))
P('  인접 보스 비: ' + ' · '.join(f'{n}: ×{HPC[n]/HPC[n-10]:.2f}' for n in list(HPC)[1:]))
for dm, dlab in ((1.0, 'c 모델'), (4.0, 'verify 대안(사람 D ×4)'), (2.0, '가운데(×2)')):
    for tg, lab in ((1.0, '세계수 없음'), (1.18, '세계수 4단계 +18%'), (1.42, '세계수 8단계 +42%')):
        h, g, t = runh(dict(FULL, tree_gold=tg), fC, dm, players=1500)
        P(f'  [{dlab} · {lab}] 60~100 {t:.0f}분 | 허들 중앙/평균/90% ' + ' · '.join(f'{n}: {v[0]:.0f}/{v[1]:.0f}/{v[2]:.0f}' for n, v in h.items()) + ' | 판당 실패 ' + ' · '.join(f'{n}: {FR[n]:.0%}' for n in FR))
        J[f'{dlab}|{lab}'] = {'t60_100': t, 'hurdle': {str(n): v for n, v in h.items()}, 'fail': dict(FR)}
h, g, t = run(dict(FULL, mul=lambda n: 210), players=300)
P('  파밍 1판 D 증가(싱크 6개, 새 열림): ' + ' · '.join(f'{n}: ×{v:.3f}' for n, v in g.items() if not math.isnan(v)))
J['hp_c'] = HPC
h, g, t = run(dict(BASEO, mul=lambda n: 210), players=1500)
P(f'\n### 지금(LOOP 210 한 값, 싱크 2개, c 모델): 60~100 {t:.0f}분 · 허들 중앙/평균/90%: ' + ' · '.join(f'{n}: {v[0]:.0f}/{v[1]:.0f}/{v[2]:.0f}' for n, v in h.items()))
json.dump(J, open(os.path.join(HERE, 'theory9r2_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
open(os.path.join(HERE, 'theory9r2_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
print('\n'.join(OUT))
