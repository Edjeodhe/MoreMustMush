# -*- coding: utf-8 -*-
"""R3 초반(스테이지 1~7) 판 수입 근사식 맞추기.
자료: BalanceData/round3_t0/ (T3-0, 가안 EARLY_* 코드, 시드 1~3) + 2회차 m15 1판(초반 개편 전, 7군락 15초, 0등급만).
식 (스테이지 <= 6, 0등급 버섯만):
    I = T * g * (1+f) * (kappa * c**alpha + sigma * dev)
      T = 판 시간(초), g = 정산 배율 1.1 + (ed_price 효과-1), f = 타격 점수 고정값(ed_score),
      c = 최대 군락 수(Game.ComputeStats 그대로), dev = 장치 수(dev_stump/moss/acorn)
    스테이지 7: 위에 (1 + tau7/(1+f)) — 높은 등급(에픽 5·유니크 20점) 타격이 섞여 생기는 몫
power·md_bar·md_combo는 kappa에 접는다(따로 맞출 자료가 모자람).
다시 돌리면 같은 결과 (난수 없음). 실행: python BalanceData/theory/r3_fit.py
"""
import json, math, os
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
T0 = os.path.join(ROOT, "BalanceData", "round3_t0")
R2 = os.path.join(ROOT, "BalanceData", "round2_sweep")
EARLY_COL_MUL_DRAFT = [0.4, 0.45, 0.5, 0.6, 0.7, 0.85]   # T3-0 때 코드 값
MAP_ZOOM = [1.5, 1.35, 1.2, 1.1, 1.0]                      # DB md_map Efx_Lv0~4
ED_PRICE = [1.0, 1.3, 1.6, 1.9]


def js_round(x):
    return math.floor(x + 0.5)


def area(map_lv):
    z = MAP_ZOOM[map_lv]
    return (1 / (z * z)) ** 0.7


def maxcol_draft(stage, nodes):
    """T3-0 코드(가안) 그대로: ColCount = ceil(12 + 5L), max(3), ×area max(4), 초반 ×EARLY_COL_MUL max(1)"""
    base = max(3, math.ceil(12 + 5 * nodes.get("ed_cols", 0)))
    c = max(4, js_round(base * area(nodes.get("md_map", 0))))
    if stage <= 6:
        c = max(1, js_round(c * EARLY_COL_MUL_DRAFT[stage - 1]))
    return c


def round_time_draft(stage, nodes):
    dur = [0, 2, 4, 6][nodes.get("ps_dur", 0)]
    base = 10 if stage <= 4 else 15 + min(10, 0.5 * (stage - 1))
    return base + dur


def points():
    pts = []
    for s in (1, 2, 3):
        st = [json.loads(l) for l in open(os.path.join(T0, f"states_s{s}.jsonl"), encoding="utf-8")]
        ph = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))
        for i in range(1, 7):          # 판 2~7 (판 1은 튜토리얼: 군락 1개 + 낱개 8개)
            nodes = st[i - 1]["nodes"]
            stage = ph["stageR"][i]
            pts.append(dict(seed=s, stage=stage, inc=ph["incR"][i], c=maxcol_draft(stage, nodes),
                            T=round_time_draft(stage, nodes), f=[0, 1, 2, 3][nodes.get("ed_score", 0)],
                            g=1.1 + ED_PRICE[nodes.get("ed_price", 0)] - 1,
                            dev=sum(nodes.get(k, 0) for k in ("dev_stump", "dev_moss", "dev_acorn"))))
    return pts


def fit(pts):
    """스테이지 2~6으로 kappa·alpha·sigma (로그 오차 최소, 격자 탐색 — 재현 가능)"""
    early = [p for p in pts if p["stage"] <= 6]
    best = None
    for alpha in np.arange(0.0, 1.51, 0.01):
        for sigma in np.arange(0.0, 8.01, 0.05):
            # kappa는 닫힌 꼴이 없어 1차원 탐색
            for kappa in np.arange(0.2, 3.0, 0.01):
                e = 0
                for p in early:
                    pred = p["T"] * p["g"] * (1 + p["f"]) * (kappa * p["c"] ** alpha + sigma * p["dev"])
                    e += (math.log(pred) - math.log(p["inc"])) ** 2
                if best is None or e < best[0]:
                    best = (e, kappa, alpha, sigma)
    e, kappa, alpha, sigma = best
    rmse = math.sqrt(e / len(early))
    return dict(kappa=round(kappa, 2), alpha=round(alpha, 2), sigma=round(sigma, 2), rmse_log=round(rmse, 3), n=len(early))


def predict(p, m, tau=0.0):
    return p["T"] * p["g"] * (1 + p["f"] + tau) * (m["kappa"] * p["c"] ** m["alpha"] + m["sigma"] * p["dev"])


def main():
    pts = points()
    m = fit(pts)
    out = dict(model=m, points=[])
    for p in pts:
        pr = predict(p, m)
        q = dict(p, pred0=round(pr, 1))
        if p["stage"] == 7:
            q["tau7"] = round(p["inc"] / (pr / (1 + p["f"])) - (1 + p["f"]), 2)   # 높은 등급 몫 역산
        out["points"].append(q)
    taus = [q["tau7"] for q in out["points"] if "tau7" in q]
    out["tau7"] = dict(values=taus, mean=round(sum(taus) / len(taus), 2))
    # 바깥 확인: 2회차 m15 1판(0등급만, 군락 7, 15초, 노드 0) — 맞춤에 안 씀
    chk = []
    for s in (1, 2, 3):
        ph = json.load(open(os.path.join(R2, f"m15_s{s}.json"), encoding="utf-8"))
        chk.append(ph["incR"][0])
    p0 = dict(T=15, g=1.1, f=0, c=7, dev=0)
    out["check_round2_R1"] = dict(obs=chk, pred=round(predict(p0, m), 1))
    json.dump(out, open(os.path.join(os.path.dirname(__file__), "r3_fit.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(m, ensure_ascii=False))
    for q in out["points"]:
        print(q["seed"], q["stage"], "c", q["c"], "T", q["T"], "dev", q["dev"], "f", q["f"], "obs", q["inc"], "pred", q["pred0"], q.get("tau7", ""))
    print("tau7", out["tau7"], "check 2회차 R1", out["check_round2_R1"])


if __name__ == "__main__":
    main()
