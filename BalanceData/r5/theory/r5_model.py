# -*- coding: utf-8 -*-
"""5회차 이론 모델 (theory, manager#20) — 5회차_요청.md P-1~P-8, 판정 G1~G4.

입력
  - Assets/Resources/DB/db.json (지금 DB: Hp_Mul king 0.35 · cleaner 2.33 · angel 93.33 · phantom 128.33 · mover 163.33)
  - BalanceData/round3_t3/main/log_s1~3.log (3회차 T3-3, 10-16, 관문 모드 3시드): 판 수입 표의 원본
보정 기준 (측정값, 문서.md 4회차 · 인계 10-19)
  - 4회차(봇 SKILL 0.6, 실제 조준, 6시드): Hp_Mul king 0.15 · cleaner 1 · angel 40 · phantom 55 · mover 70 일 때
    도전 횟수 10: 1·1·1·2·1·2 / 20: 2~3 / 30: 3~4 / 40·50: 2~5 (중앙 4)
  - king 0.8 일 때 12~13번 (같은 F안 가격 가정)
모델
  - 판 수입: 숲(표시 스테이지 < 20)은 '판 번호'별, 밤부터는 '스테이지'별 T3-3 일반 판 중앙값(3시드).
    초반 버섯 체력·낱개 버섯 변화는 수입 배율 (체력비)^(-BETA) · (1 + 낱개 몫 × 늘린 비율)로 넣는다.
  - 봇 = 가장 싼 레벨부터 산다(Game.BulkBuy, 균사석·싱크 제외). 가격 × COST_MUL(올림).
  - 보스 판: 처치 = ln(KAPPA[보스] · dps) − ln(need) + SIGMA·z ≥ 0,  z ~ N(0,1)
      dps  = 공격력(1+Σps_atk*)·1.1 · 무더기 파쇄 · 치명 기댓값 · 속도 · (1 + 0.5(영구 수확기 − 1))
      need = 보스 체력 · (1/(판 시간 − 2) + 회복률)   (청소부 회복률 0.05/3 per 초, BalanceSim 관문식과 같은 꼴)
      날씨 = 나쁜 날씨 3종 중 하나(폭우 속도 ×0.8 · 한파 −3초 · 포자 바람)
    KAPPA(보스별)·SIGMA는 4회차 측정 도전 횟수에 맞춘다 (봇 조준·기믹이 다 들어간 값).
  - 봇 재도전 = BalanceSim(실제 판 모드): 지면 끄고, 무언가 사면 다음 판 또는 3판마다 다시 켠다.
실행: python BalanceData/r5/theory/r5_model.py → r5_out.json · r5_curve.csv · r5_out.txt (난수 시드 고정, 결정적)
"""
import csv
import json
import math
import os
import random
import re
import statistics as stx

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DB = os.path.join(ROOT, "Assets", "Resources", "DB", "db.json")
LOGS = [os.path.join(ROOT, "BalanceData", "round3_t3", "main", f"log_s{s}.log") for s in (1, 2, 3)]

TIER3_HP = 40.0            # tuned 레전드리 체력 (Defs.cs PRESETS tuned hp[3])
BOSS_HP_K = 1.0
STAGE_GROWTH = 1.01
ZONE_FROM = [1, 20, 45, 75, 105]   # 표시 스테이지 기준 (Defs.World THEMES)
BOSS_ORDER = ["king", "cleaner", "angel", "phantom", "mover"]
LOOP_HP = 220.0
CLEANER_HEAL = 0.05 / 3.0
KB, T0 = 0.5, 2.0
OVERHEAD = 30.0
BOSS_REWARD_MUL = 2.0
FAIL_SCORE = 0.04          # 진 판 골드 / 일반 판 (BOSS_SCORE_MUL 0.05 × BOSS_FAIL_GOLD 0.8)
RETRY_FARM = 3             # BalanceSim.cs:215 실제 판 모드
BAD_WEATHER = [("storm", 0.8, 0.0), ("wind", 1.0, 0.0), ("cold", 1.0, 3.0)]
BALLS_R, BALLS_S = {}, {}
ALPHA_B = 0.5              # 영구 수확기 수 → 판 수입 탄력 (기록보다 수확기가 많으면 (많은 수/기록 수)^ALPHA_B), 민감도 0·0.75
BETA = 0.6                 # 초반 수입의 버섯 체력 탄력 (판 수입 ∝ 체력^-BETA), 민감도 0.3·1.0
MC = 3000
STOP_DISP = 56             # 표시 스테이지 56 = 보스 50 처치 뒤
MAX_ROUNDS = 420
TRY_CAP = 40

