# -*- coding: utf-8 -*-
"""R2-12 (manager#8, 사용자 16:17): 공격력 노드를 ×10 점프 → 단계적 성장(단계 사이 ×2~3)으로 바꾼 안.

1) 공격력 노드 6개의 레벨당 증가(Efx_Lv1~4) 후보를 db.json 값 대신 넣는다(최대 레벨은 그대로).
2) plan.py 그대로(구매 일정 → hp_for → 가격 반복)로 보스 체력을 다시 낸다. 스테이지 50은 MARGIN_LAST를
   이분해 "기준 진행의 스테이지 50 첫 처치 판 = KILL50"이 되게 한다(3시간 목표 우선, 사용자 결정 15:59).
3) 싱크 inf_atk(공격력 × 1.05^L) · inf_gold(정산 골드 × 1.025^L), 가격 c0 × 1.076^L, c0 = 스테이지 50 일반 판 골드 × 3.
   열림 = 부모 최대(inf_atk ← ps_atk4, inf_gold ← ed_score3p). 평가 진행(민감도)에만 넣는다(가격 맞추기에는 안 넣음).
4) 민감도(H0 ±30%, MOVER_H, TAU, 수입 ×0.5·×2, 재도전 간격) — 싱크 있음/없음.
진 판 = 안 A(지금 코드: 보스 타격 골드 × BOSS_SCORE_MUL 0.05 × BOSS_FAIL_GOLD 0.8, 사용자 결정 15:59).

python r212.py            → theory_r212.json, theory_boss_curve_r212.csv
THEORY_CURVE=B python r212.py   (후보 B: 1/3/6/10)
결정적(난수 없음).
"""
import csv, json, math, os
os.environ.setdefault("THEORY_FAIL", "A")
import boss as B, plan as P

HERE = os.path.dirname(os.path.abspath(__file__))
ATK = ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4")
STEP_OF = {"ps_atk": 0, "ps_atk2": 1, "ps_atk2p": 1, "ps_atk3": 2, "ps_atk3p": 2, "ps_atk4": 3}
CURVES = {
    "old": None,                    # 지금 DB: 1 / 7.5 / 75 / 750
    "A": (1.0, 2.5, 6.0, 15.0),     # 제안: 단계 사이 ×2.5 · ×2.4 · ×2.5
    "B": (1.0, 3.0, 6.0, 10.0),     # 사용자 예(10→30→60→100)의 비율 그대로 ÷10: ×3 · ×2 · ×1.67
}
CURVE = os.environ.get("THEORY_CURVE", "A")
KILL50 = int(os.environ.get("THEORY_KILL50", "164"))   # 이전 제안안(R2-4)과 같은 스테이지 50 첫 처치 판 (≈ 173분)
P.MARGIN_FIRST = 0.65    # verify#6: H0×0.7·최악 날씨에서도 r ≤ 0.93 (0.7이면 r 1.00로 경계)
# 제안 Hp_Mul(반올림, 표에 낼 값). 대왕송이·청소부는 지금 DB 값 유지(첫 도전 여유 충분), 움직이는 버섯은 처치 판 계단의 가운데(5.5~6.0 → R165)
FINAL_MUL = {"A": {10: 0.21, 20: 0.184, 30: 1.13, 40: 2.83, 50: 5.5}}
SINK = dict(atk=1.05, gold=1.025, ratio=1.076, c0_rounds=3.0, levels=600)
ORIG = {k: list(B.NODE[k]["efx"]) for k in ATK}


def patch(curve):
    for k in ATK:
        n = B.NODE[k]
        if curve is None:
            n["efx"] = list(ORIG[k])
        else:
            d = curve[STEP_OF[k]]
            n["efx"] = [i * d if i <= n["max"] else 0.0 for i in range(5)]


def atk_table(curve):
    out, cum = [], 1.0
    for k in ATK:
        n = B.NODE[k]
        d = n["efx"][1]
        cum += n["efx"][n["max"]]
        out.append(dict(key=k, max=n["max"], per_lv=d, efx=n["efx"][:n["max"] + 1], cum_after=cum))
    return out


# ---------- 싱크 (평가 진행에만) ----------
_stats0, _income0 = B.stats, B.income


def stats_sink(lv, stage, rounds, weather="wind", gm=None):
    s = _stats0(lv, stage, rounds, weather, gm)
    s["atk"] *= SINK["atk"] ** lv.get("inf_atk", 0)
    s["ginf"] = SINK["gold"] ** lv.get("inf_gold", 0)
    return s


