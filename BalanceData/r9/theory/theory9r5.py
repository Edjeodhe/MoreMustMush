# -*- coding: utf-8 -*-
"""9-R5: 사용자 결정 6 + verify#32. 9-R4 모델 위에 끈기(보스 피해 x step^파밍판, 상한) 추가. 결정적. 다시 돌리기: python -X utf8 BalanceData/r9/theory/theory9r5.py → theory9r5_out.txt/json
핵심 바뀐 점
  1. 사람 트리 진행을 따로 둔다: 사람 누적 수입(봇의 0.6배)이 봇 k10의 누적 '트리 구매액'에 닿은 봇 판 m의 트리 상태를 쓴다.
     → 싱크 열림도 DB 규칙대로(투지 = ps_atk4 최대, 위력·기세 = fest 최대, 질주·동료 = ps_ball3 최대 — 사람 트리 판 m 기준)
     → 파밍 판에도 트리를 사면 힘이 오른다.
  2. 사람 보스 D(표시 n) = c × Pb(n) × [T(m)/T(n)] × 싱크 힘 ÷ 실효 체력
     Pb(n) = 봇 k10 보스 판 DPS×(판 시간−2) ÷ (1+0.07·봇 투지),  T = (투지 뺀 공격력) × 수확기^0.7 × (1 + 0.03 × 후반 스킬 레벨 합)  ← 가정
     실효 체력: 90 환상 = 기록 maxHp ÷ 본체(×1.97), 70 청소부 = × (1 + 0.01 × (판 시간−2) × 0.7)
  3. 싱크 효과는 코드대로: 속도·스킬 피해는 합에 더함(속도 1.36 + 0.01L, 스킬 피해 1.25 + 0.10L)
  4. 파밍 1판 성장 ≈ (한 판 수입 ÷ 그 싱크에 쓴 누적 골드) ÷ ln(누적 골드 ÷ 시작가) → 시작가를 '그 허들 직전 수입'에 맞춰 엇갈리게 둔다(새로 열린 싱크가 성장을 만든다)
"""
import csv, json, math, os, random, statistics as S
HERE = os.path.dirname(os.path.abspath(__file__)); TD = os.path.join(HERE, '..', 'test', 'T9-0')
DB = json.load(open(os.path.join(HERE, '..', '..', '..', 'Assets', 'Resources', 'DB', 'db.json'), encoding='utf-8'))
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a))
COST = {c['Cost_ID']: [c[f'Cost_Lv{i}'] for i in range(1, 5)] for c in DB['Upgrade_Cost']}
UPC = {u['Upg_Key']: COST.get(u['Cost_ID']) for u in DB['Upgrade']}
SKL = {'bolt', 'tornado', 'shock', 'fest', 'clone', 'split', 'burst', 'pierce', 'sharpen', 'barclone', 'accel', 'magnet', 'clear'}
def zmul(n): e = 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4; return 10 ** e * 1.01 ** (n - 1)
def hp_now(n): return 40 * zmul(n) * 210

def load(s):
    R = {}; La = Lg = 0; spend = 0.0; sk = 0; op = {}
    for r in csv.DictReader(open(os.path.join(TD, f'rounds_k10_s{s}.csv'), encoding='utf-8')):
        n = int(r['stage']) - 2
        atk = float(r['atk']); A0 = atk / (1 + 0.07 * La)
        R[n] = dict(boss=r['bossFlag'] == '1', net=float(r['net']), La=La, Lg=Lg, mhp=float(r['bossMaxHp'] or 0), dps=float(r['grossDps'] or 0),
                    dur=float(r['dur']), T=A0 * int(r['balls']) ** 0.7 * (1 + 0.03 * sk), spend=spend)
        for b in filter(None, r['bought'].split(';')):
            k, lv = b.split(':'); a, c = (int(x) for x in lv.split('>'))
            if k == 'inf_atk': La = c; continue
            if k == 'inf_gold': Lg = c; continue
            pr = UPC.get(k)
            if pr: spend += sum(math.ceil(pr[i] * 0.9) for i in range(a, c) if i < len(pr))
            if k in SKL: sk += c - a
            if k in ('fest', 'ps_ball3', 'ps_atk4', 'ed_score3p') and c == (3 if k in ('ps_atk4', 'ed_score3p') else 4): op.setdefault(k, n)
        R[n]['spendAfter'] = spend
    return R, op
