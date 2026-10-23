# -*- coding: utf-8 -*-
"""9회차 검증 (verify, manager#39): 이론_9 9-R을 theory9r.py를 쓰지 않고 다시 계산한다.
다시 돌리기: python -X utf8 BalanceData/r9/verify/verify9.py  → verify9_out.txt (결정적, 난수 시드 고정)
입력: BalanceData/r9/test/T9-0/rounds_k06|k10_s*.csv, lag_k06|k10_s*.csv. 8회차 값(T8-3 lag 54)은 테스트_8.md에서 옮김.
"""
import csv, math, os, random, statistics as S
from statistics import NormalDist
NDist = NormalDist()
HERE = os.path.dirname(os.path.abspath(__file__))
TD = os.path.join(HERE, '..', 'test', 'T9-0')
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a)); print(OUT[-1])

# ---------------- 원자료 ----------------
def load(k, s):
    rows = {}; La = Lg = 0
    with open(os.path.join(TD, f'rounds_{k}_s{s}.csv'), encoding='utf-8') as f:
        for r in csv.DictReader(f):
            n = int(r['stage']) - 2          # 표시 = 내부 - 2
            d = dict(n=n, boss=r['bossFlag'] == '1', zone=r['zone'], net=float(r['net']), atk=float(r['atk']),
                     made=float(r['made'] or 0), harv=float(r['harvests'] or 0), dur=float(r['dur']),
                     hp=float(r['bossMaxHp'] or 0), dps=float(r['grossDps'] or 0), pm=float(r['playMin']), La=La, Lg=Lg)
            for b in filter(None, r['bought'].split(';')):
                key, lv = b.split(':'); to = int(lv.split('>')[1])
                if key == 'inf_atk': La = to
                if key == 'inf_gold': Lg = to
            rows.setdefault(n, []).append(d)   # 보스 실패 시 같은 n이 여럿일 수 있음 (T9-0엔 없음)
    return {n: v[-1] for n, v in rows.items()}
DATA = {k: {s: load(k, s) for s in range(1, 7)} for k in ('k06', 'k10')}
def lagrows(k):
    out = []
    for s in range(1, 7):
        with open(os.path.join(TD, f'lag_{k}_s{s}.csv'), encoding='utf-8') as f:
            out += [dict(boss=int(r['boss']), d=float(r['dpsOverMax']) * (float(r['dur']) - 2), cl=r['cleared'] == '1', s=s) for r in csv.DictReader(f)]
    return out
LAG = {k: lagrows(k) for k in ('k06', 'k10')}
def botD(k, s, n):
    r = DATA[k][s][n]; return r['dps'] / r['hp'] * (r['dur'] - 2)
med = S.median

# ================= 1. 정정 3가지 =================
P('## 1. theory 정정 확인 (T9-0)')
for k in ('k06', 'k10'):
    hf = [DATA[k][s][n]['harv'] / DATA[k][s][n]['made'] for s in range(1, 7) for n in range(70, 75) if not DATA[k][s][n]['boss']]
    hs = [DATA[k][s][n]['harv'] / DATA[k][s][n]['made'] for s in range(1, 7) for n in range(75, 101) if not DATA[k][s][n]['boss']]
    jump = [S.mean([DATA[k][s][n]['net'] for n in range(75, 80)]) / DATA[k][s][74]['net'] for s in range(1, 7)]
    P(f'  {k}: 수확률 들판 70~74 중앙 {med(hf):.4f}(최소 {min(hf):.4f}) · 바다 75~100 중앙 {med(hs):.4f}(최소 {min(hs):.4f}) · 진입 뜀(75~79 평균/74) ' + ' '.join(f'×{j:.1f}' for j in jump))

