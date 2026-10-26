# -*- coding: utf-8 -*-
"""9-R3: 사용자 결정 3(사람 c 모델 · 시간 허들 우선·점점 길게 · 싱크 상한 넓게 · 표시 100까지) + verify#29 반영. 결정적.
다시 돌리기: python -X utf8 BalanceData/r9/theory/theory9r3.py → theory9r3_out.txt, theory9r3_out.json
사람 보스 D (verify 방식, 표시마다 봇 실측):
  Pb(n) = 봇 k10 표시 n 보스 판의 (DPS × (판 시간 − 2)) ÷ (1 + 0.07·봇 투지 레벨)   ← 트리·스킬·수확기 성장 포함, 싱크(투지) 몫만 뺌
  사람 D = c × Pb(n) × 사람 싱크 힘 ÷ 새 체력,  c = 0.234 (표시 50, 봇 투지 0)
  사람 싱크 힘 = (1+0.15·투지) × ((8+수확기싱크)/8)^0.7 × (1 + W((1+발동률)(1+피해) − 1)) × (1+속도)^0.7,  W 0.5
  사람 판 수입 = 0.6 × 봇 k10 일반 판 중앙 × (1+0.04·풍년_사람)/(1+0.04·풍년_봇) × 세계수 골드 곱
허들 = (진 판 + 파밍 2판씩) × 1.35분, 사람 사이 ln D 0.70 · 판마다 0.73
"""
import csv, json, math, os, random, statistics as S
from statistics import NormalDist
HERE = os.path.dirname(os.path.abspath(__file__)); TD = os.path.join(HERE, '..', 'test', 'T9-0')
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a))

def load(k):
    net, pb, lg, botd = {}, {}, {}, {}
    for s in range(1, 7):
        La = Lg = 0
        for r in csv.DictReader(open(os.path.join(TD, f'rounds_{k}_s{s}.csv'), encoding='utf-8')):
            n = int(r['stage']) - 2
            if r['bossFlag'] == '1':
                dmg = float(r['grossDps']) * (float(r['dur']) - 2)
                pb.setdefault(n, []).append(dmg / (1 + 0.07 * La)); botd.setdefault(n, []).append(dmg / float(r['bossMaxHp']))
            else:
                net.setdefault(n, []).append(float(r['net'])); lg.setdefault(n, []).append(Lg)
            for b in filter(None, r['bought'].split(';')):
                key, lv = b.split(':'); c = int(lv.split('>')[1])
                if key == 'inf_atk': La = c
                if key == 'inf_gold': Lg = c
    med = lambda d: {n: S.median(v) for n, v in d.items()}
    return med(net), med(pb), med(lg), med(botd)
NET, PB, LG, BOTD = load('k10')
C = 0.234
def zmul(n): e = 0 if n < 20 else 1 if n < 45 else 2 if n < 75 else 3 if n < 105 else 4; return 10 ** e * 1.01 ** (n - 1)
def net_at(n): return NET[n] if n in NET else NET[n - 1]
P('## 1. 봇 k10 보스 힘(싱크 뺌) Pb, 표시 60 대비: ' + ' · '.join(f'{n}: ×{PB[n]/PB[60]:.2f}' for n in (60, 70, 80, 90, 100)))
P('   사람 D(c 모델, 지금 체력 LOOP 210, 사람 싱크 0): ' + ' · '.join(f'{n}: {C*PB[n]/(40*zmul(n)*210):.2f}' for n in (60, 70, 80, 90, 100)))

# ---- 싱크 (9-R3 가격 사다리: 바다 진입 뒤 한꺼번에 몰아 사지 않게, 상한 넓게) ----
SINKS = {   # key: (시작가 DB, 가격비, 레벨당, 상한 레벨, 열림 표시)
    'inf_atk':   (3.63e12, 1.06, 0.15, None, 64),
    'inf_gold':  (3.63e12, 1.06, 0.04, None, 64),
    'inf_sdmg':  (5.0e11, 1.08, 0.10, None, 58),
    'inf_srate': (5.0e11, 1.15, 0.02, 50, 58),
    'inf_spd':   (5.0e11, 1.12, 0.01, 50, 60),
    'inf_ball':  (3.0e13, 3.0, 1.0, 6, 72),
}
W = 0.5
def power(L):
    return ((1 + 0.15 * L['inf_atk']) * ((8 + L['inf_ball']) / 8) ** 0.7 *
            (1 + W * ((1 + 0.02 * L['inf_srate']) * (1 + 0.10 * L['inf_sdmg']) - 1)) * (1 + 0.01 * L['inf_spd']) ** 0.7)
