# -*- coding: utf-8 -*-
"""8회차 이론 모델 (theory, manager#25) — 초반 판 수입·구매 진행·사람 보정.

판 수입 근사 (내부 판 1~23, 일반 판):
  ln net(n, 상태) = FE(n) + Σ b_k · ln x_k(상태, n)
  - x_k: 타격 점수 (s̄(n) + scoreFlat) · 정산 goldMul · 판 시간 dur · 군락 최대 maxCol(코드 식) · zoom · 콤보 (1 + C̄·comboK)
         · 영구 수확기 · 바 넓이 · 숲 장치 · 재생 · 희귀 포자 · 꼬마 수확기 · 수확기 크기·공속 · 1단계 스킬
  - b_k: 구조로 정한 값(점수·정산 1) + T8-0 회귀(early_fit2_out.txt) 범위 안의 값. 아래 ELAST
  - FE(n): T8-0(시드 1~6) 판 n의 [ln net − Σ b ln x] 평균. 종 수·낱개 버섯·초반 체력·도감 별·피버처럼 노드 밖 몫이 다 여기 들어간다
    → 같은 판에서 노드 상태만 바뀐 경우를 예측한다 (멀리 벗어나면 오차가 커진다)
사람 = 같은 진행 규칙(가장 싼 것부터) + 판 수입 × A_HUMAN(n) (조준·콤보 손해). A_HUMAN은 지표 B 첫 판 평균 획득(내부 1~13)에 맞춘다.
다시 돌리기: python -X utf8 BalanceData/r8/theory/model8.py → model8_out.txt · model8_out.json (결정적)"""
import json, math, os, statistics as S
import numpy as np
from tree import load, price, at_max, parent_ok

HERE = os.path.dirname(os.path.abspath(__file__))
COST_MUL = 0.9
MAP_ZOOM = [1.68, 1.5, 1.35, 1.2, 1.1]
COL_AREA_EXP = 0.8
ELAST = dict(score=1.0, price=1.0, dur=0.8, cols=0.7, zoom=1.0, combo=1.0, balls=1.04, bar=0.3, dev=1.0, regen=0.1,
             rare=0.3, kid=1.0, size=0.5, spd=0.3, skill=1.0)
COMBO_BAR = 5.0          # 봇 평균 바 콤보 (b.combo) — 바에 맞은 횟수, 바닥에 닿으면 0 (SKILL 0.6 → 놓칠 확률 ~17%)
BOSS_WIN_NET = 2.6       # 보스 처치 판 net / 직전 일반 판 (T8-0 내부 14 중앙 2,434 / 932)
B_HUMAN = {1: 17, 2: 26, 3: 46, 4: 53, 5: 58, 6: 73, 7: 92, 8: 111, 9: 200, 10: 221, 11: 183, 12: 286, 13: 257}   # 8회차_요청 지표 B


def sbar(n):
    """일반 판 타격 1회 평균 등급 기본 점수 (가정): 내부 1~6 초반 종 = 일반 등급만(Defs.cs EARLY_SPECIES 모두 tier 0) → 1"""
    return 1.0 if n <= 6 else 1.5


def V(nodes, lv, k):
    n = nodes.get(k)
    if n is None:
        return 0.0
    L = min(lv.get(k, 0), n['max'])
    return n['efx'][L]


