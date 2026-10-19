# -*- coding: utf-8 -*-
"""R4 — 공격력 I(ps_atk) 쪼개기 + 스테이지 10 첫 보스(king) Hp_Mul (2026-10-19, manager 요청).

입력
  - Assets/Resources/DB/db.json (지금 DB = 3회차 + king 0.8 + 2·3단계 ×0.55, ps_atk 임시 35·130·480·1,800)
  - BalanceData/round3_t3/main/log_s1~3.log (T3-3 실측: king 0.8 · ps_atk 370·1,400·5,200·20,000 · 관문 H0 0.9)
모델
  - 판 수입 I(스테이지) = T3-3 일반 판(보스 판 제외) 시드 중앙값. 공격력이 수입에 주는 몫은 넣지 않는다(보수적).
  - 봇 = Game.BulkBuy 그대로: 판 끝마다 '살 수 있는 것 중 가장 싼 레벨'을 못 살 때까지 산다(균사석 노드 제외).
  - 보스 관문 = BalanceSim.BossGateTime 그대로: t = T0 + 체력 / (공격력 × H0 × 속도 × (1 + KB(핀볼−1))), 판 시간 안이면 처치.
    공격력 = (1 + ps_atk 효과) × 1.1 (별 보너스 ab_atk = 0, 민감도 +5%). 날씨 6종 같은 확률(폭우 속도 ×0.8, 한파 −3초).
  - 봇 보스 규칙 = BalanceSim.Progression: 지면 보스를 끄고, 공격력 레벨을 하나 더 사거나 5판 파밍하면 다시 켠다.
실행: python BalanceData/theory/r4_model.py  → r4_out.json · r4_curve.csv (난수 시드 고정, 결정적)
"""
import csv
import json
import math
import os
import random
import re
import statistics as stx

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DB = os.path.join(ROOT, "Assets", "Resources", "DB", "db.json")
LOGS = [os.path.join(ROOT, "BalanceData", "round3_t3", "main", f"log_s{s}.log") for s in (1, 2, 3)]

# 게임 상수 (코드·DB에서 읽은 값)
TIER3_HP = 40.0           # tuned 프리셋 레전드리 체력 (Defs.cs PRESETS tuned hp[3])
BOSS_HP_K = 1.0           # DB Const
STAGE_GROWTH = 1.01       # DB Const
BOSS_STAGE = 10
H0, KB, T0 = 0.9, 0.5, 2.0  # BalanceSim GATE_H · GATE_KB · GATE_T0
ATK_BASE_MUL = 1.1        # Game.cs:227 (1 + 0.1 + ab["atk"] ...)
WEATHER = [("clear", 1.0, 0), ("rain", 1.0, 0), ("moon", 1.0, 0), ("wind", 1.0, 0), ("storm", 0.8, 0), ("cold", 1.0, 3)]
FARM_RETRY = 5            # BalanceSim.cs:207 (관문 모드 5판)
FAIL_INC = 0.7            # 보스 진 판 수입 / 일반 판 (T3-3 기록 0.5~1.0, BOSS_FAIL_GOLD 0.8)
KILL_INC = 2.5            # 처치 판 = 이번 판(≈0.5) + max(이번, 직전 일반) × BOSS_REWARD_MUL 2
MAX_STAGE = 19
MC = 20000

# 비교할 공격력 I 안: (Efx_Lv1~4 누적, Cost_Lv1~4)
SCHEMES = {
    "S0_옛370":   ([1, 2, 3, 4], [370, 1400, 5200, 20000]),
    "S1_임시35":  ([1, 2, 3, 4], [35, 130, 480, 1800]),
    "S2_규칙밖":  ([0.5, 1, 2, 4], [110, 250, 1000, 3800]),
    "C1_효과그대로": ([1, 2, 3, 4], [220, 840, 3200, 12000]),
    "C2_쪼갬0.5": ([0.5, 1.5, 3, 4], [220, 840, 3200, 12000]),
    "C5_쪼갬0.75": ([0.75, 1.5, 3, 4], [220, 840, 3200, 12000]),
    "C7_최소사슬": ([0.5, 1.5, 3, 4], [200, 760, 2900, 11000]),
    "C8_규칙그대로": ([1, 2, 3, 4], [200, 760, 2900, 11000]),
    "F_최종쪼갬": ([1.5, 2.5, 3.5, 4], [65, 240, 920, 3500]),
}
HP_MULS = [0.8, 0.7, 0.6, 0.55, 0.5, 0.45, 0.4, 0.3, 0.24, 0.2]
PROPOSAL = "F_최종쪼갬"
PROPOSAL_HPMUL = 0.2


