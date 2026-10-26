# -*- coding: utf-8 -*-
"""9회차 theory 모델 (표시 50~100). 결정적. 다시 돌리기: python -X utf8 BalanceData/r9/theory/theory9.py → theory9_out.txt
입력: t84_state.json(t84_extract.py), ../../balance_phase_s1~3.json(옛 지역 전환), ../../r8/theory/boss_fit.py(사람 D 분포)
식 (게임 코드):
  ZoneMul(z, n) = ZONE_STEP^e(z) * 1.01^(n-1)   (Defs.World.cs:62, e = 지역 순번 0..4)
  일반 버섯 체력 = TIERS[0].hp(tuned 1) * ZoneMul,  보스 체력 = 40 * ZoneMul(최신 지역) * mul (Defs.Boss.cs:57, mul = Hp_Mul 또는 BOSS_LOOP_HP_MUL)
  타격 점수 = (등급 점수 + scoreFlat) * scoreMul(ZoneMul 포함) — 타격마다 (RoundSim.Balls.cs:322)
근사 (가정, 오차는 out에):
  kappa = 공격력 / 일반 버섯 체력.  판 수입 = B(n) * Zrel * h(kappa) * (1 + sg*Lg)
  h(k) = k^EF (k>=KC) ;  KC^EF * (k/KC)^GAM (k<KC)   — k<KC면 '피해 한정'(버섯이 안 죽어 연쇄·피버가 끊김)
"""
import json, math, os, statistics as S, sys
from statistics import NormalDist
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'r8', 'theory'))
from boss_fit import fit
N = NormalDist()
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a))

ZONES = ['forest', 'night', 'field', 'sea', 'ruins']; FROM = [1, 20, 45, 75, 105]
def zone_at(n): return max(i for i, f in enumerate(FROM) if n >= f)
def zmul(e, n): return 10 ** e * 1.01 ** (n - 1)

# ---------- 1. 옛 기록: 지역 전환 수입비 → GAM ----------
P('## 1. 옛 기록(balance_phase_s1~3, 10-15 빌드) 지역 전환: 스테이지 50 고정·공격력 105 고정, 일반 판(bossR=2)만')
rat = {'sea': [], 'ruins': []}
for s in (1, 2, 3):
    d = json.load(open(os.path.join(HERE, '..', '..', f'balance_phase_s{s}.json'), encoding='utf-8'))
    z, inc, b = d['zone'], d['incR'], d['bossR']
    for i in range(1, len(z)):
        if z[i] != z[i - 1] and z[i] in rat:
            pre = [inc[j] for j in range(i - 8, i) if b[j] == 2]; post = [inc[j] for j in range(i, min(len(z), i + 8)) if b[j] == 2]
            r = S.median(post) / S.median(pre); rat[z[i]].append(r)
            k0 = 105 / (10 ** ZONES.index(z[i - 1]) * 1.01 ** 49); k1 = 105 / (10 ** ZONES.index(z[i]) * 1.01 ** 49)
            P(f'  시드 {s} {z[i-1]}→{z[i]} 판 {i}: 전 {S.median(pre):.3g} 후 {S.median(post):.3g} ×{r:.2f}  (kappa {k0:.3g}→{k1:.3g})')
for k, v in rat.items():
    P(f'  {k}: 중앙 ×{S.median(v):.2f} → GAM = 1 − log10(비) = {1 - math.log10(S.median(v)):.2f}')
GAM = 0.93; KC = 1.0
P(f'  채택 GAM {GAM} (0.89~1.0), KC {KC} (0.64~1.5: 옛 들판 kappa 0.64가 이미 피해 한정 쪽이어야 비 ×1.0이 나옴)')

# ---------- 2. T8-4 상태와 B(n) ----------
st = json.load(open(os.path.join(HERE, 't84_state.json'), encoding='utf-8'))
st = {int(k): v for k, v in st.items()}
def h(k, ef): return k ** ef if k >= KC else KC ** ef * (k / KC) ** GAM
SER = {s: {r['disp']: r for r in st[s]} for s in st}

