# -*- coding: utf-8 -*-
"""R3 초반 개편 이론 모델 — 판 수입 근사 + 구조안(군락·시간·희귀 노드) + 1단계 앞쪽 가격 사다리 + 가장 싼 것부터 사기.

근사식 (alpha는 T3-0 자료로 못 정해 0.1/0.3/0.5 세 경우로 따로 맞춤, 기준 0.3):
    I = T * g * ((1 + f + tau) * kappa * c**alpha + (1 + f) * sigma * stump) * acorn * moss
    T 판 시간, g 정산 배율(1.1 + ed_price-1), f 타격 점수 고정값(ed_score), c 최대 군락 수,
    tau 높은 등급(에픽 5점·유니크 20점) 몫 = 스테이지 1~6은 0(초반 0등급 풀), 7 이상은 TAU7 * w(ed_rare0)
        TAU7 = T3-0 판 7에서 역산한 시드 평균 (tau7_from_t0), w = 새 노드 ed_rare0 효과(없으면 1)
판 1(튜토리얼: 군락 1 + 낱개 8)은 T3-0 평균 18골드.
모델 밖: 공격력·수확기·스킬(accel/pierce/burst)·ps_size/spd·ed_bonus — 수입 0으로 봄(판 8 이후 수입을 낮게 잡음 → 사는 수를 적게 잡는 쪽).

가격: 1단계(Tier 1) 중 지금 3,000골드 아래 50레벨 + 새 레벨 5개(ed_cols Lv4, ps_dur Lv4, ed_rare0 Lv1~3)를
  노드마다 (1레벨 값 × 레벨 사이 배율^(L-1))로 다시 매김(PRICING, 2유효숫자). 그 위 레벨은 지금 DB 값.
실행: python BalanceData/theory/r3_model.py → r3_out.json · r3_prices.json · r3_curve.csv (난수 없음, 같은 결과)
"""
import csv
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DB = os.path.join(ROOT, "Assets", "Resources", "DB", "db.json")
T0 = os.path.join(ROOT, "BalanceData", "round3_t0")

MAP_ZOOM = [1.5, 1.35, 1.2, 1.1, 1.0]
TAU7 = None          # main()에서 T3-0 판 7로 역산 (tau7_from_t0)
TUTORIAL_INC = 18
ROUNDS = 12
BAND_TOP = 3000          # 1단계 사다리가 지금 가격 곡선에 이어지는 곳 (md_kid L2 3,400 · accel/pierce L3 3,700 바로 아래)

# ---------------- 제안 구조 ----------------
ED_COLS_EFX = [4, 6, 10, 17, 27]        # 적용안 1: DB ed_cols Efx_Lv0~4 (Lv4 27 = 지금 Lv3 12+5*3)
PS_DUR_EFX = [0, 2, 4, 7, 11]           # 적용안 2: BASE_TIME 10, ps_dur Lv4 +11 (= 지금 15+6)
BASE_TIME_NEW = 10
ED_RARE0_EFX = [0.2, 0.45, 0.7, 1.0]    # 적용안 3(새 노드, 선택): 에픽 이상 등장 가중치 배율, 다 사면 1 = 지금
ED_SCORE_EFX = [0, 1, 2, 3]
ED_PRICE_EFX = [1.0, 1.3, 1.6, 1.9]
NEW_PARENT = {"ps_dur": "power", "ed_rare0": "ed_cols"}
NEW_MAX = {"ed_cols": 4, "ps_dur": 4, "ed_rare0": 3}

