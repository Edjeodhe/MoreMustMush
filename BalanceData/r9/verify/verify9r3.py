# -*- coding: utf-8 -*-
"""9-R3 검증 (verify, manager#43·#44). theory9r3.py를 쓰지 않고 다시 계산한다. 결정적.
다시 돌리기: python -X utf8 BalanceData/r9/verify/verify9r3.py → verify9r3_out.txt
theory와 다른 점(독립 구현):
  - 시드별 원 기록(rounds_k10_s*.csv)을 그대로 쓴다(theory는 시드 중앙값). 사람 p는 시드 1~6을 돌아가며 쓴다.
  - 싱크 열림은 시드별 DB 조건(fest 최대 → 위력·기세, ps_ball3 최대 → 질주·동료)에서 읽는다(theory는 고정 58·60·72).
  - 변형: (P) 환상 버섯 분신 몫(기록 maxHp ÷ 본체 체력), (H) 청소부 회복(1%/초 × 싸움 시간), (S) 속도·스킬 피해를 코드대로 '합에 더하기'
"""
import csv, math, os, random, statistics as S
HERE = os.path.dirname(os.path.abspath(__file__)); TD = os.path.join(HERE, '..', 'test', 'T9-0')
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a)); print(OUT[-1])

def zmul(n):
    e = 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4
    return 10 ** e * 1.01 ** (n - 1)
BASE = 281; LOOP_BOSS = [1.00, 3.28, 4.66, 22.4, 22.96]; LOOP_LATER = 2.14
def boss_mul(n):   # 제안 Defs.Boss.cs:59 식 (n = 표시 스테이지, 10의 배수)
    k = n // 10 - 1; lap = k // 5
    if lap == 0: return None
    return BASE * (LOOP_BOSS[k % 5] if lap == 1 else LOOP_LATER)
def boss_hp(n): return 40 * zmul(n) * boss_mul(n)
def hp_now(n): return 40 * zmul(n) * 210

# ---------- 1. 반복 보스 체력 60~300 (단조 증가?) ----------
P('## 1. 제안 식 체력 60~300 (40 × ZoneMul × 281 × LOOP_BOSS/LOOP_LATER)')
seq = list(range(60, 310, 10)); prev = None; bad = []
row = []
for n in seq:
    h = boss_hp(n); r = h / prev if prev else float('nan'); prev = h
    row.append(f'{n}:{h:.3g}(×{r:.2f})' if n > 60 else f'{n}:{h:.3g}')
    if n > 60 and r <= 1: bad.append(n)
P('  ' + ' · '.join(row)); P(f'  감소 지점: {bad or "없음"} · mul 확인: ' + ' · '.join(f'{n}:{boss_mul(n):.0f}' for n in (60, 70, 80, 90, 100, 110)))

# ---------- 원 기록 ----------
def load(s):
    R = {}; La = Lg = 0; op = {}
    for r in csv.DictReader(open(os.path.join(TD, f'rounds_k10_s{s}.csv'), encoding='utf-8')):
        n = int(r['stage']) - 2
        for b in filter(None, r['bought'].split(';')):
            k, lv = b.split(':'); to = int(lv.split('>')[1])
            if k == 'inf_atk': La = to
            if k == 'inf_gold': Lg = to
            if k in ('fest', 'ps_ball3') and to == 4: op.setdefault(k, n)
        R[n] = dict(boss=r['bossFlag'] == '1', net=float(r['net']), La=La, Lg=Lg, mhp=float(r['bossMaxHp'] or 0),
                    dps=float(r['grossDps'] or 0), dur=float(r['dur']), fight=float(r['fightT'] or 0))
    return R, op
K = {}; OPEN = {}
for s in range(1, 7): K[s], OPEN[s] = load(s)
P('\n## 2. 시드별 싱크 열림(표시): ' + ' · '.join(f's{s} fest {OPEN[s]["fest"]} / ps_ball3 {OPEN[s]["ps_ball3"]}' for s in K))
P('   theory 가정: 위력·기세 58, 질주 60, 동료 72 → 실제(k10 봇) 위력·기세 72~75, 질주·동료 67~70')
split = {n: S.median(K[s][n]['mhp'] / hp_now(n) for s in K) for n in (60, 70, 80, 90, 100)}
P('   기록 maxHp ÷ 본체 체력(210 기준): ' + ' · '.join(f'{n}: ×{v:.2f}' for n, v in split.items()) + '  ← 90 환상 버섯 분신 몫')

