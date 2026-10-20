# G3: 싱크 구간 판 수입 성장 (inf_gold 효과 g, 가격 비 k=1.076, 시작가 P0). 판 수입을 모두 싱크(inf_gold)에 쓴다고 본 상한 근사.
# I0 = 싱크 열릴 때 판 수입(2회차 기록 1.0~1.25e12), P0 = 3.63e12 × COST_MUL
import json, math, os
def run(g, k=1.076, P0=3.63e12, I0=1.1e12, rounds=60, share=1.0):
    L, gold, I, out = 0, 0.0, I0, []
    for r in range(1, rounds + 1):
        gold += I
        while gold * share >= P0 * k ** L:
            gold -= P0 * k ** L; L += 1
        I = I0 * g ** L
        out.append((r, L, I))
    return out
res = {}
for name, g, cm in (("지금 1.025", 1.025, 1.0), ("제안 1.04", 1.04, 0.9), ("제안 1.04 (P-1 없이)", 1.04, 1.0), ("대안 1.05", 1.05, 0.9)):
    o = run(g, P0=3.63e12 * cm)
    res[name] = {"L@20": o[19][1], "I@20/I0": o[19][2] / 1.1e12, "L@40": o[39][1], "I@40/I0": o[39][2] / 1.1e12, "L@60": o[59][1], "I@60/I0": o[59][2] / 1.1e12,
                 "growth_per_round_20_40": (o[39][2] / o[19][2]) ** (1 / 20) - 1}
for name, a in (("inf_atk 지금 1.05", 1.05), ("inf_atk 제안 1.07", 1.07)):
    o = run(a, P0=3.63e12)
    res[name] = {"atk@40/atk0": a ** o[39][1], "L@40": o[39][1]}
json.dump(res, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "g3_sink_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
for k, v in res.items(): print(k, {a: (round(b, 4) if isinstance(b, float) else b) for a, b in v.items()})
