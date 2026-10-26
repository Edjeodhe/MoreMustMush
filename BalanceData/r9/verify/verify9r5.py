# -*- coding: utf-8 -*-
"""9-R5 검증 (verify, manager#48). theory9r5*.py를 쓰지 않는다. 결정적.
다시 돌리기: python -X utf8 BalanceData/r9/verify/verify9r5.py → verify9r5_out.txt
verify9r4.py의 앞부분(원 기록·트리 지출·연속 트리 판·싱크)을 그대로 불러 쓰고, 몬테카를로 고리는 끈기를 넣어 새로 짰다.
끈기(제안 코드 RoundSim.Effects.cs:419~420 그대로): 진 판(끝까지 감) +1, 보스 스테이지 파밍 판 +2, 지금 끝내기 0, 처치하면 0.
  보스 피해 × min(3, 1.05^(점수/2)) — 반복 보스(표시 60~)만.
"""
import io, math, os, random, statistics as S, contextlib
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'verify9r4.py'), encoding='utf-8').read()
NS = {'__file__': os.path.join(HERE, 'verify9r4.py'), '__name__': 'v4'}
with contextlib.redirect_stdout(io.StringIO()):
    exec(compile(src.split("P('\\n## 3. 재현")[0], 'verify9r4_head', 'exec'), NS)
K, OP, SINK, power, m_of, T_at, Tpow, zmul, hp_now = (NS[k] for k in ('K', 'OP', 'SINK', 'power', 'm_of', 'T_at', 'Tpow', 'zmul', 'hp_now'))
SINK['inf_gold'][4] = 'ed_score3p'
OUT = []
def P(*a): OUT.append(' '.join(str(x) for x in a)); print(OUT[-1])

BASE = 157; LOOP_BOSS = [1.00, 3.75, 7.98, 32.2, 76.7]; LOOP_LATER = 7.0
def boss_mul(n):
    k = n // 10 - 1; lap = k // 5
    return None if lap == 0 else BASE * (LOOP_BOSS[k % 5] if lap == 1 else LOOP_LATER)
def boss_hp(n): return 40 * zmul(n) * boss_mul(n)
P('## 1. 체력 60~300 (BOSS_LOOP_HP_MUL 157 × LOOP_BOSS, 110~ × LOOP_LATER 7.0)')
prev = None; row = []; bad = []
for n in range(60, 310, 10):
    h = boss_hp(n); row.append(f'{n}:{h:.3g}' + (f'(×{h/prev:.3f})' if prev else '')); bad += [n] if prev and h <= prev else []; prev = h
P('  ' + ' · '.join(row)); P(f'  감소 지점: {bad or "없음"} · LATER 하한 {LOOP_BOSS[4] / (10 * 1.01 ** 10):.3f} · 110/100 ×{boss_hp(110)/boss_hp(100):.4f}')
P('  theory 표(1.13e6·4.68e6·1.10e8·4.90e8·1.29e9)와 비: ' + ' · '.join(f'{n}: {boss_hp(n)/v:.3f}' for n, v in ((60, 1.13e6), (70, 4.68e6), (80, 1.10e8), (90, 4.90e8), (100, 1.29e9))))
HP5 = {n: boss_hp(n) for n in (60, 70, 80, 90, 100)}

def grit_mul(pts, step=1.05, cap=3.0): return min(cap, step ** (pts / 2))
def sim(hp=HP5, players=1500, seed=21, farm=2, sp=0.70, sn=0.73, inc=0.6, tg=1.0, ks=0.03, W=0.5, bot=False, rmin=1.35,
        grit=True, lose_pts=1, farm_pts=2, lose_inc=0.8, glog=None, gpts=None, byseed=None):
    rng = random.Random(seed); res = {n: [0, 0, []] for n in hp}; T = []
    for p in range(players):
        s = 1 + p % 6; R = K[s]; z = 0 if bot else rng.gauss(0, sp)
        L = {k: 0 for k in SINK}; tree = R[50]['spent']; gold = 0.0; t = 0.0; total = R[100]['spent']
        def shop():
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
        def income(n):
            mm = n if not R[n]['boss'] else n - 1
            return inc * tg * R[mm]['net'] * (1 + 0.04 * L['inf_gold']) / (1 + 0.04 * R[mm]['Lg'])
        def D(n, pts):
            b = R[n]; pb = b['dps'] * (b['dur'] - 2) / (1 + 0.07 * b['La'])
            need = hp[n] * b['mhp'] / hp_now(n)
            if n == 70: need *= 1 + 0.01 * (b['dur'] - 2) * (0.1 if bot else 0.7)
            tr = 1.0 if bot else T_at(R, m_of(R, tree), ks) / Tpow(b, ks)
            return (1.0 if bot else 0.234) * pb * tr * power(L, W) / need * (grit_mul(pts) if grit else 1)
        for n in range(51, 101):
            if n % 10 or n < 60:
                gold += income(n); shop(); t += rmin; continue
            fails = rounds = 0; pts = 0
            while True:
                rounds += 1
                if math.log(D(n, pts)) + z + rng.gauss(0, sn) >= 0 or fails > 300: break
                fails += 1; pts += lose_pts
                gold += income(n) * lose_inc; shop()          # 진 판 골드 = 직전 일반 판 × lose_inc (코드 BOSS_FAIL_GOLD 0.8 × 보스 판/일반 판 비, 미측정)
                for _ in range(farm):
                    d0 = D(n, pts); gold += income(n); shop(); pts += farm_pts; rounds += 1
                    if glog is not None: glog.setdefault(n, []).append(D(n, pts) / d0)
            if gpts is not None: gpts.setdefault(n, []).append(pts)
            gold += 2 * income(n); shop()
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
def gl(G): return ' · '.join(f'{n}: ×{math.exp(S.mean(math.log(x) for x in v)):.3f}' for n, v in sorted(G.items()))
TG4 = 1 + 0.92 * 0.12 * 3; TG8 = 1 + 0.92 * 0.12 * 7

