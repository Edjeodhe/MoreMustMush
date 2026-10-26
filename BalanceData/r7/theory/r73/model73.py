# 7-3 거대 버섯: 처치율 모델 + 조정안 비교 (theory, 2026-10-21)
# 실행: 프로젝트 루트에서  python -X utf8 BalanceData/r7/theory/r73/model73.py
# 입력: BalanceData/r7/test/T7-4/GXnew_m25·GXold_m25·GX_m29_rounds.csv (거대 강제 판), phase_k06_s*.json (봇 진행),
#       netreg_out.json (netreg.py: 거대가 살아 있던 시간 → 판 net)
# 출력: model73_out.txt, model73_out.json (난수 없음: 같은 입력이면 같은 결과)
#
# 모델: 판마다 거대에게 줄 수 있는 "창(등장 5초 ~ 판 끝) 안 누적 피해" C ~ 로그정규(m(c), s).
#   처치 = C >= H. m·s는 표시 25 측정(이진 결과 288판)에 맞추고, 다른 스테이지는
#   C(c) = C(25) × [K_state(c)·공격력(c)·W(c)] / [K_state(25)·공격력(25)·W(25)] 로 옮긴다 (6회차 보스 DPS 근사와 같은 꼴).
#   s에는 시드(플레이어 상태) 차이와 판 차이가 함께 들어 있다.
import json, math, sys, statistics as S
sys.path.insert(0, 'BalanceData/r7/theory/r73')
from explore import load

OUT = 'BalanceData/r7/theory/r73/'
T4 = 'BalanceData/r7/test/T7-4/'
lines = []
def P(*a):
    s = ' '.join(str(x) for x in a); lines.append(s); print(s)
def Phi(z): return 0.5 * (1 + math.erf(z / math.sqrt(2)))

# ---------- 게임 상수 (코드·DB 확인값) ----------
TUNED_HP = [1, 4, 12, 40]          # Defs.cs PRESETS["tuned"].hp → TIERS[t].hp
GIANT_K = 40                       # RoundSim.Effects.cs:189  hp = T.hp * 40 * st.shroomHp
SPAWN_T = 5.0                      # 거대 등장 runT 5 (경고 4)
def zone(c):                       # 밤 지역(표시 20~44): ZONE_STEP 10 × STAGE_GROWTH 1.01^(c-1)
    return (10.0 if c >= 20 else 1.0) * 1.01 ** (c - 1)
def stagemul_new(c): return max(1 + 0.03 * (c - 1), min(1 + 0.06 * (c - 1), 2.4))
def stagemul_old(c): return 1 + 0.03 * (c - 1)
KS = {10: 1.07, 20: 28.1, 30: 292.5, 40: 387.0}    # 6회차 K_state 닻 (BalanceData/r6/theory/model.py)
def kstate(x):
    xs = sorted(KS)
    for a, b in zip(xs, xs[1:]):
        if a <= x <= b:
            w = (x - a) / (b - a); return math.exp(math.log(KS[a]) * (1 - w) + math.log(KS[b]) * w)
    return KS[xs[0]] if x < xs[0] else KS[xs[-1]]

# ---------- 1. 봇 진행 (T7-4 시드 1~6: 표시 스테이지별 공격력·판 길이·생긴 버섯 등급 비중) ----------
PH = {}
for s in range(1, 7):
    d = json.load(open(f'{T4}phase_k06_s{s}.json', encoding='utf-8'))
    m = {}
    for i, st in enumerate(d['stageR']):
        c = st - 4
        if d['bossR'][i] or i >= len(d['madeT']): continue
        dur = (d['playR'][i] - d['playR'][i - 1] - 30) if i > 0 else None
        made = d['madeT'][i][:4]; tot = sum(made)
        m[c] = dict(atk=d['atkR'][i], dur=dur, share=[x / tot for x in made] if tot else None)
    PH[s] = m
def med(c, k):
    v = [PH[s][c][k] for s in PH if c in PH[s] and PH[s][c][k]]
    if not v: v = [PH[s][c + 1][k] for s in PH if c + 1 in PH[s] and PH[s][c + 1][k]]   # 보스 판(10·20)은 다음 판 값
    return S.median(v)
