# -*- coding: utf-8 -*-
"""2회차 제안안: 보스 파밍 배분 → 보스 체력 + 가격 수준.

목표(사용자 결정 14:38): 스테이지 50 첫 처치 ≈ 3시간, 초반 보스(10·20) 첫 도전 처치, 30·40·50은 갈수록 긴 벽.

순환(가격 → 공격력 → 벽 → 수입 → 가격)을 끊으려고 "구매 일정"을 먼저 고정한다.
  1) 구매 일정: 단계 1 레벨은 판 1~20, 단계 2는 21~89, 단계 3은 90~165에 고르게(단계 안 순서 = 지금 가격 순).
  2) 보스 체력 = 목표 처치 판에 일정대로 가진 능력치로 겨우 잡히는 체력 (수입 모델과 무관).
     첫 도전 보스(10·20)는 보스 날씨 3종 중 최악 × 여유 0.9, 벽 보스는 포자 바람(영향 없음) × 1.0.
  3) 계획 스테이지 일정 + 일정 능력치로 판 수입을 근사하고, 레벨 가격 = 그 레벨 몫의 창 동안 버는 골드.
  4) 3)의 가격·2)의 체력으로 진행 모델(boss.progression, 가장 싼 것부터 사기 + 관문 + 재도전 규칙)을 돌려
     실제 수입으로 가격을 다시 매긴다(로그 감쇠 0.5, 반복). 체력은 고정.
python plan.py → theory_plan.json, theory_boss_plan.json, theory_boss_curve_plan.csv
"""
import json, math, os
import boss as B

HERE = os.path.dirname(os.path.abspath(__file__))

# 보스별 목표: (스테이지, 목표 처치 판, 첫 도전 처치인가). 파밍 배분 = 처치 판 − 도착 판: 0 · 0 · 14 · 35 · 67 (합 116판)
# 스테이지 50은 "트리 전부(단계 3 창 끝)의 능력치 × MARGIN_LAST"로 정한다: H·MOVER_H가 낮아도 트리 안에서 반드시 풀리게(갇힘 방지).
#   그래서 처치는 트리 완료보다 조금 앞(공격력 IV 2레벨 무렵)에 오고, 단계 3 창 끝은 처치 판보다 뒤로 둔다.
TARGET = [(10, 10, True), (20, 20, True), (30, 44, False), (40, 89, False), (50, 171, False)]
WINDOWS = {1: (1, 20), 2: (21, 89), 3: (90, 170)}   # 단계별 구매 창(판)
MARGIN_LAST = float(os.environ.get('THEORY_MARGIN_LAST', '0.35'))   # verify#4 ①: H0×0.7 + MOVER_H 0.4 + TAU 0이 겹쳐도 트리 안에서 풀리게
REWARD_ROUNDS = 2.0       # 제안 (c): 처치 보상 = 직전 일반 판 골드 × 2
FAIL_ROUNDS = None if os.environ.get('THEORY_FAIL', 'B') == 'A' else 0.5   # R2-11: B = 진 판 net = 직전 일반 판 × 0.5 (A = 지금 코드, 보스 타격 골드 × 0.8)
LOOK_TIER = None          # R2-11: BOSS_SCORE_TIER는 빼고(−1 = 그림 버섯 등급 그대로) BOSS_SCORE_MUL로만 줄인다
B.SCORE_MUL = 0.05        # R2-11: Const BOSS_SCORE_MUL (진 보스 판 < 직전 일반 판, r211.py)
MARGIN_FIRST, MARGIN_WALL = 0.7, 0.7
MARGIN_BY = {40: float(os.environ.get('THEORY_MARGIN_40', '0.5'))}   # 스테이지 40: H0×0.7·MOVER_H 0.4·TAU 0이 겹쳐도 단계 3 공격력 몇 레벨로 풀리게   # 벽 보스는 목표 판 능력치의 70%로 잡히게(H ±30%에서도 트리 안에서 풀리게: 갇힘 방지)
ITERS, DAMP = 12, 0.5
INF = float("inf")


