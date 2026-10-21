# 7-3 거대 버섯 독립 검증 (verify, 2026-10-21)
# 실행: 프로젝트 루트에서  python -X utf8 BalanceData/r7/verify/verify73.py
# theory의 model73.py·explore.py·netreg.py를 import하지 않는다. CSV·phase json을 직접 읽고, 식은 코드에서 다시 옮겼다.
import json, math, statistics as S, collections
import numpy as np
from scipy.stats import norm
from scipy.optimize import minimize

T4 = 'BalanceData/r7/test/T7-4/'
OUT = 'BalanceData/r7/verify/verify73_out.txt'
L = []
def P(*a):
    s = ' '.join(str(x) for x in a); L.append(s); print(s)

# ---------- 코드 식 (Defs.cs:90 tuned hp, Defs.World.cs:62 ZoneMul, ZONE_RAMP 0, DB ZONE_STEP 10·STAGE_GROWTH 1.01, Effects.cs:189) ----------
TH = [1, 4, 12, 40]
FROM = [1, 20, 45, 75, 105]
def zone(c):
    i = max(k for k, f in enumerate(FROM) if c >= f)
    return 10 ** i * 1.01 ** (c - 1)
def H_now(t, c, base=40): return TH[t] * base * zone(c)

# ---------- 1. CSV 직접 읽기 + 체력 식 대조 ----------
def load(tag):
    lines = open(T4 + tag + '_rounds.csv', encoding='utf-8').read().splitlines()
    head = lines[0].split(',')
    rows = []
    for l in lines[1:]:
        v = l.split(',')
        r = dict(zip(head, v[:len(head)]))
        g = v[len(head):]          # gTier,gHp,gSpawnT,gKillT,gSp (헤더에 없는 꼬리 칸)
        r['seed'] = int(r['state']); r['disp'] = int(r['stage']) - 4; r['dur'] = float(r['dur']); r['net'] = float(r['net'])
        r['t'] = int(g[0]); r['H'] = float(g[1]); r['sp'] = float(g[2]); r['kill'] = float(g[3])
        r['kt'] = r['kill'] - r['sp'] if r['kill'] >= 0 else None
        r['tag'] = tag
        rows.append(r)
    return rows
D = {tag: load(tag) for tag in ('GX_m25', 'GXnew_m25', 'GXold_m25', 'GX_m29', 'GX_end')}
P('== 1. 거대 체력 = tuned hp × 40 × ZoneMul(표시) 대조')
for tag, rows in D.items():
    x = [r for r in rows if r['t'] >= 0]
    err = max(abs(r['H'] / H_now(r['t'], r['disp']) - 1) for r in x)
    P(f' {tag}: {len(x)}판, 최대 상대 오차 {err:.2e}, 등장 시각 {sorted(set(r["sp"] for r in x))}')
# GX_m25가 GXnew 앞 K 6판과 같은지 (theory가 뺀 이유)
a = {(r['seed'], r['weather'], int(r['k'])): (r['t'], r['net']) for r in D['GX_m25']}
b = {(r['seed'], r['weather'], int(r['k'])): (r['t'], r['net']) for r in D['GXnew_m25']}
same = sum(1 for k in a if k in b and a[k] == b[k])
P(f' GX_m25 72판 중 GXnew_m25와 (시드,날씨,k) 같고 등급·net까지 같은 판: {same}')

# ---------- 2. 표시 25 맞춤: (i) 이진 MLE (theory 방식 재현) (ii) 처치 시간을 쓰는 중도절단 MLE (독립 방법) ----------
R25 = [r for r in D['GXnew_m25'] + D['GXold_m25'] if r['t'] >= 0]
def nll_bin(p, rows, Wref=30.0):
    m, s = p
    if s <= 0.05: return 1e9
    z = 0
    for r in rows:
        He = r['H'] * Wref / (r['dur'] - 5)
        q = norm.sf((math.log(He) - m) / s); q = min(max(q, 1e-12), 1 - 1e-12)
        z -= math.log(q) if r['kt'] is not None else math.log(1 - q)
    return z
