# -*- coding: utf-8 -*-
"""9-R4 검증 (verify, manager#46). theory9r4.py를 쓰지 않고 다시 계산한다. 결정적.
다시 돌리기: python -X utf8 BalanceData/r9/verify/verify9r4.py → verify9r4_out.txt
theory와 다른 점(독립 구현):
  - 사람 트리 상태: 봇 누적 트리 구매액(DB 가격으로 내가 다시 셈)에 사람 누적 트리 지출이 닿는 '연속' 판 m(로그 보간). theory는 정수 판(바닥).
  - 트리 힘 지표 T의 스킬 몫 k(theory 0.03 가정)를 0·0.03·0.06으로 흔든다.
  - 봇 시드별(1~6) 허들 평균을 따로 내서 '단조'가 시드 흔들림 밖인지 본다.
  - 75 전후 판별 싱크 구매 수, 300까지 체력 표, 90·100 미리 낮추기 표.
"""
import csv, json, math, os, random, statistics as S
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.join(HERE, '..', '..', '..'); TD = os.path.join(HERE, '..', 'test', 'T9-0')
DB = json.load(open(os.path.join(ROOT, 'Assets', 'Resources', 'DB', 'db.json'), encoding='utf-8'))
ROWS = json.load(open(os.path.join(HERE, '..', 'theory', 'final_rows_r4.json'), encoding='utf-8'))
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a)); print(OUT[-1])

def zmul(n):
    e = 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4
    return 10 ** e * 1.01 ** (n - 1)
BASE = 123; LOOP_BOSS = [1.00, 2.98, 6.62, 25.2, 54.5]; LOOP_LATER = 4.96
def boss_mul(n):
    k = n // 10 - 1; lap = k // 5
    return None if lap == 0 else BASE * (LOOP_BOSS[k % 5] if lap == 1 else LOOP_LATER)
def boss_hp(n): return 40 * zmul(n) * boss_mul(n)
def hp_now(n): return 40 * zmul(n) * 210

P('## 1. 제안 체력 60~300 (40 × ZoneMul × 123 × LOOP_BOSS / LOOP_LATER 4.96)')
prev = None; row = []; bad = []
for n in range(60, 310, 10):
    h = boss_hp(n)
    if prev: row.append(f'{n}:{h:.3g}(×{h/prev:.3f})'); bad += [n] if h <= prev else []
    else: row.append(f'{n}:{h:.3g}')
    prev = h
P('  ' + ' · '.join(row)); P(f'  감소 지점: {bad or "없음"} · mul ' + ' · '.join(f'{n}:{boss_mul(n):.1f}' for n in (60, 70, 80, 90, 100, 110)))
P(f'  100→110 여유 ×{boss_hp(110)/boss_hp(100):.4f} · LOOP_LATER 단조 하한 {LOOP_BOSS[4]*boss_hp(100)/boss_hp(110)*LOOP_LATER/LOOP_BOSS[4]:.3f}→ 경계 {LOOP_BOSS[4]/(10*1.01**10):.3f}')

# ---------- 원 기록 + 트리 지출 ----------
COST = {c['Cost_ID']: [c[f'Cost_Lv{i}'] for i in range(1, 5)] for c in DB['Upgrade_Cost']}
UP = {u['Upg_Key']: u for u in DB['Upgrade']}
SKILLS = {u['Upg_Key'] for u in DB['Upgrade'] if u['Skill']}
def load(s):
    R = {}; La = Lg = 0; spent = 0.0; sk = 0; op = {}
    for r in csv.DictReader(open(os.path.join(TD, f'rounds_k10_s{s}.csv'), encoding='utf-8')):
        n = int(r['stage']) - 2
        R[n] = dict(boss=r['bossFlag'] == '1', net=float(r['net']), La=La, Lg=Lg, mhp=float(r['bossMaxHp'] or 0),
                    dps=float(r['grossDps'] or 0), dur=float(r['dur']), atk=float(r['atk']) / (1 + 0.07 * La), balls=int(r['balls']), sk=sk, spent=spent)
        for b in filter(None, r['bought'].split(';')):
            k, lv = b.split(':'); a, c = (int(x) for x in lv.split('>'))
            if k == 'inf_atk': La = c; continue
            if k == 'inf_gold': Lg = c; continue
            u = UP.get(k)
            if u is None: continue
            pr = COST[u['Cost_ID']]
            spent += sum(math.ceil(pr[i] * 0.9) for i in range(a, min(c, 4)))
            if k in SKILLS: sk += c - a
            if c == u['Efx_MaxLv'] and k in ('fest', 'ps_ball3', 'ps_atk4', 'ed_score3p'): op.setdefault(k, n)
    return R, op