# ---------- DB ----------
def load_db():
    d = json.load(open(DB, encoding="utf-8"))
    cost = {c["Cost_ID"]: c for c in d["Upgrade_Cost"]}
    byid = {u["Upg_ID"]: u for u in d["Upgrade"]}
    nodes = {}
    for u in d["Upgrade"]:
        c = cost[u["Cost_ID"]]
        mx = int(u["Efx_MaxLv"])
        if mx == 0:
            continue  # 싱크
        if any(c.get(f"Gem_Lv{i}", 0) for i in range(1, 5)):
            continue  # 균사석 노드 (봇이 안 산다)
        nodes[u["Upg_Key"]] = {
            "tier": int(u["Tier"]),
            "max": mx,
            "cost": [float(c[f"Cost_Lv{i}"]) for i in range(1, mx + 1)],
            "efx": [float(u[f"Efx_Lv{i}"]) for i in range(0, mx + 1)],
            "pre": byid[u["Upg_Pre"]]["Upg_Key"] if u["Upg_Pre"] in byid else None,
            "req": byid[u["Upg_Req"]]["Upg_Key"] if u["Upg_Req"] in byid else None,
            "cost_id": u["Cost_ID"],
            "upg_id": u["Upg_ID"],
        }
    return nodes


def with_scheme(nodes, scheme):
    efx, cost = SCHEMES[scheme]
    n = {k: dict(v) for k, v in nodes.items()}
    n["ps_atk"] = dict(n["ps_atk"], cost=[float(x) for x in cost], efx=[0.0] + [float(x) for x in efx])
    return n


# ---------- T3-3 기록 ----------
ROW = re.compile(r"^R(\d+) \| [^|]+\| [^|]+\| (\d+)s \| (\d+) \| (\d+) \| ([\d.,]+[KMB]?) \|.*?\| \+(\d+) \|")


def num(s):
    s = s.replace(",", "")
    mul = {"K": 1e3, "M": 1e6, "B": 1e9}.get(s[-1], 1)
    return float(s[:-1] if s[-1] in "KMB" else s) * mul


def parse_logs():
    runs = []
    for p in LOGS:
        rows, boss_next = [], False
        for line in open(p, encoding="utf-8"):
            if line.startswith("  보스 R"):
                boss_next = ("처치" in line, "실패" in line)
                continue
            m = ROW.match(line)
            if not m:
                continue
            st, T, balls, atk, inc, buys = int(m[1]), int(m[2]), int(m[3]), int(m[4]), num(m[5]), int(m[6])
            rows.append({"stage": st, "T": T, "atk": atk, "inc": inc, "buys": buys,
                         "boss": bool(boss_next), "kill": bool(boss_next and boss_next[0])})
            boss_next = False
        runs.append(rows)
    return runs


def income_curve(runs):
    cur, spread = {}, {}
    for s in range(1, MAX_STAGE + 1):
        v = [r["inc"] for run in runs for r in run if r["stage"] == s and not r["boss"]]
        cur[s] = stx.median(v)
        spread[s] = (min(v), max(v), stx.pstdev([math.log(x) for x in v]) if len(v) > 1 else 0.0)
    return cur, spread


def observed_boss10(runs):
    out = []
    for run in runs:
        b = [r for r in run if r["stage"] == 10 and r["boss"]]
        n10 = sum(1 for r in run if r["stage"] == 10)
        lv9 = sum(r["buys"] for r in run if r["stage"] <= 9)
        out.append({"attempts": len(b), "rounds_at_10": n10, "levels_by_R9": lv9,
                    "atk_disp_first_try": b[0]["atk"], "atk_disp_kill": [r["atk"] for r in b if r["kill"]][0]})
    return out


