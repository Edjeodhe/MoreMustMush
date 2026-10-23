# -*- coding: utf-8 -*-
"""9-R2 검증 (verify, manager#41). theory9r2.py를 쓰지 않고 다시 계산한다. 결정적.
다시 돌리기: python -X utf8 BalanceData/r9/verify/verify9r2.py → verify9r2_out.txt
핵심 차이: theory9r2는 사람 보스 D를 표시 60 한 점(D60)에 묶고 60 뒤 성장을 싱크로만 본다.
여기서는 9-R처럼 표시마다 봇 실측 D(트리·스킬 성장 포함)를 바탕으로 쓰고, 싱크 몫만 바꿔 끼운다(60~100).
"""
import csv, math, os, random, statistics as S
from statistics import NormalDist
ND = NormalDist(); HERE = os.path.dirname(os.path.abspath(__file__)); TD = os.path.join(HERE, '..', 'test', 'T9-0')
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a)); print(OUT[-1])
med = S.median

def load(s):
    rows = {}; La = Lg = 0
    with open(os.path.join(TD, f'rounds_k10_s{s}.csv'), encoding='utf-8') as f:
        for r in csv.DictReader(f):
            n = int(r['stage']) - 2
            for b in filter(None, r['bought'].split(';')):
                k, lv = b.split(':'); to = int(lv.split('>')[1])
                if k == 'inf_atk': La = to
                if k == 'inf_gold': Lg = to
            rows[n] = dict(boss=r['bossFlag'] == '1', net=float(r['net']), atk=float(r['atk']), La=La, Lg=Lg,
                           hp=float(r['bossMaxHp'] or 0), dps=float(r['grossDps'] or 0), dur=float(r['dur']))
    return rows
K = {s: load(s) for s in range(1, 7)}
def zmul(n):
    e = 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4
    return 10 ** e * 1.01 ** (n - 1)
LOOP = 780
A1 = [1.00, 1.77, 1.08, 4.43, 6.30]; A2 = [1.17, 1.63, 1.98, 1.86, 1.91]
def hp_new(n):
    lap = (n // 10 - 1) // 5; idx = (n // 10 - 1) % 5
    return 40 * zmul(n) * LOOP * (A1 if lap == 1 else A2)[idx]
def hp_now(n): return 40 * zmul(n) * 210

# ---------- ① 반복 보스 식 ----------
P('## ① 반복 보스 체력 (제안 식: 40 × ZoneMul × 780 × 배열)')
seq = list(range(60, 310, 10))
P('  ' + ' · '.join(f'{n}:{hp_new(n):.3g}' for n in seq))
P('  인접 비: ' + ' · '.join(f'{n}:×{hp_new(n)/hp_new(n-10):.2f}' for n in seq[1:]))
for a, b in ((100, 150), (150, 200), (200, 250), (250, 300)):
    P(f'  {a}→{b} 바퀴 증가 ×{hp_new(b)/hp_new(a):.2f}')

# ---------- ② 사람 D 바탕의 성장 (theory9r2가 빼는 것) ----------
P('\n## ② 봇 k10의 "싱크 뺀 보스 힘"(D × 체력 ÷ 투지몫) 60 대비 — theory9r2는 60 뒤 이 값을 1로 고정')
base = {}
for n in (60, 70, 80, 90, 100):
    v = [K[s][n]['dps'] / K[s][n]['hp'] * (K[s][n]['dur'] - 2) * K[s][n]['hp'] / (1 + 0.07 * K[s][n]['La']) for s in K]
    base[n] = med(v)
P('  ' + ' · '.join(f'{n}: ×{base[n]/base[60]:.2f}' for n in base))

# ---------- ③ 몬테카를로 (표시별 봇 바탕) ----------
SINK = {'inf_atk': (3.63e12, 1.06, None, 64), 'inf_gold': (3.63e12, 1.06, None, 64), 'inf_sdmg': (2e11, 1.06, None, 55),
        'inf_srate': (2e11, 1.06, 50, 55), 'inf_spd': (2e11, 1.08, 30, 55), 'inf_ball': (2e13, 4.0, 4, 70)}
def power(L, W=0.5, eb=0.7):
    return ((1 + 0.15 * L['inf_atk']) * ((8 + L['inf_ball']) / 8) ** eb *
            (1 + W * ((1 + 0.02 * L['inf_srate']) * (1 + 0.10 * L['inf_sdmg']) - 1)) * (1 + 0.01 * L['inf_spd']) ** eb)
def shop(L, gold, n):
    while True:
        best = None; bc = float('inf')
        for k, (p0, kk, cap, op) in SINK.items():
            if n < op or (cap is not None and L[k] >= cap): continue
            c = math.ceil(p0 * kk ** L[k] * 0.9)
            if c < bc: best, bc = k, c
        if best is None or bc > gold: return gold
        gold -= bc; L[best] += 1
def income(R, n, L, scale):
    m = n if not R[n]['boss'] else n - 1
    return scale * R[m]['net'] * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * R[m]['Lg'])
