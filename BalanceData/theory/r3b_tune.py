# -*- coding: utf-8 -*-
"""R3-12 판 1~7 가격 다듬기 (기록용 탐색 — 결과는 r3b_model.EARLY에 손으로 옮겨 적었다).

목표: 판 1~6 구매 1~2개가 수입 배율 ×0.8·×1.0·×1.25 모두에서, 판마다 ln 잡음(sd 0.30)이 있어도 잘 성립.
  목적 = 세 배율에서 몬테카를로(300회, 공통 난수) 판 1~6 중 0개·3개 이상인 판 수 평균의 합
  제약 = 사다리 바로 앞 대비 ≤ ×1.42(지금 값 md_kid3→ed_bonus1p ×1.41 포함), 노드 안 다음 레벨 ×1.15~×3.9, 레벨 순서
변수 = 판 1~8에 사는 앞쪽 20레벨 가격 (나머지는 r3b_model.LADDER)
실행: python BalanceData/theory/r3b_tune.py → r3b_tune.json (난수 시드 고정, 같은 결과)
"""
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r3_model as R3  # noqa: E402
import r3b_model as M  # noqa: E402

VARS = [("power", 1), ("ed_score", 1), ("ed_cols", 1), ("ps_dur", 1), ("ed_price", 1), ("power", 2), ("md_bar", 1),
        ("ed_score", 2), ("ed_cols", 2), ("dev_stump", 1), ("ps_dur", 2), ("ed_price", 2), ("md_combo", 1), ("md_bar", 2),
        ("power", 3), ("ed_regen", 1), ("ed_score", 3), ("ed_cols", 3), ("ed_rare0", 1), ("ps_dur", 3)]
MULS = (0.8, 1.0, 1.25)
SD = 0.30
RUNS = 300


def build(early):
    par, db_prices, price_of = M.price_table()
    po = dict(price_of)
    po.update(early)
    return par, po


def feasible(po):
    seq = sorted(((k, L, p) for (k, L), p in po.items() if p <= 20000), key=lambda t: (t[2], t[0], t[1]))
    for i in range(1, len(seq)):
        if seq[i][2] / seq[i - 1][2] > 1.42 + 1e-9:
            return False
    by = {}
    for (k, L), p in po.items():
        by.setdefault(k, {})[L] = p
    for k, lv in by.items():
        for L in sorted(lv):
            if L - 1 in lv:
                r = lv[L] / lv[L - 1]
                if r > 3.9 or r < 1.15:
                    return False
    return True


def objective(m, par, po, noises):
    tot = 0.0
    for mul in MULS:
        bad = 0
        for nz in noises:
            log = M.simulate(m, po, par, mul, 6, noise=nz)
            bad += sum(1 for x in log if not 1 <= x["n"] <= 2)
        tot += bad / len(noises)
    return tot


def main():
    fit = json.load(open(os.path.join(HERE, "r3b_fit.json"), encoding="utf-8"))
    taus = M.tau7_new(fit)
    m = M.Model(fit, round(sum(taus) / len(taus), 2))
    _, _, start = M.price_table()
    early = {kv: start[kv] for kv in VARS}
    rng = random.Random(11)
    noises = [[math.exp(rng.gauss(0, SD)) for _ in range(6)] for _ in range(RUNS)]
    par, po = build(early)
    assert feasible(po), "시작 가격이 제약을 어김"
    best = objective(m, par, po, noises)
    hist = [round(best, 3)]
    for _ in range(6):
        improved = False
        for kv in VARS:
            for f in (0.8, 0.9, 1.1, 1.25):
                trial = dict(early)
                trial[kv] = R3.nice(early[kv] * f)
                par, po = build(trial)
                if not feasible(po):
                    continue
                v = objective(m, par, po, noises)
                if v < best - 1e-9:
                    best, early, improved = v, trial, True
        hist.append(round(best, 3))
        if not improved:
            break
    out = dict(objective=best, history=hist, early={f"{k}{L}": p for (k, L), p in sorted(early.items(), key=lambda t: t[1])})
    json.dump(out, open(os.path.join(HERE, "r3b_tune.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(out, ensure_ascii=False))


if __name__ == "__main__":
    main()
