# -*- coding: utf-8 -*-
"""세계수 수확 보따리 골드를 판 수입에 연동 (Z2, theory 2026-10-19).
식: 보따리 골드/초 = (lastNormalGold / (lastNormalSec + BAG_ROUND_GAP)) * BAG_RATIO[단계] * EffTerm(raw / BAG_REF[단계])
    raw = 지금 BagRate 골드/초 (WT.bagMul 뺀 값), BAG_REF = 대표 배치(B-0) 골드/초.
    EffTerm(x) = x (x<=1), min(EFF_MAX, sqrt(x)) (x>1), raw<=0 이면 0.
실행: python BalanceData/theory/bag_z2.py -> bag_z2_out.json, bag_z2_out.txt (난수 없음)
"""
import json
import math
import os
import statistics

import bag_model as bm

HERE = os.path.dirname(os.path.abspath(__file__))

BAG_RATIO = [0.004, 0.0054, 0.0106, 0.0134, 0.018, 0.025, 0.037, 0.05]
EFF_MAX = 4.0
BAG_ROUND_GAP = 30.0
IDLE_CAP = 0.25            # 방치 시간당 / 플레이 시간당 상한 (강화 최대에서도)
BAG_SEC = 8 * 3600
# 장식·gm_herb 모두 최대일 때 raw에 곱해지는 근사 배율 (꼬마 배율 밖):
# gm_herb 1.6 x bagEff(1+0.10*2.2) x treeGold(1+0.08*2.2) x lucky(1+0.05*2.2) x 꼬마 add (1.15+0.22+0.30+0.11)/1.15
DECO_NODE_MAX = 1.6 * 1.22 * 1.176 * 1.11 * ((1.15 + 0.22 + 0.30 + 0.11) / 1.15)
JOIN_STAGE = {k: i + 1 for i, k in enumerate(bm.JOIN)}


def eff_term(x):
    if x <= 0:
        return 0.0
    return x if x <= 1 else min(EFF_MAX, math.sqrt(x))


def ref_gold(s):
    """기준 배치: 진화 직전 배치를 새 단계로 옮긴 것 = 꼬마 max(1, s-1)명 Lv10·각성 0·나이테 10 (B-0 규칙으로 배치)."""
    return bm.bag_rate(s, nkids=max(1, s - 1))[0]


def raw_gold(s, nkids, ring, lv_of, aw_of, aff=1):
    """bag_model.bag_rate를 꼬마별 레벨·각성으로 (호감도 Lv aff)."""
    kids = bm.JOIN[:nkids]
    lv = {k: lv_of(k) for k in kids}
    aw = {k: aw_of(k) for k in kids}
    where = bm.place(s, kids, sum(lv.values()))
    assigned = [k for z in where for k in where[z]]
    slots = bm.zone_slots(s, bm.MUSH_SLOTS[s - 1])
    old = bm.AFF_GREAT
    bm.AFF_GREAT = 0.01 * aff
    try:
        g = sum(bm.zone_rate(s, z, slots.get(z, 0), ks, ring, lv, aw, assigned)[0] for z, ks in where.items())
    finally:
        bm.AFF_GREAT = old
    return g


def max_aw(s, k):
    return max(0, min(3, s - JOIN_STAGE[k]))


def scenarios(s):
    """단계 s의 raw/ref 배율 (WT.bagMul 없음)."""
    ref = ref_gold(s)
    out = {
        "해금": raw_gold(s, max(1, s - 1) if s > 1 else 1, 1, lambda k: 1 if s == 1 else 10, lambda k: 0) / ref,
        "진화직후": raw_gold(s, max(1, s - 1), 1, lambda k: 10, lambda k: 0) / ref,
        "대표": bm.bag_rate(s)[0] / ref,
        "꼬마최대": raw_gold(s, s, 10, lambda k: min(30, 10 * (max_aw(s, k) + 1)), lambda k: max_aw(s, k), aff=10) / ref,
    }
    out["전부최대"] = out["꼬마최대"] * DECO_NODE_MAX
    return out