def share(c):
    v = [PH[s][c]['share'] for s in PH if c in PH[s] and PH[s][c]['share']]
    return [S.mean(x[t] for x in v) for t in range(4)]
def W(c): return med(c, 'dur') - SPAWN_T
def power(c): return kstate(c) * med(c, 'atk') * W(c)      # 상대 누적 피해 (봇 c 상태가 c 판에서)

# ---------- 2. 표시 25 측정에 맞추기 (이진: 잡았나) ----------
rows25 = [r for tag in ('GXnew_m25', 'GXold_m25') for r in load(tag) if r['gTier'] >= 0]   # GX_m25(K=6)는 GXnew 앞 6판과 같은 판이라 뺀다
rows29 = [r for r in load('GX_m29') if r['gTier'] >= 0]
def nll_bin(rows, m, s, Wref):
    L = 0
    for r in rows:
        Heff = r['gHp'] * Wref / (r['dur'] - SPAWN_T)      # 창 길이 차이(비 32초 대 맑음 35초)를 체력 쪽으로
        p = 1 - Phi((math.log(Heff) - m) / s); p = min(max(p, 1e-9), 1 - 1e-9)
        L -= math.log(p) if r['kt'] is not None else math.log(1 - p)
    return L
best = None
for i in range(0, 241):
    m = 8.0 + i * 0.01
    for j in range(20, 201, 2):
        s_ = j * 0.01
        l = nll_bin(rows25, m, s_, 30.0)
        if best is None or l < best[0]: best = (l, m, s_)
_, M25, SIG = best
P('== 2. 표시 25 맞춤 (거대 강제 288판, 이진 처치)')
P(f' 창 30초 누적 피해 C: 중앙 {math.exp(M25):.0f} (DPS 약 {math.exp(M25) / 30:.0f}/s), 로그 표준편차 {SIG:.2f}')
def pk_rows(rows, m, s, Wref):
    out = {}
    for t in range(4):
        x = [r for r in rows if r['gTier'] == t]
        if not x: continue
        pm = S.mean(1 - Phi((math.log(r['gHp'] * Wref / (r['dur'] - SPAWN_T)) - m) / s) for r in x)
        k = sum(r['kt'] is not None for r in x)
        out[t] = f"측정 {k}/{len(x)}={k / len(x):.2f} 모델 {pm:.2f}"
    return out
P(' 등급별:', pk_rows(rows25, M25, SIG, 30.0))
GR = power(29) / power(25)
P(f'\n 표시 29 예측 (맞추지 않음): C 비 ×{GR:.2f} (K_state ×{kstate(29) / kstate(25):.2f}, 공격력 ×{med(29, "atk") / med(25, "atk"):.2f}, 창 ×{W(29) / W(25):.2f})')
P(' 등급별:', pk_rows(rows29, M25 + math.log(GR * 30 / W(29)), SIG, 30.0))
kt29 = [r['kt'] for r in rows29 if r['gTier'] == 3 and r['kt'] is not None]
D29 = S.median(r['gHp'] for r in rows29 if r['gTier'] == 3) / S.median(kt29)
D25 = math.exp(M25) / 30
GR_obs = D29 * W(29) / (D25 * W(25))
P(f' 표시 29 레전드리 처치 시간 중앙 {S.median(kt29):.1f}초 → DPS 약 {D29:.0f}/s, 25 대비 C ×{GR_obs:.2f} (모델 ×{GR:.2f})')
P(f' → 스테이지당 성장: 모델 ×{GR ** .25:.2f} · 관측 ×{GR_obs ** .25:.2f}. 21~24는 관측 성장을 거꾸로 쓴 값을 감도(더 비관)로 같이 본다')

def mC(c, who='bot', steep=False):
    """표시 c 판의 ln C 중앙 (C = 창 W(c) 안 누적 피해). who = 'bot' 또는 lag(사람: 봇 c-lag 상태로 c 판)"""
    if steep and who == 'bot':
        return M25 + (c - 25) * math.log(GR_obs) / 4 + math.log(W(c) / W(25))
    x = c - (0 if who == 'bot' else who)
    return M25 + math.log(kstate(x) * med(x, 'atk') * W(c) / (kstate(25) * med(25, 'atk') * W(25)))