K = {}; OP = {}
for s in range(1, 7): K[s], OP[s] = load(s)
P('\n## 2. 봇 트리: ' + ' · '.join(f's{s} ps_atk4 {OP[s]["ps_atk4"]}·ed_score3p {OP[s]["ed_score3p"]}·ps_ball3 {OP[s]["ps_ball3"]}·fest {OP[s]["fest"]}·총액 {K[s][100]["spent"]:.3g}' for s in K))
def Tpow(r, ks): return r['atk'] * r['balls'] ** 0.7 * (1 + ks * r['sk'])
def m_of(R, tree):   # 연속 판: spent(m) ≤ tree < spent(m+1) 사이를 선형 보간
    m = 50
    while m < 100 and R[m + 1]['spent'] <= tree: m += 1
    if m >= 100: return 100.0
    a, b = R[m]['spent'], R[m + 1]['spent']
    return m + (0 if b <= a else min(1, (tree - a) / (b - a)))
def T_at(R, mf, ks):
    m = int(mf); f = mf - m
    if m >= 100: return Tpow(R[100], ks)
    return math.exp((1 - f) * math.log(Tpow(R[m], ks)) + f * math.log(Tpow(R[m + 1], ks)))

SINK = {}   # final_rows_r4에서 직접 읽는다 (+ 기존 투지·풍년)
for u in ROWS['Upgrade']:
    c = next(x for x in ROWS['Upgrade_Cost'] if x['Cost_ID'] == u['Cost_ID'])
    SINK[u['Upg_Key']] = [c['Cost_Lv1'], c['Cost_Lv2'] / c['Cost_Lv1'], u['Efx_Lv1'] - 1, {'inf_srate': 50, 'inf_spd': 50, 'inf_ball': 6}.get(u['Upg_Key']),
                          {12050: 'fest', 11022: 'ps_ball3'}[u['Upg_Req']]]
SINK['inf_atk'] = [3.63e12, 1.06, 0.15, None, 'ps_atk4']; SINK['inf_gold'] = [3.63e12, 1.06, 0.04, None, 'ed_score3p']
P('  싱크(final_rows_r4): ' + ' · '.join(f'{k} {v[0]:.2g}×{v[1]:.2f} +{v[2]:.2f} 상한 {v[3]} 열림 {v[4]}' for k, v in SINK.items()))
def power(L, W=0.5):
    e = {k: v[2] for k, v in SINK.items()}
    sp = (1.36 + e['inf_spd'] * L['inf_spd']) / 1.36; sd = (1.25 + e['inf_sdmg'] * L['inf_sdmg']) / 1.25
    return (1 + e['inf_atk'] * L['inf_atk']) * ((8 + L['inf_ball']) / 8) ** 0.7 * (1 + W * ((1 + e['inf_srate'] * L['inf_srate']) * sd - 1)) * sp ** 0.7

HP4 = {n: boss_hp(n) for n in (60, 70, 80, 90, 100)}
def sim(hp=HP4, players=1500, seed=11, farm=2, sp=0.70, sn=0.73, inc=0.6, tg=1.0, ks=0.03, W=0.5, bot=False, rmin=1.35, cnt=None, byseed=None, glog=None):
    rng = random.Random(seed); res = {n: [0, 0, []] for n in hp}; T = []
    for p in range(players):
        s = 1 + p % 6; R = K[s]; z = 0 if bot else rng.gauss(0, sp)
        L = {k: 0 for k in SINK}; tree = R[50]['spent']; gold = 0.0; t = 0.0; total = R[100]['spent']
        def shop(n, c=None):
            nonlocal gold, tree
            if tree < total: use = min(gold, total - tree); tree += use; gold -= use
            mf = m_of(R, tree)
            while True:
                best = None; bc = float('inf')
                for k, (p0, kk, e, cap, opk) in SINK.items():
                    if mf < OP[s][opk] or (cap is not None and L[k] >= cap): continue
                    pr = math.ceil(p0 * kk ** L[k] * 0.9)
                    if pr < bc: best, bc = k, pr
                if best is None or bc > gold: return
                gold -= bc; L[best] += 1
                if c is not None: c[0] += 1
        def income(n):
            mm = n if not R[n]['boss'] else n - 1
            return inc * tg * R[mm]['net'] * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * R[mm]['Lg'])
        def D(n):
            b = R[n]; pb = b['dps'] * (b['dur'] - 2) / (1 + 0.07 * b['La'])
            need = hp[n] * b['mhp'] / hp_now(n)
            if n == 70: need *= 1 + 0.01 * (b['dur'] - 2) * (0.1 if bot else 0.7)
            tr = 1.0 if bot else T_at(R, m_of(R, tree), ks) / Tpow(b, ks)
            return (1.0 if bot else 0.234) * pb * tr * power(L, W) / need
        for n in range(51, 101):
            if n % 10 or n < 60:
                gold += income(n); c = [0] if (cnt is not None and 73 <= n <= 79) else None
                shop(n, c); t += rmin
                if c is not None: cnt.setdefault(n, []).append(c[0])
                continue
            fails = rounds = 0
            while True:
                rounds += 1
                if math.log(D(n)) + z + rng.gauss(0, sn) >= 0 or fails > 300: break
                fails += 1
                for _ in range(farm):
                    d0 = D(n); gold += income(n); shop(n); rounds += 1
                    if glog is not None: glog.setdefault(n, []).append(D(n) / d0)
                if farm == 0: gold += income(n) * 0.8; shop(n)
            gold += 2 * income(n); shop(n)
            h = (rounds - 1) * rmin; q = res[n]; q[0] += fails; q[1] += fails + 1; q[2].append(h); t += rounds * rmin
            if byseed is not None: byseed.setdefault((n, s), []).append(h)
        T.append(t)
    o = {n: dict(fail=q[0] / q[1], mean=S.mean(q[2]), med=S.median(q[2]), p90=sorted(q[2])[int(.9 * len(q[2]))],
                 stuck=sum(x > 60 for x in q[2]) / len(q[2])) for n, q in res.items()}
    return o, S.median(T), sorted(T)[int(.9 * len(T))]
