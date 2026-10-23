# -*- coding: utf-8 -*-
"""9-R: 사용자 결정(시간 허들) 반영 모델. T9-0(게임 규칙 지역 이동) 기준. 결정적(난수 시드 고정).
다시 돌리기: python -X utf8 BalanceData/r9/theory/theory9r.py → theory9r_out.txt, theory9r_out.json
입력: ../test/T9-0/rounds_k10_s*.csv (SKILL 1.0 봇, 사람 근사의 바탕), rounds_k06_s*.csv(참고)
사람 모델:
  - 보스 D(판 안 피해/체력): 사람 D = c × 봇k10 D(같은 표시) × (사람 공격력/봇 공격력) × (봇이 맞은 체력/새 체력)
    c: 표시 50 mover 150(지금 DB)에서 사람 ln D = mu − ln(150/200) − ln 1.91 + ln R (8회차 boss8r2 식, mu 0 s 1.01, R 1.65) 와 봇k10 D50을 맞춤
    D 흔들림 s 1.01 = 사람 사이 0.70 + 판마다 0.73 (가정)
  - 판 수입: 사람 = 0.6 × 봇k10 판 수입 × (1+0.04Lg_h)/(1+0.04Lg_b) × (공격력비)^EF  (0.6 = D1 표시 48~52 사람/봇)
  - 사람은 표시 HSTART부터 무한 노드를 산다(트리 완료가 봇k10 72~75보다 늦음, 가정)
  - 보스 실패 → 그 스테이지에서 FARM판 파밍 뒤 다시 도전. 허들 시간 = (실패 판 + 파밍 판) × 1.35분
  - 세계수(봇은 안 씀): 판 골드 +5~7.5%(가호)·공격력 +5%(꼬마 각성)·제한시간 +2초 → 판 수입 ×1.12, 공격력 ×1.05
    + 보따리: 8시간 보따리 = 판 5.5개(4단계 기준)~19.6개(8단계 기준)어치(이론.md Z2), 플레이 1시간마다 한 번 받는다고 보면 판당 +12~45% 골드.
"""
import csv, json, math, os, random, statistics as S, sys
from statistics import NormalDist
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'r8', 'theory'))
from boss_fit import fit
N = NormalDist(); OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a))
TD = os.path.join(HERE, '..', 'test', 'T9-0')

def load(k, s):
    La = Lg = 0; rows = {}
    for r in csv.DictReader(open(os.path.join(TD, f'rounds_{k}_s{s}.csv'), encoding='utf-8')):
        n = int(r['stage']) - 2
        rec = dict(n=n, boss=r['bossFlag'] == '1', net=float(r['net']), atk=float(r['atk']), balls=int(r['balls']), La=La, Lg=Lg,
                   dps=float(r['grossDps'] or 0), hp=float(r['bossMaxHp'] or 0), zone=r['zone'], dur=float(r['dur']), pm=float(r['playMin']))
        for b in filter(None, r['bought'].split(';')):
            key, lv = b.split(':'); c = int(lv.split('>')[1])
            if key == 'inf_atk': La = c
            if key == 'inf_gold': Lg = c
        rows[n] = rec
    return rows
K10 = {s: load('k10', s) for s in range(1, 7)}
SEEDS = list(K10)

# ---- EF: 바다 판 수입의 공격력 탄력 (봇 k10 표시 76~100 일반 판) ----
xs, ys = [], []
for s in SEEDS:
    for n in range(76, 101):
        r = K10[s][n]
        if r['boss']: continue
        xs.append(math.log(r['atk'])); ys.append(math.log(r['net'] / (1 + 0.04 * r['Lg']) / 1.01 ** n))
mx, my = S.mean(xs), S.mean(ys)
EF = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / sum((x - mx) ** 2 for x in xs)
rm = math.sqrt(S.mean([(y - my - EF * (x - mx)) ** 2 for x, y in zip(xs, ys)]))
P(f'## 1. 바다 판 수입 ∝ 공격력^EF: EF = {EF:.2f} (봇 k10 76~100 일반 판 {len(xs)}개, 잔차 RMSE(ln) {rm:.2f}; 스테이지 1.01·골드 노드 몫 뺌)')
EF_FIELD = 0.3

# ---- 사람 보정 c ----
MU, SD = fit(0.50, 0.48)
def lnDh50(R): return MU - math.log(150 / 200) - math.log(1.91) + math.log(R)
def botD(s, n):
    r = K10[s][n]; return r['dps'] / r['hp'] * (r['dur'] - 2)