# 1단계 앞쪽 가격: 노드마다 (1레벨 값, 레벨 사이 배율, 레벨 수). 레벨 사이 배율 <= 4 (조건), 사는 순서는 가격 순서(가장 싼 것부터)
# 지금 3,000 아래 50레벨 + 새 레벨 5개(ed_cols Lv4 · ps_dur Lv4 · ed_rare0 Lv1~3). 그 위(3,400~)는 지금 DB 값 그대로
PRICING = {
    "power":     (12, 3.5, 4),
    "ed_score":  (15, 3.3, 3),
    "ed_cols":   (19, 3.0, 4),
    "ps_dur":    (24, 3.0, 4),
    "ed_price":  (30, 3.0, 3),
    "md_bar":    (36, 3.0, 3),
    "dev_stump": (60, 1.0, 1),
    "md_combo":  (95, 3.0, 3),
    "ed_regen":  (130, 3.0, 3),
    "ed_rare0":  (200, 3.0, 3),
    "md_map":    (230, 2.6, 4),
    "ps_atk":    (370, 3.8, 2),     # Lv3 5,200 · Lv4 20,000은 지금 값
    "dev_moss":  (420, 1.0, 1),
    "ps_size":   (450, 2.0, 3),
    "ps_spd":    (480, 2.0, 3),
    "ed_bonus":  (550, 2.3, 3),
    "accel":     (700, 3.0, 2),     # Lv3 3,700 · Lv4 14,000 지금 값
    "dev_acorn": (900, 1.0, 1),
    "pierce":    (1000, 2.5, 2),    # Lv3 3,700 · Lv4 14,000 지금 값
    "md_kid":    (1300, 1.0, 1),    # Lv2 3,400 · Lv3 6,800 지금 값
    "burst":     (1600, 1.0, 1),    # Lv2 5,000 ~ 지금 값
    "ps_ball":   (2400, 1.0, 1),
}


def js_round(x):
    return math.floor(x + 0.5)


def area(map_lv):
    z = MAP_ZOOM[map_lv]
    return (1 / (z * z)) ** 0.7


def nice(x):
    """2유효숫자 (10 아래는 정수)"""
    if x < 10:
        return float(max(1, round(x)))
    e = 10 ** (math.floor(math.log10(x)) - 1)
    return float(round(x / e) * e)


def ladder_from_pricing():
    items = []
    for k, (p1, q, n) in PRICING.items():
        for L in range(1, n + 1):
            items.append((k, L, nice(p1 * q ** (L - 1))))
    items.sort(key=lambda t: (t[2], t[0], t[1]))
    return items


# ---------------- 게임 식 (지금 / 제안) ----------------
def maxcol_new(nodes):
    return max(1, js_round(ED_COLS_EFX[nodes.get("ed_cols", 0)] * area(nodes.get("md_map", 0))))


def maxcol_draft(stage, nodes):
    mul = [0.4, 0.45, 0.5, 0.6, 0.7, 0.85]
    base = max(3, math.ceil(12 + 5 * nodes.get("ed_cols", 0)))
    c = max(4, js_round(base * area(nodes.get("md_map", 0))))
    return max(1, js_round(c * mul[stage - 1])) if stage <= 6 else c


def time_new(stage, nodes):
    base = 10 if stage <= 4 else BASE_TIME_NEW + min(10, 0.5 * (stage - 1))
    return base + PS_DUR_EFX[nodes.get("ps_dur", 0)]


def time_draft(stage, nodes):
    base = 10 if stage <= 4 else 15 + min(10, 0.5 * (stage - 1))
    return base + [0, 2, 4, 6][min(3, nodes.get("ps_dur", 0))]


# ---------------- 맞춤 ----------------
def t0_points():
    pts = []
    for s in (1, 2, 3):
        st = [json.loads(l) for l in open(os.path.join(T0, f"states_s{s}.jsonl"), encoding="utf-8")]
        ph = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))
        for i in range(1, 6):   # 판 2~6 (스테이지 2~6)
            n = st[i - 1]["nodes"]
            stg = ph["stageR"][i]
            pts.append(dict(c=maxcol_draft(stg, n), T=time_draft(stg, n), f=ED_SCORE_EFX[n.get("ed_score", 0)],
                            g=1.1 + ED_PRICE_EFX[n.get("ed_price", 0)] - 1, stump=n.get("dev_stump", 0), inc=ph["incR"][i]))
    return pts