# ---------- 봇 ----------
def parent_ok(n, lv, k):
    a = n[k]
    return (a["pre"] is None or lv.get(a["pre"], 0) >= 1) and (a["req"] is None or lv.get(a["req"], 0) >= n[a["req"]]["max"])


def bulk_buy(n, lv, gold, log, rnd):
    while True:
        best, bc = None, math.inf
        for k, a in n.items():
            L = lv.get(k, 0)
            if L >= a["max"] or not parent_ok(n, lv, k):
                continue
            if a["cost"][L] < bc:
                best, bc = k, a["cost"][L]
        if best is None or gold < bc:
            return gold
        gold -= bc
        lv[best] = lv.get(best, 0) + 1
        log.append((rnd, best, lv[best], bc))


def boss_hp(hpmul):
    return TIER3_HP * BOSS_HP_K * STAGE_GROWTH ** (BOSS_STAGE - 1) * hpmul


def gate(n, lv, hpmul, weather, ab_atk=0.0):
    _, spdw, cold = weather
    atk_flat = 1 + n["ps_atk"]["efx"][lv.get("ps_atk", 0)]
    A = atk_flat * (ATK_BASE_MUL + ab_atk)
    spd = n["ps_spd"]["efx"][lv.get("ps_spd", 0)] * spdw
    balls = 1 + lv.get("ps_ball", 0)
    T = 10 + 0.5 * (BOSS_STAGE - 1) + n["ps_dur"]["efx"][lv.get("ps_dur", 0)] - cold
    t = T0 + boss_hp(hpmul) / (A * H0 * spd * (1 + KB * (balls - 1)))
    return t <= T, t, T