def price(k, L): p0, kk, _, cap, _ = SINKS[k]; return math.ceil(p0 * kk ** L * 0.9)
def buy(L, gold, n):
    while True:
        best, bc = None, float('inf')
        for k, (p0, kk, e, cap, op) in SINKS.items():
            if n < op or (cap is not None and L[k] >= cap): continue
            c = price(k, L[k])
            if c < bc: best, bc = k, c
        if best is None or bc > gold: return gold
        gold -= bc; L[best] += 1

def run(hp, players=1500, seed=5, tree=1.0, human=True, farm=2, horizon=100):
    rng = random.Random(seed); res = {n: [] for n in range(60, horizon + 1, 10)}; FT = {n: [0, 0] for n in res}; tot = []; lv = {n: [] for n in res}; grow = {n: [] for n in res}
    cc = C if human else 1.0; inc_mul = 0.6 if human else 1.0
    for pl in range(players):
        person = rng.gauss(0, 0.70) if human else rng.gauss(0, 0.2); L = {k: 0 for k in SINKS}; gold = 0.0; t = 0.0
        for n in range(51, horizon + 1):
            def inc(): return inc_mul * tree * net_at(min(n, 100)) * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * LG.get(min(n, 100), LG[99]))
            if n % 10 == 0 and n >= 60:
                rounds = tries = 0
                while True:
                    tries += 1; rounds += 1
                    lnD = math.log(cc * PB[n] * power(L) / hp[n]) + person + rng.gauss(0, 0.73)
                    if lnD >= 0 or tries > 100: break
                    for _ in range(farm):
                        p0 = power(L); gold += inc(); gold = buy(L, gold, n); rounds += 1
                        if pl < 300: grow[n].append(power(L) / p0)
                FT[n][0] += tries - 1; FT[n][1] += tries
                if pl < 300: lv[n].append(dict(L))
                gold += 2 * inc(); gold = buy(L, gold, n); res[n].append((rounds - 1) * 1.35); t += rounds * 1.35
            else:
                gold += inc(); gold = buy(L, gold, n); t += (1.35 if human else 1.16)
        tot.append(t)
    h = {n: (S.median(v), S.mean(v), sorted(v)[int(0.9 * len(v))]) for n, v in res.items()}
    fr = {n: a / b for n, (a, b) in FT.items()}
    lvm = {n: {k: S.median([x[k] for x in v]) for k in SINKS} for n, v in lv.items()}
    gr = {n: (math.exp(S.mean([math.log(x) for x in v])) if v else float('nan')) for n, v in grow.items()}
    return h, fr, S.median(tot), lvm, gr

# 목표: 허들 평균(파밍하며 넘는 시간) 60 9 → 100 21분, 점점 길게 (모바일 방치형: 지역/챕터 보스 벽이 뒤로 갈수록 길어짐)
TGT = {60: 9, 70: 12, 80: 15, 90: 18, 100: 21}
def tune(tree=1.0):
    hp = {n: 40 * zmul(n) * 210 for n in TGT}
    for n in sorted(TGT):
        lo, hi = hp[n] * 0.05, hp[n] * 400
        for _ in range(24):
            mid = math.sqrt(lo * hi); hp[n] = mid
            m = run(hp, players=500, horizon=n, tree=tree)[0][n][1]
            if m < TGT[n]: lo = mid
            else: hi = mid
        hp[n] = float('%.3g' % math.sqrt(lo * hi))
    return hp
