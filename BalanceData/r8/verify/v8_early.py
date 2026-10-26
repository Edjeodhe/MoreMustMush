# -*- coding: utf-8 -*-
"""verify 8회차 — 초반(내부 1~23) 수치안 독립 재계산.
theory 코드(tree.py·model8.py·design8.py)를 import하지 않는다. db.json·T8-0 CSV를 직접 읽는다.
  1) T8-0 rounds_s*.csv의 bought 열로 판 시작 노드 레벨을 다시 만든다(theory rounds_levels.json과 대조).
  2) 판 수입 근사: ln net = FE(n) + Σ b·ln x. x는 Game.ComputeStats를 옮긴 내 식(군락은 폭우 ×1.2 기댓값 포함).
     b는 (a) theory 값 (b) 판 고정효과 + 시드 사이 변동 OLS 추정 (c) 민감도 격자.
  3) 진행: Game.BulkBuy(가장 싼 것부터, ParentOk, gem 제외, 동점은 DB 순서)를 내가 다시 옮김.
  4) 사람 = 봇 수입 × A(n) (지표 B 내부 1~13에 다시 맞춤).
  5) 지표: 판별 배율(1→8 기하·2→8 기하·판별 최소), 구매 수, 다음 가격/수입, 수확기 2·3개 판, 13·23판 수입.
다시 돌리기: python -X utf8 BalanceData/r8/verify/v8_early.py → v8_early_out.txt"""
import csv, json, math, os, statistics as S
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.join(ROOT, 'Assets', 'Resources', 'DB', 'db.json')
T80 = os.path.join(ROOT, 'BalanceData', 'r8', 'test', 'T8-0')
COST_MUL = 0.9
B_HUMAN = {1: 17, 2: 26, 3: 46, 4: 53, 5: 58, 6: 73, 7: 92, 8: 111, 9: 200, 10: 221, 11: 183, 12: 286, 13: 257}

# 이론_8 0절 E4 (DB 값)
E4 = {
    'power': dict(costs=[6, 24, 96, 380]), 'ed_score': dict(costs=[8, 30, 110]),
    'ed_cols': dict(costs=[16, 55, 150, 520], efx=[4, 7, 9, 11, 12]), 'ps_dur': dict(costs=[24, 75, 220, 650]),
    'ed_price': dict(costs=[32, 95, 260]), 'md_bar': dict(costs=[50, 160, 400]), 'ps_atk': dict(costs=[45, 180, 700, 2400]),
    'md_map': dict(costs=[260, 450, 750, 1700]), 'ps_ball': dict(costs=[200, 800]), 'md_kid': dict(costs=[500, 1500, 4000]),
}
LOOSE_NOW = [10, 10, 9, 8, 6, 4]
LOOSE_E4 = [10, 10, 10, 10, 9, 8]


# ---------------- DB ----------------
def load_nodes(plan=None):
    d = json.load(open(DB, encoding='utf-8-sig'))
    cost = {c['Cost_ID']: c for c in d['Upgrade_Cost']}
    key = {u['Upg_ID']: u['Upg_Key'] for u in d['Upgrade']}
    nodes = {}
    for u in d['Upgrade']:
        k = u['Upg_Key']
        if k == 'homing':                     # Defs.OFF_NODES
            continue
        c = cost[u['Cost_ID']]
        mx = u['Efx_MaxLv']
        nodes[k] = dict(id=k, pre=key.get(u['Upg_Pre']), req=key.get(u['Upg_Req']), max=mx, tier=u['Tier'],
                        efx=[u['Efx_Lv%d' % i] for i in range(5)], costs=[c['Cost_Lv%d' % i] for i in range(1, 5)],
                        gem=any(c['Gem_Lv%d' % i] > 0 for i in range(1, 5)), inf=(mx == 0))
    for k, v in (plan or {}).items():
        if 'costs' in v:
            nodes[k]['costs'] = list(v['costs']) + nodes[k]['costs'][len(v['costs']):]
        if 'efx' in v:
            nodes[k]['efx'] = list(v['efx'])
    return nodes