HP4 = {"king": 0.15, "cleaner": 1.0, "angel": 40.0, "phantom": 55.0, "mover": 70.0}       # 4회차 측정 때
HP_NOW = {"king": 0.35, "cleaner": 2.33, "angel": 93.33, "phantom": 128.33, "mover": 163.33}  # 지금 db.json
TRIES4 = {"king": 1.33, "cleaner": 2.5, "angel": 3.5, "phantom": 3.5, "mover": 3.5}       # 4회차 측정 평균(범위 가운데)
KING08_TRIES = 12.5


# ---------------- DB ----------------
def load_db():
    d = json.load(open(DB, encoding="utf-8"))
    cost = {c["Cost_ID"]: c for c in d["Upgrade_Cost"]}
    byid = {u["Upg_ID"]: u for u in d["Upgrade"]}
    nodes = {}
    for u in d["Upgrade"]:
        c = cost[u["Cost_ID"]]
        mx = int(u["Efx_MaxLv"])
        if mx == 0 or any(c.get(f"Gem_Lv{i}", 0) for i in range(1, 5)):
            continue
        nodes[u["Upg_Key"]] = {
            "tier": int(u["Tier"]), "max": mx,
            "cost": [float(c[f"Cost_Lv{i}"]) for i in range(1, mx + 1)],
            "efx": [float(u[f"Efx_Lv{i}"]) for i in range(0, mx + 1)],
            "pre": byid[u["Upg_Pre"]]["Upg_Key"] if u["Upg_Pre"] in byid else None,
            "req": byid[u["Upg_Req"]]["Upg_Key"] if u["Upg_Req"] in byid else None,
        }
    sinks = {u["Upg_Key"]: float(cost[u["Cost_ID"]]["Cost_Lv1"]) for u in d["Upgrade"] if int(u["Efx_MaxLv"]) == 0}
    return nodes, sinks, d


# ---------------- 판 수입 표 (T3-3) ----------------
UNIT = {"K": 1e3, "M": 1e6, "B": 1e9, "T": 1e12, "Qa": 1e15, "Qi": 1e18}
ROW = re.compile(r"^R(\d+) \| (\d+)분 \| ([^|]+)\| (\d+)s \| (\d+) \| ([^|]+)\| ([^|]+)\|")


def num(s):
    s = s.replace(",", "").strip()
    m = re.match(r"^([\d.]+)([A-Za-z]*)$", s)
    return float(m.group(1)) * UNIT.get(m.group(2), 1)


def parse_logs():
    runs = []
    for p in LOGS:
        rows, boss = [], None
        for line in open(p, encoding="utf-8"):
            if line.strip().startswith("보스 R"):
                boss = "clear" if "처치" in line else "fail"
                continue
            m = ROW.match(line)
            if not m:
                continue
            rows.append({"stage": int(m[1]), "zone": m[3].strip(), "sec": int(m[4]), "balls": int(m[5]), "inc": num(m[7]), "boss": boss})
            boss = None
        runs.append(rows)
    return runs


def income_tables(runs):
    by_round, by_stage = {}, {}
    for run in runs:
        for i, r in enumerate(run, 1):
            if r["boss"]:
                continue
            if r["zone"] == "숲":
                by_round.setdefault(i, []).append(r["inc"])
            by_stage.setdefault(r["stage"], []).append(r["inc"])
    forest = {k: stx.median(v) for k, v in sorted(by_round.items())}
    global BALLS_R, BALLS_S
    br, bs = {}, {}
    for run in runs:
        for i, r in enumerate(run, 1):
            if r["zone"] == "숲":
                br.setdefault(i, []).append(r["balls"])
            bs.setdefault(r["stage"], []).append(r["balls"])
    BALLS_R = {k: stx.median(v) for k, v in br.items()}
    BALLS_S = {k: stx.median(v) for k, v in bs.items()}
    # 판 번호별 표는 단조 증가로 다듬는다 (숲 안에서 판 수입은 노드를 살수록 오른다): 앞 판 최댓값
    best, fr = 0.0, {}
    for k in range(1, max(forest) + 1):
        best = max(best, forest.get(k, 0.0))
        fr[k] = best
    st = {k: stx.median(v) for k, v in sorted(by_stage.items())}
    full = {}
    known = sorted(k for k in st if k >= 20)
    for s in range(20, 121):
        if s in st:
            full[s] = st[s]
            continue
        lo = max([k for k in known if k < s], default=None)
        hi = min([k for k in known if k > s], default=None)
        if lo is None:
            full[s] = st[hi]
        elif hi is None:
            full[s] = st[lo]
        else:
            w = (s - lo) / (hi - lo)
            full[s] = math.exp(math.log(st[lo]) * (1 - w) + math.log(st[hi]) * w)
    return fr, full, st