K, OP = {}, {}
for s in range(1, 7): K[s], OP[s] = load(s)
C = 0.234
P('## 0. 봇 k10 트리: fest 최대 ' + ' '.join(str(OP[s]['fest']) for s in K) + ' · ps_ball3 최대 ' + ' '.join(str(OP[s]['ps_ball3']) for s in K) +
  ' · ps_atk4 최대 ' + ' '.join(str(OP[s]['ps_atk4']) for s in K) + f' · 트리 총액(시드 1) {K[1][100]["spend"]:.3g}')
P('   T(트리 힘 지표) 60 대비 vs Pb(보스 판 실측, 투지 뺌) 60 대비 — T가 Pb 성장을 얼마나 설명하는지: ' + ' · '.join(
    f"{n}: T ×{S.median(K[s][n]['T']/K[s][60]['T'] for s in K):.2f} / Pb ×{S.median(K[s][n]['dps']*(K[s][n]['dur']-2)/(1+0.07*K[s][n]['La'])/(K[s][60]['dps']*(K[s][60]['dur']-2)) for s in K):.2f}" for n in (70, 80, 90, 100)))

def sinks(cfg):
    return cfg
BASE_SINKS = {   # key: [시작가 DB, 가격비, 레벨당, 상한, 열림(트리 조건 키)]
    'inf_atk': [3.63e12, 1.06, 0.15, None, 'ps_atk4'], 'inf_gold': [3.63e12, 1.06, 0.04, None, 'ps_atk4'],
    'inf_sdmg': [5e11, 1.08, 0.10, None, 'fest'], 'inf_srate': [5e11, 1.15, 0.02, 50, 'fest'],
    'inf_spd': [5e11, 1.12, 0.01, 50, 'ps_ball3'], 'inf_ball': [3e13, 3.0, 1.0, 6, 'ps_ball3']}
def power(L, W=0.5, SK=None):
    e = {k: v[2] for k, v in (SK or BASE_SINKS).items()}
    sp = (1.36 + e['inf_spd'] * L['inf_spd']) / 1.36; sd = (1.25 + e['inf_sdmg'] * L['inf_sdmg']) / 1.25
    return (1 + e['inf_atk'] * L['inf_atk']) * ((8 + L['inf_ball']) / 8) ** 0.7 * (1 + W * ((1 + e['inf_srate'] * L['inf_srate']) * sd - 1)) * sp ** 0.7