def scr(n, L):   # Game.NodeCost 화면 가격
    return math.ceil(n['costs'][L] * COST_MUL) if L < n['max'] else math.inf


def pok(nodes, lv, n):   # Game.ParentOk
    if n['pre'] and lv.get(n['pre'], 0) < 1:
        return False
    if n['req'] and lv.get(n['req'], 0) < nodes[n['req']]['max']:
        return False
    return True


def next_buy(nodes, lv):
    best, bc = None, math.inf
    for k, n in nodes.items():                # dict 순서 = DB 행 순서 = NODES 순서 (동점이면 앞 노드)
        if n['gem'] or n['inf']:
            continue
        L = lv.get(k, 0)
        if L >= n['max'] or not pok(nodes, lv, n):
            continue
        c = scr(n, L)
        if c < bc:
            best, bc = k, c
    return best, bc


def E(nodes, lv, k):
    n = nodes.get(k)
    if not n:
        return 0.0
    return n['efx'][min(lv.get(k, 0), n['max'])]


# ---------------- 능력치 (Game.ComputeStats 일부, 내 식) ----------------
MAP_ZOOM_KEY = 'md_map'


def stats(nodes, lv, n, rain_share=1 / 3):
    cst = max(1, n - 4)
    base_t = 10 if n <= 4 else 10 + min(10, 0.5 * (cst - 1))
    dur = base_t + E(nodes, lv, 'ps_dur') + E(nodes, lv, 'ps_dur2')
    zoom = E(nodes, lv, 'md_map2') if lv.get('md_map2', 0) > 0 else E(nodes, lv, 'md_map')
    cc = round(E(nodes, lv, 'ed_cols')) + round(E(nodes, lv, 'ed_cols2'))
    area = (1 / (zoom * zoom)) ** 0.8

    def colsf(rain):
        m = max(1, math.ceil(cc * (1.2 if rain else 1)))
        return max(1, math.floor(m * area + 0.5))
    c_clear, c_rain = colsf(False), colsf(True)
    lncols = (1 - rain_share) * math.log(c_clear) + rain_share * math.log(c_rain)
    flat = sum(E(nodes, lv, k) for k in ('ed_score', 'ed_score1p', 'ed_score2', 'ed_score2p'))
    gold = 1 + 0.1 + (E(nodes, lv, 'ed_price') - 1) + E(nodes, lv, 'ed_price2')
    comboK = E(nodes, lv, 'md_combo') + E(nodes, lv, 'md_combo2')
    balls = 1 + lv.get('ps_ball', 0) + lv.get('ps_ball2', 0)
    atk = (1 + E(nodes, lv, 'ps_atk') + E(nodes, lv, 'ps_atk2') + E(nodes, lv, 'ps_atk2p')) * 1.1
    return dict(dur=dur, zoom=zoom, cols_clear=c_clear, lncols=lncols, flat=flat, gold=gold, comboK=comboK, balls=balls,
                bar=E(nodes, lv, 'md_bar'), atk=atk, kid=E(nodes, lv, 'md_kid'),
                dev=sum(1 for k in ('dev_stump', 'dev_moss', 'dev_acorn') if lv.get(k, 0)),
                regen=E(nodes, lv, 'ed_regen'), rare=E(nodes, lv, 'ed_rare0'), size=E(nodes, lv, 'ps_size'), spd=E(nodes, lv, 'ps_spd'),
                skills=sum(lv.get(k, 0) for k in ('accel', 'pierce', 'burst')))


SBAR_HI = 1.5   # 내부 7~ 타격 1회 평균 등급 기본 점수(가정, 민감도에서 1.2·2.0)