def sim(cc, players=2000, seed=5, farm=2, sp=0.70, sn=0.73, scale=0.6, rmin=1.35, W=0.5, eb=0.7, hpf=hp_new, bot=False, grow_log=None):
    rng = random.Random(seed); res = {n: [0, 0, []] for n in (60, 70, 80, 90, 100)}; T = []
    for p in range(players):
        s = 1 + p % 6; R = K[s]; z = 0 if bot else rng.gauss(0, sp)
        L = {k: 0 for k in SINK}; gold = 0.0; t = 0.0
        for n in range(51, 101):
            if n % 10 or n < 60:
                gold = shop(L, gold + income(R, n, L, scale), n); t += rmin; continue
            b = R[n]; d0 = cc * b['dps'] / b['hp'] * (b['dur'] - 2) / (1 + 0.07 * b['La']) * hp_now(n) / hpf(n)
            fails = 0; rounds = 0
            while True:
                rounds += 1
                if math.log(d0 * power(L, W, eb)) + z + rng.gauss(0, sn) >= 0 or fails > 200: break
                fails += 1
                for _ in range(farm):
                    p0 = power(L, W, eb); gold = shop(L, gold + income(R, n, L, scale), n); rounds += 1
                    if grow_log is not None: grow_log.setdefault(n, []).append(power(L, W, eb) / p0)
                if farm == 0:   # 봇: 재도전 판에도 수입
                    gold = shop(L, gold + income(R, n, L, scale), n)
            gold = shop(L, gold + 2 * income(R, n, L, scale), n)
            q = res[n]; q[0] += fails; q[1] += fails + 1; q[2].append((rounds - 1) * rmin); t += rounds * rmin
        T.append(t)
    return {n: (q[0] / q[1], med(q[2]), S.mean(q[2]), sorted(q[2])[int(.9 * len(q[2]))]) for n, q in res.items()}, med(T), sorted(T)[int(.9 * len(T))]
def show(lab, r, t0=78):
    res, tm, t9 = r
    P(f'  {lab}: 51~100 {tm:.0f}분 → 100 도달 {tm+t0:.0f}분 (90% {t9+t0:.0f}) | 판당 실패 ' + ' · '.join(f'{n}:{v[0]:.0%}' for n, v in res.items())
      + ' | 허들 중앙/평균/90% ' + ' · '.join(f'{n}:{v[1]:.0f}/{v[2]:.0f}/{v[3]:.0f}' for n, v in res.items()))
P('\n## ③ 사람 세 모델 (표시별 봇 실측 D 바탕 + 싱크 몫, 9-R2 체력, 파밍 2판)')
GL = {}
show('c 0.234', sim(0.234, grow_log=GL))
P('  파밍 1판 D 증가(기하평균): ' + ' · '.join(f'{n}: ×{math.exp(S.mean(math.log(x) for x in v)):.3f}' for n, v in sorted(GL.items())))
show('×2 (0.468)', sim(0.468)); show('×4 (0.936)', sim(0.936))
show('c, 스킬 가중 W 0.25', sim(0.234, W=0.25)); show('c, W 1.0', sim(0.234, W=1.0))
show('c, 수확기·속도 탄력 0.35', sim(0.234, eb=0.35))
show('c, 체력 지금(210)·싱크 6개', sim(0.234, hpf=hp_now))

P('\n## ④ 봇 (k10, c 1, 흔들림 0.28, 매 판 재도전, 판 1.16분, 표시 50 처치 ≈ 52.7분)')
for lab, sc in (('봇 수입 그대로', 1.0),):
    r = sim(1.0, players=600, farm=0, sp=0, sn=0.28, scale=sc, rmin=1.16, bot=True)
    show(lab, r, t0=52.7)
r = sim(1.0, players=600, farm=0, sp=0, sn=0.28, rmin=1.16, bot=True, W=0.25, eb=0.35); show('봇, 싱크 효과 약하게(W .25·탄력 .35)', r, t0=52.7)

# ---------- ⑤ 100 뒤 장기 추세 (사람 가운데, 결정적) ----------
P('\n## ⑤ 100 뒤 (사람 가운데 c 모델, 100의 바탕 고정, 수입 ×1.01/판·105 ×10, 파밍 없이 매 판 구매)')
for cc, lab in ((0.234, 'c'), (0.936, '×4')):
    R = K[1]; L = {k: 0 for k in SINK}; gold = 0.0
    for n in range(51, 101): gold = shop(L, gold + income(R, n, L, 0.6), n)
    b = R[100]; d100 = cc * b['dps'] / b['hp'] * (b['dur'] - 2) / (1 + 0.07 * b['La']) * hp_now(100)
    net100 = R[99]['net'] / (1 + 0.04 * R[99]['Lg'])
    row = []
    for n in range(101, 301):
        inc = 0.6 * net100 * 1.01 ** (n - 99) * (10 if n >= 105 else 1) * (1 + 0.04 * L['inf_gold'])
        gold = shop(L, gold + inc, n)
        if n % 10 == 0:
            row.append(f'{n}:D {d100*power(L)/hp_new(n):.2f}')
    P(f'  {lab}(시드1): ' + ' · '.join(row))
    P(f'    300 레벨: ' + ', '.join(f'{k} {v}' for k, v in L.items()))

# ---------- ⑥ 세계수 골드 보정 ----------
P('\n## ⑥ 세계수 tb.gold += 0.06(단계−1): goldMul = ... × (1 + SettleBonus + tb.gold) (`Game.cs:276`)')
SB = (1.9 - 1) + 1.0 + (1.6 - 1) + 0.1   # ed_price 3 · ed_price2 2 · ed_trade 2 최대 + 0.1, 도감 ab.price 제외
for ab in (0.0, 0.3, 0.6):
    for st in (4, 8):
        add = 0.06 * (st - 1); bless = 0.05 * (1.5 if st >= 8 else 1)
        P(f'  SettleBonus {SB+ab:.1f}(도감 +{ab}) · {st}단계: 판 골드 ×{(1+SB+ab+add+bless)/(1+SB+ab):.3f} (theory 가정 ×{1+add:.2f})')
open(os.path.join(HERE, 'verify9r2_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