def ef_fit(k, n0, n1, mode):
    xs, ys, gs = [], [], []
    for s in range(1, 7):
        for n in range(n0, n1 + 1):
            r = DATA[k][s][n]
            if r['boss']: continue
            y = math.log(r['net'] / (1 + 0.04 * r['Lg']))
            if mode == 'fix101': y -= n * math.log(1.01)
            xs.append([math.log(r['atk']), n]); ys.append(y); gs.append(s)
    # 시드 고정효과(시드별 평균 빼기) + 선택적으로 n 기울기 자유
    def demean(v, g):
        m = {};
        for vi, gi in zip(v, g): m.setdefault(gi, []).append(vi)
        mm = {gi: S.mean(l) for gi, l in m.items()}
        return [vi - mm[gi] for vi, gi in zip(v, g)]
    if mode in ('fix101', 'pooled'):
        x = [a[0] for a in xs]
        if mode == 'fix101_fe': pass
        mx, my = S.mean(x), S.mean(ys)
        b = sum((a - mx) * (c - my) for a, c in zip(x, ys)) / sum((a - mx) ** 2 for a in x)
        return b
    x1 = demean([a[0] for a in xs], gs); x2 = demean([a[1] for a in xs], gs); y = demean(ys, gs)
    if mode == 'fe_fix101':
        y = demean([yy - a[1] * math.log(1.01) for yy, a in zip(ys, xs)], gs)
        return sum(a * c for a, c in zip(x1, y)) / sum(a * a for a in x1)
    # fe_free: 두 변수 OLS
    s11 = sum(a * a for a in x1); s22 = sum(a * a for a in x2); s12 = sum(a * b for a, b in zip(x1, x2))
    s1y = sum(a * c for a, c in zip(x1, y)); s2y = sum(a * c for a, c in zip(x2, y))
    det = s11 * s22 - s12 * s12
    corr = s12 / math.sqrt(s11 * s22)
    return (s22 * s1y - s12 * s2y) / det, (s11 * s2y - s12 * s1y) / det, corr
P('  바다 판 수입 공격력 탄력 EF (76~100 일반 판):')
for k in ('k10', 'k06'):
    b0 = ef_fit(k, 76, 100, 'fix101'); b1 = ef_fit(k, 76, 100, 'fe_fix101'); b2 = ef_fit(k, 76, 100, 'fe_free')
    P(f'    {k}: theory식(풀, 스테이지 1.01 고정) {b0:.3f} · 시드 고정효과 {b1:.3f} · 시드FE+스테이지 기울기 자유 EF {b2[0]:.2f} (스테이지 판당 ×{math.exp(b2[1]):.3f}, ln공격력-스테이지 상관 {b2[2]:.2f})')
# 판 수입 판당 증가와 공격력 판당 증가
for k in ('k10',):
    g_net = med([(DATA[k][s][99]['net'] / DATA[k][s][81]['net']) ** (1 / 18) for s in range(1, 7)])
    g_atk = med([(DATA[k][s][99]['atk'] / DATA[k][s][81]['atk']) ** (1 / 18) for s in range(1, 7)])
    P(f'    {k} 81→99 판당: 판 수입 ×{g_net:.3f}, 공격력 ×{g_atk:.3f}, inf_gold 몫 포함 → 공격력만으로 설명하면 EF {math.log(g_net/1.01)/math.log(g_atk):.2f} 이하')
P('  일반 판 진행: `RoundSim.Effects.cs:419~420` 보스 판이 아니면 aborted가 아닌 한 G.stage++ → 일반 판엔 실패 조건 없음(코드 확인).')

# ================= 2. 사람 보스 모델 c =================
P('\n## 2. 사람 보스 모델 c')
def ln_cond_fit(q, left):
    # lnD~N(mu,s), P(D<1)=q, E[D|D<1]=1-left 를 직접 풀기 (이분법)
    z = NDist.inv_cdf(q)
    def cm(s):
        mu = -s * z
        return math.exp(mu + s * s / 2) * NDist.cdf((-mu - s * s) / s) / NDist.cdf(-mu / s)
    lo, hi = 0.01, 4.0
    for _ in range(80):
        m = (lo + hi) / 2
        if cm(m) > 1 - left: lo = m
        else: hi = m
    s = (lo + hi) / 2; return -s * z, s
MU, SD = ln_cond_fit(0.50, 0.48)
P(f'  8회차 B 50 맞춤 재계산: mu {MU:+.3f} s {SD:.3f} (theory 0, 1.01)')
def Dh50(R): return math.exp(MU - math.log(150 / 200) - math.log(1.91) + math.log(R))
for k in ('k10', 'k06'):
    d50 = med([botD(k, s, 50) for s in range(1, 7)])
    P(f'  {k}: 봇 D50 중앙 {d50:.2f} (시드 ' + ' '.join(f'{botD(k, s, 50):.2f}' for s in range(1, 7)) + f') → c = {Dh50(1.65)/d50:.3f} (R 1.4 {Dh50(1.4)/d50:.3f} · 1.9 {Dh50(1.9)/d50:.3f})')
