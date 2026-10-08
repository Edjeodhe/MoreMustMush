# -*- coding: utf-8 -*-
"""MoreMustMush 판 수입 근사 모델 + "가장 싼 것부터 전부" 진행 재계산.

사용:  python model.py            → 적합·재현·조건 점검, theory_fit_<프리셋>.json · theory_curve_<프리셋>.csv
       (프리셋·기록 폴더는 cfg.py — 기본 tuned T2, spec 은 THEORY_PRESET=spec)
       python model.py --prices X.json   → 다른 가격표로 진행만 계산

판 수입(점수골드+보너스) 근사:
    I = 10^c · Z · (s̄ + 타격점수 고정값) · T · (1 + κ·콤보계수) · B^β · C^γ · 10^(δ·스킬수)
      Z  지역 배율 10^지역 · 1.01^(판-1)  (지역 = BalanceSim.PickZone 규칙, 공격력이 일반 체력×Z의 절반 이상)
      T  라운드 시간(초), B 영구 핀볼 수, C 군락지 수(필드 넓이 반영), 스킬수 = 해금한 스킬 15종 중 개수
    Z·(s̄+고정값)·T 는 코드 구조 그대로(지수 1), 나머지 계수는 시뮬 기록 312판(시드 3개 × 104판)에 log10 최소제곱으로 맞춘다.
결정적(같은 입력 → 같은 출력). 몬테카를로 잡음도 고정 시드.
"""
import argparse, csv, json, math, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nodes import NODES, NODE, GOLD_NODES, load_prices
from replay import replay, bulk_buy, cost, parent_ok
from features import stats, atk_flat, atk_mul, zone_mul, THEME_FROM
import cfg
from replay import data_prices

OUT = os.path.dirname(os.path.abspath(__file__))
OVERHEAD = 30.0            # 한 판 = 라운드 시간 + 30초 (BalanceSim.OVERHEAD_SEC)
SEEDS = (1, 2, 3)
KAPPA_GRID = [0, 1, 2, 5, 10, 20, 35, 50, 100, 200, 500, 1000, None]   # None = κ→∞ (점수 ∝ 콤보 계수)   # verify M2: 격자를 넓힘
SBAR_GRID = [0.1, 0.3, 0.5, 1, 1.5, 2, 3, 5, 10]
CROSS = 0.66   # 효율 넘김 = 조사.md P7, 허용오차 없음 (verify 수정 요청 1, BalanceSim.CROSS)


# ---------- 1. 근사식 적합 ----------
ZONE_TERMS = True   # verify 2단계(2) 요청 1: 지역별 상수(밤·들판·바다) — 들판 수입 ×0.7 과소 보정


def design(s):
    d = [1.0, math.log10(s["balls"]), math.log10(s["cols"]), s["skills"]]
    if ZONE_TERMS:
        d += [float(s["zone"] == 1), float(s["zone"] == 2), float(s["zone"] >= 3)]
    return d


def fixed_part(s, sbar, kappa):
    combo = s["comboK"] if kappa is None else 1 + kappa * s["comboK"]
    return math.log10(s["Z"] * (sbar + s["sflat"]) * s["dur"] * combo)


def sim_rows():
    rows = []
    for sd in SEEDS:
        for r in replay(sd):
            s = stats(r["levels_before"], r["stage"])
            s.update(inc=r["inc"], seed=sd)
            rows.append(s)
    return rows


def fit(rows):
    best = None
    for kappa in KAPPA_GRID:
        for sbar in SBAR_GRID:
            X = np.array([design(s) for s in rows])
            y = np.array([math.log10(s["inc"]) - fixed_part(s, sbar, kappa) for s in rows])
            b, *_ = np.linalg.lstsq(X, y, rcond=None)
            e = y - X @ b
            rmse = float(np.sqrt((e ** 2).mean()))
            if best is None or rmse < best["rmse"] - 1e-9:
                best = dict(kappa=kappa, sbar=sbar, coef=[float(v) for v in b], rmse=rmse)
    return best


def predict_log(s, p):
    return fixed_part(s, p["sbar"], p["kappa"]) + float(np.dot(design(s), p["coef"]))


