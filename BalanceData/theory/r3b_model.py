# -*- coding: utf-8 -*-
"""R3 개정(R3-11~) 모델 — T3-α 포화식 + 낱개 버섯 + 그루터기 ×3 + 판 4~7 가격 조정 → 판 1~12 가장 싼 것부터 사기.

수입 근사는 r3b_fit.py (T3-α로 맞춤, T3-0으로 바깥 확인). 게임 식·DB 읽기·구매 규칙은 r3_model.py를 그대로 쓴다.
    I(스테이지 s ≥ 2) = g * [ (1 + f + tau) * rho * Y(c, T) + (1 + f) * ( stump * S3(T) + qL * N(s) ) ]
    I(스테이지 1, 튜토리얼) = 18.3 (T3-0 평균, 군락 1 + 낱개 8)
N(s) = 판 시작 때 한 번 뿌리는 낱개 버섯 수 (LOOSE). 다시 생기지 않는다. 노드와 무관(군락 구매의 몫을 깎지 않게).
실행: python BalanceData/theory/r3b_model.py → r3b_out.json · r3b_curve.csv · r3b_prices.json (몬테카를로는 시드 고정)
"""
import csv
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r3_model as R3  # noqa: E402
import r3b_fit as FIT  # noqa: E402

ROUNDS = 12
TUTORIAL_INC = 18.33
STUMP_MUL_NEW = 3                       # 적용안: RoundSim.Balls.cs:222 `* 5` → `* 3`
LOOSE = {2: 6, 3: 5, 4: 4, 5: 3, 6: 2}  # 적용안: 스테이지별 낱개 버섯 수 (판 시작 때 한 번, 다시 안 생김, 스테이지 7부터 0)
NOISE_SD = 0.40                         # T3-α 판 하나의 ln 표준편차 (c 2~6 칸 0.31~0.52)
MC_RUNS = 2000

# 1단계 앞쪽 가격 — 노드마다 사다리에 넣는 레벨의 골드 (그 위 레벨은 지금 DB 값). r3_model.PRICING(R3-4)을 대체한다.
# 판 1~8 칸은 r3b_tune.py(판 1~6 구매 1~2개가 수입 ×0.8~×1.25·잡음에서 성립하도록 탐색) 결과를 손으로 고르게 다듬었다.
LADDER = {
    "power":     [12, 46, 170, 510],
    "ed_score":  [16, 60, 230],
    "ed_cols":   [22, 84, 250, 650],
    "ps_dur":    [30, 110, 300, 800],
    "ed_price":  [40, 150, 380],
    "md_bar":    [55, 200, 440],
    "dev_stump": [220],
    "md_combo":  [260, 600, 1300],
    "ed_regen":  [280, 700, 1700],
    "ed_rare0":  [320, 800, 2000],
    "md_map":    [340, 750, 1700, 4000],
    "ps_atk":    [370, 1400],
    "dev_moss":  [420],
    "ps_size":   [450, 900, 1800],
    "ps_spd":    [480, 960, 1900],
    "ed_bonus":  [550, 1300, 2900],
    "accel":     [700, 2100],
    "dev_acorn": [900],
    "pierce":    [1000, 2500],
    "md_kid":    [1300],
    "burst":     [1600],
    "ps_ball":   [2400],
}


def ladder():
    items = [(k, L, float(p)) for k, lv in LADDER.items() for L, p in enumerate(lv, 1)]
    items.sort(key=lambda t: (t[2], t[0], t[1]))
    return items


def price_table():
    par, db_prices = R3.db_tier1()
    price_of = dict(db_prices)
    for (k, L, p) in ladder():
        price_of[(k, L)] = p
    return par, db_prices, price_of