Db50 = S.median([botD(s, 50) for s in SEEDS])
C = math.exp(lnDh50(1.65)) / Db50
P(f'## 2. 사람/봇k10 보스 D 비 c = {C:.3f} (봇 D50 중앙 {Db50:.2f}, 사람 D50 {math.exp(lnDh50(1.65)):.2f}; R 1.4~1.9 → c {math.exp(lnDh50(1.4))/Db50:.3f}~{math.exp(lnDh50(1.9))/Db50:.3f})')
P('   지금 값 사람 판당 실패율(봇k10 실측 D × c): ' + ' · '.join(
    f'{n}: {N.cdf(-math.log(C * S.median([botD(s, n) for s in SEEDS])) / SD):.0%}' for n in (50, 60, 70, 80, 90, 100)))

SP, SN = 0.70, 0.73   # 사람 사이 · 판마다
ZE = lambda e, n: 10 ** e * 1.01 ** (n - 1)
def zone_e(n): return 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4
HP_NOW = {60: 210, 70: 210, 80: 210, 90: 210, 100: 210}

def run(o, tree=False, seed_rng=7, players=3000, report=True):
    """사람 진행: 표시 HSTART~100. 반환: 허들(보스별) 분 분포, 도달 분, 판 수입 등"""
    rng = random.Random(seed_rng)
    sa, k, p0 = o['sa'], o['k'], 3.63e12
    gm = (1.12 if tree else 1.0); am = (1.05 if tree else 1.0); bag = o.get('bag', 0.0) if tree else 0.0
    res = {n: [] for n in (60, 70, 80, 90, 100)}
    global TR; TR = {}
    tot = []
    for pl in range(players):
        s = SEEDS[pl % 6]; R = K10[s]
        person = rng.gauss(0, SP)
        La = Lg = 0; gold = 0.0; t = o.get('t50', 78.0)
        for n in range(51, 101):
            b = R[n]; A0 = b['atk'] / (1 + 0.07 * b['La'])
            def atk_h(): return A0 * (1 + sa * La) * am
            def inc_h():
                ef = EF if zone_e(n) >= 3 else EF_FIELD
                return 0.6 * gm * (1 + bag) * (R[n - 1]['net'] if b['boss'] else b['net']) * (1 + 0.04 * Lg) / (1 + 0.04 * b['Lg']) * (atk_h() / b['atk']) ** ef
            def buy():
                nonlocal gold, La, Lg
                if n < o['hstart']: return
                while True:
                    pa = math.ceil(p0 * k ** La * 0.9); pg = math.ceil(p0 * k ** Lg * 0.9)
                    if min(pa, pg) > gold: break
                    if pa <= pg: gold -= pa; La += 1
                    else: gold -= pg; Lg += 1
            if n % 10 == 0 and n >= 60:
                hp_ratio = o['mul'][n] / HP_NOW[n]
                tries = 0; rounds = 0
                while True:
                    tries += 1; rounds += 1
                    lnD = math.log(C) + math.log(botD(s, n)) + math.log(atk_h() / b['atk']) - math.log(hp_ratio * o.get('pity', 1.0) ** (tries - 1)) + person + rng.gauss(0, SN)
                    if lnD >= 0 or tries > 60: break
                    for _ in range(o['farm']):
                        gold += inc_h(); buy(); rounds += 1
                gold += 2 * inc_h(); buy()
                res[n].append((rounds - 1) * 1.35); TR.setdefault(n, []).append(tries); t += rounds * 1.35
            else:
                gold += inc_h(); buy(); t += 1.35
        tot.append(t)
    out = {n: (S.median(v), S.mean(v), sorted(v)[int(0.9 * len(v))]) for n, v in res.items()}
    first = {}
    for n in res:
        b = [K10[s][n] for s in SEEDS]
    return out, S.median(tot), sorted(tot)[int(0.9 * len(tot))]

def first_fail(o, n, La_h):
    """표시 n 첫 도전 사람 판당 실패율(사람 가운데, 사람 공격력 = 봇 A0 × (1+sa·La_h))"""
    v = []
    for s in SEEDS:
        b = K10[s][n]; A0 = b['atk'] / (1 + 0.07 * b['La'])
        lnD = math.log(C * botD(s, n)) + math.log(A0 * (1 + o['sa'] * La_h) / b['atk']) - math.log(o['mul'][n] / HP_NOW[n])
        v.append(N.cdf(-lnD / SD))
    return S.median(v)

