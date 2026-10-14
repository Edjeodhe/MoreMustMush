# -*- coding: utf-8 -*-
"""verify R2-13 (manager#11): EnforceShape 지금 규칙(올림)과 안 (d)(상한 내림)을 BalanceSim.cs 그대로 독립 재현.

theory r213.py는 줄 셋만 따로 본다. 여기서는 Priced 노드 전체(STEP_FIX 전역 체인 · RaiseNode · 줄 규칙)를
BalanceSim.EnforceShape(:455~485) 순서대로 돌린다. Nice = 유효숫자 2자리 (C# Math.Round = 짝수 반올림, Python round와 같음).

python verify_r213.py → verify_r213_out.txt
"""
import json, math, os, sys, io

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = io.StringIO()
def P(*a):
    print(*a); print(*a, file=OUT)

NODE_STEP_MAX = 4.0; STEP_FIX = 1.8; PRICE_STEP_MAX = 1.4
LINES = [
    (["ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"], [1, 2.5, 2.5, 6, 6, 15], 0.53),
    (["ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p"], [1, 1, 10, 10, 100, 100], 0.66),
    (["ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p"], [1, 1, 10, 10, 100, 100], 0.66),
]
LINE_IDS = {k for ids, _, _ in LINES for k in ids}

db = json.load(open(os.path.join(ROOT, "Assets", "Resources", "DB", "db.json"), encoding="utf-8"))
UC = {r["Cost_ID"]: r for r in db["Upgrade_Cost"]}
ID2K = {u["Upg_ID"]: u["Upg_Key"] for u in db["Upgrade"]}
NODES = []   # NODES 순서 = DB 행 순서 (dict 순서, OrderBy는 안정 정렬)
for u in db["Upgrade"]:
    n = int(u["Efx_MaxLv"]); c = UC[u["Cost_ID"]]
    if n == 0: continue                                            # inf (싱크)
    if any(c["Gem_Lv%d" % i] for i in range(1, 5)): continue        # gem
    NODES.append(dict(id=u["Upg_Key"], tier=int(u["Tier"]), max=n,
                      db=[float(c["Cost_Lv%d" % i]) for i in range(1, n + 1)],
                      parent=ID2K.get(u["Upg_Pre"]), req=ID2K.get(u["Upg_Req"])))
NODE = {n["id"]: n for n in NODES}

def nice(v):
    if v < 10: return max(1.0, float(round(v)))
    e = 10 ** (math.floor(math.log10(v)) - 1)
    return round(v / e) * e
def nice_down(v):
    if v < 10: return max(1.0, float(math.floor(v)))
    e = 10 ** (math.floor(math.log10(v)) - 1)
    return math.floor(v / e + 1e-9) * e

def apply(m):
    return {n["id"]: [nice(max(1, v * m[n["tier"]])) for v in n["db"]] for n in NODES}

def raise_node(v):
    for i in range(1, len(v)): v[i] = max(v[i], v[i - 1])
    for i in range(len(v) - 2, -1, -1): v[i] = max(v[i], nice(v[i + 1] / NODE_STEP_MAX * 1.05))

def step_fix(price):
    flat = sorted(((k, i) for k, arr in price.items() for i in range(len(arr))), key=lambda x: price[x[0]][x[1]])
    for k in range(len(flat) - 2, -1, -1):
        (a, i), (b, j) = flat[k], flat[k + 1]
        price[a][i] = max(price[a][i], nice(price[b][j] / STEP_FIX * 1.03))

def lines_raise(price):   # 지금 규칙 (:473~481)
    for ids, per, cross in LINES:
        for t in range(len(ids) - 1, 0, -1):
            pa, pb = price[ids[t - 1]], price[ids[t]]
            need = pb[0] / NODE_STEP_MAX if per[t] == per[t - 1] else pb[0] / per[t] * per[t - 1] / cross
            if pa[-1] < need: pa[-1] = nice(need * 1.06)
            raise_node(pa)

def lines_cap(price, fixed=False):   # 안 (d), 이론.md 적용안 #1 그대로 (fixed=True: 검증 수정안, 아래 주석)
    for ids, per, cross in LINES:
        for t in range(1, len(ids)):
            pa, pb = price[ids[t - 1]], price[ids[t]]
            cap = pa[-1] * NODE_STEP_MAX if per[t] == per[t - 1] else pa[-1] * per[t] / per[t - 1] * cross
            pb[0] = min(pb[0], nice_down(cap / 1.06))
            if fixed: pb[0] = max(pb[0], pa[-1])                           # 줄 오르기만 (경계 비 ≥ 1 보장)
            for i in range(1, len(pb)):
                pb[i] = min(pb[i], nice_down(pb[i - 1] * NODE_STEP_MAX / 1.05))
                if fixed: pb[i] = max(pb[i], pb[i - 1])

def enforce(m, mode, passes=4, trace=None):
    price = apply(m)
    for p in range(passes):
        step_fix(price)
        for v in price.values(): raise_node(v)
        if trace is not None: trace.append(("pass%d_before_line" % p, {k: list(v) for k, v in price.items()}))
        if mode == "old": lines_raise(price)
        elif mode == "d": lines_cap(price)
        elif mode == "d2": lines_cap(price, True)
        elif mode == "none": pass
    if mode in ("d", "d2"): lines_cap(price, mode == "d2")
    return price