def fit_report(rows, p):
    e = np.array([math.log10(s["inc"]) - predict_log(s, p) for s in rows])
    out = dict(rmse_log10=float(np.sqrt((e ** 2).mean())), mean_bias_log10=float(e.mean()),
               median_abs_ratio=float(10 ** np.median(np.abs(e))), p90_abs_ratio=float(10 ** np.percentile(np.abs(e), 90)))
    # 판 구간별 편향
    bins = [(1, 19), (20, 44), (45, 74), (75, 104)]
    out["bias_by_zone"] = {int(z): float(np.mean([x for x, s in zip(e, rows) if s["zone"] == z])) for z in sorted({s["zone"] for s in rows})}
    out["bias_by_range"] = {}
    for a, b in bins:
        sel = [x for x, s in zip(e, rows) if a <= s["stage"] <= b]
        out["bias_by_range"][f"R{a}-R{b}"] = dict(mean=float(np.mean(sel)), rmse=float(np.sqrt(np.mean(np.square(sel)))))
    # 시드 안 잔차 자기상관(1판 차이) — 잡음 모형에 쓴다
    ac = []
    for sd in SEEDS:
        es = [x for x, s in zip(e, rows) if s["seed"] == sd]
        ac.append(float(np.corrcoef(es[:-1], es[1:])[0, 1]))
    out["lag1_autocorr"] = ac
    # 시드 사이 흔들림(같은 판, 같은 모델 예측에서 시드끼리의 차) — 모델 오차가 아니라 시뮬 자체 잡음의 하한
    return out, e


# ---------- 2. 진행 재계산 ----------
def progression(prices, p, rounds=104, noise=None, rng=None, sigma=0.0, rho_ac=0.0):
    """새 게임에서 판마다: 수입 = 모델 예측(×잡음) → 가장 싼 것부터 전부. 시뮬(BalanceSim.Progression)과 같은 순서."""
    lv, gold, t = {}, 0.0, 0.0
    tiers = {1: [], 2: [], 3: []}
    for n in GOLD_NODES:
        tiers[n["tier"]].append(n)
    maxlv = {k: sum(n["max"] for n in v) for k, v in tiers.items()}
    first, done = {}, {}
    rec, eps = [], 0.0
    for i in range(rounds):
        stage = i + 1
        s = stats(lv, stage)
        logi = predict_log(s, p)
        if rng is not None:
            eps = rho_ac * eps + math.sqrt(1 - rho_ac ** 2) * rng.normal(0, sigma)
            logi += eps
        inc = 10 ** logi
        gold += inc
        gold, k = bulk_buy(lv, gold, prices)
        t += s["dur"] + OVERHEAD
        for tr in (1, 2, 3):
            have = sum(lv.get(n["id"], 0) for n in tiers[tr])
            if tr not in first and have > 0:
                first[tr] = (stage, t / 60)
            if tr not in done and have == maxlv[tr]:
                done[tr] = (stage, t / 60)
        rec.append(dict(stage=stage, zone=s["zone"], Z=s["Z"], inc=inc, bought=k, gold=gold, minutes=t / 60,
                        levels=sum(lv.values())))
    return dict(first=first, done=done, rec=rec, buy_rounds=sum(1 for r in rec if r["bought"] > 0))


OOS = [(os.path.join(cfg.ROOT, "BalanceData", "test", "before_1414"), (4, 5, 6))] if cfg.PRESET == "tuned" else []


def oos_report(p):
    """표본 밖 시드(맞출 때 안 쓴 기록)에서 같은 계수로 잰 오차"""
    out = {}
    for data, seeds in OOS:
        if not os.path.exists(data):
            continue
        e, by = [], {}
        for sd in seeds:
            for r in replay(sd, data=data):
                s = stats(r["levels_before"], r["stage"])
                x = math.log10(r["inc"]) - predict_log(s, p)
                e.append(x)
                by.setdefault(s["zone"], []).append(x)
        out[os.path.basename(data)] = dict(seeds=list(seeds), n=len(e), rmse_log10=float(np.sqrt(np.mean(np.square(e)))),
                                           mean_bias=float(np.mean(e)), bias_by_zone={int(z): float(np.mean(v)) for z, v in sorted(by.items())})
    return out


