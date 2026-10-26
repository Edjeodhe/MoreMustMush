# -*- coding: utf-8 -*-
"""5회차 제안안 계산 (r5_model.py의 모델을 쓴다). 실행: python BalanceData/r5/theory/r5_main.py
결과: r5_out.json · r5_out.txt · r5_curve.csv (난수 시드 고정, 다시 돌리면 같다)"""
import csv
import json
import math
import os
import random
import statistics as stx

import r5_model as M

HERE = M.HERE
TUT = 4
P1_COST = 0.9
P3_MUL = 0.9
PS_BALL_NEW = [800.0, 2400.0]                       # P-4 (DB 값, 표시는 ×COST_MUL)
PS_BALL3_NEW = [990e9, 1.4e12, 2.0e12, 3.0e12]      # P-4 후반 +1 두 번 (ps_ball3 2 → 4레벨)
G1_TARGETS = {"king": None, "cleaner": 2.3, "angel": 2.3, "phantom": 4.0, "mover": 5.0}   # king은 지금 값 유지(P-2만으로 1~2번)


def early_hp_new(s):  # P-6: 내부 판 1~13(T1~T4 + 표시 1~9) ×0.9
    return M.early_hp_now(s) * (0.9 if s <= TUT + 9 else 1.0)


def sc_now():
    return M.scenario("NOW", hp=dict(M.HP_NOW), early_hp=M.early_hp_now, loose=True)


def sc_prop(hp, **kw):
    base = dict(hp=dict(hp), off=TUT, cost_mul=P1_COST, early_hp=early_hp_new, loose=True, ps_ball=PS_BALL_NEW,
                ps_ball3=PS_BALL3_NEW, spore=True, growth_shift=True)
    base.update(kw)
    return M.scenario("PROP", **base)


def solve_hp(nodes, fr, st, mk, hp0, targets, kappa, sigma):
    hp = dict(hp0)
    for i, b in enumerate(M.BOSS_ORDER):
        stage = 10 * (i + 1)
        if targets.get(b) is None:
            continue
        lo, hi = math.log(hp[b]) - 3, math.log(hp[b]) + 3
        for _ in range(15):
            mid = (lo + hi) / 2
            hp[b] = math.exp(mid)
            m = M.mc(nodes, fr, st, mk(hp), kappa, sigma, runs=300, seed=31 + i, stop_disp=stage + 1)[stage]["mean"]
            if m > targets[b]:
                hi = mid
            else:
                lo = mid
        hp[b] = math.exp((lo + hi) / 2)
    return hp


def nice(x):
    if not x or x <= 0:
        return 0
    e = math.floor(math.log10(x)) - 1
    return round(x / 10 ** e) * 10 ** e


def stage_income(nodes, fr, st, sc, kappa, sigma, disp_list, runs=300, seed=77):
    rng = random.Random(seed)
    acc = {d: [] for d in disp_list}
    for _ in range(runs):
        o = M.simulate(nodes, fr, st, sc, kappa, sigma, rng, stop_disp=max(disp_list) + 1, record=True)
        for c in o["curve"]:
            if c["disp"] in acc and not (c["disp"] >= 10 and c["disp"] % 10 == 0):
                acc[c["disp"]].append(c["inc"])
    return {d: (stx.mean(v) if v else None) for d, v in acc.items()}