def nll_cens(p, rows, Wref=30.0):
    # C30 = 30초 창 누적 피해. 잡은 판: C30 관측 = H / kt × 30 (창 안 피해가 고르게 들어간다는 가정). 못 잡은 판: C30 < H × 30 / W
    m, s = p
    if s <= 0.05: return 1e9
    z = 0
    for r in rows:
        W = r['dur'] - 5
        if r['kt'] is not None:
            c = math.log(r['H'] / max(r['kt'], 0.05) * Wref)
            z -= norm.logpdf((c - m) / s) - math.log(s)
        else:
            z -= norm.logcdf((math.log(r['H'] * Wref / W) - m) / s)
    return z
fb = minimize(nll_bin, [9.4, 0.7], args=(R25,), method='Nelder-Mead').x
fc = minimize(nll_cens, [9.4, 0.7], args=(R25,), method='Nelder-Mead').x
P('\n== 2. 표시 25 맞춤 (GXnew+GXold 288판)')
P(f' (i) 이진 MLE: C30 중앙 {math.exp(fb[0]):.0f} (DPS {math.exp(fb[0]) / 30:.0f}/s), s {fb[1]:.3f}   ← theory 12,330 · 0.74')
P(f' (ii) 처치 시간 중도절단 MLE: C30 중앙 {math.exp(fc[0]):.0f} (DPS {math.exp(fc[0]) / 30:.0f}/s), s {fc[1]:.3f}')
def tier_tab(rows, m, s, Wref=30.0):
    out = []
    for t in range(4):
        x = [r for r in rows if r['t'] == t]
        if not x: continue
        k = sum(r['kt'] is not None for r in x)
        pm = S.mean(norm.sf((math.log(r['H'] * Wref / (r['dur'] - 5)) - m) / s) for r in x)
        out.append(f't{t} {k}/{len(x)}={k / len(x):.2f} 모델 {pm:.2f}')
    return ' · '.join(out)
P(' (i) 등급별:', tier_tab(R25, *fb))
P(' (ii) 등급별:', tier_tab(R25, *fc))
# 시드별 DPS (중도절단, s 공통) — 시드 차이가 s를 얼마나 차지하나
P(' 시드별 C30 중앙 (중도절단, 시드 하나씩 맞춤):')
seedm = {}
for sd in range(1, 7):
    x = [r for r in R25 if r['seed'] == sd]
    f = minimize(nll_cens, [9.4, 0.5], args=(x,), method='Nelder-Mead').x
    seedm[sd] = f
    P(f'  시드 {sd}: C30 {math.exp(f[0]):.0f} (DPS {math.exp(f[0]) / 30:.0f}/s) 판 안 s {f[1]:.2f}  처치 {sum(r["kt"] is not None for r in x)}/{len(x)}')
between = S.pstdev([v[0] for v in seedm.values()]); within = S.mean([v[1] for v in seedm.values()])
P(f' 시드 사이 로그 표준편차 {between:.2f} · 시드 안(판 차이) 평균 {within:.2f} · 합성 √(b²+w²) {math.hypot(between, within):.2f}')
# 시드 하나 빼고 맞춤 (s 안정성)
lo = []
for sd in range(1, 7):
    f = minimize(nll_bin, [9.4, 0.7], args=([r for r in R25 if r['seed'] != sd],), method='Nelder-Mead').x
    lo.append((sd, math.exp(f[0]), f[1]))
P(' 시드 하나 뺀 이진 맞춤:', ' · '.join(f'-{sd}: {m:.0f}/{s:.2f}' for sd, m, s in lo))

# ---------- 3. 다른 측정점 (표시 29, 끝 상태 56) ----------
def c30_obs(rows):
    f = minimize(nll_cens, [10.5, 0.5], args=(rows,), method='Nelder-Mead').x
    return f