def pkill(m, H): return 1 - Phi((math.log(H) - m) / SIG)

# ---------- 3. 조정안 ----------
def H_now(t, c): return TUNED_HP[t] * GIANT_K * zone(c)
def mk_a(mul): return lambda t, c: mul[t] * GIANT_K * zone(c)
ONE = lambda c: [1, 1, 1, 1]
OLD = lambda c: [(stagemul_old(c) / stagemul_new(c)) ** t for t in range(4)]
OPTS = {
    '지금': dict(H=H_now, tierw=ONE),
    '(a1) 거대 등급 배수 1·2·3·4': dict(H=mk_a([1, 2, 3, 4]), tierw=ONE),
    '(a2) 거대 등급 배수 1·2·4·6': dict(H=mk_a([1, 2, 4, 6]), tierw=ONE),
    '(a3) 거대 등급 배수 1·2.5·5·8': dict(H=mk_a([1, 2.5, 5, 8]), tierw=ONE),
    '(a4) 거대 등급 배수 1·3·6·12': dict(H=mk_a([1, 3, 6, 12]), tierw=ONE),
    '(b) 거대 종 선택만 옛 stageMul': dict(H=H_now, tierw=OLD),
    '(c) 레전드리 거대는 표시 29부터': dict(H=H_now, tierw=lambda c: [1, 1, 1, 1 if c >= 29 else 0]),
    '(c2) 유니크 거대 25·레전드리 29부터': dict(H=H_now, tierw=lambda c: [1, 1, 1 if c >= 25 else 0, 1 if c >= 29 else 0]),
    '(b+a2) 옛 stageMul + 배수 1·2·4·6': dict(H=mk_a([1, 2, 4, 6]), tierw=OLD),
    '(참고 a1h) 배수 1·2·3·4 + 기본 40→25': dict(H=lambda t, c: [1, 2, 3, 4][t] * 25 * zone(c), tierw=ONE),
}
STAGES = list(range(21, 30))   # 20·30은 보스 판(거대 없음)

# 수입: netreg 기울기 (ln net, 거대가 살아 있던 시간 비율 0→1). 거대는 맞을 때마다 (등급 점수 + 고정)×3 점수 → 살아 있는 동안 점수 스펀지
REG = json.load(open(OUT + 'netreg_out.json', encoding='utf-8'))['slopes']
SLOPE = [REG[f't{t}_alive_slope'][0] for t in range(4)]
GIANT_P = 0.08    # 표시 21~30 봇: ed_giant 미구매 (mid25·mid29 상태 확인) → Lv0 0.08
def alive(m, H):
    # E[min(H/C, 1)]: 피해가 창 안에 고르게 들어간다고 보면 살아 있는 시간 비율
    acc = n = 0
    for i in range(-40, 41):
        z = i / 10; w = math.exp(-z * z / 2); C = math.exp(m + SIG * z)
        acc += w * min(H / C, 1.0); n += w
    return acc / n
def tiers(o, c):
    sh = share(c); w = o['tierw'](c); g = [sh[t] * w[t] for t in range(4)]; tot = sum(g); return [x / tot for x in g]
def evaluate(o, who='bot', steep=False):
    res = {}
    for c in STAGES:
        g = tiers(o, c); m = mC(c, who, steep)
        pk = [pkill(m, o['H'](t, c)) for t in range(4)]
        lnv = sum(g[t] * SLOPE[t] * alive(m, o['H'](t, c)) for t in range(4))
        res[c] = dict(share=g, pk=pk, all=sum(g[t] * pk[t] for t in range(4)), lnv=lnv)
    return res