def income_sink(s):
    return _income0(s) * s.get("ginf", 1.0)


def add_sink(c0):
    pr = [c0 * SINK["ratio"] ** L for L in range(SINK["levels"])]
    for key, req, br in (("inf_atk", "ps_atk4", "ps"), ("inf_gold", "ed_score3p", "ed")):
        n = dict(id=key, br=br, tier=4, max=SINK["levels"], efx=[0] * 5, gold=pr, gem=[0] * SINK["levels"], parent=None, req=req)
        B.NODE[key] = n
        B.GOLD_NODES.append(n)
    B.stats, B.income = stats_sink, income_sink


def remove_sink():
    B.GOLD_NODES[:] = [n for n in B.GOLD_NODES if n["tier"] != 4]
    B.NODE.pop("inf_atk", None); B.NODE.pop("inf_gold", None)
    B.stats, B.income = _stats0, _income0


def run(prices, hp, gate=None, **kw):
    old = dict(B.GATE)
    if gate:
        B.GATE.update(gate)
    try:
        cfg = dict(prices=prices, hp=hp, reward_rounds=P.REWARD_ROUNDS, look_tier=P.LOOK_TIER, fail_rounds=P.FAIL_ROUNDS,
                   max_rounds=600, stop_stage=50, snap=True)
        cfg.update(kw)
        return B.progression(cfg)
    finally:
        B.GATE.clear(); B.GATE.update(old)


def solve_for(kill50):
    """MARGIN_LAST 이분: 기준 진행의 스테이지 50 첫 처치 판 = kill50"""
    lo, hi = 0.05, 3.0
    best = None
    for _ in range(30):
        m = (lo + hi) / 2
        P.MARGIN_LAST = m
        hp, plan_inc, prices, hist = P.solve()
        kr = run(prices, hp)["log"].get(50, {}).get("kill_r", 10 ** 6)
        best = (m, hp, plan_inc, prices, hist, kr)
        if kr > kill50:
            hi = m
        elif kr < kill50:
            lo = m
        else:
            break
    return best


def r1_table(res, hp):
    """첫 도전 때 처치 시간 비 r1(포자 바람, H ×0.7/1.0/1.3), 벽 W, 처치 때 공격력"""
    rows = []
    for st in (10, 20, 30, 40, 50):
        e = res["log"].get(st)
        if not e:
            continue
        b = P.boss_def(st)
        ra = e["first_r"]
        s = B.stats(res["snaps"][ra - 1], st, ra, "wind")
        _, t, T, dps = B.gate(s, b, hp[st])
        worst = max(B.gate(B.stats(res["snaps"][ra - 1], st, ra, w), b, hp[st])[1] / (B.stats(res["snaps"][ra - 1], st, ra, w)["dur"] - B.GATE["T0"])
                    for w in B.BOSS_WEATHERS)
        rows.append(dict(stage=st, boss=b["id"], hp=float("%.4g" % hp[st]), hp_mul=round(hp[st] / (B.TIER_HP[3] * B.zone_mul(st)), 4),
                         first_r=ra, first_min=round(e["first_min"], 1), tries=e["tries"], kill_r=e.get("kill_r"),
                         kill_min=round(e["kill_min"], 1) if "kill_min" in e else None, W=(e.get("kill_r") or 0) - ra,
                         atk_first=round(s["atk"], 1), atk_kill=round(e.get("atk", 0), 1),
                         r1={"x0.7": round(t / 0.7 / T, 2), "x1.0": round(t / T, 2), "x1.3": round(t / 1.3 / T, 2)},
                         r1_worst_weather_x0_7=round(worst / 0.7, 2)))
    return rows


def kill_hits(res):
    """일반 버섯을 몇 대에 잡나(능력치 확인용): 스테이지별 흔함(등급 0)·전설(등급 3) 체력 ÷ 공격력"""
    out = []
    for st in (5, 15, 25, 44, 50):
        r = next((x for x in res["rec"] if x["stage"] >= st), res["rec"][-1])
        z = B.zone_mul(st)
        out.append(dict(stage=st, r=r["r"], atk=round(r["atk"], 1), common=round(B.TIER_HP[0] * z / r["atk"], 2), legend=round(B.TIER_HP[3] * z / r["atk"], 1)))
    return out