D50K10 = med([botD('k10', s, 50) for s in range(1, 7)])
C = Dh50(1.65) / D50K10
P(f'  사람 D50 = {Dh50(1.65):.2f} → 사람 판당 실패 {NDist.cdf(-math.log(Dh50(1.65))/SD):.0%}')
# lag 8 비교
P('  lag 8(SKILL 1.0) D vs 봇 본 판 D, 같은 표시 (D = DPS/maxHp × (판 시간 − 2)):')
R8 = {}
for n in (60, 70, 80, 90, 100):
    l8 = med([r['d'] for r in LAG['k10'] if r['boss'] == n]); b = med([botD('k10', s, n) for s in range(1, 7)])
    l8b = med([r['d'] for r in LAG['k06'] if r['boss'] == n]); bb = med([botD('k06', s, n) for s in range(1, 7)])
    R8[n] = l8 / b
    P(f'    {n}: k10 봇 D {b:.2f} · lag8 D {l8:.2f} (비 {l8/b:.2f}) | k06 봇 {bb:.2f} · lag8 {l8b:.2f} (비 {l8b/bb:.2f}) | c모델 사람 D {C*b:.2f} → 지금 실패 {NDist.cdf(-math.log(C*b)/SD):.0%}')
# 8회차 T8-3 (mover 150, SKILL 1.0, homing 0): lag0 0.19/s, lag4 0.08/s, lag8 0.02/s
r8_50 = 0.02 / 0.19; r4_50 = 0.08 / 0.19
P(f'  표시 50(T8-3): lag4/lag0 {r4_50:.2f} · lag8/lag0 {r8_50:.3f}. c {C:.3f} = lag 4와 8 사이(로그 보간 lag ≈ {4 + 4*math.log(r4_50/C)/math.log(r4_50/r8_50):.1f}).')
P(f'  사람 D50 {Dh50(1.65):.2f} vs lag8 D50 ≈ 0.02×35 = 0.70 → 사람 = lag8 상태 × {Dh50(1.65)/0.70:.2f}(조준 이점 a, 검증_8 2-d a 1.6~2.6과 같은 방향)')
ALT = {n: R8[n] * Dh50(1.65) / 0.70 for n in R8}   # lag 8 + a 고정 모델의 사람/봇 비
P('  대안 모델 "사람 = lag 8 상태 × a(50에서 맞춤)"의 사람/봇 D 비: ' + ' · '.join(f'{n}: {v:.2f}(c의 ×{v/C:.1f})' for n, v in ALT.items()))
# D1 0.6 정합: 봇 수입 판당 증가로 몇 판 지연인지
for a, b in ((41, 49), (51, 59), (61, 69), (76, 89), (91, 99)):
    g = med([(DATA['k10'][s][b]['net'] / DATA['k10'][s][a]['net']) ** (1 / (b - a)) for s in range(1, 7) if not DATA['k10'][s][a]['boss'] and not DATA['k10'][s][b]['boss']])
    P(f'    봇 k10 {a}~{b} 판당 수입 ×{g:.3f} → 사람 수입 0.6 = {math.log(1/0.6)/math.log(g):.1f}판 지연')

# ================= 3. 몬테카를로 (독립 구현) =================
HP_NOW = 210.0
PLANS = {'now': {n: 210 for n in (60, 70, 80, 90, 100)},
         'a': {60: 390, 70: 910, 80: 620, 90: 1060, 100: 1390},
         'b': {60: 440, 70: 1080, 80: 890, 90: 1640, 100: 1940},
         'c': {n: 370 * r for n, r in zip((60, 70, 80, 90, 100), (1, 2.19, 1.78, 2.65, 3.35))}}
