# -*- coding: utf-8 -*-
"""research "구간 증가율 합산 10^7.8 vs 필요 10^8.4" 확인: 판 수입의 R1 → 끝 성장을 근사식의 항별 자릿수로 나눈다.
사용: python decompose.py (tuned) · THEORY_PRESET=spec python decompose.py"""
import json, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cfg
import model as M
from features import stats

def parts(s, p):
    z_zone = s["zone"]
    out = {
        "지역 ×10^지역": z_zone * 1.0,
        "스테이지 1.01^(판-1)": (s["stage"] - 1) * math.log10(1.01),
        "타격 점수 (s̄+고정값)": math.log10(p["sbar"] + s["sflat"]),
        "라운드 시간": math.log10(s["dur"]),
        "콤보 계수": math.log10(s["comboK"] if p["kappa"] is None else 1 + p["kappa"] * s["comboK"]),
    }
    d = M.design(s)
    names = ["상수", "핀볼 수", "군락지 수", "스킬 수", "밤 상수", "들판 상수", "바다 상수"]
    for n, x, c in zip(names, d, p["coef"]):
        out[n] = x * c
    return out

def main():
    rows = M.sim_rows()
    p = M.fit(rows)
    end = max(r["stage"] for r in rows if r["seed"] == 1)
    fin = {sd: max(M.load_phase_done(sd)) for sd in M.SEEDS} if hasattr(M, "load_phase_done") else None
    res = {}
    for sd in M.SEEDS:
        rs = [r for r in rows if r["seed"] == sd]
        from replay import load_phase
        lim = max(load_phase(sd)["done"][1:])
        a, b = parts(rs[0], p), parts(rs[lim - 1], p)
        diff = {k: round(b[k] - a[k], 2) for k in a}
        real = math.log10(rs[lim - 1]["inc"]) - math.log10(rs[0]["inc"])
        res[sd] = dict(R1=rs[0]["inc"], end_round=lim, end=rs[lim - 1]["inc"], real_decades=round(real, 2),
                       model_decades=round(sum(diff.values()), 2), parts=diff,
                       purchase_only=round(sum(v for k, v in diff.items() if k not in ("지역 ×10^지역", "스테이지 1.01^(판-1)", "밤 상수", "들판 상수", "바다 상수")), 2))
    json.dump(res, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), f"theory_decompose_{cfg.TAG}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(res, ensure_ascii=False, indent=1))

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