def sim_summary():
    """시뮬 기록(시드 1~3)의 같은 지표 — 모델 재현 비교용"""
    from replay import load_phase
    out = {}
    for sd in SEEDS:
        ph = load_phase(sd)
        rows = replay(sd)
        lim = max(ph["done"][1:])
        z = {}
        for r in rows:
            zz = stats(r["levels_before"], r["stage"])["zone"]
            for k in (1, 2, 3):
                if zz >= k and k not in z:
                    z[k] = r["stage"]
        g = cur = 0
        for b in ph["buys"][:lim]:
            cur = cur + 1 if b == 0 else 0
            g = max(g, cur)
        out[sd] = dict(first=ph["first"][1:], done=ph["done"][1:], finish=lim, done_min=round(ph["doneMin"][3], 1),
                       zone_entry={"밤": z.get(1), "들판": z.get(2), "바다": z.get(3)},
                       buy_round_share=sum(1 for b in ph["buys"][:lim] if b > 0) / lim, max_gap_rounds=g,
                       p6=p6_check(ph["inc"][:lim], [stats(r["levels_before"], r["stage"])["zone"] for r in rows[:lim]]))
    return out


def p6_check(incs, zones, Zs=None):
    """조사.md P6: 5판 묶음(R1~5, R6~10, …) 중앙값 수입의 묶음 간 비. 뒤 묶음에 지역이 바뀐 판이 있으면 ≤ ×10, 아니면 ≤ ×5.
    (최대 비, 어긴 묶음 수)"""
    med = [float(np.median(incs[i:i + 5])) for i in range(0, len(incs) - 4, 5)]
    mx, bad = 0.0, 0
    self_where = None
    for j in range(1, len(med)):
        r = med[j] / max(1.0, med[j - 1])
        if r > mx:
            zpart = None
            if Zs is not None:
                zpart = float(np.median(Zs[5 * j:5 * j + 5]) / np.median(Zs[5 * j - 5:5 * j]))
            self_where = (5 * j + 1, zpart)
        mx = max(mx, r)
        zchg = any(zones[k] != zones[k - 1] for k in range(5 * j, min(len(zones), 5 * j + 5)))
        if r > (10 if zchg else 5):
            bad += 1
    if Zs is not None:
        return mx, bad, self_where
    return mx, bad


def sim_p6():
    from replay import load_phase
    out = {}
    for sd in SEEDS:
        ph = load_phase(sd)
        rows = replay(sd)
        lim = max(ph["done"][1:])
        zs = [stats(r["levels_before"], r["stage"])["zone"] for r in rows[:lim]]
        out[sd] = p6_check(ph["inc"][:lim], zs)
    return out


def finish_round(pr):
    d = pr["done"]
    return max(d[t][0] for t in (1, 2, 3)) if all(t in d for t in (1, 2, 3)) else None


def monte_carlo(prices, p, sigma, rho_ac, n=200, seed=7):
    rng = np.random.default_rng(seed)
    fins, mins, buyr, gaps = [], [], [], []
    ph = {k: [] for k in ("first1", "done1", "first2", "done2", "first3", "done3")}
    zones = {1: [], 2: [], 3: []}
    p6 = []
    for _ in range(n):
        pr = progression(prices, p, rounds=140, rng=rng, sigma=sigma, rho_ac=rho_ac)
        f = finish_round(pr)
        fins.append(f if f else 141)
        mins.append(pr["done"][3][1] if f else float("nan"))
        for t in (1, 2, 3):
            ph[f"first{t}"].append(pr["first"].get(t, (141,))[0])
            ph[f"done{t}"].append(pr["done"].get(t, (141,))[0])
        for z in (1, 2, 3):
            zones[z].append(next((r["stage"] for r in pr["rec"] if r["zone"] >= z), 141))
        lim = f if f else 140
        buyr.append(sum(1 for r in pr["rec"][:lim] if r["bought"] > 0) / lim)
        g = cur = 0
        for r in pr["rec"][:lim]:
            cur = cur + 1 if r["bought"] == 0 else 0
            g = max(g, cur)
        gaps.append(g)
        p6.append(p6_check([r["inc"] for r in pr["rec"][:lim]], [r["zone"] for r in pr["rec"][:lim]], [r["Z"] for r in pr["rec"][:lim]]))
    fins = np.array(fins)
    q = lambda v: [float(np.percentile(v, 10)), float(np.median(v)), float(np.percentile(v, 90))] if len(v) else None
    return dict(n=n, sigma=sigma, rho=rho_ac, p10=float(np.percentile(fins, 10)), median=float(np.median(fins)),
                p90=float(np.percentile(fins, 90)), unfinished=int((fins > 140).sum()),
                finish_minutes=q([m for m in mins if m == m]),
                phases_p10_med_p90={k: q(v) for k, v in ph.items()},
                zone_entry_p10_med_p90={["", "밤", "들판", "바다"][z]: q(v) for z, v in zones.items()},
                buy_round_share=q(buyr), max_gap_rounds=q(gaps),
                p6_max_ratio=q([x[0] for x in p6]), p6_violations=q([x[1] for x in p6]),
                p6_max_block_start=q([x[2][0] for x in p6 if x[2]]),
                p6_max_zone_part=q([x[2][1] for x in p6 if x[2] and x[2][1]]))


