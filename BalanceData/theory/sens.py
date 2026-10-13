# -*- coding: utf-8 -*-
"""plan.py 제안안(가격·체력 고정)의 민감도: H0 ±30%, MOVER_H, TAU, 판 수입 ×0.5·×2, 균사석으로 gm_boss·gm_atk 구매, 재도전 간격.
python sens.py → theory_sens.json"""
import json, os
import boss as B, plan as P

HERE = os.path.dirname(os.path.abspath(__file__))


def one(prices, hp, gate=None, **kw):
    old = dict(B.GATE)
    if gate:
        B.GATE.update(gate)
    try:
        cfg = dict(prices=prices, hp=hp, reward_rounds=P.REWARD_ROUNDS, look_tier=P.LOOK_TIER, fail_rounds=P.FAIL_ROUNDS, max_rounds=400, stop_stage=50)
        cfg.update(kw)
        res = B.progression(cfg)
    finally:
        B.GATE.clear(); B.GATE.update(old)
    s = B.summarize(res)
    return dict(min=round(res["minutes"]), rounds=res["rounds"], stage=res["stage"],
                tries=[e["tries"] for e in s], kill_r=[e["kill_r"] for e in s], first_try=[e["tries"] == 1 for e in s][:2])


def main():
    hp, _, prices, _ = P.solve()
    cases = {
        "기준": {}, "H0×0.7": dict(gate=dict(H0=0.63)), "H0×1.3": dict(gate=dict(H0=1.17)),
        "MOVER_H 0.4": dict(gate=dict(MOVER_H=0.4)), "MOVER_H 0.8": dict(gate=dict(MOVER_H=0.8)),
        "TAU 0": dict(gate=dict(TAU=0.0)), "H0×0.7+MOVER_H 0.4": dict(gate=dict(H0=0.63, MOVER_H=0.4)),
        "H0×0.7+MOVER_H 0.4+TAU 0": dict(gate=dict(H0=0.63, MOVER_H=0.4, TAU=0.0)), "TAU 0.4": dict(gate=dict(TAU=0.4)),
        "수입×0.5": dict(inc_mul=0.5), "수입×2": dict(inc_mul=2.0),
        "균사석→gm_boss·gm_atk": dict(gems=True), "재도전 3판": dict(retry=3), "재도전 8판": dict(retry=8),
    }
    out = {k: one(prices, hp, **v) for k, v in cases.items()}
    json.dump(out, open(os.path.join(HERE, "theory_sens.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    for k, v in out.items():
        print(k, v)


if __name__ == "__main__":
    main()