def show(lab, r, t0=78):
    o, tm, t9 = r
    P(f'  {lab}: 100 도달 {tm+t0:.0f}분(90% {t9+t0:.0f}) | 허들 중앙/평균/90% ' + ' · '.join(f'{n}:{v["med"]:.0f}/{v["mean"]:.0f}/{v["p90"]:.0f}' for n, v in o.items())
      + ' | 막힘 ' + ' · '.join(f'{n}:{v["stuck"]:.1%}' for n, v in o.items()) + ' | 판당 실패 ' + ' · '.join(f'{n}:{v["fail"]:.0%}' for n, v in o.items()))
    return o

P('\n## 3. 재현 (세계수 없음 · 4단계 +12%)')
GL = {}; BS = {}; CN = {}
show('세계수 없음', sim(glog=GL, byseed=BS, cnt=CN))
P('    파밍 1판 D: ' + ' · '.join(f'{n}: ×{math.exp(S.mean(math.log(x) for x in v)):.3f}' for n, v in sorted(GL.items())))
P('    봇 시드별 허들 평균(사람 250명씩): ' + ' | '.join(f'{n}: ' + '/'.join(f'{S.mean(BS[(n, s)]):.0f}' for s in range(1, 7)) for n in (60, 70, 80, 90, 100)))
P('    표시 73~79 일반 판 싱크 구매 수 중앙/90%/최대: ' + ' · '.join(f'{n}: {S.median(v):.0f}/{sorted(v)[int(.9*len(v))]}/{max(v)}' for n, v in sorted(CN.items())))
TG4 = 1 + 0.92 * 0.12 * 3; TG8 = 1 + 0.92 * 0.12 * 7
GL4 = {}; BS4 = {}
show(f'세계수 4단계(판 ×{TG4:.2f})', sim(tg=TG4, glog=GL4, byseed=BS4))
P('    파밍 1판 D: ' + ' · '.join(f'{n}: ×{math.exp(S.mean(math.log(x) for x in v)):.3f}' for n, v in sorted(GL4.items())))
P('    봇 시드별 허들 평균: ' + ' | '.join(f'{n}: ' + '/'.join(f'{S.mean(BS4[(n, s)]):.0f}' for s in range(1, 7)) for n in (60, 70, 80, 90, 100)))
show(f'세계수 8단계(판 ×{TG8:.2f})', sim(tg=TG8, players=1000))
P('  난수 시드 바꿔 세계수 없음 허들 평균: ' + ' | '.join(
    f'seed {sd}: ' + '/'.join(f'{v["mean"]:.0f}' for v in sim(seed=sd, players=1000)[0].values()) for sd in (1, 2, 3)))

P('\n## 4. 모델 가정 민감도 (세계수 없음 / 4단계)')
for lab, kw in (('T 스킬 몫 k 0', dict(ks=0.0)), ('k 0.06', dict(ks=0.06)), ('σ판 0.55', dict(sn=0.55)), ('σ판 0.40', dict(sn=0.40)),
                ('W 0.25', dict(W=0.25)), ('수입 ×0.5', dict(inc=0.3)), ('파밍 0판', dict(farm=0)), ('파밍 4판', dict(farm=4))):
    show(lab + ' · 없음', sim(players=800, **kw)); show(lab + ' · 4단계', sim(players=800, tg=TG4, **kw))

P('\n## 5. 90·100을 미리 낮추면 (4단계 기준, 기준/σ0.40/W0.25/수입×0.5)')
for f in (1.0, 0.8, 0.65, 0.5):
    hp = dict(HP4); hp[90] *= f; hp[100] *= f
    for lab, kw in (('기준', {}), ('σ0.40', dict(sn=0.40)), ('W0.25', dict(W=0.25)), ('수입×0.5', dict(inc=0.3))):
        o, tm, _ = sim(hp=hp, players=800, tg=TG4, **kw)
        P(f'  90·100 ×{f} {lab}: 90 평균 {o[90]["mean"]:.0f}분 막힘 {o[90]["stuck"]:.1%} · 100 평균 {o[100]["mean"]:.0f}분 막힘 {o[100]["stuck"]:.1%} · 판당 실패 {o[90]["fail"]:.0%}/{o[100]["fail"]:.0%} · 100 도달 {tm+78:.0f}분')

P('\n## 6. 봇 (k10, 매 판 재도전, 판 1.16분, 50 처치 52.7분)')
show('봇', sim(players=300, farm=0, sp=0, sn=0.28, inc=1.0, rmin=1.16, bot=True), t0=52.7)
open(os.path.join(HERE, 'verify9r4_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