BASE = dict(sa=0.07, k=1.076, mul=dict(HP_NOW), farm=2, hstart=76)
NEW = dict(BASE, sa=0.15, k=1.06)
OPTS = [('지금(LOOP 210, 투지 +7%p·×1.076)', BASE), ('무한 노드만 새 값(LOOP 210, +15%p·×1.06)', NEW)]
# 목표: 세계수 없이 허들 중앙 60 10 · 70 12 · 80 15 · 90 17 · 100 20분 → mul 찾기(보스마다, 위 순서대로)
TGT = {60: 10, 70: 12, 80: 15, 90: 17, 100: 20}
def tune(base=None):
    base = base or NEW
    mul = dict(HP_NOW)
    for n in (60, 70, 80, 90, 100):
        lo, hi = 20.0, 20000.0
        for _ in range(22):
            mid = math.sqrt(lo * hi); mul[n] = mid
            med = run(dict(base, mul=dict(mul)), players=900, report=False)[0][n][0]
            if med < TGT[n]: lo = mid
            else: hi = mid
        mul[n] = round(math.sqrt(lo * hi), -1)
    return mul
MUL0 = tune()
OPTS.append(('9-R a 보스별 mul만(+15%p·×1.06)', dict(NEW, mul=MUL0)))
PITY = dict(NEW, pity=0.85)
MUL = tune(PITY)
OPTS.append(('9-R 권장 b 보스별 mul + 실패마다 체력 ×0.85', dict(PITY, mul=MUL)))
TGT = {60: 6, 70: 7, 80: 9, 90: 10, 100: 12}
MULC = tune(PITY)
OPTS.append(('9-R c 3시간 맞춤(허들 6~12분) + ×0.85', dict(PITY, mul=MULC)))
P('  c(3시간 맞춤, 목표 6/7/9/10/12분): ' + ' · '.join(f'{n}: {v:.0f}' for n, v in MULC.items()))
P('  a(보스별 mul만): ' + ' · '.join(f'{n}: {v:.0f}' for n, v in MUL0.items()))
P(f'\n## 3. 목표 허들(세계수 없이, 사람 중앙) {TGT} 분에 맞는 보스별 체력 배율: ' + ' · '.join(f'{n}: {v:.0f}' for n, v in MUL.items()))
J = {'EF': EF, 'C': C, 'mul': MUL, 'mulC': MULC, 'runs': {}}
for name, o in OPTS:
    for tree, bag in ((False, 0.0), (True, 0.12), (True, 0.45)):
        oo = dict(o, bag=bag)
        h, tmed, t90 = run(oo, tree=tree)
        lab = '세계수 없음' if not tree else f'세계수(판 ×1.12·공격 ×1.05·보따리 +{int(bag*100)}%)'
        P(f'\n### {name} · {lab}: 표시 100 도달 중앙 {tmed:.0f}분 (상위 10% {t90:.0f}분)')
        P('  허들(실패+파밍 분) 중앙/평균/90%: ' + ' · '.join(f'{n}: {v[0]:.0f}/{v[1]:.0f}/{v[2]:.0f}' for n, v in h.items()))
        P('  평균 도전 · 판당 실패율(전체 도전 기준): ' + ' · '.join(f'{n}: {S.mean(v):.1f}번 {1-1/S.mean(v):.0%}' for n, v in TR.items()))
        J['runs'][f'{name}|{lab}'] = {'t100': tmed, 'hurdle': {str(n): v for n, v in h.items()}}
    P('  첫 도전 판당 실패율(사람 가운데, 무한 투지 레벨 = 80: 6 · 90: 18 · 100: 28 가정): ' + ' · '.join(
        f'{n}: {first_fail(o, n, {60: 0, 70: 0, 80: 6, 90: 18, 100: 28}[n]):.0%}' for n in (60, 70, 80, 90, 100)))
# 체력
P('\n## 4. 보스 체력 (40 × ZoneMul(최신 지역) × mul)')
for n in (60, 70, 80, 90, 100):
    P(f'  {n}: 지금 {40*ZE(zone_e(n), n)*210:.3g} → 9-R {40*ZE(zone_e(n), n)*MUL[n]:.3g} (×{MUL[n]/210:.2f})')
json.dump(J, open(os.path.join(HERE, 'theory9r_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
open(os.path.join(HERE, 'theory9r_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
print('\n'.join(OUT))