HP = tune()
mul = {n: HP[n] / (40 * zmul(n)) for n in HP}
P(f'\n## 2. 허들 평균 목표 {TGT}분(c 모델, 세계수 없음)에 맞는 체력')
P('  체력: ' + ' · '.join(f'{n}: {v:.3g}' for n, v in HP.items()) + ' · 인접 비: ' + ' · '.join(f'{n}: ×{HP[n]/HP[n-10]:.2f}' for n in (70, 80, 90, 100)))
P('  mul(= 체력 ÷ 40·ZoneMul): ' + ' · '.join(f'{n}: {v:.0f}' for n, v in mul.items()))
J = {'HP': HP, 'mul': mul}
for tree, lab in ((1.0, '세계수 없음'), (1.18, '세계수 4단계 ×1.18'), (1.42, '세계수 8단계 ×1.42')):
    h, fr, t, lvm, gr = run(HP, tree=tree)
    P(f'\n### 사람 c · {lab}: 51~100 {t:.0f}분 → 표시 100 약 {78 + t:.0f}분')
    P('  허들 중앙/평균/90%(분): ' + ' · '.join(f'{n}: {v[0]:.0f}/{v[1]:.0f}/{v[2]:.0f}' for n, v in h.items()))
    P('  판당 실패율: ' + ' · '.join(f'{n}: {v:.0%}' for n, v in fr.items()))
    if tree == 1.0:
        P('  파밍 1판 힘 증가: ' + ' · '.join(f'{n}: ×{v:.3f}' for n, v in gr.items()))
        P('  보스 처치 때 싱크 레벨(중앙): ' + ' | '.join(f"{n}: 투지{v['inf_atk']:.0f} 풍년{v['inf_gold']:.0f} 위력{v['inf_sdmg']:.0f} 기세{v['inf_srate']:.0f} 질주{v['inf_spd']:.0f} 동료{v['inf_ball']:.0f}" for n, v in lvm.items()))
    J[lab] = {'t': t, 'h': {str(n): v for n, v in h.items()}, 'fail': {str(n): v for n, v in fr.items()}}
# 파밍 판 수 민감도
for f in (0, 1, 4):
    h, fr, t, _, _ = run(HP, farm=f, players=800)
    P(f'  (파밍 {f}판/실패) 허들 평균 ' + ' · '.join(f'{n}: {v[1]:.0f}' for n, v in h.items()) + f' · 51~100 {t:.0f}분')
# 봇
h, fr, t, lvm, _ = run(HP, human=False, players=300)
P(f'\n### 봇(k10 수입·D, 싱크 6개): 51~100 {t:.0f}분 → 표시 100 약 {52 + t:.0f}분 · 도전 수 ' + ' · '.join(f'{n}: {1/(1-v):.2f}번' for n, v in fr.items()))
# 100 뒤 벽 시점(체력: 100 mover mul 고정 + ZoneMul, 수입: 100 값 ×1.01^·폐허 ×10)
def net_ext(n): return net_at(100) * 1.01 ** (n - 100) * (10 if n >= 105 else 1)
P('\n## 3. 100 뒤(모바일 개편 전, 참고): 체력 = 40·ZoneMul·mul(100) 고정, 보스 힘 Pb는 100 값 그대로(트리 성장 없음)')
rng = random.Random(3); L = {k: 0 for k in SINKS}; gold = 0.0
for n in range(51, 301):
    nn = min(n, 100)
    gold += 0.6 * (net_at(nn) if n <= 100 else net_ext(n)) * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * LG.get(nn, LG[99])); gold = buy(L, gold, n)
    if n % 50 == 0 and n > 100:
        hpn = 40 * zmul(n) * mul[100]; D = C * PB[100] * power(L) / hpn
        P(f'  {n}: 사람 가운데 D {D:.2f} (투지 {L["inf_atk"]}) — 1 아래면 허들이 파밍 판 수에 크게 기댐')
json.dump(J, open(os.path.join(HERE, 'theory9r3_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
open(os.path.join(HERE, 'theory9r3_out.txt'), 'w', encoding='utf-8').write(chr(10).join(OUT) + chr(10))
print(chr(10).join(OUT))