# ---------- 3. 몬테카를로 ----------
SINK = {'inf_atk': (3.63e12, 1.06, None), 'inf_gold': (3.63e12, 1.06, None), 'inf_sdmg': (5e11, 1.08, None),
        'inf_srate': (5e11, 1.15, 50), 'inf_spd': (5e11, 1.12, 50), 'inf_ball': (3e13, 3.0, 6)}
def opened(k, n, s, fixed):
    if fixed: return n >= {'inf_atk': 64, 'inf_gold': 64, 'inf_sdmg': 58, 'inf_srate': 58, 'inf_spd': 60, 'inf_ball': 72}[k]
    if k in ('inf_atk', 'inf_gold'): return n >= 64
    return n >= (OPEN[s]['fest'] if k in ('inf_sdmg', 'inf_srate') else OPEN[s]['ps_ball3'])
def power(L, add=False, W=0.5):
    sp = (1.36 + 0.01 * L['inf_spd']) / 1.36 if add else 1 + 0.01 * L['inf_spd']
    sd = (1.25 + 0.10 * L['inf_sdmg']) / 1.25 if add else 1 + 0.10 * L['inf_sdmg']
    return (1 + 0.15 * L['inf_atk']) * ((8 + L['inf_ball']) / 8) ** 0.7 * (1 + W * ((1 + 0.02 * L['inf_srate']) * sd - 1)) * sp ** 0.7
def shop(L, gold, n, s, fixed, cnt=None):
    while True:
        best = None; bc = float('inf')
        for k, (p0, kk, cap) in SINK.items():
            if not opened(k, n, s, fixed) or (cap is not None and L[k] >= cap): continue
            c = math.ceil(p0 * kk ** L[k] * 0.9)
            if c < bc: best, bc = k, c
        if best is None or bc > gold: return gold
        gold -= bc; L[best] += 1
        if cnt is not None: cnt[0] += 1
def income(R, n, L, sc):
    m = n if (n in R and not R[n]['boss']) else n - 1
    return sc * R[m]['net'] * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * R[m]['Lg'])
def base_d(R, n, cc, hpf, fix_split, heal, hum):
    b = R[n]; dmg = b['dps'] * (b['dur'] - 2) / (1 + 0.07 * b['La'])
    need = hpf(n) * (b['mhp'] / hp_now(n) if fix_split else 1)
    if heal and n == 70: need *= 1 + 0.01 * (b['dur'] - 2) * (0.7 if hum else 0.1)   # 청소부 3%/3초, 사람은 끝까지 감
    return cc * dmg / need
def sim(cc=0.234, players=1500, seed=11, farm=2, sp=0.70, sn=0.73, sc=0.6, rmin=1.35, hpf=boss_hp, fixed=False,
        fix_split=False, heal=False, add=False, tg=1.0, bot=False, horizon=100, W=0.5, glog=None, burst=None):
    rng = random.Random(seed); res = {n: [0, 0, []] for n in range(60, horizon + 1, 10)}; T = []
    for p in range(players):
        s = 1 + p % 6; R = K[s]; z = 0 if bot else rng.gauss(0, sp)
        L = {k: 0 for k in SINK}; gold = 0.0; t = 0.0
        for n in range(51, horizon + 1):
            if n % 10 or n < 60:
                cnt = [0] if (burst is not None and n in (74, 75, 76, 77)) else None
                gold = shop(L, gold + tg * income(R, n, L, sc), n, s, fixed, cnt); t += rmin
                if cnt is not None: burst.setdefault(n, []).append(cnt[0])
                continue
            d0 = base_d(R, n, cc, hpf, fix_split, heal, not bot); fails = rounds = 0
            while True:
                rounds += 1
                if math.log(d0 * power(L, add, W)) + z + rng.gauss(0, sn) >= 0 or fails > 300: break
                fails += 1
                for _ in range(farm):
                    p0 = power(L, add, W); gold = shop(L, gold + tg * income(R, n, L, sc), n, s, fixed); rounds += 1
                    if glog is not None: glog.setdefault(n, []).append(power(L, add, W) / p0)
                if farm == 0: gold = shop(L, gold + tg * income(R, n, L, sc) * (1 if bot else 0.8), n, s, fixed)
            gold = shop(L, gold + 2 * tg * income(R, n, L, sc), n, s, fixed)
            q = res[n]; q[0] += fails; q[1] += fails + 1; q[2].append((rounds - 1) * rmin); t += rounds * rmin
        T.append(t)
    out = {n: dict(fail=q[0] / q[1], med=S.median(q[2]), mean=S.mean(q[2]), p90=sorted(q[2])[int(.9 * len(q[2]))],
                   p99=sorted(q[2])[int(.99 * len(q[2]))], stuck=sum(x > 60 for x in q[2]) / len(q[2])) for n, q in res.items()}
    return out, S.median(T), sorted(T)[int(.9 * len(T))]