def level_order():
    out = []
    for t in (1, 2, 3):
        lv = sorted((n["gold"][L], i, L, n["id"]) for i, n in enumerate(B.GOLD_NODES) if n["tier"] == t for L in range(n["max"]))
        a, b = WINDOWS[t]
        for k, (_, i, L, key) in enumerate(lv):
            out.append((a - 1 + (b - a + 1) * (k + 1) / len(lv), key, L))
    return out


XS = level_order()


def lv_at(r):
    """판 r 시작 때 일정상 가진 레벨 (판 r−1 끝까지 산 것)"""
    lv = {}
    for x, key, L in XS:
        if math.ceil(x - 1e-9) <= r - 1:
            lv[key] = max(lv.get(key, 0), L + 1)
    return lv


def planned_stage(r):
    """계획 스테이지 일정: 일반 판은 +1, 보스 앞에서는 목표 처치 판까지 머문다. 반환 (스테이지, 처치 판인가)"""
    stage = 1
    kills = {st: rk for st, rk, _ in TARGET}
    for rr in range(1, r + 1):
        if rr == r:
            return stage, (stage % 10 == 0 and kills.get(stage) == rr)
        if stage % 10 != 0 or kills.get(stage, INF) <= rr:
            stage += 1


def boss_def(stage):
    return B.BOSSES[(stage // B.BOSS_EVERY - 1) % len(B.BOSSES)]


def hp_for(lv, stage, r, first, margin=None):
    b = boss_def(stage)
    best = INF
    for w in (B.BOSS_WEATHERS if first else ["wind"]):
        s = B.stats(lv, stage, r, w)
        _, _, T, dps = B.gate(s, b, 1.0)
        if b["id"] == "cleaner":
            hp = dps * T / (1 + B.CONST["CLEANER_HEAL"] / B.CONST["CLEANER_INT"] * T)
        elif b["id"] == "phantom":
            hp = dps * T / B.GATE["PHANTOM"]
        else:
            hp = dps * T
        best = min(best, hp)
    return best * (margin if margin is not None else MARGIN_FIRST if first else MARGIN_WALL)


def planned_income(rounds=200):
    out, last = [], 0.0
    for r in range(1, rounds + 1):
        st, kill = planned_stage(r)
        inc = B.income(B.stats(lv_at(r), st, r, "clear"))
        if kill:
            inc = last * REWARD_ROUNDS
        else:
            last = inc
        out.append(dict(r=r, stage=st, inc=inc))
    return out


def cum_at(rec, x):
    n, i = len(rec), int(math.floor(x))
    c = sum(e["inc"] for e in rec[:min(i, n)])
    c += (x - i) * (rec[i]["inc"] if i < n else rec[-1]["inc"])
    if i > n:
        c += (i - n) * rec[-1]["inc"]
    return c


def make_prices(rec, old=None):
    pr = {n["id"]: list(n["gold"]) for n in B.GOLD_NODES}
    prev, last = 0.0, 1.0
    for x, key, L in XS:
        p = max(cum_at(rec, x) - cum_at(rec, prev), last)
        if old is not None:
            p = max(math.exp(DAMP * math.log(p) + (1 - DAMP) * math.log(old[key][L])), last)
        pr[key][L] = p
        prev, last = x, p
    return pr


def run(prices, hp, stop=50, rounds=400):
    return B.progression(dict(prices=prices, hp=hp, reward_rounds=REWARD_ROUNDS, look_tier=LOOK_TIER, fail_rounds=FAIL_ROUNDS,
                              max_rounds=rounds, stop_stage=stop))


def tier_scale(prices):
    out = {}
    for t in (1, 2, 3):
        cur = sum(p for n in B.GOLD_NODES if n["tier"] == t for p in n["gold"])
        new = sum(p for n in B.GOLD_NODES if n["tier"] == t for p in prices[n["id"]])
        out[t] = dict(now=float("%.3g" % cur), plan=float("%.3g" % new), ratio=round(new / cur, 3))
    return out


def shape(prices):
    seq = sorted(p for n in B.GOLD_NODES for p in prices[n["id"]])
    jump = max(b / a for a, b in zip(seq, seq[1:]))
    inner = max(prices[n["id"]][L + 1] / prices[n["id"]][L] for n in B.GOLD_NODES for L in range(n["max"] - 1))
    return dict(max_jump=round(jump, 2), max_in_node=round(inner, 2))


def solve():
    hp = {st: hp_for(lv_at(rk - 3 if first else rk), st, rk, first, MARGIN_LAST if st == TARGET[-1][0] else MARGIN_BY.get(st))
          for st, rk, first in TARGET}   # 첫 도전 보스는 일정보다 3판 늦은 능력치로(초반 수입 흔들림 여유)
    plan_inc = planned_income()
    prices = make_prices(plan_inc)
    hist = []
    for it in range(ITERS):
        res = run(prices, {**hp, 60: INF}, stop=60, rounds=260)
        prices = make_prices(res["rec"], prices)
        hist.append(dict(it=it, kills={k: e.get("kill_r") for k, e in res["log"].items()}, done=dict(res["done"])))
        if it >= ITERS // 2:
            # 첫 도전 보스: 실제 진행(가장 싼 것부터)의 도착 판 능력치로도 최악 날씨에서 잡히게 낮춘다
            chk = B.progression(dict(prices=prices, hp=hp, reward_rounds=REWARD_ROUNDS, look_tier=LOOK_TIER, fail_rounds=FAIL_ROUNDS, snap=True,
                                     max_rounds=60, stop_stage=30))
            for st, rk, first in TARGET:
                if first and st in chk["log"]:
                    ra = chk["log"][st]["first_r"]
                    hp[st] = min(hp[st], hp_for(chk["snaps"][ra - 1], st, ra, True))
    return hp, plan_inc, prices, hist


def main():
    hp, plan_inc, prices, hist = solve()
    res = run(prices, hp, stop=50, rounds=400)
    full = run(prices, {**hp, 60: INF}, stop=60, rounds=260)
    atk_plan = {st: round(B.stats(lv_at(rk), st, rk, "wind")["atk"], 1) for st, rk, _ in TARGET}
    zone = {st: B.zone_mul(st) for st, _, _ in TARGET}
    out = dict(target=TARGET, windows=WINDOWS, reward_rounds=REWARD_ROUNDS, look_tier=LOOK_TIER,
               hp={str(k): round(v, 1) for k, v in hp.items()},
               hp_over_40zone={str(k): round(v / (40 * zone[k]), 4) for k, v in hp.items()},
               atk_plan=atk_plan, bosses=B.summarize(res), rounds=res["rounds"], minutes=round(res["minutes"], 1),
               tier_done=full["done"], tier_scale=tier_scale(prices), shape=shape(prices), hist=hist[-4:],
               plan_income={e["r"]: float("%.3g" % e["inc"]) for e in plan_inc if e["r"] in (1, 10, 20, 30, 44, 54, 89, 98, 120, 165)},
               prices={k: [float("%.3g" % x) for x in v] for k, v in prices.items()})
    json.dump(out, open(os.path.join(HERE, "theory_plan.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    B.write("plan", dict(prices=prices, hp=hp, reward_rounds=REWARD_ROUNDS, look_tier=LOOK_TIER, fail_rounds=FAIL_ROUNDS), res)
    print(f"끝 R{res['rounds']} · {res['minutes']:.0f}분 · 단계 완료 판 {full['done']}")
    print("HP", out["hp"], "\nHP/(40·ZoneMul)", out["hp_over_40zone"], "\n계획 공격력", atk_plan)
    for row in out["bosses"]:
        print("  ", row)
    print("가격", out["tier_scale"], out["shape"])
    print("계획 수입", out["plan_income"])
    print("수렴", hist[-3:])
    return out


if __name__ == "__main__":
    main()