f29 = c30_obs([r for r in D['GX_m29'] if r['t'] >= 0])
fend = c30_obs([r for r in D['GX_end'] if r['t'] >= 0])
P('\n== 3. 다른 측정점 (전부 처치 → 처치 시간으로)')
P(f' 표시 29: C30 중앙 {math.exp(f29[0]):.0f} (DPS {math.exp(f29[0]) / 30:.0f}/s) s {f29[1]:.2f} → 25 대비 ×{math.exp(f29[0] - fc[0]):.2f} (스테이지당 ×{math.exp((f29[0] - fc[0]) / 4):.2f})')
P(f' 끝 상태(표시 56 판): C30 중앙 {math.exp(fend[0]):.0f} (DPS {math.exp(fend[0]) / 30:.0f}/s) s {fend[1]:.2f}')
P(' 주의: 처치 시간 기반 DPS는 "창 안 고른 피해" 가정. 판 초반(rush·공 수)이 몰리면 짧은 처치 시간이 DPS를 부풀린다 → 표시 29·56은 위쪽으로 치우칠 수 있음')

# ---------- 4. 봇 진행 (phase json) — madeT는 "스테이지" 인덱스, stageR/atkR는 "판" 인덱스 ----------
PH = {}
for sd in range(1, 7):
    d = json.load(open(f'{T4}phase_k06_s{sd}.json', encoding='utf-8'))
    m = {}
    for i, st in enumerate(d['stageR']):
        c = st - 4
        if d['bossR'][i] == 1: continue
        dur = d['playR'][i] - d['playR'][i - 1] - 30 if i else None
        m.setdefault(c, {})
        m[c]['atk'] = d['atkR'][i]; m[c]['dur'] = dur
    for st in range(1, len(d['madeT']) + 1):
        c = st - 4; mt = d['madeT'][st - 1][:4]
        if c in m and sum(mt): m[c]['share_ok'] = [x / sum(mt) for x in mt]
    # theory 방식(판 인덱스로 madeT 읽기)
    for i, st in enumerate(d['stageR']):
        c = st - 4
        if d['bossR'][i] or i >= len(d['madeT']) or c not in m: continue
        mt = d['madeT'][i][:4]
        m[c]['share_th'] = [x / sum(mt) for x in mt] if sum(mt) else None
    PH[sd] = m
P('\n== 4. 등급 비중: madeT 인덱스 (BalanceSim.cs:170 SetTiers는 madeT[stage-1], phase의 stageR·atkR는 판 순서)')
P(' 시드 4는 보스 10 재도전으로 판 2개가 더 있어 판 인덱스 ≠ 스테이지 인덱스. model73.py:47~52가 madeT[i](판 인덱스)를 쓴다 → 시드 4만 2스테이지 뒤 값')
def share(c, key):
    v = [PH[s][c][key] for s in PH if c in PH[s] and PH[s][c].get(key)]
    return [S.mean(x[t] for x in v) for t in range(4)], len(v)
for c in (21, 22, 23, 24, 25):
    a, na = share(c, 'share_th'); b, nb = share(c, 'share_ok')
    P(f' {c}: theory {" ".join("%.3f" % x for x in a)} (n{na}) · 바로 읽기 {" ".join("%.3f" % x for x in b)} (n{nb})')

# ---------- 5. 21~24 외삽: 네 가지 방법 ----------
KS = {10: 1.07, 20: 28.1, 30: 292.5, 40: 387.0}
def kstate(x):
    xs = sorted(KS)
    for a_, b_ in zip(xs, xs[1:]):
        if a_ <= x <= b_:
            w = (x - a_) / (b_ - a_); return math.exp(math.log(KS[a_]) * (1 - w) + math.log(KS[b_]) * w)
    return KS[xs[0]] if x < xs[0] else KS[xs[-1]]
