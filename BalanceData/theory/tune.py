# -*- coding: utf-8 -*-
"""모델로 가격표를 다시 매긴다 (BalanceSim.TunePrices + EnforceShape 를 근사 모델 위에서, 더 엄한 모양 조건으로).

사는 순서: BalanceSim.OrderedLevels 와 같음(노드 기본가 × 증가율^레벨 순, 코어 제외).
k번째 레벨 가격 = 모델 진행(몬테카를로 중앙값)의 누적 수입 곡선에서 [finish·k/N, finish·(k+1)/N] 구간에 버는 골드.
그다음 모양 조건(올리기만): 정렬 가격 바로 앞 대비 ≤ STEP_FIX, 노드 안 ≤ NODE_STEP_MAX, 효율 넘김 ≤ CROSS.
감쇠 고정점 반복 후 finish 비율을 이분해 완료 중앙값을 목표 판에 맞춘다.
결정적: 같은 입력이면 같은 가격표(몬테카를로 시드 고정).

사용: python tune.py [--target 104] [--step 1.5]  → theory_prices.json (+ 표준출력 요약)
"""
import argparse, json, math, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nodes import NODES, NODE, GOLD_NODES, load_prices
from replay import cost
import model as M

OUT = os.path.dirname(os.path.abspath(__file__))
NODE_STEP_MAX = 4.0
CROSS = 0.66     # 조사.md P7: 효율 ≥ ×1.5 → 1/1.5 = 0.667, 여유 1%


def nice(v):
    if v < 10:
        return max(1.0, round(v))
    e = 10 ** (math.floor(math.log10(v)) - 1)
    return round(v / e) * e


def nice_up(v):
    """유효숫자 2자리로 올림 — 조건을 맞추려고 올릴 때 반올림이 여유를 먹지 않게(verify 수정 요청 1)"""
    if v < 10:
        return max(1.0, math.ceil(v - 1e-9))
    e = 10 ** (math.floor(math.log10(v)) - 1)
    return math.ceil(v / e - 1e-9) * e


def ordered_levels():
    lv = [(n["gold"] * n["g"] ** L, i, n["id"], L) for i, n in enumerate(GOLD_NODES) if not n["core"] for L in range(n["max"])]
    lv.sort(key=lambda x: (x[0], x[1], x[3]))  # C# OrderBy 는 안정 정렬 → 같은 값이면 NODES 순서
    return [(nid, L) for _, _, nid, L in lv]


def enforce_shape(price, step_fix):
    def raise_node(v):
        for i in range(1, len(v)):
            v[i] = max(v[i], v[i - 1])
        for i in range(len(v) - 2, -1, -1):
            v[i] = max(v[i], nice_up(v[i + 1] / NODE_STEP_MAX))
    for _ in range(6):
        flat = sorted(((price[k][i], k, i) for k in price for i in range(len(price[k]))))
        for j in range(len(flat) - 2, -1, -1):
            _, k, i = flat[j]
            _, k1, i1 = flat[j + 1]
            price[k][i] = max(price[k][i], nice_up(price[k1][i1] / step_fix))
        for v in price.values():
            raise_node(v)
        for ids, per in M.TIER_LINES:
            for t in range(len(ids) - 1, 0, -1):
                pa, pb = price[ids[t - 1]], price[ids[t]]
                need = pb[0] / NODE_STEP_MAX if per[t] == per[t - 1] else pb[0] / per[t] * per[t - 1] / CROSS
                if pa[-1] < need:
                    pa[-1] = nice_up(need)
                raise_node(pa)
    return price


def cum_curve(prices, p, sigma, rho, n=100, rounds=130, seed=11):
    """몬테카를로 판별 누적 수입의 중앙값"""
    rng = np.random.default_rng(seed)
    cums = []
    for _ in range(n):
        pr = M.progression(prices, p, rounds=rounds, rng=rng, sigma=sigma, rho_ac=rho)
        cums.append(np.cumsum([r["inc"] for r in pr["rec"]]))
    return np.concatenate([[0.0], np.median(np.array(cums), axis=0)])


def retune(prices, p, sigma, rho, finish_round, damp, step_fix, upcap):
    cum = cum_curve(prices, p, sigma, rho)
    levels = ordered_levels()
    N = len(levels)

    def C(x):
        x = max(0.0, min(len(cum) - 1, x))
        i = int(math.floor(x))
        return cum[-1] if i >= len(cum) - 1 else cum[i] + (cum[i + 1] - cum[i]) * (x - i)

    tgt, prev = [], 0.0
    for k in range(N):
        x0, x1 = finish_round * k / N, finish_round * (k + 1) / N
        prev = max(prev, C(x1) - C(x0))
        tgt.append(prev)
    pnew = []
    for k, (nid, L) in enumerate(levels):
        old = cost(NODE[nid], L, prices)
        pnew.append(math.exp(damp * math.log(max(1, tgt[k])) + (1 - damp) * math.log(max(1, old))))
    for k in range(1, N):
        pnew[k] = min(max(pnew[k], pnew[k - 1]), pnew[k - 1] * upcap)
    price = {nid: [0.0] * NODE[nid]["max"] for nid, _ in levels}
    for k, (nid, L) in enumerate(levels):
        price[nid][L] = nice(pnew[k])
    price = enforce_shape(price, step_fix)
    out = dict(prices)
    out.update(price)
    return out


def evaluate(prices, p, sigma, rho, n=200):
    mc = M.monte_carlo(prices, p, sigma, rho, n=n)
    return mc, M.shape_check(prices)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--target", type=float, default=104)
    ap.add_argument("--step", type=float, default=1.5)
    ap.add_argument("--iters", type=int, default=8)
    a = ap.parse_args()
    rows = M.sim_rows()
    p = M.fit(rows)
    rep, _ = M.fit_report(rows, p)
    sigma, rho = rep["rmse_log10"], max(0.0, float(np.mean(rep["lag1_autocorr"])))
    base = load_prices()
    log = []
    # finish 비율(가격을 다 사게 할 판)을 이분: 완료 중앙값이 목표에 들어오게
    lo, hi = 80.0, 125.0
    best = None
    for outer in range(7):
        fr = 0.5 * (lo + hi)
        pr = dict(base)
        for it in range(a.iters):
            pr = retune(pr, p, sigma, rho, fr, damp=0.6, step_fix=a.step, upcap=a.step)
        mc, shp = evaluate(pr, p, sigma, rho, n=120)
        log.append(dict(finish_param=fr, median=mc["median"], p10=mc["p10"], p90=mc["p90"], max_step=shp["max_step_sorted"],
                        node_step=shp["node_step"], cross=f"{shp['cross_ok']}/{shp['cross_total']}"))
        print(log[-1], flush=True)
        if best is None or abs(mc["median"] - a.target) < abs(best[1]["median"] - a.target):
            best = (pr, mc, shp, fr)
        if mc["median"] > a.target:
            hi = fr
        elif mc["median"] < a.target:
            lo = fr
        else:
            break
    pr, _, _, fr = best
    mc, shp = evaluate(pr, p, sigma, rho, n=300)
    res = dict(target=a.target, step_fix=a.step, finish_param=fr, monte_carlo=mc, shape=shp, search=log,
               dead=M.dead_share(pr)["share"])
    json.dump({k: [float(x) for x in v] for k, v in pr.items()}, open(os.path.join(OUT, "theory_prices.json"), "w", encoding="utf-8"), ensure_ascii=False)
    json.dump(res, open(os.path.join(OUT, "theory_tune.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(res, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