def feats(nodes, lv, n):
    E = lambda k: V(nodes, lv, k)
    cst = max(1, n - 4)
    flat = sum(E(k) for k in ('ed_score', 'ed_score1p', 'ed_score2', 'ed_score2p'))
    gold = 1 + 0.1 + (E('ed_price') - 1) + E('ed_price2')
    base = 10 if n <= 4 else 10 + min(10, 0.5 * (cst - 1))
    dur = base + E('ps_dur') + E('ps_dur2')
    zoom = 1.0 if lv.get('md_map2', 0) else MAP_ZOOM[min(lv.get('md_map', 0), 4)]
    cc = round(E('ed_cols')) + E('ed_cols2')
    cols = max(1, int(math.floor(cc * (1 / zoom ** 2) ** COL_AREA_EXP + 0.5)))
    combo = 1 + COMBO_BAR * (E('md_combo') + E('md_combo2'))
    balls = 1 + lv.get('ps_ball', 0) + lv.get('ps_ball2', 0)
    dev = 1 + 0.10 * sum(1 for d in ('dev_stump', 'dev_moss', 'dev_acorn') if lv.get(d, 0))
    regen = 3.0 / max(0.25, E('ed_regen'))
    rare = 1 + E('ed_rare0')
    kid = 1 + 1.2 * E('md_kid')          # 바 타격마다 kidP 확률로 8초 임시 수확기 → 판 평균 수확기 +약 1.2·kidP (가정)
    skill = 1 + 0.04 * sum(lv.get(k, 0) for k in ('accel', 'pierce', 'burst'))
    return dict(score=sbar(n) + flat, price=gold, dur=dur, cols=cols, zoom=zoom, combo=combo, balls=balls,
                bar=E('md_bar'), dev=dev, regen=regen, rare=rare, kid=kid, size=E('ps_size'), spd=E('ps_spd'), skill=skill)


def lnF(nodes, lv, n):
    f = feats(nodes, lv, n)
    return sum(ELAST[k] * math.log(f[k]) for k in ELAST)


# ---------- T8-0 기준선 ----------
def t80_rows():
    rows = json.load(open(os.path.join(HERE, 'rounds_levels.json'), encoding='utf-8'))
    return rows


def fit_fe(nodes, rows, nmax=23):
    fe, res = {}, []
    for n in range(1, nmax + 1):
        rs = [r for r in rows if r['stage'] == n and r['bossFlag'] == 0]
        if not rs:
            continue
        v = [math.log(r['net']) - lnF(nodes, r['lv'], n) for r in rs]
        fe[n] = S.mean(v)
        res += [x - fe[n] for x in v]
    return fe, float(np.sqrt(np.mean(np.square(res))))


SEGMENTS = ((2, 6), (7, 13), (15, 23))   # 판 1(튜토리얼 첫 판)은 그대로, 종 풀이 바뀌는 7·보스 14에서 끊는다


def smooth_fe(fe):
    """FE(n)의 판 사이 잡음(시드 6개 평균, ±0.14)을 구간 안 3점 이동 평균으로 줄인다. 진행 비교(판당 배율)에만 쓴다"""
    out = dict(fe)
    for lo, hi in SEGMENTS:
        for n in range(lo, hi + 1):
            w = [fe[m] for m in (n - 1, n, n + 1) if lo <= m <= hi and m in fe]
            out[n] = sum(w) / len(w)
    return out


# ---------- 진행 ----------
def cheapest(nodes, lv):
    best, bc = None, math.inf
    for k, nd in nodes.items():
        if nd['gem'] or nd['inf']:
            continue
        L = lv.get(k, 0)
        if at_max(nd, L) or not parent_ok(nodes, lv, nd):
            continue
        c = price(nd, L)
        if c < bc:
            best, bc = nd, c
    return best, bc


def simulate(nodes, fe, a_fn=lambda n: 1.0, nmax=23, boss_rounds=(14,), start_gold=0):
    lv, gold, out, last_norm = {}, start_gold, [], None
    for n in range(1, nmax + 1):
        f = feats(nodes, lv, n)
        if n in boss_rounds:
            net = BOSS_WIN_NET * last_norm
        else:
            net = math.exp(fe[n] + lnF(nodes, lv, n)) * a_fn(n)
            last_norm = net
        gold += net
        bought = []
        while True:
            nd, c = cheapest(nodes, lv)
            if nd is None or gold < c:
                break
            gold -= c
            lv[nd['id']] = lv.get(nd['id'], 0) + 1
            bought.append((nd['id'], lv[nd['id']], c))
        nd, c = cheapest(nodes, lv)
        out.append(dict(n=n, net=net, buys=len(bought), bought=bought, next=c, nextKey=nd['id'] if nd else None,
                        ratio=c / net, gold=gold, balls=f['balls'], cols=f['cols'], dur=f['dur'], score=f['score'], goldMul=f['price'],
                        lv=dict(lv)))
    return out