def fitB(s, ef):
    xs, ys = [], []
    for n, r in SER[s].items():
        if 71 <= n <= 100 and not r['boss']:
            k = r['atk'] / zmul(2, n)
            xs.append(n); ys.append(math.log(r['net'] / ((1 + 0.04 * r['infG0']) * h(k, ef))))
    mx, my = S.mean(xs), S.mean(ys)
    b = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / sum((x - mx) ** 2 for x in xs)
    rm = math.sqrt(S.mean([(y - my - b * (x - mx)) ** 2 for x, y in zip(xs, ys)]))
    return mx, my, b, rm

P('\n## 2. T8-4 표시 71~100(들판, PickZone) 잔차 B(n) = net / ((1+0.04 Lg) h(kappa)) 의 판당 기울기 — EF 맞춤')
for ef in (0.0, 0.2, 0.3, 0.45, 0.6, 0.8):
    fs = [fitB(s, ef) for s in st]
    sl = [f[2] for f in fs]
    P(f'  EF {ef}: B 판당 ×{math.exp(S.mean(sl)):.4f} (시드 ×{math.exp(min(sl)):.4f}~×{math.exp(max(sl)):.4f}), 잔차 RMSE(ln) {S.mean([f[3] for f in fs]):.3f}')
EF = 0.3
P(f'  채택 EF {EF} (범위 0~0.6). 판 하나 잡음 RMSE(ln) ≈ 0.3 (날씨·폭우 군락 +4)')
BF = {}
for s in st:
    mx, my, b, rm = fitB(s, EF)
    BF[s] = (lambda mx, my, b: (lambda n: math.exp(my + b * (n - mx))))(mx, my, b)
A0 = {s: S.median([r['atk'] / (1 + 0.07 * r['infA0']) for n, r in SER[s].items() if 71 <= n <= 100]) for s in st}
P('  무한 노드 뺀 공격력 A0(71~100, 시드별): ' + ' '.join(f'{A0[s]:.0f}' for s in st))

# ---------- 3. 사람 보스 D ----------
MU, SD = fit(0.50, 0.48)          # 지표 B mover(표시 50): 실패 50%, 남은 48%
M50 = 1.91
HP50 = 40 * zmul(2, 50) * 190
def lnD50(R): return MU - math.log(190 / 200) - math.log(M50) + math.log(R)
P(f'\n## 3. 사람 보스 D(판 안 피해/체력) 분포: mover 맞춤 mu {MU:+.2f} s {SD:.2f}. 지금 mover 190에서 표시 50 사람 실패 ' +
  ' · '.join(f'R {r}: {N.cdf(-lnD50(r)/SD):.0%}' for r in (1.4, 1.65, 1.9)) + ' (8회차 8-R2-1과 같은 식)')
P0_50 = 239 * 4 ** 0.7  # T8-4 표시 50 봇 상태 (공격력 239, 수확기 4) = 사람 앵커

def eexp(n, esea):
    e = zone_at(n)
    return e if e < 3 else esea + (e - 3) * (esea - 2)
def boss_hp(n, mul, esea): return 40 * zmul(eexp(n, esea), n) * mul
def boss_lnD(n, atk, balls, mul, esea, R=1.65, SK=1.0):
    return lnD50(R) + math.log(atk * balls ** 0.7 * SK / P0_50) - math.log(boss_hp(n, mul, esea) / HP50)
def boss_q(n, atk, balls, mul, esea, R=1.65, SK=1.0): return N.cdf(-boss_lnD(n, atk, balls, mul, esea, R, SK) / SD)
def mul_for(n, atk, balls, q, esea, R=1.65, SK=1.0):
    return math.exp(boss_lnD(n, atk, balls, 1.0, esea, R, SK) + SD * N.inv_cdf(q))