def med(c, k):
    v = [PH[s][c][k] for s in PH if c in PH[s] and PH[s][c].get(k)]
    if not v: v = [PH[s][c + 1][k] for s in PH if c + 1 in PH[s] and PH[s][c + 1].get(k)]
    return S.median(v)
def W(c): return med(c, 'dur') - 5
M25, SIG = fb
g_obs = math.exp((f29[0] - fc[0]) / 4)
def lnC(c, how, x=None):
    """표시 c 판, 봇 x 상태(기본 c)의 창 W 누적 피해 ln 중앙"""
    x = c if x is None else x
    if how == 'K':        # theory: K_state × 공격력 × 창
        return M25 + math.log(kstate(x) * med(x, 'atk') * W(c) / (kstate(25) * med(25, 'atk') * W(25)))
    if how == 'atk':      # 공격력 × 창만 (K 성장 없음 = 낙관)
        return M25 + math.log(med(x, 'atk') * W(c) / (med(25, 'atk') * W(25)))
    if how == 'obs':      # 25→29 관측 성장률을 거꾸로
        return M25 + (x - 25) * math.log(g_obs) + math.log(W(c) / W(25))
    if how == 'pess':     # 비관: 스테이지당 ×1.6 (관측보다 가파름)
        return M25 + (x - 25) * math.log(1.6) + math.log(W(c) / W(25))
HOWS = ['K', 'atk', 'obs', 'pess']
P('\n== 5. 표시별 봇 창 피해 중앙 C (외삽 방법별)')
for c in range(21, 30):
    P(f' {c}: W {W(c):.0f}s atk {med(c, "atk"):.1f} | ' + ' · '.join(f'{h} {math.exp(lnC(c, h)):.0f}' for h in HOWS))

# ---------- 6. 조정안 처치율 (봇·사람) ----------
def ramp(c, n):          # 거대만 새 지역 배율을 n판에 걸쳐 올림 (지역 첫 판 c=from)
    i = max(k for k, f in enumerate(FROM) if c >= f)
    if i == 0 or n <= 0: return 1.0
    e = min(1.0, (c - FROM[i] + 1) / n)
    return 10 ** (e - 1)
OPTS = {
    '지금': lambda t, c: H_now(t, c),
    '(a1) 1·2·3·4': lambda t, c: [1, 2, 3, 4][t] * 40 * zone(c),
    '(a1h) 1·2·3·4, 기본 25': lambda t, c: [1, 2, 3, 4][t] * 25 * zone(c),
    '(a1+r5) a1 + 거대만 지역 ×10을 5판에': lambda t, c: [1, 2, 3, 4][t] * 40 * zone(c) * ramp(c, 5),
    '(a5) 1·1.5·2·3': lambda t, c: [1, 1.5, 2, 3][t] * 40 * zone(c),
    '(d) 지금 등급 + 거대만 지역 ×10을 5판에': lambda t, c: H_now(t, c) * ramp(c, 5),
}
def evalopt(H, how, lag=0, wmul=1.0, sig=None):
    sig = sig or SIG
    res = {}
    for c in range(21, 30):
        sh, _ = share(c, 'share_ok')
        m = lnC(c, how, c - lag) + math.log(wmul)
        pk = [norm.sf((math.log(H(t, c)) - m) / sig) for t in range(4)]
        res[c] = (sum(sh[t] * pk[t] for t in range(4)), pk)
    return res
P('\n== 6. 조정안 처치율 (봇 21~29, 등급 비중은 스테이지 인덱스로 바로 읽은 값)')
P(' 안 | 방법 | 21 22 23 24 25 26 27 28 29 | 평균 | 최저 | 레전드리 21·23')
SUM = {}
for name, H in OPTS.items():
    for how in HOWS:
        r = evalopt(H, how)
        v = [r[c][0] for c in range(21, 30)]
        SUM[(name, how)] = r
        P(f' {name} | {how} | {" ".join("%.2f" % x for x in v)} | {S.mean(v):.2f} | {min(v):.2f} | {r[21][1][3]:.2f}·{r[23][1][3]:.2f}')