# ---------- 3. 가격 모양 조건 ----------
TIER_LINES = [  # BalanceSim.TIER_LINES 와 같음
    (["ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"], [1, 10, 10, 100, 100, 1000]),
    (["ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p"], [1, 1, 10, 10, 100, 100]),
    (["ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p"], [1, 1, 10, 10, 100, 100]),
]


def shape_check(prices):
    vals = sorted(cost(n, L, prices) for n in GOLD_NODES if not n["core"] for L in range(n["max"]))
    max_step = max(b / max(1, a) for a, b in zip(vals, vals[1:]))
    node_step = 1.0
    for n in GOLD_NODES:
        if n["core"]:
            continue
        for L in range(1, n["max"]):
            node_step = max(node_step, cost(n, L, prices) / max(1, cost(n, L - 1, prices)))
    ok = tot = ok7 = 0
    worst_cross = 0.0
    for ids, per in TIER_LINES:
        for t in range(1, len(ids)):
            a, b = NODE[ids[t - 1]], NODE[ids[t]]
            if per[t] == per[t - 1]:
                node_step = max(node_step, cost(b, 0, prices) / max(1, cost(a, a["max"] - 1, prices)))
                continue
            tot += 1
            r = (cost(b, 0, prices) / per[t]) / (cost(a, a["max"] - 1, prices) / per[t - 1])
            worst_cross = max(worst_cross, r)
            if r <= CROSS:   # BalanceSim.PriceCheck (관리자 변경: CROSS 0.66, 허용오차 ×1.001 — 여기선 0)
                ok += 1
            if r <= 1 / 1.5 + 1e-9:   # 조사.md P7 (효율 ≥ ×1.5)
                ok7 += 1
    # 사는 순서(시간 순) 기준 가격 뜀: 판 안에서 여러 개를 사면 그 판의 최고가 대비 다음 판 첫 가격
    return dict(max_step_sorted=max_step, node_step=node_step, cross_ok=ok, cross_total=tot, cross_p7_ok=ok7, worst_cross_ratio=worst_cross,
                n_levels=len(vals), total_gold=sum(vals))


# ---------- 4. 효과 없는(판 수입과 무관한) 노드의 골드 비중 ----------
NO_ROUND_GOLD = {  # 시뮬(세금 제외·상점 판매 없음)에서 판 골드에 닿지 않는 노드
    "ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p",  # 수확 개수 → 창고(판매 안 함)
    "ed_price", "ed_price2",  # 판매가 (엽전 수확기 없으면 무관)
    "ed_tax", "ed_trade", "md_chef", "ed_multi",  # 세금·의뢰·농장 건설·수확 ×3(창고)
}


def dead_share(prices):
    tot = dead = 0.0
    per = {}
    for n in GOLD_NODES:
        if n["core"]:
            continue
        c = sum(cost(n, L, prices) for L in range(n["max"]))
        tot += c
        if n["id"] in NO_ROUND_GOLD:
            dead += c
            per[n["id"]] = c
    return dict(dead_gold=dead, total_gold=tot, share=dead / tot, nodes=per)


