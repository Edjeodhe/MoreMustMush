# -*- coding: utf-8 -*-
"""R2-11 표: 스테이지 10~50 보스별 일반 판 / 진 판 / 처치 판 net, P14 지표(r1·W·처치 판).
plan.py 제안안(가격·체력)으로 진행을 돌린다. 보스 판에는 일반 버섯이 없다(RoundSim.cs:96·102, Effects.cs:106 재생도 없음).
진 판: 안 A = 지금 코드(보스 타격 골드 × BOSS_SCORE_MUL × BOSS_FAIL_GOLD), 안 B = 직전 일반 판 × BOSS_FAIL_GOLD(0.5).
처치 판: 보스 타격 골드 + max(보스 타격 골드 × BOSS_REWARD_MUL, 직전 일반 판(파밍 판 포함) × BOSS_REWARD_ROUNDS).
python r211.py → theory_r211.json"""
import json, os
import boss as B, plan as P

HERE = os.path.dirname(os.path.abspath(__file__))
COMBO_UP = 2.0   # 사람 콤보·피버 상한(verify 2회차 A: 모델 콤보 1+5·comboK는 하한, 최대 ×2~3)


def main():
    hp, _, prices, _ = P.solve()
    res = B.progression(dict(prices=prices, hp=hp, reward_rounds=P.REWARD_ROUNDS, look_tier=P.LOOK_TIER,
                             fail_rounds=P.FAIL_ROUNDS, snap=True, max_rounds=400, stop_stage=50))
    rec, snaps, tab = res["rec"], res["snaps"], {}
    for i, x in enumerate(rec):
        if x["kind"] not in ("fail", "kill"):
            continue
        st = x["stage"] - (1 if x["kind"] == "kill" else 0)
        b = P.boss_def(st)
        last = next(y["inc"] for y in reversed(rec[:i]) if y["kind"] in ("run", "farm"))
        s = B.stats(snaps[i], st, x["r"], "wind")
        _, t, T, dps = B.gate(s, b, hp[st])
        A = s["atk"] * s["heavy"] * s["bossMul"] * (1 + s["crit"] * (s["critMul"] - 1))
        g_full = B.boss_income(s, b, T * dps / A, True, P.LOOK_TIER) / (1 + B.CONST["BOSS_REWARD_MUL"])   # 판 내내 때린 보스 타격 골드(배율 적용)
        g_kill = g_full * min(1.0, t / T)
        e = tab.setdefault(st, dict(boss=b["id"], first_r=x["r"], tries=0))
        e["tries"] += 1
        if "normal" not in e:
            e["normal"] = last
            e["fail_A"] = g_full * B.CONST["BOSS_FAIL_GOLD"]
            e["fail_A_up"] = e["fail_A"] * COMBO_UP
            e["fail_B"] = last * 0.5
            rr = {}
            for k, h in (("x0.7", 0.7), ("x1.0", 1.0), ("x1.3", 1.3)):
                rr[k] = round(t / h / T, 2)
            e["r1"] = rr
        if x["kind"] == "kill":
            e["kill_r"] = x["r"]; e["normal_at_clear"] = last
            e["clear"] = g_kill + max(g_kill * B.CONST["BOSS_REWARD_MUL"], last * P.REWARD_ROUNDS)
            e["W"] = x["r"] - e["first_r"]
    for e in tab.values():
        e["fail_A/N"] = e["fail_A_up"] / e["normal"]
        e["clear/N"] = e["clear"] / e["normal_at_clear"]
    out = {str(k): {kk: (float("%.3g" % v) if isinstance(v, float) else v) for kk, v in e.items()} for k, e in sorted(tab.items())}
    json.dump(dict(BOSS_SCORE_MUL=B.SCORE_MUL, table=out, minutes=round(res["minutes"], 1), rounds=res["rounds"]),
              open(os.path.join(HERE, "theory_r211.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    for k, e in out.items():
        print(k, e)


if __name__ == "__main__":
    main()