def main():
    patch(CURVES[CURVE])
    m, hp_raw, plan_inc, prices, hist, kr = solve_for(KILL50)
    fm = FINAL_MUL.get(CURVE)
    hp = {st: B.TIER_HP[3] * B.zone_mul(st) * v for st, v in fm.items()} if fm else hp_raw
    base = run(prices, hp)
    kr = base["log"].get(50, {}).get("kill_r")
    # 싱크 시작가 = 스테이지 50 일반 판(파밍 판) 골드 × 3
    n50 = [x["inc"] for x in base["rec"] if x["stage"] == 50 and x["kind"] in ("run", "farm")]
    c0 = SINK["c0_rounds"] * (n50[-1] if n50 else base["rec"][-1]["inc"])
    full = run(prices, {**hp, 60: math.inf}, stop_stage=60, max_rounds=260, snap=False)
    cases = {
        "기준": {}, "H0×0.7": dict(gate=dict(H0=0.63)), "H0×1.3": dict(gate=dict(H0=1.17)),
        "MOVER_H 0.4": dict(gate=dict(MOVER_H=0.4)), "TAU 0": dict(gate=dict(TAU=0.0)),
        "H0×0.7+MOVER_H 0.4": dict(gate=dict(H0=0.63, MOVER_H=0.4)),
        "H0×0.7+MOVER_H 0.4+TAU 0": dict(gate=dict(H0=0.63, MOVER_H=0.4, TAU=0.0)),
        "수입×0.5": dict(inc_mul=0.5), "수입×2": dict(inc_mul=2.0), "재도전 3판": dict(retry=3), "재도전 8판": dict(retry=8),
    }
    sens = {}
    for sink in (False, True):
        if sink:
            add_sink(c0)
        for k, v in cases.items():
            r = run(prices, hp, **v)
            s = B.summarize(r)
            sens[("싱크 " if sink else "") + k] = dict(kill50=r["log"].get(50, {}).get("kill_r"), min=round(r["minutes"]), tries=[e["tries"] for e in s],
                                                     first_try_10_20=[e["tries"] == 1 for e in s][:2],
                                                     sink=[r["lv"].get("inf_atk", 0), r["lv"].get("inf_gold", 0)] if sink else None)
        if sink:
            remove_sink()
    rows = r1_table(base, hp)
    # 공격력 노드 레벨별 "레벨당 증가 %"(그 레벨을 사기 직전 평평한 공격력 대비) — 단계 넘김 확인
    seq, cum = [], 1.0
    for k in ATK:
        n = B.NODE[k]
        for L in range(n["max"]):
            d = n["efx"][L + 1] - n["efx"][L]
            seq.append(dict(key=k, lv=L + 1, add=d, pct=round(100 * d / cum, 1)))
            cum += d
    out = dict(curve=CURVE, per_level=CURVES[CURVE], atk=atk_table(CURVES[CURVE]), margin_last=round(m, 4), kill50=kr,
               margins=dict(first=P.MARGIN_FIRST, wall=P.MARGIN_WALL, by=P.MARGIN_BY, last=round(m, 4)),
               bosses=rows, minutes=round(base["minutes"], 1), rounds=base["rounds"], tier_done=full["done"],
               shape=P.shape(prices), tier_scale=P.tier_scale(prices), pct_per_level=seq, hits=kill_hits(base),
               sink_c0=float("%.3g" % c0), sens=sens, hp_raw={str(k): float("%.4g" % v) for k, v in hp_raw.items()},
               hp_mul_raw={str(k): round(v / (B.TIER_HP[3] * B.zone_mul(k)), 4) for k, v in hp_raw.items()}, hist=hist[-3:])
    json.dump(out, open(os.path.join(HERE, f"theory_r212{'' if CURVE == 'A' else '_' + CURVE}.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1, default=str)
    with open(os.path.join(HERE, f"theory_boss_curve_r212{'' if CURVE == 'A' else '_' + CURVE}.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(base["rec"][0].keys()))
        w.writeheader(); w.writerows(base["rec"])
    print(f"곡선 {CURVE} {CURVES[CURVE]} · MARGIN_LAST {m:.4f} · 스테이지 50 처치 R{kr} · {base['minutes']:.0f}분 · 단계 완료 {full['done']}")
    for a in out["atk"]:
        print("  ", a)
    for r in rows:
        print("  ", r)
    print("  hits", out["hits"])
    print("  c0", out["sink_c0"], "shape", out["shape"])
    for k, v in sens.items():
        print("  ", k, v)
    return out


if __name__ == "__main__":
    main()