def long_run(nodes, fr, st, sc, kappa, sigma, runs=150, seed=99, harv=None):
    rng = random.Random(seed)
    done = []
    for _ in range(runs):
        o = M.simulate(nodes, fr, st, sc, kappa, sigma, rng, stop_disp=400, record=False, harv=harv() if harv else None)
        done.append(o["tree_done"] or (999, 9999.0, 0))
    rs = sorted(x[0] for x in done)
    ms = sorted(x[1] for x in done)
    return {"tree_round_med": rs[len(rs) // 2], "tree_min_med": ms[len(ms) // 2],
            "tree_round_p10_p90": (rs[len(rs) // 10], rs[len(rs) * 9 // 10]), "tree_disp_med": sorted(x[2] for x in done)[len(done) // 2]}


def harvester_buyer(prices):
    """P-5 공방 지출 상한: n번째 수확기를 표시 15n부터 살 수 있으면 해금하고 레벨업(lvGold × 0.6·1.2·2.4·4.8)을 트리보다 먼저 산다."""
    def make():
        hs = {"owned": 0, "lv": {}}

        def buy(state, gold, d, r):
            for i, (unlock, lvgold) in enumerate(prices):
                if i >= state["owned"]:
                    if d >= 15 * (i + 1) and gold >= unlock:
                        gold -= unlock
                        state["owned"] = i + 1
                        state["lv"][i] = 1
                    else:
                        break
                while i in state["lv"] and state["lv"][i] < 5:
                    c = math.ceil(lvgold * [0, 0.6, 1.2, 2.4, 4.8][state["lv"][i]])
                    if gold < c:
                        break
                    gold -= c
                    state["lv"][i] += 1
            return gold
        return {"init": hs, "buy": buy}
    return make


def fmt_row(name, r):
    return f"{name}: " + " · ".join(
        f"{b}: 평균 {r[b]['mean']} 중앙 {r[b]['median']} (1번 {r[b]['p_le1']:.0%}, ≤3 {r[b]['p_le3']:.0%}, p10~p90 {r[b]['p10']}~{r[b]['p90']}, 도착 판 {r[b]['arrive_round_med']})"
        for b in (10, 20, 30, 40, 50)) + f" · 50 처치 {r['kill50_min_median']}분 R{r['kill50_round_median']}"


def main():
    nodes, sinks, db = M.load_db()
    fr, st, raw = M.income_tables(M.parse_logs())
    out = {"income_forest_by_round": fr, "income_by_stage_raw": raw}
    cache = os.path.join(HERE, "r5_calib.json")
    if os.path.exists(cache):
        c = json.load(open(cache, encoding="utf-8"))
        err, sigma, kappa, m08 = c["err"], c["sigma"], c["kappa"], c["m08"]
    else:
        err, sigma, kappa, m08 = M.calibrate(nodes, fr, st)
        json.dump({"err": err, "sigma": sigma, "kappa": kappa, "m08": m08}, open(cache, "w", encoding="utf-8"), indent=1)
    out["calib"] = {"sigma": sigma, "kappa": kappa, "king0.8_mean_tries": m08, "log_err": err}
    S4 = M.scenario("S4")
    out["S4"] = M.mc(nodes, fr, st, S4, kappa, sigma)
    out["NOW"] = M.mc(nodes, fr, st, sc_now(), kappa, sigma)
    hp_lit = {b: M.HP_NOW[b] * (1 if b == "king" else P3_MUL) for b in M.BOSS_ORDER}
    out["PROP_literal_hp"] = hp_lit
    out["PROP_literal"] = M.mc(nodes, fr, st, sc_prop(hp_lit), kappa, sigma)
    print("calib", sigma, kappa, m08, flush=True)
    for nm in ("S4", "NOW", "PROP_literal"):
        print(fmt_row(nm, out[nm]), flush=True)
    hp_fit = solve_hp(nodes, fr, st, sc_prop, hp_lit, G1_TARGETS, kappa, sigma)
    hp_nice = {b: float(f"{hp_fit[b]:.3g}") for b in M.BOSS_ORDER}
    out["G1_targets"] = G1_TARGETS
    out["PROP_fit_hp_raw"] = hp_fit
    out["PROP_fit_hp"] = hp_nice
    out["PROP_fit"] = M.mc(nodes, fr, st, sc_prop(hp_nice), kappa, sigma)
    print("fit", hp_nice, flush=True)
    print(fmt_row("PROP_fit", out["PROP_fit"]), flush=True)
    sens = {
        "beta0.3": sc_prop(hp_nice, beta=0.3), "beta1.0": sc_prop(hp_nice, beta=1.0),
        "time_display": sc_prop(hp_nice, time_key="display"), "no_P2": sc_prop(hp_nice, off=0, growth_shift=False),
        "ps_ball_old": sc_prop(hp_nice, ps_ball=None), "ps_ball3_old": sc_prop(hp_nice, ps_ball3=None),
        "early_income_x0.7": sc_prop(hp_nice, map_inc=0.7), "no_P1": sc_prop(hp_nice, cost_mul=1.0),
        "alpha_b0": sc_prop(hp_nice, alpha_b=0.0), "alpha_b0.75": sc_prop(hp_nice, alpha_b=0.75),
    }
    out["sens"] = {k: M.mc(nodes, fr, st, v, kappa, sigma, runs=1200) for k, v in sens.items()}
    # 다른 보정(σ) 으로 같은 Hp를 봤을 때
    alt = {}
    for sg in (0.2, 0.3, 0.45):
        if abs(sg - sigma) < 1e-9:
            continue
        kap2 = M.fit_kappa(nodes, fr, st, S4, sg)
        alt[f"sigma{sg}"] = {"NOW": M.mc(nodes, fr, st, sc_now(), kap2, sg, runs=1200), "PROP_fit": M.mc(nodes, fr, st, sc_prop(hp_nice), kap2, sg, runs=1200)}
    out["sens_sigma"] = alt

    disp = [15, 30, 45, 60, 75, 90]
    probe = sorted({x for dd in disp for x in (dd - 1, dd + 1, dd) if not (x % 10 == 0)})
    A = stage_income(nodes, fr, st, sc_prop(hp_nice), kappa, sigma, probe)
    A15 = {}
    for dd in disp:
        vals = [A.get(x) for x in (dd - 1, dd, dd + 1) if A.get(x)]
        A15[dd] = stx.mean(vals) if vals else None
    out["A_15n_model"] = A15
    out["A_15n_log_raw"] = {dd: raw.get(dd) for dd in disp}
    order = ["coin", "saw", "hoe", "spore", "mill", "bell"]
    shop = []
    for i, k in enumerate(order):
        a = A15[disp[i]]
        lvg = nice(2.5 * a)
        shop.append({"n": i + 1, "ball": k, "disp_stage": disp[i], "A": a, "unlock": nice(2 * a), "lvGold": lvg,
                     "lvup": [lvg * m for m in (0.6, 1.2, 2.4, 4.8)]})
    out["shop"] = shop
    out["tree_PROP_alpha0"] = long_run(nodes, fr, st, sc_prop(hp_nice, alpha_b=0.0), kappa, sigma)
    out["tree_PROP_alpha0.75"] = long_run(nodes, fr, st, sc_prop(hp_nice, alpha_b=0.75), kappa, sigma)
    out["tree_S4"] = long_run(nodes, fr, st, S4, kappa, sigma)
    out["tree_NOW"] = long_run(nodes, fr, st, sc_now(), kappa, sigma)
    out["tree_PROP"] = long_run(nodes, fr, st, sc_prop(hp_nice), kappa, sigma)
    out["tree_PROP_shop"] = long_run(nodes, fr, st, sc_prop(hp_nice), kappa, sigma, harv=harvester_buyer([(x["unlock"], x["lvGold"]) for x in shop]))

    o = M.simulate(nodes, fr, st, sc_prop(hp_nice), kappa, sigma, None, stop_disp=60, record=True)
    with open(os.path.join(HERE, "r5_curve.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["round", "stage_int", "disp", "inc", "gold_after", "dps", "balls", "min"])
        for c in o["curve"]:
            w.writerow([c["round"], c["stage_int"], c["disp"], f"{c['inc']:.4g}", f"{c['gold_after']:.4g}", f"{c['dps']:.4g}", c["balls"], c["min"]])
    out["det_prop_buys_first30"] = [x for x in o["log"] if x[0] <= 30]
    o2 = M.simulate(nodes, fr, st, sc_now(), kappa, sigma, None, stop_disp=60, record=True)
    out["det_now_buys_first30"] = [x for x in o2["log"] if x[0] <= 30]
    out["det_now_balls"] = [c["balls"] for c in o2["curve"][:40]]
    out["det_prop_balls"] = [c["balls"] for c in o["curve"][:40]]
    out["det_now_dps"] = [round(c["dps"], 2) for c in o2["curve"][:40]]
    out["det_prop_dps"] = [round(c["dps"], 2) for c in o["curve"][:40]]

    with open(os.path.join(HERE, "r5_out.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1, default=str)
    L = [f"보정: SIGMA {sigma} · KAPPA " + ", ".join(f"{k} {v:.3g}" for k, v in kappa.items()) + f" · king 0.8 → {m08:.1f}번 (측정 12~13)"]
    for name in ("S4", "NOW", "PROP_literal", "PROP_fit"):
        L.append(fmt_row(name, out[name]))
    L.append(f"PROP_literal Hp {hp_lit}")
    L.append(f"PROP_fit Hp {hp_nice}")
    for k, r in out["sens"].items():
        L.append(f"민감도 {k}: " + " · ".join(f"{b} {r[b]['mean']}(1번 {r[b]['p_le1']:.0%})" for b in (10, 20, 30, 40, 50)) + f" · 50 처치 {r['kill50_min_median']}분")
    for k, v in alt.items():
        for kk, r in v.items():
            L.append(f"{k} {kk}: " + " · ".join(f"{b} {r[b]['mean']}" for b in (10, 20, 30, 40, 50)))
    L.append("A(15n) 모델: " + ", ".join(f"{k}: {v:.3g}" for k, v in A15.items()) + " | 기록 원값: " + ", ".join(f"{k}: {v:.3g}" for k, v in out["A_15n_log_raw"].items() if v))
    L.append("공방: " + " | ".join(f"{x['n']} {x['ball']} @{x['disp_stage']} 해금 {x['unlock']:.3g} lvGold {x['lvGold']:.3g}" for x in shop))
    for k in ("tree_S4", "tree_NOW", "tree_PROP", "tree_PROP_shop", "tree_PROP_alpha0", "tree_PROP_alpha0.75"):
        L.append(f"{k}: {out[k]}")
    L.append("영구 수확기 수 판 1~40 지금: " + "".join(str(x) for x in out["det_now_balls"]))
    L.append("영구 수확기 수 판 1~40 제안: " + "".join(str(x) for x in out["det_prop_balls"]))
    L.append("dps 판 1~40 지금: " + str(out["det_now_dps"]))
    L.append("dps 판 1~40 제안: " + str(out["det_prop_dps"]))
    txt = "\n".join(L)
    open(os.path.join(HERE, "r5_out.txt"), "w", encoding="utf-8").write(txt + "\n")
    print(txt)


if __name__ == "__main__":
    main()