def run(SK, hp, players=1500, seed=11, farm=2, sp=0.70, sn=0.73, inc=0.6, tg=1.0, bot=False, rmin=1.35, horizon=100, W=0.5, stat=None, grit=(1.05, 3.0, 'farm')):
    rng = random.Random(seed); res = {n: [0, 0, []] for n in range(60, horizon + 1, 10)}; T = []; grow = {n: [] for n in res}
    for p in range(players):
        s = 1 + p % 6; R = K[s]; z = 0 if bot else rng.gauss(0, sp)
        L = {k: 0 for k in SK}; tree = R[50]['spend']; t = 0.0; gold = 0.0
        def m_of():   # 사람 트리 판: 봇 누적 트리 구매액이 tree 이하인 마지막 판
            m = 50
            while m < 100 and R[m + 1]['spend'] <= tree: m += 1
            return m
        def opened(k, m): return m >= OP[s][SK[k][4]]
        def shop(n):
            nonlocal gold, tree
            m = m_of(); need = R[100]['spend'] - tree
            if need > 0:   # 트리를 먼저 산다(가장 싼 것부터 = 트리 칸이 대부분 싱크보다 쌈)
                use = min(gold, need); tree += use; gold -= use; m = m_of()
            while True:
                best = None; bc = float('inf')
                for k, (p0, kk, e, cap, opk) in SK.items():
                    if not opened(k, m) or (cap is not None and L[k] >= cap): continue
                    c = math.ceil(p0 * kk ** L[k] * 0.9)
                    if c < bc: best, bc = k, c
                if best is None or bc > gold: return
                gold -= bc; L[best] += 1
        def income(n):
            mm = n if not R[n]['boss'] else n - 1
            return inc * tg * R[mm]['net'] * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * R[mm]['Lg'])
        def D(n, g=0):
            b = R[n]; m = m_of()
            pb = b['dps'] * (b['dur'] - 2) / (1 + 0.07 * b['La'])
            need = hp[n] * (b['mhp'] / hp_now(n))
            if n % 50 == 20: need *= 1 + 0.01 * (b['dur'] - 2) * (0.1 if bot else 0.7)
            return (1.0 if bot else C) * pb * (R[m]['T'] / b['T']) * power(L, W, SK) / need * min(grit[1], grit[0] ** g)
        for n in range(51, horizon + 1):
            if n % 10 or n < 60:
                gold += income(n); shop(n); t += rmin; continue
            fails = rounds = g = 0
            while True:
                rounds += 1
                if math.log(D(n, g)) + z + rng.gauss(0, sn) >= 0 or fails > 300: break
                fails += 1
                g += grit[2] if isinstance(grit[2], (int, float)) else (1 if grit[2] == 'all' else 0)   # 진 판의 끈기 몫(파밍 판 = 1)
                for _ in range(farm):
                    d0 = D(n, g); gold += income(n); shop(n); rounds += 1; g += 1
                    if p < 300: grow[n].append(D(n, g) / d0)
            gold += 2 * income(n); shop(n)
            q = res[n]; q[0] += fails; q[1] += fails + 1; q[2].append((rounds - 1) * rmin); t += rounds * rmin
        T.append(t)
        if stat is not None and p < 300: stat.append(dict(L))
    o = {n: dict(fail=q[0] / q[1], med=S.median(q[2]), mean=S.mean(q[2]), p90=sorted(q[2])[int(.9 * len(q[2]))],
                 stuck=sum(x > 60 for x in q[2]) / len(q[2]), g=(math.exp(S.mean([math.log(x) for x in grow[n]])) if grow[n] else float('nan'))) for n, q in res.items()}
    return o, S.median(T), sorted(T)[int(.9 * len(T))]
def show(lab, r, t0=78):
    o, tm, t9 = r
    P(f'  {lab}: 100 도달 {tm + t0:.0f}분(90% {t9 + t0:.0f}) | 허들 중앙/평균/90% ' + ' · '.join(f'{n}:{v["med"]:.0f}/{v["mean"]:.0f}/{v["p90"]:.0f}' for n, v in o.items()) +
      ' | 60분+ 막힘 ' + ' · '.join(f'{n}:{v["stuck"]:.1%}' for n, v in o.items()) + ' | 판당 실패 ' + ' · '.join(f'{n}:{v["fail"]:.0%}' for n, v in o.items()) +
      ' | 파밍 1판 D ' + ' · '.join(f'{n}:×{v["g"]:.3f}' for n, v in o.items()))

TGT = {60: 4, 70: 10, 80: 13, 90: 16, 100: 20}   # 허들 평균(분): 60 쉬운 허들, 70부터 점점 길게
def tune(SK, **kw):
    hp = {n: hp_now(n) for n in TGT}
    for n in sorted(TGT):
        lo, hi = hp[n] * 0.02, hp[n] * 500
        for _ in range(22):
            mid = math.sqrt(lo * hi); hp[n] = mid
            m = run(SK, hp, players=360, horizon=n, **kw)[0][n]['mean']
            if m < TGT[n]: lo = mid
            else: hi = mid
        hp[n] = float('%.3g' % math.sqrt(lo * hi))
    return hp

R5 = {'inf_atk': [3.63e12, 1.06, 0.15, None, 'ps_atk4'], 'inf_gold': [3.63e12, 1.06, 0.04, None, 'ed_score3p'],   # verify#32: 풍년 열림 = ed_score3p
      'inf_spd': [1e12, 1.10, 0.01, 50, 'ps_ball3'], 'inf_sdmg': [3e12, 1.10, 0.20, None, 'fest'],
      'inf_srate': [1e13, 1.12, 0.04, 50, 'fest'], 'inf_ball': [2e13, 2.0, 1.0, 6, 'ps_ball3']}
