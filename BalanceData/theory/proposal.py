# -*- coding: utf-8 -*-
"""현재 가격표(시뮬에서 수렴한 것)를 출발점으로 한 '작게 고치는' 수치안.

tune.py(처음부터 다시 매기기)는 모델 위에서도 진동했다: 가격이 조금 싸면 공격력 → 지역 ×10 되먹임으로 R35~45에 끝나고,
조금 비싸면 지역을 못 넘어 140판 안에 못 끝낸다(문서.md의 "R38 ↔ 구매율 36%" 진동과 같은 현상). 그래서 수렴한 가격표를 유지하고
아래만 바꾼다.
  1) 뜀 다듬기(B안): 정렬 가격에서 바로 앞의 STEP배를 넘는 가격을 STEP배로 낮춘 뒤, 노드 안 ≤ ×4 · 효율 넘김 ≤ 0.66 ·
     정렬 뜀 ≤ STEP 를 올리기만으로 맞춘다(BalanceSim.EnforceShape 와 같은 방식, STEP_FIX 1.8 → 1.5, CROSS 0.7 → 0.66)
  2) 완료 중앙값이 목표 ±2판 밖이면 가격 ≥ LATE_FROM 레벨에 s배(이분). 지금은 1)만으로 R104라 s = 1
결정적(몬테카를로 시드 고정). 사용: python proposal.py → theory_prices.json · theory_proposal.json
"""
import json, math, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nodes import GOLD_NODES, NODE, load_prices
from replay import cost
import model as M
import features as F
from tune import enforce_shape, nice

OUT = os.path.dirname(os.path.abspath(__file__))
TARGET = 104
STEP = 1.5
LATE_FROM = 1e9   # 후반(들판 이후에 사는) 레벨


def gold_only(pr):
    return {n["id"]: list(pr[n["id"]]) for n in GOLD_NODES if not n["core"]}


def with_core(pr, base):
    out = dict(base)
    out.update(pr)
    return out


def scale_late(pr, s, frm=LATE_FROM):
    return {k: [nice(v * s) if v >= frm else v for v in vs] for k, vs in pr.items()}


def lower_cap(pr, step):
    """정렬 가격에서 바로 앞의 step배를 넘는 값을 step배(×0.98)로 낮춘다"""
    out = {k: list(v) for k, v in pr.items()}
    prev = None
    for v, k, i in sorted((v, k, i) for k, vs in pr.items() for i, v in enumerate(vs)):
        if prev is not None and out[k][i] > prev * step:
            out[k][i] = nice(prev * step * 0.98)
        prev = out[k][i]
    return out


def ctx():
    rows = M.sim_rows()
    p = M.fit(rows)
    rep, _ = M.fit_report(rows, p)
    return p, rep["rmse_log10"], max(0.0, float(np.mean(rep["lag1_autocorr"])))


def run(pr, p, sig, rho, n=200):
    mc = M.monte_carlo(pr, p, sig, rho, n=n)
    return mc, M.shape_check(pr)


def summary(name, mc, shp):
    ph = mc["phases_p10_med_p90"]
    z = mc["zone_entry_p10_med_p90"]
    return dict(name=name, finish_p10_med_p90=[mc["p10"], mc["median"], mc["p90"]], finish_min_med=mc["finish_minutes"][1] if mc["finish_minutes"] else None,
                done1=ph["done1"][1], first2=ph["first2"][1], done2=ph["done2"][1], first3=ph["first3"][1],
                night=z["밤"][1], field=z["들판"][1], sea=z["바다"][1], buy_share=round(mc["buy_round_share"][1], 3),
                max_gap=mc["max_gap_rounds"][1], p6_max=round(mc["p6_max_ratio"][1], 1), p6_bad=mc["p6_violations"][1], max_step=round(shp["max_step_sorted"], 3), node_step=round(shp["node_step"], 3),
                cross=f"{shp['cross_ok']}/{shp['cross_total']}", cross_p7=f"{shp['cross_p7_ok']}/{shp['cross_total']}", worst_cross=round(shp["worst_cross_ratio"], 3), total_gold=shp["total_gold"])


def main():
    p, sig, rho = ctx()
    base = load_prices()
    res = {}
    # 0) 현재
    mc, shp = run(base, p, sig, rho)
    res["A0_현재"] = summary("현재", mc, shp)
    # 민감도: 전체 가격 ×s
    sens = {}
    for s in (0.5, 0.8, 1.25, 2.0):
        pr = with_core({k: [v * s for v in vs] for k, vs in gold_only(base).items()}, base)
        sens[s] = M.monte_carlo(pr, p, sig, rho, n=120)["median"]
    res["민감도_전체가격배율_완료중앙값"] = sens
    # 1) 뜀 다듬기만 (A1: 올리기만 / B: 위를 낮춘 뒤 올리기)
    sm = enforce_shape(gold_only(base), STEP)
    mc, shp = run(with_core(sm, base), p, sig, rho)
    res["A1_뜀다듬기_올리기만"] = summary(f"뜀 ≤×{STEP} 올리기만", mc, shp)
    sm = enforce_shape(lower_cap(gold_only(base), STEP), STEP)
    mcB, shpB = run(with_core(sm, base), p, sig, rho, n=300)
    res["B_뜀다듬기"] = summary(f"뜀 ≤×{STEP} 낮춘 뒤 올리기", mcB, shpB)
    # 2) 뜀 다듬기 + 후반 배율 이분
    lo, hi, best = 0.5, 4.0, (1.0, mcB["median"], sm)
    for _ in range(9 if abs(mcB["median"] - TARGET) > 2 else 0):
        s = math.sqrt(lo * hi)
        pr = enforce_shape(scale_late(sm, s), STEP)
        med = M.monte_carlo(with_core(pr, base), p, sig, rho, n=120)["median"]
        if best is None or abs(med - TARGET) < abs(best[1] - TARGET):
            best = (s, med, pr)
        if med > TARGET:
            hi = s
        elif med < TARGET:
            lo = s
        else:
            break
    s, _, pr = best
    final = with_core(pr, base)
    mc, shp = run(final, p, sig, rho, n=300)
    res["제안"] = summary(f"B + 가격≥{LATE_FROM:.0e} ×{s:.2f}", mc, shp)
    res["late_scale"] = s
    # 3) 프리셋 tuned 로 바꾸면 (같은 계수로 외삽 — 확인 안 됨)
    F.HP0, F.COL0, F.COLSTEP = 1.0, 12, 5
    mc_t, shp_t = run(base, p, sig, rho, n=200)
    res["T0_tuned_현재가격"] = summary("tuned 프리셋(외삽)·현재 가격", mc_t, shp_t)
    mc_t2, _ = run(final, p, sig, rho, n=200)
    res["T2_tuned_제안가격"] = summary("tuned 프리셋(외삽)·제안 가격", mc_t2, shp)
    F.HP0, F.COL0, F.COLSTEP = 3.0, 3, 3
    # 바뀐 레벨 목록
    changes = []
    for n in GOLD_NODES:
        if n["core"]:
            continue
        a, b = base[n["id"]], final[n["id"]]
        if any(abs(x - y) > 1e-9 for x, y in zip(a, b)):
            changes.append(dict(id=n["id"], name=n["n"], tier=n["tier"], old=a, new=b))
    res["changed_nodes"] = len(changes)
    json.dump({k: [float(x) for x in v] for k, v in final.items()}, open(os.path.join(OUT, "theory_prices.json"), "w", encoding="utf-8"), ensure_ascii=False)
    json.dump(dict(result=res, changes=changes), open(os.path.join(OUT, "theory_proposal.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(res, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