class Model:
    def __init__(self, fit, tau7, rho=1.0, loose=None, stump_mul=STUMP_MUL_NEW, qL=None):
        self.par = [fit["params"][k] for k in ("A", "b", "p", "K0", "e")]
        self.sg = {int(k): v for k, v in fit["stump"].items()}
        self.tau7 = tau7
        self.rho = rho
        self.loose = LOOSE if loose is None else loose
        self.stump_mul = stump_mul
        self.qL = fit["qL"] if qL is None else qL

    def parts(self, stage, nodes, rare_node=True):
        T = R3.time_new(stage, nodes)
        g = 1.1 + R3.ED_PRICE_EFX[nodes.get("ed_price", 0)] - 1
        f = R3.ED_SCORE_EFX[nodes.get("ed_score", 0)]
        w = R3.ED_RARE0_EFX[nodes.get("ed_rare0", 0)] if rare_node else 1.0
        tau = 0 if stage <= 6 else self.tau7 * w
        c = R3.maxcol_new(nodes)
        col = g * (1 + f + tau) * self.rho * FIT.Y(self.par, c, T)
        stump = g * (1 + f) * nodes.get("dev_stump", 0) * FIT.stump_S(self.sg, T, self.stump_mul / 5)
        loose = g * (1 + f) * self.qL * self.loose.get(stage, 0)
        acorn = 1 + (2.5 / T if nodes.get("dev_acorn", 0) else 0)
        moss = 1.05 if nodes.get("dev_moss", 0) else 1
        return dict(col=col * acorn * moss, stump=stump * acorn * moss, loose=loose, T=T, c=c)

    def income(self, stage, nodes, rare_node=True):
        if stage == 1:
            return TUTORIAL_INC
        p = self.parts(stage, nodes, rare_node)
        return p["col"] + p["stump"] + p["loose"]


def simulate(m, price_of, par, mul=1.0, rounds=ROUNDS, rare_node=True, noise=None):
    nodes, gold, log = {}, 0.0, []
    for r in range(1, rounds + 1):
        inc = m.income(r, nodes, rare_node) * mul * (noise[r - 1] if noise else 1.0)
        gold += inc
        before = dict(nodes)
        gold, bought = R3.buy_all(nodes, gold, price_of, par, rare_node)
        p = m.parts(r, before, rare_node) if r > 1 else dict(col=inc, stump=0, loose=0, T=10, c=1)
        log.append(dict(round=r, T=p["T"], c=p["c"], inc=round(inc, 1), loose_share=round(p["loose"] / max(inc, 1e-9) * mul, 3),
                        buys=bought, n=len(bought), left=round(gold, 1)))
    return log


def ok12(log, upto=6):
    return all(1 <= x["n"] <= 2 for x in log[:upto])


def window(m, price_of, par, upto=6):
    """판 1~upto가 모두 1~2개인 수입 배율 구간 (0.01 간격, 1.0을 포함하는 연속 구간)"""
    good = [round(0.30 + 0.01 * i, 2) for i in range(271) if ok12(simulate(m, price_of, par, 0.30 + 0.01 * i, upto), upto)]
    if 1.0 not in good:
        return dict(lo=None, hi=None, center=None, all=good[:3] + ["..."] + good[-3:] if good else [])
    lo = hi = 1.0
    while round(lo - 0.01, 2) in good:
        lo = round(lo - 0.01, 2)
    while round(hi + 0.01, 2) in good:
        hi = round(hi + 0.01, 2)
    return dict(lo=lo, hi=hi, center=round(math.sqrt(lo * hi), 3), width_ln=round(math.log(hi / lo), 3))


def monte_carlo(m, price_of, par, sd=NOISE_SD, runs=MC_RUNS, seed=7, mul=1.0):
    rng = random.Random(seed)
    all_ok = 0
    per_round = [[0, 0, 0] for _ in range(6)]   # 0개 / 1~2개 / 3개 이상
    bad_rounds = 0
    for _ in range(runs):
        noise = [math.exp(rng.gauss(0, sd)) for _ in range(6)]
        log = simulate(m, price_of, par, mul, 6, noise=noise)
        all_ok += ok12(log)
        nbad = 0
        for i, x in enumerate(log):
            k = 0 if x["n"] == 0 else 1 if x["n"] <= 2 else 2
            per_round[i][k] += 1
            nbad += k != 1
        bad_rounds += nbad
    return dict(all_rounds_ok=round(all_ok / runs, 3), mean_bad_rounds_of6=round(bad_rounds / runs, 2),
                per_round=[dict(zero=round(a / runs, 3), ok=round(b / runs, 3), three_plus=round(c / runs, 3)) for a, b, c in per_round])


