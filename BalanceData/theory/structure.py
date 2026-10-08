# -*- coding: utf-8 -*-
"""구조 수치 실험 (tuned, T2 기록으로 맞춘 모델). 튜너(BalanceJob)가 가격 수준을 다시 맞춘다는 전제에서,
가격 말고 구조(지역 배율을 나눠 올리기, 지역 해금 판)를 바꿨을 때 원칙(P1·P6·P11)이 어떻게 바뀌는지 본다.

가격은 T3(튜너 재맞춤)가 아직 없어 "T2 가격표 × s (완료 중앙값이 R104가 되게 이분)"로 대신한다 — T3 근사.
결정적(몬테카를로 시드 고정). 사용: python structure.py → theory_structure_tuned.json
"""
import json, math, os, sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cfg
import features as F
import model as M
from nodes import GOLD_NODES
from replay import data_prices

OUT = os.path.dirname(os.path.abspath(__file__))
TARGET = 94     # 사용자 결정(14:13): 완료 목표 R84~R104 → 가운데 R94에 맞추고 범위 안인지 본다


def scaled(base, s):
    out = dict(base)
    for n in GOLD_NODES:
        if not n["core"]:
            out[n["id"]] = [v * s for v in base[n["id"]]]
    return out


def phase_metrics(prices, p, sig, rho, n=150):
    mc = M.monte_carlo(prices, p, sig, rho, n=n)
    ph = mc["phases_p10_med_p90"]
    med = lambda k: ph[k][1]
    fin = mc["median"]
    lens = {"초반": med("done1") - med("first1"), "중반": med("done2") - med("first2"), "후반": med("done3") - med("first3")}
    # 한 판 최다 구매 · 마지막 지역 진입 뒤 완료까지 판 수 (결정적 진행 한 번, 잡음 고정 시드)
    rng = np.random.default_rng(5)
    pr = M.progression(prices, p, rounds=140, rng=rng, sigma=sig, rho_ac=rho)
    lim = M.finish_round(pr) or 140
    burst = max(r["bought"] for r in pr["rec"][:lim])
    z = mc["zone_entry_p10_med_p90"]
    # σ 민감도 · 견고성(수입 ×0.8 = 가격 ×1.25, 수입 ×1.25 = 가격 ×0.8)
    sig_s = {f"σ×{k}": M.monte_carlo(prices, p, sig * k, rho, n=80)["median"] for k in (0.0, 0.5)}
    rob = {}
    for lab, f in (("수입×0.8", 1.25), ("수입×1.25", 0.8)):
        m2 = M.monte_carlo(scaled(prices, f), p, sig, rho, n=80)
        rob[lab] = dict(median=m2["median"], p90=m2["p90"], unfinished=m2["unfinished"])
    return dict(finish=[mc["p10"], fin, mc["p90"]], in_range=84 <= fin <= 104, sigma=sig_s, robust=rob,
                p6_where=mc["p6_max_block_start"][1] if mc["p6_max_block_start"] else None,
                p6_zone_part=round(mc["p6_max_zone_part"][1], 2) if mc["p6_max_zone_part"] else None, minutes=mc["finish_minutes"][1] if mc["finish_minutes"] else None,
                phases={k: [med("first" + k[-1]), med("done" + k[-1])] for k in ("t1", "t2", "t3")},
                phase_len=lens, late_share=round(lens["후반"] / fin, 3),
                zones={k: v[1] for k, v in z.items()}, after_last_zone=fin - z[list(z)[-1]][1],
                buy_share=round(mc["buy_round_share"][1], 3), max_gap=mc["max_gap_rounds"][1],
                p6_max=round(mc["p6_max_ratio"][1], 1), p6_bad=mc["p6_violations"][1], burst_levels=burst)


def fit_scale(base, p, sig, rho, lo=0.5, hi=20.0, it=10):
    best = None
    for _ in range(it):
        s = math.sqrt(lo * hi)
        med = M.monte_carlo(scaled(base, s), p, sig, rho, n=80)["median"]
        if best is None or abs(med - TARGET) < abs(best[1] - TARGET):
            best = (s, med)
        if med > TARGET:
            hi = s
        elif med < TARGET:
            lo = s
        else:
            break
    return best[0]


def main():
    rows = M.sim_rows()
    p = M.fit(rows)
    rep, _ = M.fit_report(rows, p)
    sig, rho = rep["rmse_log10"], max(0.0, float(np.mean(rep["lag1_autocorr"])))
    base = data_prices()
    res = {"preset": cfg.PRESET, "data": cfg.DATA, "note": "가격 = T2 가격표 × s (T3 근사). 구조안마다 s를 다시 맞춰 완료 중앙 R94(목표 R84~104 가운데)"}
    res["O0_T2_그대로"] = dict(scale=1.0, **phase_metrics(base, p, sig, rho))
    options = [
        ("O1_T3근사(가격만)", {}),
        ("O2_지역배율_5판에_나눠", {"ZONE_RAMP": 5}),
        ("O3_지역배율_10판에_나눠", {"ZONE_RAMP": 10}),
        ("O5_해금_1·22·50·80", {"THEME_FROM": [1, 22, 50, 80, 105]}),
        ("O6_해금_1·22·50·80+5판나눠", {"THEME_FROM": [1, 22, 50, 80, 105], "ZONE_RAMP": 5}),
    ]
    keep = dict(ZONE_RAMP=F.ZONE_RAMP, THEME_FROM=list(F.THEME_FROM))
    for name, ch in options:
        for k, v in ch.items():
            setattr(F, k, v)
        M.THEME_FROM = F.THEME_FROM
        s = fit_scale(base, p, sig, rho)
        res[name] = dict(change=ch, scale=round(s, 3), **phase_metrics(scaled(base, s), p, sig, rho))
        print(name, res[name], flush=True)
        for k, v in keep.items():
            setattr(F, k, v if not isinstance(v, list) else list(v))
        M.THEME_FROM = F.THEME_FROM
    json.dump(res, open(os.path.join(OUT, f"theory_structure_{cfg.TAG}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
