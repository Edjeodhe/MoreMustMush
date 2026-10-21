# 5회차 붕괴(G4) 대안 비교 · G2 초반 판 수입 (r5_calib.json 보정값 사용). 실행: python r5_variants.py → r5_variants_out.json
import json, os, random
import r5_model as M
import r5_main as R
HERE = M.HERE
c = json.load(open(os.path.join(HERE, "r5_calib.json"), encoding="utf-8"))
sigma, kappa = c["sigma"], c["kappa"]
nodes, sinks, db = M.load_db()
fr, st, raw = M.income_tables(M.parse_logs())
HP_FIT = {"king": 0.35, "cleaner": 1.76, "angel": 45.9, "phantom": 71.4, "mover": 106.0}
out = {}
# V1: 40·50 벽 더 높게 (목표 평균 6 · 8번)
hp_v1 = R.solve_hp(nodes, fr, st, R.sc_prop, HP_FIT, {"king": None, "cleaner": None, "angel": None, "phantom": 6.0, "mover": 8.0}, kappa, sigma)
hp_v1 = {b: float(f"{v:.3g}") for b, v in hp_v1.items()}
out["V1_hp"] = hp_v1
out["V1"] = M.mc(nodes, fr, st, R.sc_prop(hp_v1), kappa, sigma, runs=1500)
out["V1_tree"] = R.long_run(nodes, fr, st, R.sc_prop(hp_v1), kappa, sigma)
# V2: P-1을 1단계(초반)에만 (2·3단계 가격 그대로)
def sc_v2(hp):
    s = R.sc_prop(hp)
    return s
nodes_v2 = {k: dict(v, cost=[x / 0.9 for x in v["cost"]]) if v["tier"] >= 2 else dict(v) for k, v in nodes.items()}
out["V2"] = M.mc(nodes_v2, fr, st, R.sc_prop(HP_FIT), kappa, sigma, runs=1500)
out["V2_tree"] = R.long_run(nodes_v2, fr, st, R.sc_prop(HP_FIT), kappa, sigma)
# V3: P-3 문자 그대로 + ps_ball 지금(2400) — 사용자 원문만 넣으면
out["V3_literal_all"] = M.mc(nodes, fr, st, R.sc_prop({b: M.HP_NOW[b] * (1 if b == "king" else 0.9) for b in M.BOSS_ORDER}), kappa, sigma, runs=1500)
# G2: 초반 판 수입 (결정적 경로) 판 1~16
def inc_path(sc):
    o = M.simulate(nodes, fr, st, sc, kappa, sigma, None, stop_disp=14, record=True)
    return [(x["round"], x["disp"], round(x["inc"], 1)) for x in o["curve"][:16]]
out["G2_now"] = inc_path(R.sc_now())
out["G2_prop"] = inc_path(R.sc_prop(HP_FIT))
out["G2_S4"] = inc_path(M.scenario("S4"))
json.dump(out, open(os.path.join(HERE, "r5_variants_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
for k in ("V1", "V2", "V3_literal_all"):
    r = out[k]
    print(k, " · ".join(f"{b} {r[b]['mean']}(1번 {r[b]['p_le1']:.0%})" for b in (10, 20, 30, 40, 50)), "50 처치", r["kill50_min_median"], "분")
print("V1 hp", hp_v1, "tree", out["V1_tree"])
print("V2 tree", out["V2_tree"])
for k in ("G2_S4", "G2_now", "G2_prop"):
    print(k, out[k])