def xvec(st, n, b, sbar_hi=None):
    sb = 1.0 if n <= 6 else (sbar_hi or SBAR_HI)
    v = (math.log(sb + st['flat']) + math.log(st['gold']) + b['dur'] * math.log(st['dur']) + b['cols'] * st['lncols']
         + b['zoom'] * math.log(st['zoom']) + b['combo'] * math.log(1 + 5 * st['comboK']) + b['balls'] * math.log(st['balls'])
         + b['bar'] * math.log(st['bar']) + b['dev'] * math.log(1 + 0.1 * st['dev']) + b['kid'] * math.log(1 + 1.2 * st['kid'])
         + 0.1 * math.log(3.0 / max(0.25, st['regen'])) + 0.3 * math.log(1 + st['rare']) + 0.5 * math.log(st['size'])
         + 0.3 * math.log(st['spd']) + math.log(1 + 0.04 * st['skills']))
    return v


B_THEORY = dict(dur=0.8, cols=0.7, zoom=1.0, combo=1.0, balls=0.6, bar=0.3, dev=1.0, kid=1.0)


# ---------------- T8-0 ----------------
def t80_rows():
    rows = []
    for s in range(1, 7):
        lv = {}
        with open(os.path.join(T80, f'rounds_s{s}.csv'), encoding='utf-8') as f:
            for r in csv.DictReader(f):
                n = int(r['stage'])
                rows.append(dict(seed=s, n=n, boss=int(r['bossFlag']), net=float(r['net']), colsMax=int(r['colsMax'] or 0),
                                 balls=int(r['balls']), weather=r['weather'], made=int(r['made'] or 0), lv=dict(lv)))
                for it in filter(None, (r['bought'] or '').split(';')):
                    k, ch = it.split(':'); a, b2 = ch.split('>')
                    lv[k] = int(b2)
    return rows


def fit_fe(nodes, rows, b, nmax=23, sbar_hi=None):
    fe = {}
    for n in range(1, nmax + 1):
        v = [math.log(r['net']) - xvec(stats(nodes, r['lv'], n), n, b, sbar_hi) for r in rows if r['n'] == n and not r['boss'] and r['net'] > 0]
        if v:
            fe[n] = S.mean(v)
    # 구간 안 3점 이동 평균 (theory와 같은 구간 2~6, 7~13, 15~23)
    out = dict(fe)
    for lo, hi in ((2, 6), (7, 13), (15, 23)):
        for n in range(lo, hi + 1):
            w = [fe[m] for m in (n - 1, n, n + 1) if lo <= m <= hi and m in fe]
            out[n] = sum(w) / len(w)
    return out


def ols_fe(nodes, rows, nmax=23):
    """판 고정효과 + 시드 사이 변동으로 dur·cols·balls·bar 탄력 추정(점수·정산 1 고정, 나머지 theory 값 고정)"""
    keys = ['dur', 'cols', 'balls', 'bar', 'zoom']
    Y, X, G = [], [], []
    for r in rows:
        n = r['n']
        if r['boss'] or n > nmax or r['net'] <= 0:
            continue
        st = stats(nodes, r['lv'], n)
        st['lncols'] = math.log(max(1, r['colsMax']))          # 실제 판 군락 최대(폭우 포함)
        b0 = dict(B_THEORY, **{k: 0 for k in keys})
        y = math.log(r['net']) - xvec(st, n, b0)
        Y.append(y); G.append(n)
        X.append([math.log(st['dur']), st['lncols'], math.log(st['balls']), math.log(st['bar']), math.log(st['zoom'])])
    Y, X, G = np.array(Y), np.array(X), np.array(G)
    for g in set(G):                      # 판 고정효과 제거(군내 평균 빼기)
        m = G == g
        Y[m] -= Y[m].mean(); X[m] -= X[m].mean(axis=0)
    keep = [j for j in range(X.shape[1]) if X[:, j].std() > 1e-9]
    Xk = X[:, keep]
    beta, *_ = np.linalg.lstsq(Xk, Y, rcond=None)
    res = Y - Xk @ beta
    dof = len(Y) - len(keep) - len(set(G))
    s2 = res @ res / max(1, dof)
    cov = s2 * np.linalg.pinv(Xk.T @ Xk)
    return {keys[j]: (beta[i], math.sqrt(cov[i, i])) for i, j in enumerate(keep)}, {keys[j]: float(X[:, j].std()) for j in range(len(keys))}