HP4 = {60: 8.87e5, 70: 2.91e6, 80: 7.15e7, 90: 3.01e8, 100: 7.18e8}
NOG = (1.0, 1.0, 'farm')
P(chr(10) + '## 0b. ed_score3p 최대(봇 판) ' + ' '.join(str(OP[s].get('ed_score3p')) for s in K))
P(chr(10) + '## 1. 9-R4 체력 · 끈기 없음(지금 9-R4) — 파밍 판 수')
for f in (0, 1, 2, 4): show(f'파밍 {f}판', run(R5, HP4, farm=f, grit=NOG))
J = {'variants': {}}
VAR = (('A 끈기 x1.05/파밍판 상한 x3', (1.05, 3.0, 'farm')), ('B 끈기 x1.05/판(실패 포함) 상한 x3', (1.05, 3.0, 'all')), ('C 끈기 x1.04/파밍판 상한 x3', (1.04, 3.0, 'farm')))
for lab, gr in VAR:
    hp = tune(R5, grit=gr)
    P(chr(10) + f'## 2. {lab} — 체력 맞춤(허들 평균 목표 {TGT})')
    P('  체력: ' + ' · '.join(f'{n}: {v:.3g}' for n, v in hp.items()) + ' | 인접 비 ' + ' · '.join(f'{n}: x{hp[n]/hp[n-10]:.2f}' for n in (70, 80, 90, 100)))
    mul = {n: hp[n] / (40 * zmul(n)) for n in hp}
    P('  mul: ' + ' · '.join(f'{n}: {v:.0f}' for n, v in mul.items()) + ' | LOOP_BOSS(60 대비): ' + ' · '.join(f'{n}: {mul[n]/mul[60]:.3g}' for n in mul) +
      f' | BossHp(110)>=BossHp(100) 위한 LOOP_LATER 하한 {mul[100]/mul[60]*zmul(100)/zmul(110):.3f}')
    v = {'hp': hp, 'mul': mul, 'farm': {}, 'wt': {}}
    for f in (0, 1, 2, 4):
        r = run(R5, hp, farm=f, grit=gr); show(f'파밍 {f}판 · 세계수 없음', r); v['farm'][f] = {'t100': r[1] + 78, 'h': {str(n): x for n, x in r[0].items()}}
    for stg in (4, 8):
        tg = 1 + 0.92 * 0.12 * (stg - 1); r = run(R5, hp, tg=tg, grit=gr); show(f'세계수 +12%/단계 {stg}단계(판 x{tg:.2f})', r)
        v['wt'][stg] = {'t100': r[1] + 78, 'h': {str(n): x for n, x in r[0].items()}}
    J['variants'][lab] = v
CH = VAR[0]; HP5 = J['variants'][CH[0]]['hp']
P(chr(10) + f'## 3. 민감도 ({CH[0]}, 세계수 없음, 파밍 2판)')
for lab, kw in (('판 흔들림 σ 0.55', dict(sn=0.55)), ('판 흔들림 σ 0.40', dict(sn=0.40)), ('수입 x0.5', dict(inc=0.3)), ('스킬 몫 W 0.25', dict(W=0.25)),
                ('스킬 몫 W 1.0', dict(W=1.0)), ('사람 사이 σ 0.40', dict(sp=0.40)), ('수입 x0.5 · 세계수 4단계', dict(inc=0.3, tg=1 + 0.92 * 0.12 * 3))):
    show(lab, run(R5, HP5, players=800, grit=CH[1], **kw))