RES = {}
base = evaluate(OPTS['지금'])
P('\n== 3. 표시 21~29 거대 처치율 · 수입 (봇 = T7-4 중앙 상태, 사람 M = 봇 c-4 상태, L = c-8). 20·30은 보스 판')
for name, o in OPTS.items():
    rb = evaluate(o); rs = evaluate(o, steep=True); rm = evaluate(o, 4); rl = evaluate(o, 8)
    RES[name] = dict(bot=rb, steep=rs, M=rm, L=rl)
    P(f'\n[{name}]')
    P(' c  | 거대 등급 일.에.유.레 | 봇 등급별 처치 | 봇 전체 (감도) | 사람M | 사람L | 거대 판 ln net 변화 | 전체 판 수입 변화')
    for c in STAGES:
        b = rb[c]; d = b['lnv'] - base[c]['lnv']
        P(f' {c} | {" ".join("%.2f" % x for x in b["share"])} | {" ".join("%.2f" % x for x in b["pk"])} | {b["all"]:.2f} ({rs[c]["all"]:.2f}) | {rm[c]["all"]:.2f} | {rl[c]["all"]:.2f} | {d:+.3f} | {100 * (math.exp(GIANT_P * d) - 1):+.2f}%')
    dl = S.mean(rb[c]['lnv'] - base[c]['lnv'] for c in STAGES)
    summ = dict(bot_mean=S.mean(rb[c]['all'] for c in STAGES), bot_min=min(rb[c]['all'] for c in STAGES),
                steep_min=min(rs[c]['all'] for c in STAGES), steep_mean=S.mean(rs[c]['all'] for c in STAGES),
                leg_21_29=S.mean(rb[c]['pk'][3] for c in STAGES), M_mean=S.mean(rm[c]['all'] for c in STAGES),
                L_mean=S.mean(rl[c]['all'] for c in STAGES),
                inc_pct=100 * (math.exp(GIANT_P * dl) - 1), giant_round_pct=100 * (math.exp(dl) - 1))
    RES[name]['summary'] = summ
    P(f" 요약: 봇 평균 {summ['bot_mean']:.2f} 최저 {summ['bot_min']:.2f} (감도 평균 {summ['steep_mean']:.2f} 최저 {summ['steep_min']:.2f}) · 레전드리 {summ['leg_21_29']:.2f} · 사람M {summ['M_mean']:.2f} · L {summ['L_mean']:.2f} · 거대 판 net {summ['giant_round_pct']:+.1f}% · 전체 {summ['inc_pct']:+.2f}%")

P('\n== 4. 요약표 (표시 21~29)')
P(' 안 | 봇 평균 | 봇 최저 | 감도 평균·최저 | 레전드리 거대 | 사람M | 사람L | 거대 판 net | 전체 판 수입')
for name, v in RES.items():
    s_ = v['summary']
    P(f" {name} | {s_['bot_mean']:.2f} | {s_['bot_min']:.2f} | {s_['steep_mean']:.2f}·{s_['steep_min']:.2f} | {s_['leg_21_29']:.2f} | {s_['M_mean']:.2f} | {s_['L_mean']:.2f} | {s_['giant_round_pct']:+.1f}% | {s_['inc_pct']:+.2f}%")

P('\n== 5. 참고: 표시별 봇 창 누적 피해 중앙 C·DPS, 사람 M·L DPS, 창, 지금 거대 체력 일반/에픽/유니크/레전드리')
for c in STAGES:
    P(f" {c}: 봇 C {math.exp(mC(c)):.0f} (DPS {math.exp(mC(c)) / W(c):.0f}/s, 감도 {math.exp(mC(c, steep=True)) / W(c):.0f}/s) · M {math.exp(mC(c, 4)) / W(c):.0f}/s · L {math.exp(mC(c, 8)) / W(c):.0f}/s · 창 {W(c):.0f}s · H {'/'.join('%.0f' % H_now(t, c) for t in range(4))}")

open(OUT + 'model73_out.txt', 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
json.dump(dict(M25=M25, SIG=SIG, GR_model=GR, GR_obs=GR_obs, slope=SLOPE, giant_p=GIANT_P,
               res={k: {kk: (vv if kk == 'summary' else {str(c): x for c, x in vv.items()}) for kk, vv in v.items()} for k, v in RES.items()}),
          open(OUT + 'model73_out.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