def fit_fixed_alpha(pts, alpha):
    best = None
    for si in range(0, 161):
        sigma = si * 0.05
        for ki in range(20, 400):
            kappa = ki * 0.01
            e = sum((math.log(p["T"] * p["g"] * (1 + p["f"]) * (kappa * p["c"] ** alpha + sigma * p["stump"])) - math.log(p["inc"])) ** 2 for p in pts)
            if best is None or e < best[0]:
                best = (e, kappa, sigma)
    e, kappa, sigma = best
    return dict(alpha=alpha, kappa=round(kappa, 2), sigma=round(sigma, 2), rmse_log=round(math.sqrt(e / len(pts)), 3))


# ---------------- 수입 ----------------
def income(stage, nodes, m, tau7=None, mul=1.0, rare_node=True):
    tau7 = TAU7 if tau7 is None else tau7
    if stage == 1:
        return TUTORIAL_INC * mul
    T = time_new(stage, nodes)
    g = 1.1 + ED_PRICE_EFX[nodes.get("ed_price", 0)] - 1
    f = ED_SCORE_EFX[nodes.get("ed_score", 0)]
    w = ED_RARE0_EFX[nodes.get("ed_rare0", 0)] if rare_node else 1.0
    tau = 0 if stage <= 6 else tau7 * w
    c = maxcol_new(nodes)
    acorn = 1 + (2.5 / T if nodes.get("dev_acorn", 0) else 0)
    moss = 1.05 if nodes.get("dev_moss", 0) else 1
    # 그루터기 점수는 0등급 점수 + f (RoundSim.Balls.cs:222) → 높은 등급 몫 tau를 받지 않는다
    return mul * T * g * ((1 + f + tau) * m["kappa"] * c ** m["alpha"] + (1 + f) * m["sigma"] * nodes.get("dev_stump", 0)) * acorn * moss


def tau7_from_t0(m):
    """T3-0 판 7(스테이지 7, 높은 등급 첫 등장)에서 tau 역산: I = T*g*((1+f+tau)*kappa*c^a + (1+f)*sigma*stump)"""
    vals = []
    for s in (1, 2, 3):
        st = [json.loads(l) for l in open(os.path.join(T0, f"states_s{s}.jsonl"), encoding="utf-8")]
        ph = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))
        n = st[5]["nodes"]
        T = time_draft(7, n)
        g = 1.1 + ED_PRICE_EFX[n.get("ed_price", 0)] - 1
        f = ED_SCORE_EFX[n.get("ed_score", 0)]
        c = maxcol_draft(7, n)
        base = m["kappa"] * c ** m["alpha"]
        tau = (ph["incR"][6] / (T * g) - (1 + f) * m["sigma"] * n.get("dev_stump", 0)) / base - (1 + f)
        vals.append(round(tau, 2))
    return vals


def income_draft(stage, nodes, m, tau7=None):
    tau7 = TAU7 if tau7 is None else tau7
    """T3-0 가안 코드(EARLY_* 배율·초반 10초·높은 등급은 7부터)로 같은 근사식"""
    if stage == 1:
        return TUTORIAL_INC
    T = time_draft(stage, nodes)
    g = 1.1 + ED_PRICE_EFX[nodes.get("ed_price", 0)] - 1
    f = ED_SCORE_EFX[nodes.get("ed_score", 0)]
    tau = 0 if stage <= 6 else tau7
    c = maxcol_draft(stage, nodes)
    return T * g * ((1 + f + tau) * m["kappa"] * c ** m["alpha"] + (1 + f) * m["sigma"] * nodes.get("dev_stump", 0))