def sim(plan='c', pity=0.85, sa=0.15, k=1.06, cc=None, ratio=None, sp=0.70, sn=0.73, farm=2, inc=1.0, hstart=76,
        gm=1.0, am=1.0, players=3000, seed=11, t50=78.0, rmin=1.35, ef_sea=None):
    """사람 표시 51~100. ratio: 표시별 사람/봇 D 비(없으면 cc 상수). 반환 dict"""
    rng = random.Random(seed); mul = PLANS[plan] if isinstance(plan, str) else plan
    cc = C if cc is None else cc
    EFs = 0.02 if ef_sea is None else ef_sea
    st = {n: dict(tries=[], first=0, fails=0, mins=[]) for n in (60, 70, 80, 90, 100)}
    T = []
    for p in range(players):
        s = 1 + p % 6; R = DATA['k10'][s]; z = rng.gauss(0, sp)
        La = Lg = 0; gold = 0.0; t = t50
        def price(L): return math.ceil(3.63e12 * k ** L * 0.9)
        for n in range(51, 101):
            b = R[n]; base = R[n - 1] if b['boss'] else b
            A0 = b['atk'] / (1 + 0.07 * b['La'])
            atk = A0 * (1 + sa * La) * am
            ef = EFs if n >= 75 else 0.3
            def income():
                a_now = A0 * (1 + sa * La) * am
                return inc * 0.6 * gm * base['net'] * (1 + 0.04 * Lg) / (1 + 0.04 * base['Lg']) * (a_now / b['atk']) ** ef
            def shop():
                nonlocal gold, La, Lg
                if n < hstart: return
                while min(price(La), price(Lg)) <= gold:
                    if price(La) <= price(Lg): gold -= price(La); La += 1
                    else: gold -= price(Lg); Lg += 1
            if n % 10 or n < 60:
                gold += income(); shop(); t += rmin; continue
            rr = (ratio[n] if ratio else cc)
            fails = 0; rounds = 0
            while True:
                rounds += 1
                atk = A0 * (1 + sa * La) * am
                lnD = math.log(rr * botD('k10', s, n) * atk / b['atk'] * HP_NOW / mul[n]) - fails * math.log(pity) + z + rng.gauss(0, sn)
                if lnD >= 0 or fails >= 80: break
                fails += 1; gold += 0.0   # 진 판 골드는 무시(보수)
                for _ in range(farm):
                    gold += income(); shop(); rounds += 1
            gold += income(); shop()
            q = st[n]; q['tries'].append(fails + 1); q['first'] += fails > 0; q['fails'] += fails; q['mins'].append((rounds - 1) * rmin)
            t += rounds * rmin
        T.append(t)
    res = {}
    for n, q in st.items():
        m = sorted(q['mins']); tr = q['tries']
        res[n] = dict(first=q['first'] / players, per=q['fails'] / sum(tr), mtries=S.mean(tr), med=med(m), mean=S.mean(m), p90=m[int(.9 * len(m))], p99=m[int(.99 * len(m))])
    Ts = sorted(T)
    return res, med(Ts), Ts[int(.9 * len(Ts))]
def show(lab, r):
    res, tm, t90 = r
    P(f'  {lab}: 100 도달 중앙 {tm:.0f}분 (90% {t90:.0f})')
    P('     첫 도전 실패(전체 사람) ' + ' · '.join(f'{n}:{v["first"]:.0%}' for n, v in res.items()) +
      ' | 판당 실패(D1 B 지표) ' + ' · '.join(f'{n}:{v["per"]:.0%}' for n, v in res.items()))
    P('     허들 분 중앙/평균/90%/99% ' + ' · '.join(f'{n}:{v["med"]:.0f}/{v["mean"]:.0f}/{v["p90"]:.0f}/{v["p99"]:.0f}' for n, v in res.items()))
    return r

P('\n## 3. c안 몬테카를로 (독립 구현, 사람 3,000명)')
base = show('c안 기준(c 0.234, 파밍 2판, 실패 ×0.85)', sim('c'))
P('  -- 민감도')
for cc in (0.20, 0.27):
    show(f'c {cc}', sim('c', cc=cc))
show('c 0.234, 사람 사이 0.85·판마다 0.53 (s 1.01 같게)', sim('c', sp=0.85, sn=0.53))
show('c 0.234, 사람 사이 0.50·판마다 0.87', sim('c', sp=0.50, sn=0.87))
for fm in (0, 1, 4):
    show(f'파밍 {fm}판 뒤 재도전', sim('c', farm=fm))