# ---------------- 시나리오 ----------------
def early_hp_now(s):   # Defs.EarlyHpMul (지금, 내부 판 번호 s)
    if s <= 3:
        return 0.5
    if s >= 7:
        return 1.0
    return 0.5 + 0.5 * (s - 3) / 4


LOOSE_SHARE = [0.34, 0.30, 0.22, 0.15, 0.10, 0.07]          # 판 수입 중 낱개 버섯 몫 (이론.md R3, 판 1~6)
LOOSE_OLD, LOOSE_NOW = [6, 6, 5, 4, 3, 2], [10, 10, 9, 8, 6, 4]


def scenario(name, **kw):
    base = {"name": name, "off": 0, "cost_mul": 1.0, "hp": dict(HP4), "early_hp": lambda s: 1.0, "loose": False,
            "time_key": "internal", "ps_ball": None, "ps_ball3": None, "spore": False, "beta": BETA, "map_inc": 1.0,
            "growth_shift": False}
    base.update(kw)
    return base


def inc_mult(sc, s, d, k_boss):
    m = 1.0
    hp = sc["early_hp"](s)
    if sc["growth_shift"] and d >= 1:
        hp *= STAGE_GROWTH ** (-4)       # P-2: STAGE_GROWTH가 4판 늦게 → 같은 판에서 체력·보상 ×0.961
        m *= STAGE_GROWTH ** (-4)
    m *= hp ** (-sc["beta"])
    if sc["loose"] and 1 <= s <= 6:
        m *= 1 + LOOSE_SHARE[s - 1] * (LOOSE_NOW[s - 1] / LOOSE_OLD[s - 1] - 1)
    if sc["spore"]:
        slow = min(0.35, 0.25 + 0.03 * k_boss)
        m *= 1 + 0.02 * (0.35 - slow) / 0.10   # 포자 구름 속 시간 약 15% 가정 → 이속저하 10%p당 판 수입 약 2%
    if s <= 4 + sc["off"]:
        m *= sc["map_inc"]
    return m


def apply_nodes(nodes, sc):
    n = {k: dict(v) for k, v in nodes.items()}
    if sc["ps_ball"]:
        n["ps_ball"] = dict(n["ps_ball"], max=len(sc["ps_ball"]), cost=list(sc["ps_ball"]), efx=[0.0] + [float(i + 1) for i in range(len(sc["ps_ball"]))])
    if sc["ps_ball3"]:
        n["ps_ball3"] = dict(n["ps_ball3"], max=len(sc["ps_ball3"]), cost=list(sc["ps_ball3"]), efx=[0.0] + [float(i + 1) for i in range(len(sc["ps_ball3"]))])
    return n


# ---------------- 봇 ----------------
def parent_ok(n, lv, k):
    a = n[k]
    return (a["pre"] is None or lv.get(a["pre"], 0) >= 1) and (a["req"] is None or lv.get(a["req"], 0) >= n[a["req"]]["max"])


def price(a, L, cm):
    return math.ceil(a["cost"][L] * cm)


def bulk_buy(n, lv, gold, cm, log=None, rnd=0):
    bought = 0
    while True:
        best, bc = None, math.inf
        for k, a in n.items():
            L = lv.get(k, 0)
            if L >= a["max"] or not parent_ok(n, lv, k):
                continue
            p = price(a, L, cm)
            if p < bc:
                best, bc = k, p
        if best is None or gold < bc:
            return gold, bought
        gold -= bc
        lv[best] = lv.get(best, 0) + 1
        bought += 1
        if log is not None:
            log.append((rnd, best, lv[best], bc))


ATK_KEYS = ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4")


def E(n, lv, k):
    return n[k]["efx"][lv.get(k, 0)] if k in n else 0.0