def p5_checks(m, price_of, par):
    """기준 경로에서 구매 하나로 판 수입이 몇 배가 되는지 (다음 판 같은 스테이지 기준) — P5 ≤ ×2"""
    log = simulate(m, price_of, par)
    nodes, worst = {}, []
    for x in log:
        r = x["round"]
        for b in x["buys"]:
            k, L = b.rstrip("0123456789"), int(b[len(b.rstrip("0123456789")):])
            if r + 1 <= ROUNDS and r >= 1:
                before = m.income(r + 1, nodes)
                after_nodes = dict(nodes)
                after_nodes[k] = L
                after = m.income(r + 1, after_nodes)
                worst.append((round(after / before, 2), b, r))
            nodes[k] = L
    worst.sort(reverse=True)
    return worst[:8]


def tau7_new(fit):
    """T3-0 판 7에서 tau 역산 (포화식). 그루터기는 T3-0 당시 ×5"""
    par = [fit["params"][k] for k in ("A", "b", "p", "K0", "e")]
    sg = {int(k): v for k, v in fit["stump"].items()}
    vals = []
    for s in (1, 2, 3):
        st = [json.loads(l) for l in open(os.path.join(R3.T0, f"states_s{s}.jsonl"), encoding="utf-8")]
        ph = json.load(open(os.path.join(R3.T0, f"phase_s{s}.json"), encoding="utf-8"))
        n = st[5]["nodes"]
        T = R3.time_draft(7, n)
        g = 1.1 + R3.ED_PRICE_EFX[n.get("ed_price", 0)] - 1
        f = R3.ED_SCORE_EFX[n.get("ed_score", 0)]
        c = R3.maxcol_draft(7, n)
        tau = (ph["incR"][6] / g - (1 + f) * n.get("dev_stump", 0) * FIT.stump_S(sg, T, 1.0)) / FIT.Y(par, c, T) - (1 + f)
        vals.append(round(tau, 2))
    return vals


def auto_steps(m):
    out = {}
    for f_lv in (0, 2, 3):
        n = {"ed_score": f_lv, "dev_stump": 1, "ed_cols": 2, "ps_dur": 2, "ed_rare0": 0}
        out[f"f={R3.ED_SCORE_EFX[f_lv]}"] = dict(
            s4to5=round(m.income(5, n) / m.income(4, n), 2),
            s5to6=round(m.income(6, n) / m.income(5, n), 2),
            s6to7_rare0=round(m.income(7, n) / m.income(6, n), 2),
            s6to7_no_rare_node=round(m.income(7, n, False) / m.income(6, n, False), 2))
    return out


def colony_taste(m):
    """군락 구매(ed_cols L→L+1)의 판 수입 배율 — 낱개 있음/없음 (스테이지 3·5, f=1)"""
    out = {}
    no_loose = Model.__new__(Model)
    no_loose.__dict__.update(m.__dict__)
    no_loose.loose = {}
    for stg in (3, 5):
        for L in range(0, 3):
            a = {"ed_score": 1, "ed_cols": L}
            b = {"ed_score": 1, "ed_cols": L + 1}
            out[f"s{stg} ed_cols {L}->{L + 1}"] = dict(loose=round(m.income(stg, b) / m.income(stg, a), 2),
                                                        no_loose=round(no_loose.income(stg, b) / no_loose.income(stg, a), 2))
    return out