P(chr(10) + '## 4. 봇(k10, 9-R5 체력, 끈기 A)')
show('봇', run(R5, HP5, players=300, bot=True, inc=1.0, rmin=1.16, grit=CH[1]), t0=52)
P(chr(10) + '## 5. 권장 B(이 보스 스테이지에서 보낸 판마다 x1.05, 상한 x3) 재맞춤 — 80 목표 13 → 14(세계수 4단계에서 70 ≤ 80 단조 유지)')
GB = (1.05, 3.0, 'all')
TGT.update({80: 14})
HPB = tune(R5, grit=GB)
MULB = {n: HPB[n] / (40 * zmul(n)) for n in HPB}
LB = {n: MULB[n] / MULB[60] for n in MULB}
late_min = LB[100] * zmul(100) / zmul(110)
LATER = math.ceil(late_min * 10) / 10
P('  체력: ' + ' · '.join(f'{n}: {v:.3g}' for n, v in HPB.items()) + ' | 인접 비 ' + ' · '.join(f'{n}: x{HPB[n]/HPB[n-10]:.2f}' for n in (70, 80, 90, 100)))
P('  mul: ' + ' · '.join(f'{n}: {v:.0f}' for n, v in MULB.items()) + ' | LOOP_BOSS: ' + ' · '.join(f'{n}: {LB[n]:.3g}' for n in LB) +
  f' | LOOP_LATER 하한 {late_min:.3f} → 권장 {LATER} | 110 체력 {40*zmul(110)*MULB[60]*LATER:.3g} (100 {HPB[100]:.3g})')
JB = {'hp': HPB, 'mul': MULB, 'loop_boss': LB, 'loop_later': LATER, 'later_min': late_min, 'farm': {}, 'wt': {}, 'sens': {}}
for f in (0, 1, 2, 4):
    r = run(R5, HPB, farm=f, grit=GB); show(f'파밍 {f}판 · 세계수 없음', r); JB['farm'][f] = {'t100': r[1] + 78, 'h': {str(n): x for n, x in r[0].items()}}
for stg in (4, 8):
    for f in (0, 2):
        tg = 1 + 0.92 * 0.12 * (stg - 1); r = run(R5, HPB, tg=tg, farm=f, grit=GB); show(f'세계수 +12%/단계 {stg}단계(판 x{tg:.2f}) · 파밍 {f}판', r)
        JB['wt'][f'{stg}_{f}'] = {'t100': r[1] + 78, 'h': {str(n): x for n, x in r[0].items()}}
r = run(R5, HPB, grit=NOG); show('끈기 끔(같은 체력)', r)
for lab, kw in (('σ 0.55', dict(sn=0.55)), ('σ 0.40', dict(sn=0.40)), ('수입 x0.5', dict(inc=0.3)), ('W 0.25', dict(W=0.25)), ('W 1.0', dict(W=1.0)),
                ('수입 x0.5 · 세계수 4단계', dict(inc=0.3, tg=1 + 0.92 * 0.12 * 3)), ('W 0.25 · 세계수 4단계', dict(W=0.25, tg=1 + 0.92 * 0.12 * 3))):
    r = run(R5, HPB, players=800, grit=GB, **kw); show(lab, r); JB['sens'][lab] = {'t100': r[1] + 78, 'h': {str(n): x for n, x in r[0].items()}}
P('  봇 시드별 보스 판 D(= Pb x 싱크 힘 ÷ 실효 체력, 첫 도전) — 1 이상이면 한 번에 잡음: ')
for n in (60, 70, 80, 90, 100):
    ds = []
    for s0 in range(1, 7):
        b = K[s0][n]; need = HPB[n] * (b['mhp'] / hp_now(n))
        if n % 50 == 20: need *= 1 + 0.01 * (b['dur'] - 2) * 0.1
        pb = b['dps'] * (b['dur'] - 2) / (1 + 0.07 * b['La']); ds.append(pb / need * (1 + 0.15 * b['La']))
    P(f'    {n}: ' + ' '.join(f'{d:.2f}' for d in ds) + f' (중앙 {S.median(ds):.2f})')
show('봇', run(R5, HPB, players=300, bot=True, inc=1.0, rmin=1.16, grit=GB), t0=52)
J['B'] = JB
json.dump(J, open(os.path.join(HERE, 'theory9r5_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
open(os.path.join(HERE, 'theory9r5_out.txt'), 'w', encoding='utf-8').write(chr(10).join(OUT) + chr(10))
print(chr(10).join(OUT))
