# -*- coding: utf-8 -*-
"""세계수 수확 보따리 단계별 수입 모델 (theory, 2026-10-19).
Game.Farm.cs ZoneRate/BagRate/KidMul/KidPickRate 를 그대로 옮겼다 (장식 없음·균사석 노드 0레벨·호감도 Lv1).
실행: python BalanceData/theory/bag_model.py  -> bag_out.json, bag_out.txt (난수 없음, 다시 돌려도 같다)
"""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

# ---- Defs.Farm.cs ----
EXP = [0, 300, 1000, 3000, 8000, 20000, 50000, 120000]
EVO_GOLD = [0, 2000, 6000, 15000, 40000, 100000, 250000, 600000]
EVO_GEM = [0, 0, 10, 30, 80, 200, 500, 1200]
MUSH_SLOTS = [4, 6, 8, 10, 13, 16, 20, 24]
RING_K, RING_DIV, RING_GROW, LAST_RING_MUL = 1.35, 20, 0.03, 2.4
TMUSH = {"shelf": (60, 50, 0, 1), "bundle": (300, 200, 0, 2), "glow": (1800, 300, 1, 4), "golden": (10800, 1000, 5, 6)}  # time, gold, gem, from
GOLDEN_IN_CROWN = 0.05
ZONES = [("root", 1, 4, ["shelf"], False), ("trunk", 2, 3, ["shelf", "bundle"], False),
         ("branch", 4, 3, ["bundle", "glow"], True), ("crown", 6, 2, ["glow"], True)]
ZONE = {z[0]: z for z in ZONES}
HARV_SEC, PREFER, PER_LV = 4.0, 0.5, 0.08
BAG_HOURS = 8
KIDS = {  # zone, trait, tv, tv2
    "leaf": ("root", "speed", 0.10, 0), "dew": ("trunk", "regen", 0.20, 0), "wind": ("crown", "wind", 2, 0.5),
    "fire": ("trunk", "gem", 0.20, 0), "rock": ("root", "carry", 2, 4), "spark": ("branch", "chain", 2, 0),
    "moon": ("branch", "glow", 2, 0.30), "star": ("crown", "aura", 0.15, 0)}
JOIN = ["leaf", "dew", "wind", "fire", "rock", "spark", "moon", "star"]
UNLOCK_GEM = [0, 15, 30, 60, 120, 250, 500, 1000]
AWAKEN_MUL = [1, 1.3, 1.7, 2.2]
KLV_SUM_AT = [50, 100, 150, 200, 240]
AFF_GREAT = 0.01

# ---- 추천안 (이론.md "세계수 보따리 단계 강화") ----
BAG_MUL = [1, 1, 1.2, 1.4, 1.6, 2.2, 2.6, 3]


def zone_cap(stage, klv_sum):
    return (3 if stage >= 6 else 2) + (1 if klv_sum >= KLV_SUM_AT[1] else 0) + (1 if klv_sum >= KLV_SUM_AT[3] else 0)