def draft_compare(fit, tau7, m_new, price_new, par_new):
    """가안(T3-0 코드: EARLY_* 배율·BASE_TIME 15·낱개 없음·그루터기 ×5·지금 DB 가격)과 제안을 같은 포화식으로 비교 → 판 12 수입 비.
    verify R3 ⑤와 같은 방식: 비를 T3-0 판 9~13 판당 수입 배율 중앙값 ×1.78로 나눠 '몇 판 늦나'로 바꾼다"""
    par = [fit["params"][k] for k in ("A", "b", "p", "K0", "e")]
    sg = {int(k): v for k, v in fit["stump"].items()}
    d = json.load(open(R3.DB, encoding="utf-8"))
    up = {u["Upg_ID"]: u for u in d["Upgrade"]}
    par_old = {u["Upg_Key"]: (up[u["Upg_Pre"]]["Upg_Key"] if u["Upg_Pre"] in up else None) for u in d["Upgrade"]}
    _, db_prices = R3.db_tier1()

    def inc_old(stage, n):
        if stage == 1:
            return TUTORIAL_INC
        T = R3.time_draft(stage, n)
        g = 1.1 + R3.ED_PRICE_EFX[n.get("ed_price", 0)] - 1
        f = R3.ED_SCORE_EFX[n.get("ed_score", 0)]
        tau = 0 if stage <= 6 else tau7
        c = R3.maxcol_draft(stage, n)
        return g * ((1 + f + tau) * FIT.Y(par, c, T) + (1 + f) * n.get("dev_stump", 0) * FIT.stump_S(sg, T, 1.0))

    nodes, gold, inc_o = {}, 0.0, []
    for r in range(1, ROUNDS + 1):
        i = inc_old(r, nodes)
        inc_o.append(round(i, 1))
        gold += i
        gold, _ = R3.buy_all(nodes, gold, db_prices, par_old, False)
    new = [x["inc"] for x in simulate(m_new, price_new, par_new)]
    ratio12 = new[-1] / inc_o[-1]
    return dict(draft_inc=inc_o, new_inc=new, ratio_r12=round(ratio12, 3), delay_rounds=round(-math.log(ratio12) / math.log(1.78), 2))


def stump_p5_range(fit, tau7, price_of, par):
    """그루터기 구매 1건의 판 수입 배율: ×3/×5, T3-α 시드별 S(최소~최대)"""
    out = {}
    sg = {int(k): v for k, v in fit["stump"].items()}
    lo = {10: dict(mean=min(sg[10]["seeds"].values())), 17: dict(mean=min(sg[17]["seeds"].values()))}
    hi = {10: dict(mean=max(sg[10]["seeds"].values())), 17: dict(mean=max(sg[17]["seeds"].values()))}
    for mul in (3, 5):
        for name, s in (("평균", None), ("시드 최소", lo), ("시드 최대", hi)):
            for inc_mul in (1.0, 1.47):
                m = Model(fit, tau7, stump_mul=mul)
                if s is not None:
                    m.sg = {T: dict(mean=s[T]["mean"]) for T in (10, 17)}
                log = simulate(m, price_of, par, inc_mul)
                nodes = {}
                for x in log:
                    if "dev_stump1" in x["buys"]:
                        before = dict(nodes)
                        for b in x["buys"]:
                            if b == "dev_stump1":
                                break
                            k = b.rstrip("0123456789")
                            before[k] = int(b[len(k):])
                        after = dict(before)
                        after["dev_stump"] = 1
                        r = x["round"] + 1
                        out[f"x{mul} {name} 수입x{inc_mul}"] = dict(round=x["round"], ratio=round(m.income(r, after) / m.income(r, before), 2))
                        break
                    for b in x["buys"]:
                        k = b.rstrip("0123456789")
                        nodes[k] = int(b[len(k):])
    return out


GRID = (0.6, 0.66, 0.7, 0.76, 0.8, 0.9, 1.0, 1.1, 1.2, 1.32, 1.47, 1.6)


def per_round_table(m, price_of, par):
    """판 1~12 구매 수 × 수입 배율 (같은 배율을 모든 판에) — 판별 성립 구간 표"""
    return {str(mul): [x["n"] for x in simulate(m, price_of, par, mul)] for mul in GRID}