def check(price):
    ps = sorted(v for arr in price.values() for v in arr)
    max_step = max(ps[k] / max(1, ps[k - 1]) for k in range(1, len(ps)))
    n_step_fix = sum(1 for k in range(1, len(ps)) if ps[k] / max(1, ps[k - 1]) > STEP_FIX * 1.001)
    node_step = 1; mono_bad = 0
    for arr in price.values():
        for l in range(1, len(arr)):
            node_step = max(node_step, arr[l] / max(1, arr[l - 1])); mono_bad += arr[l] < arr[l - 1]
    ok = tot = 0; sib = 1; cross_min = 9
    for ids, per, cross in LINES:
        for t in range(1, len(ids)):
            a, b = price[ids[t - 1]], price[ids[t]]
            if per[t] == per[t - 1]:
                sib = max(sib, b[0] / a[-1]); mono_bad += b[0] < a[-1]; continue
            tot += 1
            r = (b[0] / per[t]) / (a[-1] / per[t - 1])
            cross_min = min(cross_min, r / cross)
            ok += r <= cross * 1.001
            mono_bad += b[0] < a[-1]
    return dict(max_step=max_step, step_fix_viol=n_step_fix, node_step=node_step, sib=sib, cross=f"{ok}/{tot}", mono_bad=mono_bad)

def tier_sum(price, t): return sum(sum(price[n["id"]]) for n in NODES if n["tier"] == t)
def g(x): return "%.3g" % x
def row(price, ids): return " | ".join("%s [%s]" % (k, ", ".join(g(x) for x in price[k])) for k in ids)

# ---------- 1. 진단 재현 ----------
P("== 1. 지금 규칙(올림) 재현 ==")
for m in ((None, 6.249e-5, 0.44, 3.8), (None, 0.41, 0.44, 3.8), (None, 6.249e-5, 0.44, 6.582)):
    pr = enforce(m, "old")
    P("m =", m[1:], "1단계 합", g(tier_sum(pr, 1)), "(DB×m1:", g(sum(sum(n["db"]) for n in NODES if n["tier"] == 1) * m[1]), ")")
    P("   ", row(pr, ["ps_atk", "ps_atk2", "ed_score1p", "ed_bonus1p"]))
    P("    ed_score1p L3 / DB:", g(pr["ed_score1p"][-1] / NODE["ed_score1p"]["db"][-1]),
      "ed_bonus1p L3 / DB:", g(pr["ed_bonus1p"][-1] / NODE["ed_bonus1p"]["db"][-1]),
      "ps_atk L4 / m3:", g(pr["ps_atk"][-1] / m[3]), "check", check(pr))
# Temp/price_table.json (지금 배치 마지막 m = 6.249e-5·0.44·6.582)과 대조
tp = os.path.join(ROOT, "Temp", "price_table.json")
if os.path.exists(tp):
    T = json.load(open(tp, encoding="utf-8")); pr = enforce((None, 6.249e-5, 0.44, 6.582), "old")
    diff = [(k, i, T[k][i], pr[k][i]) for k in pr for i in range(len(pr[k])) if k in T and abs(T[k][i] / pr[k][i] - 1) > 1e-6]
    P("Temp/price_table.json 대조(m 6.249e-5·0.44·6.582): 다른 레벨 %d / %d" % (len(diff), sum(len(v) for v in pr.values())), diff[:8])

P("\n1단계 합 vs m1 (지금 규칙, m2 0.44, m3 3.8): 바닥 확인")
for m1 in (0.41, 0.1, 0.01, 1e-3, 1e-4):
    P("  m1 %-7g 1단계 합 %s  ps_atk L4 %s" % (m1, g(tier_sum(enforce((None, m1, 0.44, 3.8), "old"), 1)), g(enforce((None, m1, 0.44, 3.8), "old")["ps_atk"][-1])))

# ---------- 2. 안 (d) ----------
P("\n== 2. 안 (d) 상한 모드 (이론.md 적용안 #1 그대로) ==")
for mode in ("none", "d", "d2"):
    pr = enforce((None, 0.3, 0.44, 3.8), mode)
    P("[%s] m 0.3·0.44·3.8  1단계 합 %s  2단계 합 %s  3단계 합 %s" % (mode, g(tier_sum(pr, 1)), g(tier_sum(pr, 2)), g(tier_sum(pr, 3))), check(pr))
    for ids, _, _ in LINES: P("   ", row(pr, ids))
# 1단계 합 vs m1, m2, m3 (d): 바닥이 없는지, 뒤 단계 배율에 1단계가 묶이는지
P("\n(d) 1단계 합 vs m1 (m2 0.44, m3 3.8) / DB×m1 대비")
base1 = sum(sum(n["db"]) for n in NODES if n["tier"] == 1)
for m1 in (0.3, 0.1, 0.03, 0.01, 1e-3):
    pr = enforce((None, m1, 0.44, 3.8), "d")
    P("  m1 %-6g 1단계 합 %s (×%.2f)  ps_atk %s  STEP_FIX로 오른 1단계 레벨 %d" % (
        m1, g(tier_sum(pr, 1)), tier_sum(pr, 1) / (base1 * m1), [g(x) for x in pr["ps_atk"]],
        sum(1 for n in NODES if n["tier"] == 1 for i, v in enumerate(pr[n["id"]]) if v > nice(n["db"][i] * m1) * 1.001)))