# ---------- 4. 진행 시뮬 (표시 71~100, 시드별) ----------
def sim(s, o, human=False):
    rs = SER[s]; r71 = rs[71]
    La, Lg = r71['infA0'], r71['infG0']; gold = 0.0; tmin = r71['playMin']
    rows = []; last = None
    rmin = (rs[100]['playMin'] - rs[71]['playMin']) / 29
    HR = 0.6 if human else 1.0
    if human: tmin = o.get('h71', 113.0)   # 사람 표시 50 처치 ~80분(8회차) + 21판×1.35분 + 60·70 실패 몫
    if human: rmin = 1.35
    for n in range(71, 101):
        ee = eexp(n, o['esea']) if o['game_zone'] else 2
        atk = A0[s] * (1 + o['sa'] * La) * (o['tierV'] if n >= o.get('tierV_at', 999) else 1)
        k = atk / zmul(ee, n)
        inc = BF[s](n) * zmul(ee, n) / zmul(2, n) * h(k, EF) * (1 + o['sg'] * Lg) * HR
        balls = rs[n]['balls']; fails = 0; q = 0
        if n % 10 == 0:
            if human:
                q = boss_q(n, atk, balls, o['loop'], o['esea'], o.get('R', 1.65), o.get('SK', 1.0)); fails = q / (1 - q)
                inc_round = 2 * (last or inc) + fails * (last or inc)
            else:
                inc_round = 2 * (last or inc)
            tmin += rmin * (1 + 2 * fails)
        else:
            inc_round = inc; last = inc; tmin += rmin
        gold += inc_round; buys = 0
        while True:
            pa = math.ceil(o['p0'] * o['k'] ** La * 0.9); pg = math.ceil(o['p0'] * o['k'] ** Lg * 0.9)
            if min(pa, pg) > gold: break
            if pa <= pg: gold -= pa; La += 1
            else: gold -= pg; Lg += 1
            buys += 1
        nxt = min(math.ceil(o['p0'] * o['k'] ** La * 0.9), math.ceil(o['p0'] * o['k'] ** Lg * 0.9))
        rows.append(dict(n=n, inc=inc, atk=atk, kappa=k, buys=buys, La=La, Lg=Lg, nxt=nxt / max(inc, 1), q=q, tmin=tmin, fails=fails))
    return rows