show('수입 ×0.5', sim('c', inc=0.5))
show('수입 ×2', sim('c', inc=2.0))
show('무한 노드 시작 표시 70', sim('c', hstart=70))
show('무한 노드 시작 표시 82', sim('c', hstart=82))
show('바다 EF 0.3(들판과 같음)', sim('c', ef_sea=0.3))
alt = show('대안: 사람/봇 비 = lag 8 상태 × a (표시별)', sim('c', ratio=ALT))

P('\n## 4. b안 · 실패 완화 없음')
show('b안 + ×0.85', sim('b'))
show('a안(보스별 mul, 완화 없음)', sim('a', pity=1.0))
show('c안 체력, 완화 없음', sim('c', pity=1.0))
show('지금(210, 새 무한 노드), 완화 없음', sim('now', pity=1.0))
show('지금(210, 옛 무한 노드 0.07·1.076), 완화 없음', sim('now', pity=1.0, sa=0.07, k=1.076))

# ================= 5. 봇 =================
P('\n## 5. 봇(k10)에 새 무한 노드·c안 체력')
# 봇 판 사이 흔들림: lag 반복 3판 안 ln D 표준편차
sds = []
for k in ('k10', 'k06'):
    for s in range(1, 7):
        for n in (60, 70, 80, 90, 100):
            v = [math.log(r['d']) for r in LAG[k] if r['s'] == s and r['boss'] == n]
            if len(v) >= 2: sds.append(S.stdev(v))
BSD = math.sqrt(S.mean([x * x for x in sds]))
P(f'  봇 같은 상태 판 사이 ln D 표준편차 {BSD:.2f} (lag 반복 3판, {len(sds)}묶음)')
def bot_levels(Lold, kold=1.076, knew=1.06):
    spent = (kold ** Lold - 1) / (kold - 1)
    return math.log(1 + spent * (knew - 1)) / math.log(knew)
for n in (60, 70, 80, 90, 100):
    v = []; pf = []
    for s in range(1, 7):
        b = DATA['k10'][s][n]; A0 = b['atk'] / (1 + 0.07 * b['La'])
        Ln = bot_levels(b['La']); atk_new = A0 * (1 + 0.15 * Ln)
        d = botD('k10', s, n) * atk_new / b['atk'] * HP_NOW / PLANS['c'][n]
        v.append(d); pf.append(NDist.cdf(-math.log(d) / BSD))
    # 실패 ×0.85 · 즉시 재도전 기대 도전 수
    def exp_tries(d):
        e = 0; surv = 1
        for f in range(40):
            q = NDist.cdf(-(math.log(d) - f * math.log(0.85)) / BSD); e += surv; surv *= q
        return e
    P(f'  {n}: 봇 D(새 값) 중앙 {med(v):.2f} (시드 {min(v):.2f}~{max(v):.2f}), 첫 도전 실패 {med(pf):.0%}, 기대 도전 {med([exp_tries(d) for d in v]):.1f}번')
P('  inf 레벨 환산(같은 골드): 봇 L_old 30 → L_new %.1f, 55 → %.1f' % (bot_levels(30), bot_levels(55)))

# ================= 6. 세계수 =================
P('\n## 6. 세계수 몫')
r0 = sim('c')
for lab, gm, am in (('판 ×1.12 · 공격 ×1.05 (보따리 없음)', 1.12, 1.05), ('+보따리 12%', 1.12 * 1.12, 1.05), ('+보따리 45%', 1.12 * 1.45, 1.05),
                    ('보따리 세션당 1번(≈+5%)', 1.12 * 1.05, 1.05), ('공격 ×1.05만', 1.0, 1.05), ('골드만 ×1.62', 1.62, 1.0)):
    r = sim('c', gm=gm, am=am)
    P(f'  {lab}: 100 도달 {r[1]:.0f}분 (기준 {r0[1]:.0f}, {r[1]-r0[1]:+.0f}) · 허들 평균 ' + ' · '.join(f'{n}:{r[0][n]["mean"]-r0[0][n]["mean"]:+.1f}' for n in r[0]))

open(os.path.join(HERE, 'verify9_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