def auto_steps(m):
    """노드를 그대로 두고 스테이지만 넘어갈 때 판 수입 배율 (자동 계단)"""
    out = {}
    for f_lv in (0, 2, 3):
        n = {"ed_score": f_lv, "dev_stump": 1, "ed_cols": 2, "ps_dur": 2}
        n_draft = {"ed_score": f_lv, "dev_stump": 1}
        out[f"f={ED_SCORE_EFX[f_lv]}"] = dict(
            draft_4to5=round(income_draft(5, n_draft, m) / income_draft(4, n_draft, m), 2),
            draft_6to7=round(income_draft(7, n_draft, m) / income_draft(6, n_draft, m), 2),
            new_4to5=round(income(5, n, m) / income(4, n, m), 2),
            new_6to7_rare0=round(income(7, n, m) / income(6, n, m), 2),
            new_6to7_no_rare_node=round(income(7, n, m, rare_node=False) / income(6, n, m, rare_node=False), 2))
    return out


def db_tier1():
    d = json.load(open(DB, encoding="utf-8"))
    up = {u["Upg_ID"]: u for u in d["Upgrade"]}
    cost = {c["Cost_ID"]: c for c in d["Upgrade_Cost"]}
    par, prices = {}, {}
    for u in d["Upgrade"]:
        p = up.get(u["Upg_Pre"])
        par[u["Upg_Key"]] = p["Upg_Key"] if p else None
        if u["Tier"] != 1:
            continue
        c = cost[u["Cost_ID"]]
        for L in range(1, u["Efx_MaxLv"] + 1):
            if c[f"Gem_Lv{L}"] > 0:
                continue
            prices[(u["Upg_Key"], L)] = c[f"Cost_Lv{L}"]
    par.update(NEW_PARENT)
    return par, prices


def buy_all(nodes, gold, price_of, par, rare_node):
    bought = []
    while True:
        best = None
        for (k, L), p in price_of.items():
            if not rare_node and k == "ed_rare0":
                continue
            if nodes.get(k, 0) != L - 1:
                continue
            pk = par.get(k)
            if pk is not None and nodes.get(pk, 0) < 1:
                continue
            if best is None or p < best[2]:
                best = (k, L, p)
        if best is None or best[2] > gold:
            return gold, bought
        gold -= best[2]
        nodes[best[0]] = best[1]
        bought.append(f"{best[0]}{best[1]}")


def simulate(m, price_of, par, tau7=None, mul=1.0, rounds=ROUNDS, rare_node=True):
    """가장 싼 것부터 사기 (Game.BulkBuy와 같은 규칙). 스테이지 1~13은 보스 없음 → 판 = 스테이지"""
    nodes, gold, log = {}, 0.0, []
    for r in range(1, rounds + 1):
        inc = income(r, nodes, m, tau7, mul, rare_node)
        gold += inc
        gold, bought = buy_all(nodes, gold, price_of, par, rare_node)
        log.append(dict(round=r, T=time_new(r, nodes), c=maxcol_new(nodes), inc=round(inc, 1), buys=bought, n=len(bought), left=round(gold, 1)))
    return log


def checks(price_of, top=20000):
    """1단계 전체(사다리 + 지금 값) 가격 순서로: 바로 앞 대비 배율, 노드 안 다음 레벨 배율 (top 아래만)"""
    ladder = sorted(((k, L, p) for (k, L), p in price_of.items() if p <= top), key=lambda t: (t[2], t[0], t[1]))
    seq = [p for (_, _, p) in ladder]
    jumps = [seq[i] / seq[i - 1] for i in range(1, len(seq))]
    jmax = max(range(len(jumps)), key=lambda i: jumps[i])
    inner, worst = {}, (0, None)
    for (k, L, p) in sorted(ladder, key=lambda t: (t[0], t[1])):
        if k in inner and p / inner[k] > worst[0]:
            worst = (p / inner[k], f"{k} L{L - 1}->L{L}")
        inner[k] = p
    return dict(max_jump=round(max(jumps), 2), max_jump_at=f"{ladder[jmax][0]}{ladder[jmax][1]}->{ladder[jmax + 1][0]}{ladder[jmax + 1][1]}", mean_jump=round(sum(jumps) / len(jumps), 3), monotone=all(j >= 1 for j in jumps),
                max_inner=round(worst[0], 2), max_inner_at=worst[1])