P('\n## 2. 재현 + 파밍 판 수 (끈기 0.5: 진 판 +1·파밍 판 +2)')
for tg, lab in ((1.0, '없음'), (TG4, '4단계'), (TG8, '8단계')):
    for f in (0, 1, 2, 4):
        G = {}; GP = {}
        show(f'세계수 {lab} · 파밍 {f}판', sim(tg=tg, farm=f, glog=G, gpts=GP, players=1500 if f == 2 else 1000))
        if f == 2:
            P('    파밍 1판 D: ' + gl(G))
            P('    처치 때 끈기 점수 중앙/90%/상한(44) 도달: ' + ' · '.join(f'{n}: {S.median(v):.0f}/{sorted(v)[int(.9*len(v))]}/{sum(x>=44 for x in v)/len(v):.1%}' for n, v in sorted(GP.items())))
P('\n## 3. 끈기 끔 (같은 체력 = 끈기 없이 체력만 들어간 빌드)')
show('세계수 없음', sim(grit=False)); show('4단계', sim(grit=False, tg=TG4))
P('\n## 4. 시드별 (세계수 없음 · 파밍 2판 · 사람 250명/시드) + 난수 시드')
BS = {}; sim(byseed=BS)
P('  ' + ' | '.join(f'{n}: ' + '/'.join(f'{S.mean(BS[(n, s)]):.0f}' for s in range(1, 7)) for n in (60, 70, 80, 90, 100)))
P('  난수 시드: ' + ' | '.join(f'seed {sd}: ' + '/'.join(f'{v["mean"]:.0f}' for v in sim(seed=sd, players=1000)[0].values()) for sd in (1, 2, 3)))
P('\n## 5. 민감도 (세계수 없음 / 4단계)')
for lab, kw in (('σ판 0.40', dict(sn=0.40)), ('W 0.25', dict(W=0.25)), ('수입 ×0.5', dict(inc=0.3)), ('수입 ×2', dict(inc=1.2)), ('k 0', dict(ks=0.0))):
    show(lab + ' · 없음', sim(players=800, **kw)); show(lab + ' · 4단계', sim(players=800, tg=TG4, **kw))
P('\n## 6. 끈기 변형 (4단계 · 파밍 0판/2판 100 도달·막힘)')
for lp, fp, lab in ((1, 2, '제안 +1/+2'), (2, 2, '진 판 +2'), (0, 2, '진 판 0')):
    a = sim(tg=TG4, farm=0, lose_pts=lp, farm_pts=fp, players=800); b = sim(tg=TG4, farm=2, lose_pts=lp, farm_pts=fp, players=800)
    P(f'  {lab}: 0판 {a[1]+78:.0f}분·100 막힘 {a[0][100]["stuck"]:.1%} / 2판 {b[1]+78:.0f}분·100 막힘 {b[0][100]["stuck"]:.1%}')
P('\n## 6b. 진 판 골드(직전 일반 판 대비) 민감도 - theory는 0, 위 표는 0.8')
for li in (0.0, 0.3, 0.5, 0.8):
    for tg, lab in ((1.0, '없음'), (TG4, '4단계')):
        a = sim(tg=tg, farm=0, lose_inc=li, players=800); b = sim(tg=tg, farm=2, lose_inc=li, players=800)
        P(f'  진 판 골드 {li} · 세계수 {lab}: 바로 재도전 {a[1]+78:.0f}분(100 막힘 {a[0][100]["stuck"]:.1%}) vs 파밍 2판 {b[1]+78:.0f}분(100 막힘 {b[0][100]["stuck"]:.1%})')
P('\n## 7. 봇 (k10, 매 판 재도전, 진 판 +1)')
show('봇', sim(players=300, farm=0, sp=0, sn=0.28, inc=1.0, rmin=1.16, bot=True), t0=52.7)
open(os.path.join(HERE, 'verify9r5_out.txt'), 'w', encoding='utf-8').write('\n'.join(OUT) + '\n')