def window_2to6(m, price_of, par):
    """판 1(튜토리얼, T3-0 16~21골드로 거의 일정)을 빼고 판 2~6만 1~2개인 구간"""
    good = []
    for i in range(271):
        mul = round(0.30 + 0.01 * i, 2)
        log = simulate(m, price_of, par, mul, 6)
        log[0]["n"] = max(1, log[0]["n"]) if mul >= 0.66 else log[0]["n"]
        if all(1 <= x["n"] <= 2 for x in log[1:6]):
            good.append(mul)
    lo = hi = 1.0
    while round(lo - 0.01, 2) in good:
        lo = round(lo - 0.01, 2)
    while round(hi + 0.01, 2) in good:
        hi = round(hi + 0.01, 2)
    return dict(lo=lo, hi=hi, center=round(math.sqrt(lo * hi), 3))


def worst_early_buys(m):
    """가장 싼 순서가 아닌 사람이 일찍 살 때 P5 (구매 하나로 다음 판 수입 배율) — 판 4·5 같은 스테이지에서"""
    out = {}
    for stg in (3, 4, 5, 6):
        base = {"power": 1, "ed_score": 1, "md_bar": 1}
        for k, L in (("dev_stump", 1), ("ed_cols", 1), ("ed_score", 2), ("ps_dur", 1), ("ed_price", 1)):
            a = dict(base)
            b = dict(base)
            b[k] = L
            out[f"s{stg} {k}{L}"] = round(m.income(stg, b) / m.income(stg, a), 2)
    return out


# ---- 첫 보스 스테이지 10 (사용자 결정 2026-10-16, manager: 14 → 10) ----
BOSS_STAGE = 10
KING_HP = 40 * 5.0 * 0.63 * 1.01 ** (BOSS_STAGE - 1)   # TIERS[3].hp(tuned 40) × BOSS_HP_K 5 × Hp_Mul king 0.63 × STAGE_GROWTH^9 = 137.8
PS_SPD = [1.0, 1.12, 1.24, 1.36]
WEATHERS = {"맑음": (1.0, 0), "폭풍(속도 ×0.8)": (0.8, 0), "한파(−3초)": (1.0, 3)}


def gate(nodes, stage=BOSS_STAGE, ab_atk=0.0, hp=KING_HP):
    """BalanceSim.BossGateTime (H0 0.9, KB 0.5, T0 2). 핀볼 수 = 1 + ps_ball. 날씨별 (처치 시간, 싸울 시간, 처치?)"""
    atk = (1 + nodes.get("ps_atk", 0)) * (1 + 0.1 + ab_atk)
    balls = 1 + nodes.get("ps_ball", 0)
    out = {}
    for w, (spd_w, minus) in WEATHERS.items():
        H = 0.9 * PS_SPD[nodes.get("ps_spd", 0)] * spd_w * (1 + 0.5 * (balls - 1))
        T = R3.time_new(stage, nodes) - minus - 2
        t = hp / (atk * H)
        out[w] = (round(t, 1), round(T, 1), t <= T)
    return out


def simulate_boss(m, price_of, par, mul=1.0, rounds=ROUNDS, ab_atk=0.0, hp=KING_HP, retry=5):
    """판 10에 보스 관문. 지면 스테이지 10에서 파밍(일반 판), 관문 예상(가장 나쁜 날씨 아님 = 맑음)이 처치거나 5판 지나면 다시 도전.
    처치 판 골드 = 2 × 직전 일반 판(BOSS_REWARD_MUL), 진 판 ≈ 0 (보스 점수 ×0.05 × 0.8). 날씨는 도전 순서대로 폭풍 → 한파 → 맑음"""
    nodes, gold, log, stage, last, tries, since = {}, 0.0, [], 1, 0.0, 0, 0
    order = list(WEATHERS)[1:] + ["맑음"]
    for r in range(1, rounds + 1):
        boss = stage == BOSS_STAGE and (tries == 0 or since >= retry or gate(nodes, ab_atk=ab_atk, hp=hp)["맑음"][2])
        if boss:
            w = order[tries % 3]
            g = gate(nodes, ab_atk=ab_atk, hp=hp)[w]
            tries += 1
            since = 0
            inc = 2 * last if g[2] else 0.0
            note = f"보스({w}) {'처치' if g[2] else '패배'} t {g[0]}/{g[1]}"
            if g[2]:
                stage += 1
        else:
            inc = m.income(stage, nodes) * mul
            last = inc
            note = ""
            since += 1
            if stage != BOSS_STAGE:
                stage += 1
        gold += inc
        gold, bought = R3.buy_all(nodes, gold, price_of, par, True)
        log.append(dict(round=r, stage=stage, inc=round(inc, 1), n=len(bought), buys=bought, note=note))
    return log