P('\n 사람 (봇 c-4 상태, 창: 봇 창 그대로 = theory / 사용자 판 시간 20~21초 → 창 15.5초)')
for name, H in OPTS.items():
    out = []
    for wm_name in ('봇창', '사용자창'):
        rr = {}
        for c in range(21, 30):
            pass
        r = evalopt(H, 'K', lag=4) if wm_name == '봇창' else None
        if wm_name == '사용자창':
            r = {}
            for c in range(21, 30):
                sh, _ = share(c, 'share_ok')
                m = lnC(c, 'K', c - 4) + math.log((10 + min(10, 0.5 * (c - 1)) + 0.5 - 5) / W(c))
                pk = [norm.sf((math.log(H(t, c)) - m) / SIG) for t in range(4)]
                r[c] = (sum(sh[t] * pk[t] for t in range(4)), pk)
        v = [r[c][0] for c in range(21, 30)]
        out.append(f'{wm_name} 21·22·23 {v[0]:.2f}·{v[1]:.2f}·{v[2]:.2f} 평균 {S.mean(v):.2f}')
    P(f' {name}: ' + ' | '.join(out))

# ---------- 7. 수입: 회귀 독립 재계산 (그룹 평균 빼기) ----------
rows = [r for r in R25]
for r in rows: r['form'] = 'new' if r['tag'] == 'GXnew_m25' else 'old'
grp = collections.defaultdict(list)
for r in rows: grp[(r['seed'], r['weather'], r['form'])].append(r)
X = []; y = []
for g, rs in grp.items():
    ys = [math.log(r['net']) for r in rs]
    xs = [[((r['kt'] if r['kt'] is not None else 30.0) / 30.0) * (r['t'] == k) for k in range(4)] for r in rs]
    my = S.mean(ys); mx = [S.mean(x[k] for x in xs) for k in range(4)]
    for yy, xx in zip(ys, xs):
        y.append(yy - my); X.append([xx[k] - mx[k] for k in range(4)])
X = np.array(X); y = np.array(y)
b, *_ = np.linalg.lstsq(X, y, rcond=None)
e = y - X @ b; dof = len(y) - 4 - len(grp); s2 = (e @ e) / dof
se = np.sqrt(np.diag(s2 * np.linalg.inv(X.T @ X)))
P('\n== 7. 판 net 회귀 독립 재계산 (그룹 평균 빼기, 같은 모형)')
P(' 기울기 ±se:', ' · '.join(f't{k} {b[k]:+.3f}±{se[k]:.3f}' for k in range(4)), f' (theory t3 +0.255±0.042)')
# 창 W를 판별 dur-5로 (theory는 30 고정)
X2 = []; y2 = []
for g, rs in grp.items():
    ys = [math.log(r['net']) for r in rs]
    xs = [[(min(r['kt'], r['dur'] - 5) if r['kt'] is not None else r['dur'] - 5) / (r['dur'] - 5) * (r['t'] == k) for k in range(4)] for r in rs]
    my = S.mean(ys); mx = [S.mean(x[k] for x in xs) for k in range(4)]
    for yy, xx in zip(ys, xs):
        y2.append(yy - my); X2.append([xx[k] - mx[k] for k in range(4)])
b2, *_ = np.linalg.lstsq(np.array(X2), np.array(y2), rcond=None)
P(' 창을 판별 dur−5로:', ' · '.join(f't{k} {b2[k]:+.3f}' for k in range(4)))
SL = list(b)
def alive(m, H):
    zs = np.linspace(-4, 4, 161); w = np.exp(-zs * zs / 2); C = np.exp(m + SIG * zs)
    return float((w * np.minimum(H / C, 1)).sum() / w.sum())