# ---------------- 진행 ----------------
def made_med(rows):
    return {n: S.median([r['made'] for r in rows if r['n'] == n and not r['boss']]) for n in range(1, 8)}


def loose_fe(fe, rows, loose, el):
    if not loose:
        return fe
    md = made_med(rows)
    fe2 = dict(fe)
    for i in range(1, 6):
        fe2[i + 1] += el * math.log(1 + (loose[i] - LOOSE_NOW[i]) / md[i + 1])
    return fe2


def run(nodes, fe, b, a_fn=lambda n: 1.0, nmax=23, boss=(14,), win=2.6, sbar_hi=None, scale=1.0, start_gold=0):
    lv, gold, out, last = {}, start_gold, [], None
    for n in range(1, nmax + 1):
        st = stats(nodes, lv, n)
        if n in boss:
            net = win * last
        else:
            net = math.exp(fe[n] + xvec(st, n, b, sbar_hi)) * a_fn(n) * scale
            last = net
        gold += net
        bought = []
        while True:
            k, c = next_buy(nodes, lv)
            if k is None or gold < c:
                break
            gold -= c; lv[k] = lv.get(k, 0) + 1; bought.append(f'{k}{lv[k]}')
        k, c = next_buy(nodes, lv)
        out.append(dict(n=n, net=net, buys=len(bought), bought=bought, ratio=c / net, nxt=k, balls=st['balls'], cols=st['cols_clear'],
                        atk=st['atk'], dur=st['dur'], lv=dict(lv)))
    return out


def fit_A(nodes, fe, b, sbar_hi=None):
    best = None
    for a0 in np.arange(0.40, 1.01, 0.01):
        for sl in np.arange(-0.05, 0.0051, 0.0025):
            f = lambda n, a0=a0, sl=sl: max(0.15, a0 + sl * (n - 1))
            sim = run(nodes, fe, b, f, nmax=13, boss=(), sbar_hi=sbar_hi)
            e = [math.log(r['net'] / B_HUMAN[r['n']]) for r in sim]
            sse = sum(x * x for x in e)
            if best is None or sse < best[0]:
                best = (sse, a0, sl)
    sse, a0, sl = best
    return a0, sl, math.sqrt(sse / 13)


def human_fn(a0, sl, d=0.885):
    return lambda n: max(0.15, a0 + sl * (min(n, 13) - 1)) * (d ** (n - 13) if n > 13 else 1)


def metrics(sim):
    g = {r['n']: r['net'] for r in sim}
    per = [g[n] / g[n - 1] for n in range(2, 9)]
    first = lambda k: next((r['n'] + 1 for r in sim if r['balls'] < k and any(x.startswith('ps_ball') for x in r['bought'])
                            and 1 + r['lv'].get('ps_ball', 0) + r['lv'].get('ps_ball2', 0) >= k), None)
    return dict(g18=(g[8] / g[1]) ** (1 / 7), g28=(g[8] / g[2]) ** (1 / 6), g813=(g[13] / g[8]) ** (1 / 5), pmin=min(per),
                n_below15=sum(1 for p in per if p < 1.5), buys_min=min(r['buys'] for r in sim if r['n'] <= 13),
                buys_avg=S.mean(r['buys'] for r in sim if r['n'] <= 13),
                ratio_max2=max(r['ratio'] for r in sim if 2 <= r['n'] <= 13),
                n_ratio_gt06=sum(1 for r in sim if r['n'] <= 13 and r['ratio'] > 0.6),
                ball2=first(2), ball3=first(3), net13=g[13], net23=g[23],
                atk13=next(r['atk'] for r in sim if r['n'] == 13), balls13=next(r['balls'] for r in sim if r['n'] == 14) if len(sim) >= 14 else None)