def atk_levels(lv):
    return sum(lv.get(k, 0) for k in ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"))


def run(n, inc, hpmul, rng=None, ab_atk=0.0, fixed_weather=None):
    """판 1~MAX_STAGE. rng가 없으면 날씨 = fixed_weather(없으면 맑음)."""
    lv, gold, log = {}, 0.0, []
    rnd, stage, tries, at10 = 0, 1, 0, 0
    boss_on, farm, atk_before = True, 0, 0
    arrive = None
    while stage <= MAX_STAGE and rnd < 80:
        rnd += 1
        if stage == BOSS_STAGE:
            at10 += 1
            if arrive is None:
                arrive = {"round": rnd, "levels": sum(lv.values()), "ps_atk": lv.get("ps_atk", 0), "ps_dur": lv.get("ps_dur", 0),
                          "atk_flat": 1 + n["ps_atk"]["efx"][lv.get("ps_atk", 0)], "spent": sum(c for *_, c in log)}
            if boss_on:
                w = rng.choice(WEATHER) if rng else (fixed_weather or WEATHER[0])
                tries += 1
                ok, _, _ = gate(n, lv, hpmul, w, ab_atk)
                if ok:
                    gold += inc[stage] * KILL_INC
                    stage += 1
                else:
                    gold += inc[stage] * FAIL_INC
                    boss_on, farm, atk_before = False, 0, atk_levels(lv)
            else:
                gold += inc[stage]
                farm += 1
        else:
            gold += inc[stage]
            stage += 1
        gold = bulk_buy(n, lv, gold, log, rnd)
        if stage == BOSS_STAGE and not boss_on and (atk_levels(lv) > atk_before or farm >= FARM_RETRY):
            boss_on = True
    return {"tries": tries, "rounds_at_10": at10, "arrive": arrive, "log": log, "rounds": rnd}


def boss_mc(n, inc, hpmul, ab_atk=0.0, runs=MC, seed=11):
    rng = random.Random(seed)
    tries, at10 = [], []
    for _ in range(runs):
        r = run(n, inc, hpmul, rng, ab_atk)
        tries.append(r["tries"])
        at10.append(r["rounds_at_10"])
    dist = {k: tries.count(k) / runs for k in sorted(set(tries))}
    return {"p_1try": dist.get(1, 0), "p_le2": dist.get(1, 0) + dist.get(2, 0), "mean_tries": sum(tries) / runs,
            "mean_rounds_at_10": sum(at10) / runs, "dist": dist}


# ---------- 가격 사다리 검사 ----------
def ladder_checks(n, cap=110000):
    items = sorted((a["cost"][i], k, i + 1) for k, a in n.items() for i in range(a["max"]) if a["cost"][i] <= cap)
    steps = [(items[i][0] / items[i - 1][0], items[i - 1][1:], items[i][1:]) for i in range(1, len(items)) if items[i - 1][0] > 0]
    worst = max(steps, key=lambda s: s[0])
    atk_items = [s for s in steps if "ps_atk" in (s[1][0], s[2][0])]
    a = n["ps_atk"]
    intra = [a["cost"][i] / a["cost"][i - 1] for i in range(1, a["max"])]
    line = n["ps_atk2"]["cost"][0] / a["cost"][-1]
    per_last = a["efx"][-1] - a["efx"][-2]
    gpp_last = a["cost"][-1] / per_last
    gpp_next = n["ps_atk2"]["cost"][0] / (n["ps_atk2"]["efx"][1] - n["ps_atk2"]["efx"][0])
    return {"global_max_step": round(worst[0], 3), "global_worst_pair": [list(worst[1]), list(worst[2])],
            "max_step_touching_ps_atk": round(max(s[0] for s in atk_items), 3),
            "ps_atk_intra": [round(x, 2) for x in intra], "ps_atk_L4_to_ps_atk2_L1": round(line, 2),
            "gold_per_atk": [round(a["cost"][i] / (a["efx"][i + 1] - a["efx"][i]), 1) for i in range(a["max"])] + [round(gpp_next, 1)],
            "cross_gold_ratio(next/last)": round(gpp_next / gpp_last, 2),
            "code_PriceCheck_cross_ok(per=1,0.53)": gpp_next <= a["cost"][-1] / 1.0 * 0.53 * 1.001}


def rounds_table(log, inc_by_round):
    """레벨별 가격 ÷ 그 판 수입 = 몇 판 모아야 사는지"""
    return [{"round": r, "node": k, "lv": L, "price": c, "inc": round(inc_by_round[r], 1), "rounds": round(c / inc_by_round[r], 2)} for r, k, L, c in log]


def inc_by_round_det(n, inc, hpmul):
    """결정적 경로(맑음)에서 판별 수입"""
    lv, gold, out = {}, 0.0, {}
    r = run(n, inc, hpmul)
    # 다시 돌려 판별 수입을 적는다 (run과 같은 규칙)
    stage, rnd, boss_on, farm, atk_before = 1, 0, True, 0, 0
    log = []
    while stage <= MAX_STAGE and rnd < 80:
        rnd += 1
        if stage == BOSS_STAGE:
            if boss_on:
                ok, _, _ = gate(n, lv, hpmul, WEATHER[0])
                g = inc[stage] * (KILL_INC if ok else FAIL_INC)
                if ok:
                    stage += 1
                else:
                    boss_on, farm, atk_before = False, 0, atk_levels(lv)
            else:
                g = inc[stage]
                farm += 1
        else:
            g = inc[stage]
            stage += 1
        out[rnd] = g
        gold = bulk_buy(n, lv, gold + g, log, rnd)
        if stage == BOSS_STAGE and not boss_on and (atk_levels(lv) > atk_before or farm >= FARM_RETRY):
            boss_on = True
    return out, log, r


def main():
    base = load_db()
    runs = parse_logs()
    inc, spread = income_curve(runs)
    obs = observed_boss10(runs)
    out = {"income_median": inc, "income_spread": {s: [round(a, 1), round(b, 1), round(c, 3)] for s, (a, b, c) in spread.items()},
           "observed_T3_3": obs, "boss_hp": {h: round(boss_hp(h), 2) for h in HP_MULS}, "schemes": {}}

    # 검산: 옛 안(S0) + king 0.8로 T3-3을 재현하는지
    for sc in SCHEMES:
        n = with_scheme(base, sc)
        det_inc, det_log, det = inc_by_round_det(n, inc, 0.8)
        res = {"ladder": ladder_checks(n), "arrive_hp0.8_clear": det["arrive"],
               "atk_buys_clear_hp0.8": [(r, L, c) for r, k, L, c in det_log if k == "ps_atk"],
               "boss": {}}
        for h in HP_MULS:
            res["boss"][h] = boss_mc(n, inc, h, runs=4000 if sc != PROPOSAL else MC)
        res["boss_ab5_prop_hp"] = boss_mc(n, inc, PROPOSAL_HPMUL, ab_atk=0.055, runs=4000)
        out["schemes"][sc] = res

    # 제안안 판 수 표와 곡선
    n = with_scheme(base, PROPOSAL)
    det_inc, det_log, det = inc_by_round_det(n, inc, PROPOSAL_HPMUL)
    tab = rounds_table(det_log, det_inc)
    out["proposal_rounds_table"] = tab
    n0 = with_scheme(base, "S0_옛370")
    det_inc0, det_log0, _ = inc_by_round_det(n0, inc, 0.8)
    out["old_rounds_table_atk"] = [t for t in rounds_table(det_log0, det_inc0) if t["node"] == "ps_atk"]
    allr = [t["rounds"] for t in tab]
    out["proposal_rounds_summary"] = {"median": stx.median(allr), "p10": sorted(allr)[len(allr) // 10], "p90": sorted(allr)[len(allr) * 9 // 10],
                                      "ps_atk": [t["rounds"] for t in tab if t["node"] == "ps_atk"]}
    # 날씨별 관문 (도착 구성 · 다음 공격력 레벨)
    gates = {}
    for label, lvset in {"ps_atk0_dur2": {"ps_dur": 2}, "ps_atk1_dur2": {"ps_atk": 1, "ps_dur": 2}, "ps_atk2_dur2": {"ps_atk": 2, "ps_dur": 2},
                         "ps_atk2_dur1": {"ps_atk": 2, "ps_dur": 1}, "ps_atk1_dur3": {"ps_atk": 1, "ps_dur": 3}, "ps_atk3_dur2": {"ps_atk": 3, "ps_dur": 2}}.items():
        gates[label] = {w[0]: [round(x, 1) if isinstance(x, float) else x for x in gate(n, lvset, PROPOSAL_HPMUL, w)] for w in WEATHER}
    out["proposal_gates_hp0.5"] = gates

    with open(os.path.join(HERE, "r4_out.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1, default=str)
    with open(os.path.join(HERE, "r4_curve.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["round", "node", "lv", "price", "round_income", "rounds_of_income"])
        for t in tab:
            w.writerow([t["round"], t["node"], t["lv"], t["price"], t["inc"], t["rounds"]])

    # 요약 출력
    print("수입 중앙값(스테이지):", {s: round(v) for s, v in inc.items()})
    print("T3-3 관측:", obs)
    for sc, res in out["schemes"].items():
        b = res["boss"]
        print(f"\n[{sc}] ladder {res['ladder']}")
        print("  도착(맑음, hp0.8):", res["arrive_hp0.8_clear"], " ps_atk 구매:", res["atk_buys_clear_hp0.8"])
        for h in HP_MULS:
            print(f"  Hp_Mul {h} (체력 {boss_hp(h):.1f}): 1회 {b[h]['p_1try']:.2f} · ≤2회 {b[h]['p_le2']:.2f} · 평균 도전 {b[h]['mean_tries']:.2f} · 스테이지10 판 {b[h]['mean_rounds_at_10']:.2f}")
        bb = res["boss_ab5_prop_hp"]
        print(f"  (별 +5%, Hp_Mul {PROPOSAL_HPMUL}) 1회 {bb['p_1try']:.2f} ≤2회 {bb['p_le2']:.2f}")
    print("\n제안 판 수 요약:", out["proposal_rounds_summary"])
    print("옛 ps_atk 판 수:", out["old_rounds_table_atk"])
    for t in tab:
        print(t)
    print(json.dumps(gates, ensure_ascii=False))


if __name__ == "__main__":
    main()