CASES = [("기준 a0.3", "0.3", None, 1.0, True), ("a0.1", "0.1", None, 1.0, True), ("a0.5", "0.5", None, 1.0, True),
         ("수입x0.7", "0.3", None, 0.7, True), ("수입x1.3", "0.3", None, 1.3, True),
         ("tau 최소(T3-0 시드2)", "0.3", "min", 1.0, True), ("tau 최대(T3-0 시드3)", "0.3", "max", 1.0, True),
         ("희귀 새 노드 없음", "0.3", None, 1.0, False), ("희귀 새 노드 없음 tau 최대", "0.3", "max", 1.0, False)]


def main():
    pts = t0_points()
    models = {str(a): fit_fixed_alpha(pts, a) for a in (0.1, 0.3, 0.5)}
    global TAU7
    taus = tau7_from_t0(models["0.3"])
    TAU7 = round(sum(taus) / len(taus), 2)
    par, db_prices = db_tier1()
    ladder = ladder_from_pricing()
    price_of = dict(db_prices)
    for (k, L, p) in ladder:
        price_of[(k, L)] = p
    out = dict(models=models, ladder=ladder, checks=checks(price_of), runs={})
    out["tau7"] = dict(seeds=taus, mean=TAU7)
    for name, a, tau, mul, rare in CASES:
        t = min(taus) if tau == "min" else max(taus) if tau == "max" else tau
        out["runs"][name] = simulate(models[a], price_of, par, t, mul, ROUNDS, rare)
    # 다 산 뒤 지금과 같은지
    out["time_full_same"] = all(abs((15 + min(10, 0.5 * (s - 1)) + 6) - (BASE_TIME_NEW + min(10, 0.5 * (s - 1)) + 11)) < 1e-9 for s in range(5, 300))
    out["auto_steps"] = auto_steps(models["0.3"])
    out["cols_full"] = dict(old_lv3=max(4, js_round(27 * area(4))), new_lv4=maxcol_new({"ed_cols": 4, "md_map": 4}),
                            new_by_lv_map0=[maxcol_new({"ed_cols": L}) for L in range(5)],
                            old_by_lv_map0=[max(4, js_round(max(3, 12 + 5 * L) * area(0))) for L in range(4)])
    # 1단계 레벨 가격 합 (지금 vs 제안) — 1단계 완료(R20 목표)에 주는 영향
    t1_old = sum(db_prices.values())
    t1_new = sum(price_of.values())
    out["tier1_sum"] = dict(old=t1_old, new=t1_new, levels_old=len(db_prices), levels_new=len(price_of))
    json.dump(out, open(os.path.join(HERE, "r3_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump([dict(key=k, lv=L, gold=p) for (k, L, p) in ladder], open(os.path.join(HERE, "r3_prices.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    with open(os.path.join(HERE, "r3_curve.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["case", "round", "T", "maxCol", "income", "n_buys", "buys"])
        for name, log in out["runs"].items():
            for x in log:
                w.writerow([name, x["round"], x["T"], x["c"], x["inc"], x["n"], " ".join(x["buys"])])
    print("models", json.dumps(models, ensure_ascii=False))
    print("checks", out["checks"], "time_full_same", out["time_full_same"])
    print("tau7", out["tau7"])
    print("auto_steps", json.dumps(out["auto_steps"], ensure_ascii=False))
    print("cols", out["cols_full"], "tier1_sum", out["tier1_sum"])
    print("ladder", " ".join(f"{k}{L}:{p:g}" for (k, L, p) in ladder))
    for name, log in out["runs"].items():
        print("==", name)
        for x in log:
            print(f"  R{x['round']} T{x['T']:.1f} c{x['c']} inc {x['inc']:.0f} n{x['n']} {' '.join(x['buys'])} (left {x['left']:.0f})")


if __name__ == "__main__":
    main()