def compare(folder, p, rep, ramp):
    """T3·T4 같은 새 기록과 모델 예측 대조. 모델 계수는 cfg 기록(T2)으로 맞춘 그대로 쓴다(표본 밖 예측)."""
    import features as F
    from replay import load_phase
    d = folder if os.path.isabs(folder) else os.path.join(cfg.ROOT, folder)
    if ramp is not None:
        F.ZONE_RAMP = ramp
    prices = load_prices(os.path.join(d, "price_table.json"))
    sigma, rho_ac = rep["rmse_log10"], max(0.0, float(np.mean(rep["lag1_autocorr"])))
    mc = monte_carlo(prices, p, sigma, rho_ac)
    sims = {}
    for sd in range(1, 10):
        if os.path.exists(os.path.join(d, f"balance_phase_s{sd}.json")):
            ph = load_phase(sd, d)
            sims[sd] = dict(first=ph["first"][1:], done=ph["done"][1:], finish=max(ph["done"][1:]))
            # 판 수입 오차(그 기록의 구매 경로에서)
            ee = []
            for r in replay(sd, data=d):
                ee.append(math.log10(max(1, r["inc"])) - predict_log(stats(r["levels_before"], r["stage"]), p))
            sims[sd]["rmse_log10"] = float(np.sqrt(np.mean(np.square(ee))))
            sims[sd]["bias_log10"] = float(np.mean(ee))
    res = dict(folder=d, ramp=F.ZONE_RAMP, model=dict(finish=[mc["p10"], mc["median"], mc["p90"]], phases=mc["phases_p10_med_p90"],
                                                     zones=mc["zone_entry_p10_med_p90"], p6_max=mc["p6_max_ratio"]), sim=sims)
    print(json.dumps(res, ensure_ascii=False, indent=1))
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prices", default=None)
    ap.add_argument("--quiet", action="store_true")
    ap.add_argument("--compare", default=None, help="다른 시뮬 기록 폴더(예: T3·T4): 모델은 cfg 기록으로 맞추고, 그 폴더의 가격표로 예측해 그 폴더 실측과 대조")
    ap.add_argument("--ramp", type=int, default=None, help="--compare 때 지역 램프(Defs.ZONE_RAMP)")
    a = ap.parse_args()
    rows = sim_rows()
    p = fit(rows)
    rep, e = fit_report(rows, p)
    if a.compare:
        return compare(a.compare, p, rep, a.ramp)
    prices = load_prices(a.prices) if a.prices else data_prices()
    det = progression(prices, p, rounds=140)
    sigma = rep["rmse_log10"]
    rho_ac = max(0.0, float(np.mean(rep["lag1_autocorr"])))
    mc = monte_carlo(prices, p, sigma, rho_ac)
    # verify 수정 요청 4: 모델 잡음 크기 민감도 (p10~p90 은 모델 불확실성, 플레이어 편차 아님)
    mc_sig = {f"sigma×{k}": monte_carlo(prices, p, sigma * k, rho_ac, n=120)["median"] for k in (0.0, 0.75, 1.25)}
    shp = shape_check(prices)
    dead = dead_share(prices)
    res = dict(params=p, fit=rep, deterministic=dict(first={k: v for k, v in det["first"].items()}, done={k: v for k, v in det["done"].items()},
                                                     finish=finish_round(det), buy_rounds_104=sum(1 for r in det["rec"][:104] if r["bought"] > 0)),
               monte_carlo=mc, sigma_sensitivity=mc_sig, out_of_sample=oos_report(p), preset=cfg.PRESET, data=cfg.DATA, atk_points=__import__("features").atk_points(), sim=sim_summary(), shape=shp, dead=dict(share=dead["share"], dead_gold=dead["dead_gold"], total_gold=dead["total_gold"]))
    if not a.prices:
        json.dump(res, open(os.path.join(OUT, f"theory_fit_{cfg.TAG}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        with open(os.path.join(OUT, f"theory_curve_{cfg.TAG}.csv"), "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["stage", "zone", "model_income", "bought", "gold_after", "minutes", "levels"])
            for r in det["rec"]:
                w.writerow([r["stage"], r["zone"], f"{r['inc']:.4g}", r["bought"], f"{r['gold']:.4g}", f"{r['minutes']:.1f}", r["levels"]])
    out = sys.stdout
    print(json.dumps(res, ensure_ascii=False, indent=1), file=out)
    return res


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