def a_human_fit(nodes, fe):
    """사람 판 수입 배율 A(n) = A0 · (n 따라 선형) 을 지표 B 내부 1~13에 최소제곱(ln)으로"""
    best = None
    for a0 in np.arange(0.40, 1.01, 0.01):
        for slope in np.arange(-0.05, 0.0051, 0.0025):
            fn = lambda n, a0=a0, s=slope: max(0.15, a0 + s * (n - 1))
            sim = simulate(nodes, fe, fn, nmax=13, boss_rounds=())
            err = [math.log(r['net']) - math.log(B_HUMAN[r['n']]) for r in sim]
            sse = sum(e * e for e in err)
            if best is None or sse < best[0]:
                best = (sse, a0, slope, err)
    sse, a0, slope, err = best
    return a0, slope, math.sqrt(sse / len(err)), err


def fmt(x):
    return f'{x:,.0f}' if x < 1e5 else f'{x:.3g}'


def table(sim, title):
    L = [title, '판 | net | ×전판 | 구매 수 | 산 것 | 다음 가격/판 수입 | 수확기 | 군락 | 시간 | 점수 | 정산']
    prev = None
    for r in sim:
        L.append(f"{r['n']} | {fmt(r['net'])} | {('×%.2f' % (r['net'] / prev)) if prev else '-'} | {r['buys']} | "
                 f"{' '.join(f'{k}{l}' for k, l, c in r['bought'])} | {r['ratio']:.2f} ({r['nextKey']} {fmt(r['next'])}) | {r['balls']} | {r['cols']} | {r['dur']:.1f} | {r['score']:.1f} | {r['price']:.2f}"
                 if False else
                 f"{r['n']} | {fmt(r['net'])} | {('×%.2f' % (r['net'] / prev)) if prev else '-'} | {r['buys']} | "
                 f"{' '.join(f'{k}{l}' for k, l, c in r['bought'])} | {r['ratio']:.2f} ({r['nextKey']} {fmt(r['next'])}) | {r['balls']} | {r['cols']} | {r['dur']:.1f} | {r['score']:.1f} | {r['goldMul']:.2f}")
        prev = r['net']
    return L


if __name__ == '__main__':
    nodes, consts, boss = load(drop=('homing',))
    rows = t80_rows()
    fe, rmse = fit_fe(nodes, rows)
    out = [f'FE 잔차 RMSE(ln, 판 1~23 일반 판, 시드 사이 흔들림) {rmse:.3f} (×{math.exp(rmse):.2f})']
    bot = simulate(nodes, fe, nmax=23)
    med = {n: S.median([r['net'] for r in rows if r['stage'] == n and r['bossFlag'] == 0]) for n in fe}
    err = [math.log(r['net'] / med[r['n']]) for r in bot if r['n'] in med]
    out.append(f'봇 재현(지금 트리, 결정적 진행 vs T8-0 중앙) RMSE(ln) {np.sqrt(np.mean(np.square(err))):.3f} · 판별 ' +
               ' '.join(f"{r['n']}:{math.exp(e):.2f}" for r, e in zip([r for r in bot if r['n'] in med], err)))
    out += table(bot, '\n[봇 · 지금 트리]')
    a0, sl, hr, herr = a_human_fit(nodes, fe)
    out.append(f'\n사람 배율 A(n) = {a0:.2f} {sl:+.4f}·(n−1) (하한 0.15) · 지표 B 내부 1~13 RMSE(ln) {hr:.3f} · 판별 ' + ' '.join(f'{i + 1}:{math.exp(e):.2f}' for i, e in enumerate(herr)))
    hum = simulate(nodes, fe, lambda n: max(0.15, a0 + sl * (n - 1)), nmax=23)
    out += table(hum, '\n[사람 · 지금 트리]')
    json.dump(dict(fe=fe, rmse=rmse, a0=a0, slope=sl, human_rmse=hr, bot=bot, human=hum), open(os.path.join(HERE, 'model8_base.json'), 'w', encoding='utf-8'), ensure_ascii=False, default=str)
    txt = '\n'.join(out)
    open(os.path.join(HERE, 'model8_base_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    print(txt)