BASE = dict(game_zone=True, esea=3, sa=0.07, sg=0.04, p0=3.63e12, k=1.076, loop=210, tierV=1)
OPTS = {
    'O0p 지금·PickZone(검증)': dict(BASE, game_zone=False),
    'O0 지금·게임 규칙': dict(BASE),
    'A 바다 지수 2.4': dict(BASE, esea=2.4),
    'A+K 바다 2.4 + 가격비 1.06': dict(BASE, esea=2.4, k=1.06),
    'C 무한 공격력 +30%p': dict(BASE, sa=0.30),
    'AC 바다 2.4 + 무한 공격력 +15%p': dict(BASE, esea=2.4, sa=0.15),
    'ACK 바다 2.4 + 공격력 +15%p + 가격비 1.06': dict(BASE, esea=2.4, sa=0.15, k=1.06),
    'ACGK 바다 2.4 + 공격력 +15%p + 골드 +8%p + 1.06': dict(BASE, esea=2.4, sa=0.15, sg=0.08, k=1.06),
    'F 권장: 바다 2.5 + 공격력 +15%p + 1.06 + LOOP 240': dict(BASE, esea=2.5, sa=0.15, k=1.06, loop=240),
    'F24 (바다 2.4 같은 묶음)': dict(BASE, esea=2.4, sa=0.15, k=1.06, loop=240),
    'B 바다 단계 공격력 ×4(표시 72)': dict(BASE, tierV=4.0, tierV_at=72),
}
def agg(rb, key, n): return S.median([[r for r in rows if r['n'] == n][0][key] for rows in rb])
P('\n## 4. 진행 시뮬 표시 71~100 (봇 = T8-4 시드 1~6 상태에서 시작, 사람 = 같은 상태·수입 ×0.6·보스 실패·1.35분/판)')
P('검증 기준(T8-4 실측, 시드 중앙): 판 net 75 3.23e12 · 85 7.24e12 · 95 8.8e12 · inf_atk 100에서 17')
RES = {}
for name, o in OPTS.items():
    for human in (False, True):
        rb = [sim(s, o, human) for s in st]; RES[(name, human)] = rb
        P(f'\n### {name} · {"사람" if human else "봇"}')
        P('표시 | 판 수입 | 공격력 | kappa | 판당 구매 | inf a/g | 다음가/수입 | 보스 실패율 | 도달 분')
        for n in (71, 74, 75, 76, 80, 85, 90, 95, 100):
            P(f"{n} | {agg(rb,'inc',n):.3g} | {agg(rb,'atk',n):.0f} | {agg(rb,'kappa',n):.2f} | {agg(rb,'buys',n):.1f} | {agg(rb,'La',n):.0f}/{agg(rb,'Lg',n):.0f} | {agg(rb,'nxt',n):.2f} | {agg(rb,'q',n):.0%} | {agg(rb,'tmin',n):.0f}")
        g1 = (agg(rb, 'inc', 74) / agg(rb, 'inc', 71)) ** (1 / 3); g2 = (agg(rb, 'inc', 99) / agg(rb, 'inc', 76)) ** (1 / 23)
        P(f'  판당 배율 71→74 ×{g1:.3f} · 76→99 ×{g2:.3f} · 바다 진입 뜀(75/74) ×{agg(rb,"inc",75)/agg(rb,"inc",74):.2f} · 76~100 판당 구매 평균 {S.mean([S.mean([r["buys"] for r in rows if r["n"]>75]) for rows in rb]):.2f}')

# ---------- 5. 보스 ----------
P('\n## 5. 사람 보스 판당 실패율 — LOOP 210, 상태 = T8-4 봇(60·70) / 시뮬 사람(80~100). 괄호 = R 1.9~1.4')
TGT = {60: 0.45, 70: 0.50, 80: 0.55, 90: 0.55, 100: 0.60}
NEED = {}
for name in OPTS:
    if name.startswith('O0p'): continue
    o = OPTS[name]; line = []; need = {}
    for n in (60, 70, 80, 90, 100):
        if n <= 70:
            atk = S.median([SER[s][n]['atk'] for s in st]); balls = S.median([SER[s][n]['balls'] for s in st])
        else:
            atk = agg(RES[(name, True)], 'atk', n); balls = 8
        qs = [boss_q(n, atk, balls, o['loop'], o['esea'], R) for R in (1.9, 1.65, 1.4)]
        line.append(f'{n}: {qs[1]:.0%}({qs[0]:.0%}~{qs[2]:.0%})')
        need[n] = mul_for(n, atk, balls, TGT[n], o['esea'])
    NEED[name] = need
    P(f'  {name}: ' + ' · '.join(line))
    P(f'     목표 {"/".join(str(int(v*100)) for v in TGT.values())}%에 맞는 mul: ' + ' · '.join(f'{n}: {v:.0f}' for n, v in need.items()))
P('  SK 민감도: 사람 보스 DPS가 공격력×수확기^0.7보다 스킬 몫만큼 더 크면(표시 50 대비 SK배) 같은 mul에서 lnD + ln SK. SK 2 → 실패율 약 −25%p, 맞는 mul ×2')
json.dump({k: {str(n): v for n, v in d.items()} for k, d in NEED.items()}, open(os.path.join(HERE, 'theory9_need.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
open(os.path.join(HERE, 'theory9_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
print('\n'.join(OUT))