def dps(n, lv, spdw):
    atk = (1 + sum(E(n, lv, k) for k in ATK_KEYS)) * 1.1 * 1.1     # ComputeStats ×1.1 · 기본 수확기(sam) atkMul 1.1
    heavy = E(n, lv, "ps_heavy") or 1.0
    crit = E(n, lv, "ps_crit")
    critmul = E(n, lv, "ps_crit2") or 3.0
    spd = (E(n, lv, "ps_spd") or 1.0) * spdw
    balls = 1 + lv.get("ps_ball", 0) + lv.get("ps_ball2", 0) + lv.get("ps_ball3", 0)
    return atk * heavy * (1 + min(1, crit) * (critmul - 1)) * spd * (1 + KB * (balls - 1)), balls


def round_time(n, lv, s, d, sc, cold=0.0):
    key = s if sc["time_key"] == "internal" else d
    early = 4 if sc["time_key"] == "internal" else 4 - sc["off"]
    base = 10.0 if key <= early else 10.0 + min(10.0, 0.5 * (key - 1))
    return base + E(n, lv, "ps_dur") + E(n, lv, "ps_dur2") - cold


def zone_of(d):
    return max(i for i, f in enumerate(ZONE_FROM) if d >= f) if d >= 1 else 0


def boss_of(d):
    return BOSS_ORDER[(d // 10 - 1) % 5] if d // 10 <= 5 else "loop"


def boss_hp(d, sc):
    b = boss_of(d)
    mul = LOOP_HP if b == "loop" else sc["hp"][b]
    return TIER3_HP * BOSS_HP_K * 10 ** zone_of(d) * STAGE_GROWTH ** (d - 1) * mul


def try_boss(n, lv, s, d, sc, kappa, sigma, rng):
    b = boss_of(d)
    _, spdw, cold = rng.choice(BAD_WEATHER) if rng else BAD_WEATHER[1]
    T = round_time(n, lv, s, d, sc, cold)
    hp = boss_hp(d, sc)
    need = hp * (1.0 / max(0.5, T - T0) + (CLEANER_HEAL if b == "cleaner" else 0.0))
    v, _ = dps(n, lv, spdw)
    z = rng.gauss(0, 1) if rng else 0.0
    return math.log(kappa.get(b, kappa["mover"]) * v) - math.log(need) + sigma * z >= 0, T


def simulate(nodes, fr, st, sc, kappa, sigma, rng, stop_disp=STOP_DISP, record=False, harv=None):
    n = apply_nodes(nodes, sc)
    off = sc["off"]
    lv, gold, log = {}, 0.0, []
    s, r, mins, k_boss = 1, 0, 0.0, 0
    boss_on, farm, bought_since = True, 0, False
    last_normal = 0.0
    tries, first_arrive, kill_round, kill_min, curve = {}, {}, {}, {}, []
    hv_state = dict(harv["init"]) if harv else None
    tree_done = None
    while r < MAX_ROUNDS:
        d = s - off
        if d >= stop_disp and tree_done is not None:
            break
        if d >= stop_disp and not record:
            break
        r += 1
        is_boss_stage = d >= 10 and d % 10 == 0
        if is_boss_stage and d not in first_arrive:
            first_arrive[d] = (r, round(dps(n, lv, 1.0)[0], 2))
        base_inc = fr.get(r, fr[max(fr)]) if d < 20 else st[min(d, 120)]
        inc = base_inc * inc_mult(sc, s, d, k_boss)
        b_log = (BALLS_R.get(r) if d < 20 else BALLS_S.get(d)) or (BALLS_R[max(BALLS_R)] if d < 20 else BALLS_S[max(k for k in BALLS_S if k <= max(d, 1))] if any(k <= d for k in BALLS_S) else 1)
        inc *= (dps(n, lv, 1.0)[1] / b_log) ** sc.get("alpha_b", ALPHA_B)
        if is_boss_stage and boss_on:
            tries[d] = tries.get(d, 0) + 1
            ok, T = try_boss(n, lv, s, d, sc, kappa, sigma, rng)
            if tries[d] >= TRY_CAP:
                ok = True   # 계산 상한 (평균 도전 횟수는 이 값에서 잘린다)
            if ok:
                g = inc * FAIL_SCORE + max(inc * FAIL_SCORE, last_normal) * BOSS_REWARD_MUL
                s += 1
                k_boss += 1
                kill_round[d], kill_min[d] = r, round(mins + (T + OVERHEAD) / 60, 1)
            else:
                g = inc * FAIL_SCORE
                boss_on, farm, bought_since = False, 0, False
        else:
            T = round_time(n, lv, s, d, sc)
            g = inc
            last_normal = inc
            if is_boss_stage:
                farm += 1
            else:
                s += 1
        mins += (T + OVERHEAD) / 60
        gold += g
        if harv:
            gold = harv["buy"](hv_state, gold, d, r)
        gold, b = bulk_buy(n, lv, gold, sc["cost_mul"], log if record else None, r)
        if b > 0:
            bought_since = True
        if is_boss_stage and not boss_on and (bought_since or farm >= RETRY_FARM):
            boss_on, farm, bought_since = True, 0, False
        if tree_done is None and all(lv.get(k, 0) >= a["max"] for k, a in n.items()):
            tree_done = (r, round(mins, 1), s - off)
        if record:
            curve.append({"round": r, "stage_int": s, "disp": d, "inc": inc, "gold_after": gold, "dps": dps(n, lv, 1.0)[0], "balls": dps(n, lv, 1.0)[1], "min": round(mins, 2)})
    return {"tries": tries, "arrive": first_arrive, "kill_round": kill_round, "kill_min": kill_min, "rounds": r, "min": mins,
            "log": log, "curve": curve, "tree_done": tree_done, "lv": lv, "hv": hv_state}


def mc(nodes, fr, st, sc, kappa, sigma, runs=MC, seed=5, stop_disp=STOP_DISP):
    rng = random.Random(seed)
    res = {b: [] for b in (10, 20, 30, 40, 50)}
    mins50, rounds50 = [], []
    arrive = {b: [] for b in res}
    for _ in range(runs):
        o = simulate(nodes, fr, st, sc, kappa, sigma, rng, stop_disp)
        for b in res:
            res[b].append(o["tries"].get(b, 99))
            if b in o["arrive"]:
                arrive[b].append(o["arrive"][b][0])
        mins50.append(o["kill_min"].get(50, float("nan")))
        rounds50.append(o["kill_round"].get(50, 999))
    out = {}
    for b, v in res.items():
        v = sorted(v)
        out[b] = {"mean": round(sum(v) / len(v), 2), "median": v[len(v) // 2], "p_le1": round(sum(x <= 1 for x in v) / len(v), 3),
                  "p_le3": round(sum(x <= 3 for x in v) / len(v), 3), "p10": v[len(v) // 10], "p90": v[len(v) * 9 // 10],
                  "arrive_round_med": stx.median(arrive[b]) if arrive[b] else None}
    good = [m for m in mins50 if m == m]
    out["kill50_min_median"] = round(stx.median(good), 1) if good else None
    out["kill50_round_median"] = stx.median(rounds50)
    return out


# ---------------- 보정 (KAPPA, SIGMA) ----------------
def calibrate(nodes, fr, st):
    sc4 = scenario("S4_4회차")
    sigmas = [0.12, 0.2, 0.3]
    best = None
    for sg in sigmas:
        kap = fit_kappa(nodes, fr, st, sc4, sg)
        # king 0.8 → 12.5번 재현 오차
        sc08 = scenario("king0.8", hp=dict(HP4, king=0.8))
        m = mc(nodes, fr, st, sc08, kap, sg, runs=600, seed=21, stop_disp=11)[10]["mean"]
        err = abs(math.log(m / KING08_TRIES))
        if best is None or err < best[0]:
            best = (err, sg, kap, m)
    return best


def fit_kappa(nodes, fr, st, sc, sigma):
    kap = {b: 1.0 for b in BOSS_ORDER}
    for i, b in enumerate(BOSS_ORDER):
        stage = 10 * (i + 1)
        lo, hi = -12.0, 12.0
        for _ in range(16):
            mid = (lo + hi) / 2
            kap[b] = math.exp(mid)
            m = mc(nodes, fr, st, sc, kap, sigma, runs=200, seed=7 + i, stop_disp=stage + 1)[stage]["mean"]
            if m > TRIES4[b]:
                lo = mid
            else:
                hi = mid
        kap[b] = math.exp((lo + hi) / 2)
    return kap


def quick_test():
    import time
    nodes, sinks, _ = load_db()
    fr, st, raw = income_tables(parse_logs())
    print("forest by round:", {k: round(v) for k, v in fr.items()})
    print("stage 20..60:", {k: f"{st[k]:.3g}" for k in range(20, 61, 5)})
    t = time.time()
    rng = random.Random(1)
    kap = {b: 1.0 for b in BOSS_ORDER}
    o = simulate(nodes, fr, st, scenario("S4"), kap, 0.4, rng)
    print("1 run", time.time() - t, o["rounds"], o["tries"], o["arrive"])


if __name__ == "__main__" and os.environ.get("R5_QUICK"):
    quick_test()