P(' 거대 판 ln net 변화 (봇, 방법 K) · 전체 판(×0.08):')
for name in ('(a1) 1·2·3·4', '(a1h) 1·2·3·4, 기본 25', '(a1+r5) a1 + 거대만 지역 ×10을 5판에'):
    d = []
    for c in range(21, 30):
        sh, _ = share(c, 'share_ok'); m = lnC(c, 'K')
        d.append(sum(sh[t] * SL[t] * (alive(m, OPTS[name](t, c)) - alive(m, OPTS['지금'](t, c))) for t in range(4)))
    P(f'  {name}: 거대 판 {100 * (math.exp(S.mean(d)) - 1):+.1f}% · 전체 {100 * (math.exp(0.08 * S.mean(d)) - 1):+.2f}% (스테이지 최대 {100 * (math.exp(0.08 * min(d)) - 1):+.2f}%)')

# ---------- 8. 지역 경계 45 (들판 ×10) ----------
P('\n== 8. 들판 진입 45~48 (봇): 끝 상태 C에서 공격력 비로만 거꾸로 (K 성장 없음 = 낙관)')
for c in (45, 46, 47, 48):
    try:
        m = fend[0] + math.log(med(c, 'atk') / med(55, 'atk')) + math.log(W(c) / 30)
    except Exception:
        continue
    pk_now = [norm.sf((math.log(H_now(t, c)) - m) / SIG) for t in range(4)]
    pk_a1 = [norm.sf((math.log([1, 2, 3, 4][t] * 40 * zone(c)) - m) / SIG) for t in range(4)]
    P(f' {c}: C {math.exp(m):.0f} (atk {med(c, "atk"):.0f}/{med(55, "atk"):.0f}) · 지금 레전드리 {H_now(3, c):.0f} 처치 {pk_now[3]:.2f} 유니크 {pk_now[2]:.2f} · (a1) 레전드리 {pk_a1[3]:.2f}')

open(OUT, 'w', encoding='utf-8').write('\n'.join(L) + '\n')

# ---------- 9. T7-5 합격선용: 표시 21·23 등급별 처치 예측 (방법별 범위) ----------
P('\n== 9. 표시 21·23·25 등급별 처치 예측 (지금 / (a1) / (a5)), 방법 K·obs·pess·atk')
for c in (21, 23, 25):
    for name in ('지금', '(a1) 1·2·3·4', '(a5) 1·1.5·2·3', '(a1h) 1·2·3·4, 기본 25'):
        cells = []
        for how in HOWS:
            m = lnC(c, how)
            pk = [norm.sf((math.log(OPTS[name](t, c)) - m) / SIG) for t in range(4)]
            cells.append(f'{how} ' + '/'.join('%.2f' % x for x in pk))
        P(f' {c} {name}: H {"/".join("%.0f" % OPTS[name](t, c) for t in range(4))} | ' + ' · '.join(cells))
# 시드 5 (가장 약한 시드)만 따로: 판 안 s로
P('\n 시드 5 (표시 25 C30 4,511, 판 안 s 0.81) 기준 (a1) 25 등급별:', '/'.join('%.2f' % norm.sf((math.log([1,2,3,4][t]*40*zone(25)*30/27) - seedm[5][0]) / seedm[5][1]) for t in range(4)))
# 강제 판 net 기대 (표시 25, (a1), 거대 강제 = 거대 확률 1)
sh25, _ = share(25, 'share_ok'); m = lnC(25, 'K')
d25 = sum(sh25[t] * SL[t] * (alive(m, OPTS['(a1) 1·2·3·4'](t, 25)) - alive(m, OPTS['지금'](t, 25))) for t in range(4))
P(f' 표시 25 거대 강제 판 net 기대 (a1)/지금: ×{math.exp(d25):.3f}  (회귀 잔차 sd로 본 144판 평균의 1se ≈ ×{math.exp(float(np.sqrt(s2 / 144))):.3f})')
open(OUT, 'w', encoding='utf-8').write('\n'.join(L) + '\n')