def zone_slots(stage, total):
    op = [z for z in ZONES if stage >= z[1]]
    wsum = sum(z[2] for z in op)
    o = {z[0]: total * z[2] // wsum for z in op}
    left = total - sum(o.values())
    for z in sorted(op, key=lambda z: (-(total * z[2] % wsum), ZONES.index(z))):
        if left <= 0:
            break
        o[z[0]] += 1; left -= 1
    return o


def slot_mush(stage, zone, i):
    av = [m for m in ZONE[zone][3] if stage >= TMUSH[m][3]] or ["shelf"]
    return av[i % len(av)]


def place(stage, kids, klv_sum):
    """선호 구역 먼저, 넘치면 열린 구역 중 값이 큰 곳(수관>가지>줄기>뿌리)부터."""
    cap = zone_cap(stage, klv_sum)
    where = {z[0]: [] for z in ZONES if stage >= z[1]}
    rest = []
    for k in kids:
        z = KIDS[k][0]
        if z in where and len(where[z]) < cap:
            where[z].append(k)
        else:
            rest.append(k)
    for k in rest:
        for z in ["crown", "branch", "trunk", "root"]:
            if z in where and len(where[z]) < cap:
                where[z].append(k); break
    return where


def pick_rate(k):
    _, tr, tv, _ = KIDS[k]
    speed = (1 + (tv if tr == "speed" else 0)) * (tv if tr == "wind" else 1)
    carry = tv if tr == "carry" else 1
    chain = 1 + tv if tr == "chain" else 1
    return speed * carry * chain / HARV_SEC


def kid_mul(k, zone, lv, aw, assigned):
    z0, tr, tv, tv2 = KIDS[k]
    add = (KIDS["star"][2] if "star" in assigned else 0) + (tv2 if tr == "wind" and ZONE[zone][4] else 0)
    return (1 + PER_LV * (lv - 1)) * AWAKEN_MUL[aw] * (1 + (PREFER if z0 == zone else 0)) * (1 + add) * (1 + AFF_GREAT * 1)


def zone_rate(stage, zone, slots, kids, ring, lv, aw, assigned):
    if not kids or slots <= 0:
        return 0.0, 0.0, 0.0
    rates = {k: pick_rate(k) for k in kids}
    cap = sum(rates.values())
    grow = 1 + RING_GROW * (ring - 1) + (KIDS["dew"][2] if "dew" in kids else 0)
    pick = HARV_SEC * len(kids) / cap
    supply = gold = gem = 0.0
    for i in range(slots):
        m = slot_mush(stage, zone, i)
        parts = [(m, 1.0)]
        if zone == "crown" and stage >= TMUSH["golden"][3]:
            parts = [(m, 1 - GOLDEN_IN_CROWN), ("golden", GOLDEN_IN_CROWN)]
        cycle = sum(p * (TMUSH[mm][0] / grow + pick) for mm, p in parts)
        supply += 1 / cycle
        for mm, p in parts:
            for k in kids:
                w = p / cycle * rates[k] / cap * kid_mul(k, zone, lv[k], aw[k], assigned)
                tr, tv, tv2 = KIDS[k][1], KIDS[k][2], KIDS[k][3]
                typ = tv if (mm == "glow" and tr == "glow") else 1
                gold += w * typ * TMUSH[mm][1]
                gem += w * typ * TMUSH[mm][2] * (1 + (tv if tr == "gem" else 0) + (tv2 if tr == "glow" else 0))
    eff = min(1.0, cap / supply)
    return gold * eff, gem * eff, supply * eff


def bag_rate(stage, nkids=None, ring=10, klv=10, awaken=0):
    """초당 (골드, 균사석, 개수, 배치, 슬롯). nkids = 가진 꼬마 수 (기본 = 단계 수)."""
    n = stage if nkids is None else nkids
    kids = JOIN[:n]
    lv = {k: klv for k in kids}; aw = {k: awaken for k in kids}
    where = place(stage, kids, sum(lv.values()))
    assigned = [k for z in where for k in where[z]]
    slots = zone_slots(stage, MUSH_SLOTS[stage - 1])
    g = e = c = 0.0
    for z, ks in where.items():
        a, b, n_ = zone_rate(stage, z, slots.get(z, 0), ks, ring, lv, aw, assigned)
        g += a; e += b; c += n_
    return g, e, c, where, slots


def ring_sum(stage):
    """단계 s에서 나이테 1→10 골드 합."""
    base = EVO_GOLD[stage] if stage < 8 else EVO_GOLD[7] * LAST_RING_MUL
    return sum(base / RING_DIV * RING_K ** (L - 1) for L in range(1, 10))


# ---- 라운드 수입 (2회차 측정 round2_fit4, 관문 모드, 시드 1~3) ----
def load_runs():
    runs = []
    for s in (1, 2, 3):
        p = os.path.join(ROOT, "BalanceData", "round2_fit4", f"balance_phase_s{s}.json")
        runs.append(json.load(open(p, encoding="utf-8")))
    return runs


def boss_gem(stage):
    return 5 + 5 * (stage // 10)   # db.json BOSS_GEM_BASE 5, STEP 5, BOSS_EVERY 10


TIER_VALUE = [1, 3, 10, 40]


def round_income_by_zone(runs):
    """지역별 시간당 라운드 수입 (판 수입 / 판 간격, 보스 판 제외 일반 판의 중앙값)."""
    out = {}
    for name, lo, hi in (("숲", 1, 19), ("밤", 20, 44), ("들판", 45, 74), ("바다", 75, 104)):
        per_r, per_h = [], []
        for run in runs:
            st, b, pt, inc = run["stageR"], run["bossR"], run["playR"], run["incR"]
            for i in range(1, len(st)):
                if lo <= st[i] <= hi and b[i] == 0:
                    dt = pt[i] - pt[i - 1]
                    per_r.append(inc[i]); per_h.append(inc[i] / dt * 3600)
        per_r.sort(); per_h.sort()
        if per_r:
            out[name] = dict(n=len(per_r), round_med=per_r[len(per_r) // 2], round_lo=per_r[0], round_hi=per_r[-1],
                             hour_med=per_h[len(per_h) // 2], hour_lo=per_h[0], hour_hi=per_h[-1])
    return out


def session(run, open_stage_at=5, bag_mul=None):
    """세션(측정 기록 224분)에서 세계수 단계 진행: 골드·경험치·균사석이 되면 바로 진화, 그다음 꼬마 해금(균사석).
    경험치 = 그 판 수확 수(스테이지별 killT) × 등급 가치 (선호 ×2 무시). 보따리는 플레이하는 동안에도 쌓인다."""
    st, b, pt, inc = run["stageR"], run["bossR"], run["playR"], run["incR"]
    kill = run["killT"]
    tree, nk, gold, exp, gem = 1, 1, 0.0, 0.0, 0.0
    t_prev = None; out = []; bag_gold = 0.0
    for i in range(len(st)):
        s = st[i]
        if s < open_stage_at:
            t_prev = pt[i]; continue
        dt = pt[i] - (t_prev if t_prev is not None else pt[i] - 45); t_prev = pt[i]
        gold += inc[i]
        if b[i] in (0, 2) and s - 1 < len(kill):
            exp += sum(kill[s - 1][t] * TIER_VALUE[t] for t in range(4))
        if b[i] == 1:
            gem += boss_gem(s)
        g, e, _, _, _ = bag_rate(tree, nk)
        m = bag_mul[tree - 1] if bag_mul else 1
        gold += g * m * dt; gem += e * m * dt; bag_gold += g * m * dt
        changed = True
        while changed:
            changed = False
            if tree < 8:
                need_g = ring_sum(tree) + EVO_GOLD[tree]
                if gold >= need_g and exp >= EXP[tree] and gem >= EVO_GEM[tree]:
                    gold -= need_g; exp -= EXP[tree]; gem -= EVO_GEM[tree]; tree += 1; changed = True
                    out.append(dict(tree=tree, round=i + 1, stage=s, minute=round(pt[i] / 60, 1)))
            if nk < tree and gem >= UNLOCK_GEM[nk]:
                gem -= UNLOCK_GEM[nk]; nk += 1; changed = True
    return dict(tree=tree, kids=nk, gem_left=round(gem, 1), evo=out, bag_gold=bag_gold)


def days_to_next(stage, mul=1.0, hours_per_day=16, boss_gem_day=0):
    """세션 뒤 방치만으로 다음 진화 균사석(+그 단계 꼬마 해금)을 모으는 날 수. 하루 hours_per_day시간 쌓인다고 가정(8시간 보관 2~3번 받기)."""
    if stage >= 8:
        return None
    need = EVO_GEM[stage] + UNLOCK_GEM[stage]   # 다음 단계 진화 + 다음 단계에서 열리는 꼬마
    e = bag_rate(stage)[1] * 3600 * hours_per_day * mul + boss_gem_day
    return need / e if e > 0 else float("inf")


def main():
    lines = []; res = {"rates": [], "session": {}, "income": None, "days": {}}
    lines.append("단계 | 구역 배치 | 슬롯 | 골드/시간 | 균사석/시간 | 개수/시간 | 8시간 보따리 골드 | 그 단계 나이테 합 | 다음 진화 골드 | 다음 진화 균사석")
    for s in range(1, 9):
        g, e, c, where, slots = bag_rate(s)
        row = dict(stage=s, gold_h=g * 3600, gem_h=e * 3600, n_h=c * 3600, bag8_gold=g * 3600 * BAG_HOURS, bag8_gem=e * 3600 * BAG_HOURS,
                   ring_gold=ring_sum(s), evo_gold=EVO_GOLD[s] if s < 8 else 0, evo_gem=EVO_GEM[s] if s < 8 else 0,
                   mul=BAG_MUL[s - 1], gold_h_new=g * 3600 * BAG_MUL[s - 1], gem_h_new=e * 3600 * BAG_MUL[s - 1],
                   where=where, slots=slots)
        res["rates"].append(row)
        lines.append(f"{s} | {where} | {slots} | {g*3600:,.0f} | {e*3600:.2f} | {c*3600:,.0f} | {g*3600*8:,.0f} | {ring_sum(s):,.0f} | {row['evo_gold']:,} | {row['evo_gem']}")
    lines.append("")
    lines.append("추천안 BAG_MUL = " + str(BAG_MUL))
    for r in res["rates"]:
        lines.append(f"  {r['stage']}: ×{r['mul']} -> 골드/시간 {r['gold_h_new']:,.0f}, 균사석/시간 {r['gem_h_new']:.2f}, 8시간 {r['gold_h_new']*8:,.0f}g/{r['gem_h_new']*8:.1f}")
    for aw, lv in ((1, 20), (2, 30)):
        lines.append(f"민감도 각성 ★{aw} Lv{lv}: " + ", ".join(f"{s}:{bag_rate(s, klv=lv, awaken=aw)[0]*3600:,.0f}g/{bag_rate(s, klv=lv, awaken=aw)[1]*3600:.2f}gem" for s in range(1, 9)))
    lines.append("민감도 나이테 1 (진화 직후) 골드/시간: " + ", ".join(f"{s}:{bag_rate(s, ring=1)[0]*3600:,.0f}" for s in range(1, 9)))
    runs = load_runs()
    inc = round_income_by_zone(runs); res["income"] = inc
    lines.append("\n라운드 수입 (round2_fit4 시드 1~3, 보스 판 제외 일반 판)")
    for k, v in inc.items():
        lines.append(f"  {k}: 판당 중앙 {v['round_med']:.3g} ({v['round_lo']:.3g}~{v['round_hi']:.3g}), 시간당 중앙 {v['hour_med']:.3g} ({v['hour_lo']:.3g}~{v['hour_hi']:.3g})")
    for name, mul in (("지금", None), ("추천안", BAG_MUL)):
        res["session"][name] = []
        lines.append(f"\n세션 진행 ({name}, 세계수 열림 = 스테이지 5 가정)")
        for k, run in enumerate(runs, 1):
            r = session(run, bag_mul=mul); res["session"][name].append(r)
            ev = ", ".join(f"{e['tree']}단계@R{e['round']}(스테이지{e['stage']},{e['minute']}분)" for e in r["evo"])
            lines.append(f"  시드{k}: 끝 세계수 {r['tree']}단계, 꼬마 {r['kids']}, 남은 균사석 {r['gem_left']}, 세션 보따리 골드 {r['bag_gold']:,.0f}; {ev}")
    for name, mul in (("지금", [1] * 8), ("추천안", BAG_MUL)):
        d = [days_to_next(s, mul[s - 1]) for s in range(1, 9)]
        res["days"][name] = d
        lines.append(f"방치만으로 다음 단계 균사석(진화+꼬마) 모으는 날 수 ({name}, 하루 16시간): " + ", ".join(f"{s}->{s+1}: {x:.1f}일" if x is not None and x != float('inf') else f"{s}->{s+1}: 균사석 없음" for s, x in zip(range(1, 8), d)))
    # 세계수 단계별 그때 라운드 수입 (세션 진행에서 그 단계에 있던 스테이지 구간, 3시드 일반 판 중앙값). 6~8단계는 세션 뒤 → 마지막 20판(스테이지 100~120)
    lines.append("\n세계수 단계별 그 시점 라운드 수입과 보따리 비 (지금 / 추천안)")
    res["compare"] = []
    sess = res["session"]["지금"]
    for s in range(1, 9):
        vals = []
        for run, r in zip(runs, sess):
            st, b, pt, inc = run["stageR"], run["bossR"], run["playR"], run["incR"]
            ev = {e["tree"]: e["round"] for e in r["evo"]}
            lo = ev.get(s, 1 if s == 1 else None); hi = ev.get(s + 1, len(st))
            if s == 1:
                lo = next(i + 1 for i in range(len(st)) if st[i] >= 5)
            if lo is None:
                lo, hi = len(st) - 20, len(st)
            for i in range(max(1, lo - 1), min(hi, len(st))):
                if b[i] in (0, 2):
                    vals.append((inc[i], inc[i] / (pt[i] - pt[i - 1]) * 3600, st[i]))
        vals.sort()
        med = vals[len(vals) // 2]; stg = sorted(v[2] for v in vals)
        hs = sorted(v[1] for v in vals)
        row = res["rates"][s - 1]
        c = dict(stage=s, game_stage=(stg[0], stg[-1]), round_med=med[0], hour_med=hs[len(hs) // 2], hour_lo=hs[0],
                 bag_h=row["gold_h"], bag_h_new=row["gold_h_new"], ratio=row["gold_h"] / hs[len(hs) // 2], ratio_new=row["gold_h_new"] / hs[len(hs) // 2],
                 ratio_lo_new=row["gold_h_new"] / hs[0], bag8_rounds_new=row["gold_h_new"] * 8 / med[0])
        res["compare"].append(c)
        lines.append(f"  {s}단계: 스테이지 {stg[0]}~{stg[-1]}, 판당 중앙 {med[0]:.3g}, 시간당 중앙 {c['hour_med']:.3g} (최저 {hs[0]:.3g}); 보따리/라운드 시간당 {c['ratio']:.2e} -> {c['ratio_new']:.2e} (최저 판 기준 {c['ratio_lo_new']:.2e}), 8시간 보따리 = 판 {c['bag8_rounds_new']:.3g}개")
    json.dump(res, open(os.path.join(HERE, "bag_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(HERE, "bag_out.txt"), "w", encoding="utf-8").write("\n".join(lines))
    print("\n".join(lines))


if __name__ == "__main__":
    main()