def show(lab, r, t0=78):
    o, tm, t9 = r
    P(f'  {lab}: 100 도달 {tm + t0:.0f}분(90% {t9 + t0:.0f}) | 판당 실패 ' + ' · '.join(f'{n}:{v["fail"]:.0%}' for n, v in o.items())
      + ' | 허들 중앙/평균/90%/99% ' + ' · '.join(f'{n}:{v["med"]:.0f}/{v["mean"]:.0f}/{v["p90"]:.0f}/{v["p99"]:.0f}' for n, v in o.items())
      + ' | 60분 넘게 막힘 ' + ' · '.join(f'{n}:{v["stuck"]:.1%}' for n, v in o.items()))

P('\n## 3. 사람 c 0.234 몬테카를로 (파밍 2판, 판 1.35분, 50 처치 78분)')
GL = {}; BU = {}
show('A 재현(theory 가정: 열림 고정·분신 없음·곱셈)', sim(fixed=True, glog=GL))
P('    파밍 1판 힘 증가(기하평균): ' + ' · '.join(f'{n}: ×{math.exp(S.mean(math.log(x) for x in v)):.3f}' for n, v in sorted(GL.items())))
show('B 열림 DB대로', sim(burst=BU))
P('    표시 74~77 일반 판 구매 수 중앙/최대: ' + ' · '.join(f'{n}: {S.median(v):.0f}/{max(v)}' for n, v in sorted(BU.items())))
show('C B + 환상 분신 몫', sim(fix_split=True))
show('D C + 청소부 회복', sim(fix_split=True, heal=True))
show('E D + 속도·피해 코드대로 더하기', sim(fix_split=True, heal=True, add=True))

P('\n## 4. 파밍 판 수 민감도 (B 기준 / E 기준)')
for f in (0, 1, 2, 4):
    show(f'B 파밍 {f}판', sim(farm=f, players=1000))
    show(f'E 파밍 {f}판', sim(farm=f, players=1000, fix_split=True, heal=True, add=True))

P('\n## 5. 수입 민감도 (B)')
for sc, lab in ((0.3, '×0.5'), (1.2, '×2')): show(f'수입 {lab}', sim(sc=sc, players=1000))
P('\n## 6. 세계수 골드 곱 (B / E)')
for tg, lab in ((1.18, '4단계 ×1.18'), (1.42, '8단계 ×1.42'), (1.7, '8단계 ×1.70(단계당 +10%)'), (2.05, '8단계 ×2.05(단계당 +15%)')):
    show(f'B {lab}', sim(tg=tg, players=1000)); show(f'E {lab}', sim(tg=tg, players=1000, fix_split=True, heal=True, add=True))

P('\n## 7. 봇 (k10, c 1, 사람차 0, 판 흔들림 0.28, 매 판 재도전, 판 1.16분, 50 처치 52.7분)')
show('봇 B', sim(cc=1.0, players=300, farm=0, sp=0, sn=0.28, sc=1.0, rmin=1.16, bot=True), t0=52.7)
show('봇 E', sim(cc=1.0, players=300, farm=0, sp=0, sn=0.28, sc=1.0, rmin=1.16, bot=True, fix_split=True, heal=True, add=True), t0=52.7)

P('\n## 8. 100 뒤 사람 가운데 D (mul 600 고정, 바탕은 100 mover 값, 수입 ×1.01/판·105부터 ×10, 매 판 구매)')
for s in (1, 4):
    R = K[s]; L = {k: 0 for k in SINK}; gold = 0.0
    for n in range(51, 101): gold = shop(L, gold + income(R, n, L, 0.6), n, s, False)
    b = R[100]; dmg = 0.234 * b['dps'] * (b['dur'] - 2) / (1 + 0.07 * b['La']); net = R[99]['net'] / (1 + 0.04 * R[99]['Lg'])
    d100 = dmg * power(L) / boss_hp(100); row = [f'100:{d100:.2f}']
    for n in range(101, 301):
        gold = shop(L, gold + 0.6 * net * 1.01 ** (n - 99) * (10 if n >= 105 else 1) * (1 + 0.04 * L['inf_gold']), n, s, False)
        if n % 10 == 0 and n in (110, 120, 150, 200, 250, 280, 300): row.append(f'{n}:{dmg * power(L) / boss_hp(n):.2f}')
    P(f'  시드{s}: ' + ' · '.join(row) + f' (300 투지 {L["inf_atk"]})')
open(os.path.join(HERE, 'verify9r3_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