P("\n(d) 뒤 단계 배율이 앞 단계 가격을 바꾸는지 (m1 0.3 고정)")
ref = enforce((None, 0.3, 0.44, 3.8), "d")
for m2, m3 in ((0.2, 3.8), (1.0, 3.8), (0.44, 1.0), (0.44, 11.4), (2.0, 11.4)):
    pr = enforce((None, 0.3, m2, m3), "d")
    ch1 = sum(1 for n in NODES if n["tier"] == 1 for i in range(n["max"]) if abs(pr[n["id"]][i] / ref[n["id"]][i] - 1) > 1e-9)
    P("  m2 %-4g m3 %-4g: 1단계 합 %s (기준 %s), 바뀐 1단계 레벨 %d | 2단계 합 %s | 3단계 합 %s | 공격력 줄 II·III 첫 %s %s" % (
        m2, m3, g(tier_sum(pr, 1)), g(tier_sum(ref, 1)), ch1, g(tier_sum(pr, 2)), g(tier_sum(pr, 3)), g(pr["ps_atk2"][0]), g(pr["ps_atk3"][0])))
P("\n(d) 단계 배율에 대한 단계 합의 단조성 (다른 둘 고정 0.3·0.44·3.8)")
for t, grid in ((1, (0.1, 0.2, 0.3, 0.5, 1)), (2, (0.1, 0.2, 0.44, 1, 2)), (3, (1, 2, 3.8, 6.6, 11.4))):
    s = []
    for x in grid:
        m = [None, 0.3, 0.44, 3.8]; m[t] = x; s.append(tier_sum(enforce(tuple(m), "d"), t))
    P("  m%d %s → 단계 합 %s  단조 %s" % (t, grid, [g(v) for v in s], all(s[i] <= s[i + 1] for i in range(len(s) - 1))))
P("\n(d) 공격력 줄 레벨 중 DB×m보다 내려간(상한이 묶인) 비율, m 0.3·0.44·3.8")
for ids, _, _ in LINES:
    P("  ", ", ".join("%s ×%s" % (k, "/".join("%.3g" % (ref[k][i] / nice(NODE[k]["db"][i] * (0.3, 0.44, 3.8)[NODE[k]["tier"] - 1])) for i in range(NODE[k]["max"]))) for k in ids))
# 수렴: pass 수를 늘려도 같은지, pass 사이 진동
P("\n(d) pass 수에 따른 결과 차이 (m 0.3·0.44·3.8)")
for ps in (1, 2, 4, 8, 16):
    a = enforce((None, 0.3, 0.44, 3.8), "d", ps)
    nd = sum(1 for k in a for i in range(len(a[k])) if abs(a[k][i] / ref[k][i] - 1) > 1e-9)
    P("   passes %2d: 4 pass 결과와 다른 레벨 %d, check %s" % (ps, nd, check(a)))
# STEP_FIX가 줄 레벨을 다시 올렸다가 상한이 내리는 다툼, 그 사이 줄 밖 레벨이 끌려 오른 양
tr = []; enforce((None, 0.3, 0.44, 3.8), "d", 4, tr)
nl = enforce((None, 0.3, 0.44, 3.8), "none")
moved = [(k, i, nl[k][i], ref[k][i]) for k in ref if k not in LINE_IDS for i in range(len(ref[k])) if abs(ref[k][i] / nl[k][i] - 1) > 1e-9]
P("   줄 밖 레벨 중 '줄 규칙 없음'과 값이 다른 것: %d" % len(moved), [(k, i + 1, g(a), g(b)) for k, i, a, b in moved[:10]])
fight = 0
for p in range(1, 4):
    before = tr[p][1]
    fight += sum(1 for k in LINE_IDS for i in range(len(before[k])) if before[k][i] > tr[p - 1][1][k][i] * 1.001)
P("   pass 사이 STEP_FIX·RaiseNode가 다시 올린 줄 레벨 수(누적): %d" % fight)

# ---------- 3. 부모 잠금 (ParentOk) ----------
P("\n== 3. 줄 노드의 부모·선행(ParentOk: parent Lv≥1, req 최대) 가격, (d) m 0.3·0.44·3.8 ==")
for ids, _, _ in LINES:
    for k in ids:
        n = NODE[k]; par = n["parent"]; rq = n["req"]
        pp = ref[par][0] if par in ref else None
        P("  %-11s 줄 첫 %-8s 끝 %-8s | parent %-11s L1 %-8s | req %-11s" % (k, g(ref[k][0]), g(ref[k][-1]), par, g(pp) if pp else "-", rq))

open(os.path.join(HERE, "verify_r213_out.txt"), "w", encoding="utf-8").write(OUT.getvalue())