def boss10_report(m, price_of, par):
    out = {"KING_HP": round(KING_HP, 1)}
    for mul in (1.0, 3.0, 10.0):
        out[f"진행 수입x{mul}"] = [(x["round"], x["stage"], x["n"], x["note"]) for x in simulate_boss(m, price_of, par, mul, 16)]
    need = {}
    for atk in range(0, 5):
        for spd in (0, 3):
            for dur in (2, 3, 4):
                for ball in (0, 1):
                    n = {"ps_atk": atk, "ps_spd": spd, "ps_dur": dur, "ps_ball": ball}
                    g = gate(n)
                    need[f"ps_atk{atk} spd{spd} dur{dur} ball{ball}"] = {w: v for w, v in g.items()}
    out["need"] = need
    # 첫 도전 처치(가장 나쁜 날씨 r ≤ 0.7, R2-3 규칙)가 되는 king Hp_Mul — 판 10 도착 때 노드별
    base_nodes = {}
    for x in simulate(m, price_of, par, 1.0, 9):
        for b in x["buys"]:
            k = b.rstrip("0123456789")
            base_nodes[k] = int(b[len(k):])
    def hpmul_for(n):
        worst = min(min(1.0, 1.0) * 0 + (R3.time_new(BOSS_STAGE, n) - mi - 2) * (1 + n.get("ps_atk", 0)) * 1.1 * 0.9 * PS_SPD[n.get("ps_spd", 0)] * sw * (1 + 0.5 * n.get("ps_ball", 0))
                    for sw, mi in WEATHERS.values())
        return round(0.7 * worst / (40 * 5.0 * 1.01 ** 9), 3)
    out["arrive_nodes_R10"] = {k: v for k, v in base_nodes.items() if k in ("ps_atk", "ps_spd", "ps_dur", "ps_ball")}
    out["hpmul_first_try"] = {"기준 도착(판 9까지 산 것)": hpmul_for(base_nodes),
                              "+ps_atk1": hpmul_for({**base_nodes, "ps_atk": 1}),
                              "+ps_atk2": hpmul_for({**base_nodes, "ps_atk": 2}),
                              "ps_atk4 spd3 dur4": hpmul_for({**base_nodes, "ps_atk": 4, "ps_spd": 3, "ps_dur": 4})}
    return out