def fmtm(m):
    return (f"1→8 ×{m['g18']:.2f} · 2→8 ×{m['g28']:.2f} · 8→13 ×{m['g813']:.2f} · 판별 최소 ×{m['pmin']:.2f} (×1.5 미만 {m['n_below15']}/7판) · "
            f"구매 최소 {m['buys_min']} 평균 {m['buys_avg']:.1f} · 다음가/수입(2~13) 최대 {m['ratio_max2']:.2f} (>0.6 {m['n_ratio_gt06']}판) · "
            f"수확기2 판 {m['ball2']} · 3 판 {m['ball3']} · 13판 {m['net13']:,.0f} · 23판 {m['net23']:,.0f}")


if __name__ == '__main__':
    out = []
    rows = t80_rows()
    # 1) 레벨 재구성 대조
    th = json.load(open(os.path.join(ROOT, 'BalanceData', 'r8', 'theory', 'rounds_levels.json'), encoding='utf-8'))
    mism = sum(1 for a, t in zip(rows, th) if a['lv'] != t['lv'] or a['n'] != t['stage'])
    out.append(f'[1] T8-0 판 시작 레벨 재구성: {len(rows)}행, theory rounds_levels.json과 다른 행 {mism}')
    base = load_nodes(); plan = load_nodes(E4)
    # 군락 식 대조 (내 식 폭우 포함 vs CSV colsMax)
    d_ok = d_all = 0
    for r in rows:
        if r['boss'] or r['n'] > 23:
            continue
        st = stats(base, r['lv'], r['n'])
        pred = st['cols_clear'] if r['weather'] != 'rain' else round(math.exp((st['lncols'] - (2 / 3) * math.log(st['cols_clear'])) * 3))
        d_all += 1; d_ok += (pred == r['colsMax'])
    out.append(f'    군락 최대 내 식(폭우 ×1.2 ceil 포함) = CSV colsMax: {d_ok}/{d_all} 판 (내부 1~23 일반 판)')

    # 2) OLS 탄력
    est, sd = ols_fe(base, rows)
    out.append('[2] 판 고정효과 OLS(시드 사이 변동만): ' + ' · '.join(f'{k} {v[0]:+.2f}±{v[1]:.2f}' for k, v in est.items())
               + ' | 군내 표준편차 ' + ' '.join(f'{k} {v:.3f}' for k, v in sd.items()))

    # 3) theory 탄력으로 재현
    b = dict(B_THEORY)
    fe = fit_fe(base, rows, b)
    a0, sl, hr = fit_A(base, fe, b)
    H = human_fn(a0, sl)
    out.append(f'[3] 사람 A(n) 재맞춤 = {a0:.2f} {sl:+.4f}(n−1), RMSE(ln) {hr:.3f}  (theory 0.70 −0.0075, 0.176)')
    sims = {}
    for name, nodes, loose in (('지금', base, None), ('E4', plan, LOOSE_E4)):
        fe_p = loose_fe(fe, rows, loose, 0.7)
        sims[name] = dict(bot=run(nodes, fe_p, b), hum=run(nodes, fe_p, b, H))
        for who in ('bot', 'hum'):
            out.append(f'    {name} {"봇" if who == "bot" else "사람"}: ' + fmtm(metrics(sims[name][who])))
    out.append('    E4 사람 판별: ' + ' | '.join(f"{r['n']}:{r['net']:.0f}(×{r['net'] / p['net']:.2f}) {'+'.join(r['bought'])} r{r['ratio']:.2f}"
                                          for p, r in zip(sims['E4']['hum'], sims['E4']['hum'][1:14])))
    # 4) 첫 판 묶음: 사람 첫 판 수입 분포
    r1 = [r['net'] for r in rows if r['n'] == 1]
    out.append(f'[4] 첫 판 묶음 power(6)+ed_score1(8)=14: 봇 판 1 수입 {sorted(r1)} → ×A(1) {a0:.2f}이면 '
               + ' '.join(f'{x * a0:.1f}' for x in sorted(r1)) + f' → 14 미만 {sum(1 for x in r1 if x * a0 < 14)}/6 시드. 지표 B 판 1 평균 17(분포 없음)')
    # 5) 민감도
    out.append('[5] 민감도 (E4 사람, 각 줄 하나만 바꿈; FE·A는 그 가정에서 다시 맞춤)')
    cases = [('기준(theory 탄력)', {}, {}),
             ('수확기 탄력 0.3', dict(balls=0.3), {}), ('수확기 탄력 1.0', dict(balls=1.0), {}), ('수확기 탄력 1.5', dict(balls=1.5), {}),
             ('군락 탄력 0.5', dict(cols=0.5), {}), ('군락 탄력 1.0', dict(cols=1.0), {}),
             ('시간 탄력 0.6', dict(dur=0.6), {}), ('시간 탄력 1.06', dict(dur=1.06), {}),
             ('OLS 추정 탄력', {k: v[0] for k, v in est.items() if k in ('dur', 'cols', 'balls', 'bar')}, {}),
             ('7~ 평균 기본 점수 1.2', {}, dict(sbar=1.2)), ('7~ 평균 기본 점수 2.0', {}, dict(sbar=2.0)),
             ('낱개 탄력 0', {}, dict(loose=0.0)),
             ('사람 수입 ×0.5', {}, dict(scale=0.5)), ('사람 수입 ×0.7', {}, dict(scale=0.7)), ('사람 수입 ×1.5', {}, dict(scale=1.5)), ('사람 수입 ×2', {}, dict(scale=2.0))]
    sens = {}
    for name, bd, opt in cases:
        bb = dict(B_THEORY, **bd)
        sb = opt.get('sbar')
        fe_c = fit_fe(base, rows, bb, sbar_hi=sb)
        a0c, slc, _ = fit_A(base, fe_c, bb, sbar_hi=sb)
        Hc = human_fn(a0c, slc)
        fe_pc = loose_fe(fe_c, rows, LOOSE_E4, opt.get('loose', 0.7))
        hn = run(base, loose_fe(fe_c, rows, None, 0), bb, Hc, sbar_hi=sb, scale=opt.get('scale', 1.0))
        he = run(plan, fe_pc, bb, Hc, sbar_hi=sb, scale=opt.get('scale', 1.0))
        mn, me = metrics(hn), metrics(he)
        sens[name] = dict(now=mn, e4=me)
        out.append(f'  {name}: E4 {fmtm(me)}  || 지금 1→8 ×{mn["g18"]:.2f} 수확기2 판 {mn["ball2"]}')
    # 6) 사람 상태(보스 14·24 직전) — boss R_dps 입력 재계산
    out.append('[6] 보스 직전 사람 상태 (판 13·23 뒤 산 상태): 공격력·수확기·보스 판 시간')
    for name in ('지금', 'E4'):
        for rb in (14, 24):
            if rb == 24:
                continue
            r = sims[name]['hum'][rb - 2]
            st = stats(base if name == '지금' else plan, r['lv'], rb)
            out.append(f"    {name} 판 {rb}: 공격력 {st['atk']:.2f} · 수확기 {st['balls']} · 시간 {st['dur']:.1f}")
    for name in ('지금', 'E4'):
        r = sims[name]["hum"][22]
        st = stats(base if name == '지금' else plan, r['lv'], 24)
        out.append(f"    {name} 판 24: 공격력 {st['atk']:.2f} · 수확기 {st['balls']} · 시간 {st['dur']:.1f}")
    txt = '\n'.join(out)
    print(txt)
    open(os.path.join(HERE, 'v8_early_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    json.dump({k: {w: [dict(r, lv=None) for r in v[w]] for w in v} for k, v in sims.items()}, open(os.path.join(HERE, 'v8_early_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, default=str)