def round_sec_by_tree():
    """round2_fit4 일반 판 간격 중앙(초): 세계수 단계 시점 스테이지 구간 (bag_out.txt 세션 진행)."""
    runs = bm.load_runs()
    spans = {1: (5, 11), 2: (8, 12), 3: (11, 40), 4: (40, 70), 5: (70, 133), 6: (105, 133), 7: (105, 133), 8: (105, 133)}
    out = {}
    for s, (lo, hi) in spans.items():
        d = []
        for r in runs:
            st, pt, b = r["stageR"], r["playR"], r["bossR"]
            d += [pt[i] - pt[i - 1] for i in range(1, len(st)) if lo <= st[i] < hi and b[i] == 0]
        out[s] = statistics.median(d)
    return out


def main():
    rsec = round_sec_by_tree()
    lines, res = [], {"BAG_REF_GOLD_H": [round(ref_gold(s) * 3600) for s in range(1, 9)], "BAG_RATIO": BAG_RATIO, "EFF_MAX": EFF_MAX, "DECO_NODE_MAX": DECO_NODE_MAX, "rows": []}
    lines.append(f"BAG_REF_GOLD_H={res['BAG_REF_GOLD_H']}")
    lines.append(f"BAG_RATIO={BAG_RATIO} EFF_MAX={EFF_MAX} DECO_NODE_MAX={DECO_NODE_MAX:.2f}")
    lines.append("단계 | 판 간격(초) | 8h=판(비율1) | raw/ref 해금·진화직후·대표·꼬마최대·전부최대 | EffTerm 같은 순서 | 8h=판 같은 순서 | 방치/플레이(전부최대) | 플레이 중 포함 비")
    prev_top = None
    for s in range(1, 9):
        sc = scenarios(s)
        rho = BAG_RATIO[s - 1]
        n8 = BAG_SEC / rsec[s]
        e = {k: eff_term(v) for k, v in sc.items()}
        rounds = {k: rho * n8 * v for k, v in e.items()}
        idle = rho * e["전부최대"]
        row = dict(stage=s, round_sec=rsec[s], ratio=rho, rounds8_base=rho * n8, raw=sc, eff=e, rounds8=rounds,
                   idle_over_play_max=idle, idle_over_play_incl=idle / (1 + idle),
                   step_from_prev=(rho / BAG_RATIO[s - 2]) if s > 1 else None,
                   evolve_jump=(rho * e["진화직후"]) / prev_top if prev_top else None)
        prev_top = rho * e["대표"]   # 앞 단계 대표 배치(꼬마 s명·나이테 10)에서 진화
        res["rows"].append(row)
        lines.append(f"{s} | {rsec[s]:.0f} | {rho*n8:.2f} | " + " · ".join(f"{sc[k]:.2f}" for k in ("해금", "진화직후", "꼬마최대", "전부최대"))
                     + " | " + " · ".join(f"{e[k]:.2f}" for k in ("해금", "진화직후", "대표", "꼬마최대", "전부최대"))
                     + " | " + " · ".join(f"{rounds[k]:.1f}" for k in ("해금", "진화직후", "대표", "꼬마최대", "전부최대"))
                     + f" | 대표 {rho*e['대표']*100:.2f}% · 최대 {idle*100:.1f}% | {idle/(1+idle)*100:.1f}% | 진화 전후 x{row['evolve_jump'] or 0:.2f}")
    ok = all(r["idle_over_play_max"] <= IDLE_CAP for r in res["rows"])
    lines.append(f"상한 {IDLE_CAP*100:.0f}% 지킴: {ok}")
    json.dump(res, open(os.path.join(HERE, "bag_z2_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    open(os.path.join(HERE, "bag_z2_out.txt"), "w", encoding="utf-8").write("\n".join(lines))
    print("\n".join(lines))


if __name__ == "__main__":
    main()