def run_all():
    fit = FIT.main() if not os.path.exists(os.path.join(HERE, "r3b_fit.json")) else json.load(open(os.path.join(HERE, "r3b_fit.json"), encoding="utf-8"))
    taus = tau7_new(fit)
    tau7 = round(sum(taus) / len(taus), 2)
    par, db_prices, price_of = price_table()
    base = Model(fit, tau7)
    cases = {
        "기준": base,
        "rho 1.15 (재생 빨라진 몫)": Model(fit, tau7, rho=1.15),
        "낱개 없음": Model(fit, tau7, loose={}),
        "낱개 qL x0.7": Model(fit, tau7, qL=fit["qL"] * 0.7),
        "tau 최소": Model(fit, min(taus)),
        "tau 최대": Model(fit, max(taus)),
        "그루터기 x5 (그대로)": Model(fit, tau7, stump_mul=5),
    }
    out = dict(tau7=dict(seeds=taus, mean=tau7), ladder=ladder(), checks=R3.checks(price_of), runs={}, windows={}, mc={})
    for name, m in cases.items():
        out["runs"][name] = simulate(m, price_of, par)
        out["windows"][name] = window(m, price_of, par)
    for mul in (0.8, 1.0, 1.25):
        out["runs"][f"기준 수입 x{mul}"] = simulate(base, price_of, par, mul)
    out["runs"]["희귀 새 노드 없음"] = simulate(base, price_of, par, rare_node=False)
    out["mc"]["기준 sd0.40"] = monte_carlo(base, price_of, par)
    out["mc"]["기준 sd0.19"] = monte_carlo(base, price_of, par, sd=0.19)
    out["mc"]["rho 1.15 sd0.40"] = monte_carlo(cases["rho 1.15 (재생 빨라진 몫)"], price_of, par)
    out["p5"] = p5_checks(base, price_of, par)
    out["auto_steps"] = auto_steps(base)
    out["colony_taste"] = colony_taste(base)
    out["per_round"] = per_round_table(base, price_of, par)
    out["per_round_rho115"] = per_round_table(cases["rho 1.15 (재생 빨라진 몫)"], price_of, par)
    out["window_2to6"] = {"기준": window_2to6(base, price_of, par), "rho 1.15": window_2to6(cases["rho 1.15 (재생 빨라진 몫)"], price_of, par)}
    out["worst_early_p5"] = worst_early_buys(base)
    out["boss10"] = boss10_report(base, price_of, par)
    out["boss10_hpmul"] = {str(hm): {str(mul): [(x["round"], x["stage"], x["n"], x["note"]) for x in simulate_boss(base, price_of, par, mul, 14, hp=40 * 5.0 * hm * 1.01 ** 9)]
                                     for mul in (1.0, 2.0, 3.0)} for hm in (0.63, 0.2, 0.1)}
    out["draft_compare"] = draft_compare(fit, tau7, base, price_of, par)
    out["stump_p5"] = stump_p5_range(fit, tau7, price_of, par)
    out["tier1_sum"] = dict(old=sum(db_prices.values()), new=sum(price_of.values()), levels_old=len(db_prices), levels_new=len(price_of))
    return out


def main():
    out = run_all()
    json.dump(out, open(os.path.join(HERE, "r3b_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump([dict(key=k, lv=L, gold=p) for (k, L, p) in out["ladder"]], open(os.path.join(HERE, "r3b_prices.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    with open(os.path.join(HERE, "r3b_curve.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["case", "round", "T", "maxCol", "income", "loose_share", "n_buys", "buys"])
        for name, log in out["runs"].items():
            for x in log:
                w.writerow([name, x["round"], x["T"], x["c"], x["inc"], x["loose_share"], x["n"], " ".join(x["buys"])])
    print("tau7", out["tau7"], "checks", out["checks"], "tier1", out["tier1_sum"])
    print("ladder", " ".join(f"{k}{L}:{p:g}" for (k, L, p) in out["ladder"]))
    for name, wv in out["windows"].items():
        print("window", name, wv)
    for name, v in out["mc"].items():
        print("mc", name, v["all_rounds_ok"], v["mean_bad_rounds_of6"], [(p["zero"], p["three_plus"]) for p in v["per_round"]])
    print("p5", out["p5"])
    print("auto", json.dumps(out["auto_steps"], ensure_ascii=False))
    print("draft", out["draft_compare"])
    b10 = out["boss10"]
    print("boss10 HP", b10["KING_HP"], "arrive", b10["arrive_nodes_R10"], "hpmul", b10["hpmul_first_try"])
    for k in ("진행 수입x1.0", "진행 수입x3.0", "진행 수입x10.0"):
        print("boss10", k, b10[k])
    for k, v in b10["need"].items():
        print("need", k, v)
    print("window_2to6", out["window_2to6"])
    print("worst_early_p5", out["worst_early_p5"])
    for k, v in out["per_round"].items():
        print("per_round", k, v, " rho1.15", out["per_round_rho115"][k])
    print("stump_p5", json.dumps(out["stump_p5"], ensure_ascii=False))
    print("taste", json.dumps(out["colony_taste"], ensure_ascii=False))
    for name, log in out["runs"].items():
        print("==", name, " ".join(str(x["n"]) for x in log))
        if name in ("기준", "희귀 새 노드 없음"):
            for x in log:
                print(f"  R{x['round']} T{x['T']} c{x['c']} inc {x['inc']:.0f} loose {x['loose_share']:.2f} n{x['n']} {' '.join(x['buys'])} (left {x['left']:.0f})")


if __name__ == "__main__":
    main()
