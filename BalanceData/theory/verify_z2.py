# -*- coding: utf-8 -*-
"""verify: 보따리 골드 판 수입 연동(Z2) 독립 재계산 (bag_model.py를 쓰지 않고 Game.Farm.cs·Defs.Farm.cs에서 다시 옮김).
실행: python BalanceData/theory/verify_z2.py -> verify_z2_out.txt (난수 없음)
"""
import json, math, os, statistics

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

# Defs.Farm.cs
SLOTS = [4, 6, 8, 10, 13, 16, 20, 24]                       # WT.mushSlots
TM = {"shelf": dict(t=60, g=50, gem=0, f=1), "bundle": dict(t=300, g=200, gem=0, f=2),
      "glow": dict(t=1800, g=300, gem=1, f=4), "golden": dict(t=10800, g=1000, gem=5, f=6)}
GOLDEN = 0.05
ZN = [("root", 1, 4, ["shelf"], False), ("trunk", 2, 3, ["shelf", "bundle"], False),
      ("branch", 4, 3, ["bundle", "glow"], True), ("crown", 6, 2, ["glow"], True)]
SEC, PREF, PERLV, RINGG = 4.0, 0.5, 0.08, 0.03
K = {"leaf": ("root", "speed", .10, 0), "dew": ("trunk", "regen", .20, 0), "wind": ("crown", "wind", 2, .5),
     "fire": ("trunk", "gem", .20, 0), "rock": ("root", "carry", 2, 4), "spark": ("branch", "chain", 2, 0),
     "moon": ("branch", "glow", 2, .30), "star": ("crown", "aura", .15, 0)}
JOIN = ["leaf", "dew", "wind", "fire", "rock", "spark", "moon", "star"]   # Defs.cs:316
AWM = [1, 1.3, 1.7, 2.2]
# Z2 제안 상수
RATIO = [0.004, 0.0054, 0.0106, 0.0134, 0.018, 0.025, 0.037, 0.05]
REF_THEORY = [30364, 22773, 62991, 72739, 90829, 82246, 134055, 156570]
EFFMAX, GAP = 4.0, 30.0


def zslots(s):  # Game.ZoneSlots
    op = [z for z in ZN if s >= z[1]]; tot = SLOTS[s - 1]; ws = sum(z[2] for z in op)
    o = {z[0]: tot * z[2] // ws for z in op}; left = tot - sum(o.values())
    for z in sorted(op, key=lambda z: (-(tot * z[2] % ws), ZN.index(z))):
        if left <= 0: break
        o[z[0]] += 1; left -= 1
    return o


def zcap(s, lvsum):  # Game.ZoneCapacity
    return (3 if s >= 6 else 2) + (lvsum >= 100) + (lvsum >= 200)


def place(s, kids, lvsum):  # B-0 배치 규칙 (선호 → 수관>가지>줄기>뿌리)
    cap = zcap(s, lvsum); opn = {z[0] for z in ZN if s >= z[1]}; w = {z: [] for z in opn}; rest = []
    for k in kids:
        z = K[k][0]
        (w[z].append(k) if z in opn and len(w[z]) < cap else rest.append(k))
    for k in rest:
        for z in ("crown", "branch", "trunk", "root"):
            if z in opn and len(w[z]) < cap: w[z].append(k); break
    return w


def pick(k, z):  # KidPickRate (장식 없음)
    _, tr, tv, _ = K[k]
    sp = (1 + (tv if tr == "speed" else 0)) * (tv if tr == "wind" else 1)
    return sp * (tv if tr == "carry" else 1) * ((1 + tv) if tr == "chain" else 1) / SEC


def kmul(k, z, lv, aw, aff, assigned):  # KidMul (장식 없음)
    zz, tr, tv, tv2 = K[k]
    high = dict((x[0], x[4]) for x in ZN)[z]
    add = (K["star"][2] if "star" in assigned else 0) + (tv2 if tr == "wind" and high else 0)
    return (1 + PERLV * (lv - 1)) * AWM[aw] * (1 + (PREF if zz == z else 0)) * (1 + add) * (1 + 0.01 * aff)


def zrate(s, z, n, kids, ring, lv, aw, aff, assigned):  # ZoneRate 골드
    if not kids or n <= 0: return 0.0
    cap = sum(pick(k, z) for k in kids)
    grow = 1 + RINGG * (ring - 1) + (K["dew"][2] if "dew" in kids else 0)
    pk = SEC * len(kids) / cap; sup = gold = 0.0
    av = [m for m in dict((x[0], x[3]) for x in ZN)[z] if s >= TM[m]["f"]] or ["shelf"]
    for i in range(n):
        m = av[i % len(av)]
        parts = [(m, 1.0)] if not (z == "crown" and s >= 6) else [(m, 1 - GOLDEN), ("golden", GOLDEN)]
        cyc = sum(p * (TM[mm]["t"] / grow + pk) for mm, p in parts); sup += 1 / cyc
        for mm, p in parts:
            for k in kids:
                typ = K[k][2] if (mm == "glow" and K[k][1] == "glow") else 1
                gold += p / cyc * pick(k, z) / cap * kmul(k, z, lv[k], aw[k], aff, assigned) * typ * TM[mm]["g"]
    return gold * min(1.0, cap / sup)


def raw(s, nk, ring, lvf, awf, aff=1):  # gold/초, bagMul·장식·노드 없음
    kids = JOIN[:nk]; lv = {k: lvf(k) for k in kids}; aw = {k: awf(k) for k in kids}
    w = place(s, kids, sum(lv.values())); asg = [k for z in w for k in w[z]]; sl = zslots(s)
    return sum(zrate(s, z, sl.get(z, 0), ks, ring, lv, aw, aff, asg) for z, ks in w.items())


def eff(x):
    return 0.0 if x <= 0 else (x if x <= 1 else min(EFFMAX, math.sqrt(x)))


def maw(s, k):
    return max(0, min(3, s - (JOIN.index(k) + 1)))


def intervals():
    runs = [json.load(open(os.path.join(ROOT, "BalanceData", "round2_fit4", f"balance_phase_s{i}.json"), encoding="utf-8")) for i in (1, 2, 3)]
    sp = {1: (5, 11), 2: (8, 12), 3: (11, 40), 4: (40, 70), 5: (70, 133), 6: (105, 133), 7: (105, 133), 8: (105, 133)}
    out, inc = {}, {}
    for s, (lo, hi) in sp.items():
        d, g = [], []
        for r in runs:
            st, pt, b, ic = r["stageR"], r["playR"], r["bossR"], r["incR"]
            for i in range(1, len(st)):
                if lo <= st[i] < hi and b[i] == 0:
                    d.append(pt[i] - pt[i - 1]); g.append(ic[i])
        out[s] = statistics.median(d); inc[s] = (statistics.median(g), min(g), max(g), len(g))
    return out, inc


def main():
    L = []
    ref = [raw(s, max(1, s - 1), 10, lambda k: 10, lambda k: 0) * 3600 for s in range(1, 9)]
    L.append("① bagRefGoldH 재계산: " + ", ".join(f"{r:,.0f}" for r in ref))
    L.append("   theory 값과 차이: " + ", ".join(f"{(r / t - 1) * 100:+.3f}%" for r, t in zip(ref, REF_THEORY)))
    iv, inc = intervals()
    L.append("② 판 간격 중앙(초) " + str({s: round(v, 1) for s, v in iv.items()}))
    L.append("   그 구간 일반 판 수입 (중앙, 최소, 최대, n) " + str({s: (f"{a:.3g}", f"{b:.3g}", f"{c:.3g}", n) for s, (a, b, c, n) in inc.items()}))
    L.append("③ 단계 | 해금/진화직후 x | 대표 x | 꼬마최대 x | 8h=판(해금·기준·대표·꼬마최대·상한4) | 방치/플레이(기준·상한) | BASE_TIME10(-5초) 8h=판 기준")
    prev = {}
    for s in range(1, 9):
        R = ref[s - 1] / 3600
        xs = {
            "해금": raw(s, max(1, s - 1), 1, (lambda k: 1) if s == 1 else (lambda k: 10), lambda k: 0) / R,
            "대표": raw(s, s, 10, lambda k: 10, lambda k: 0) / R,
            "꼬마최대": raw(s, s, 10, lambda k: min(30, 10 * (maw(s, k) + 1)), lambda k: maw(s, k), aff=10) / R,
        }
        n8 = 28800 / iv[s]; rho = RATIO[s - 1]
        r8 = {k: rho * n8 * eff(v) for k, v in xs.items()}
        L.append(f"  {s} | {xs['해금']:.2f} | {xs['대표']:.2f} | {xs['꼬마최대']:.2f} | {r8['해금']:.2f} · {rho*n8:.2f} · {r8['대표']:.2f} · {r8['꼬마최대']:.2f} · {rho*n8*4:.1f}"
                 f" | {rho*100:.2f}% · {rho*400:.1f}% | {rho*28800/(iv[s]-5):.2f}")
        prev[s] = xs
    # 진화 전후: 같은 꼬마 상태(앞 단계 꼬마최대, 나이테 10)에서 진화 → 나이테 1
    L.append("④ 진화 전후 배율 (앞 단계 상태 그대로 진화, 나이테 10→1): 대표 상태 / 꼬마최대 상태 / 장식최대(x×3.94)")
    for s in range(2, 9):
        Rp, Rn = ref[s - 2] / 3600, ref[s - 1] / 3600
        out = []
        for lvf, awf, aff in ((lambda k: 10, lambda k: 0, 1),
                              (lambda k, s=s: min(30, 10 * (maw(s - 1, k) + 1)), lambda k, s=s: maw(s - 1, k), 10)):
            before = RATIO[s - 2] * eff(raw(s - 1, s - 1, 10, lvf, awf, aff) / Rp)
            after = RATIO[s - 1] * eff(raw(s, s - 1, 1, lvf, awf, aff) / Rn)
            out.append(after / before)
        bm = raw(s - 1, s - 1, 10, lambda k, s=s: min(30, 10 * (maw(s - 1, k) + 1)), lambda k, s=s: maw(s - 1, k), 10) / Rp * 3.94
        am = raw(s, s - 1, 1, lambda k, s=s: min(30, 10 * (maw(s - 1, k) + 1)), lambda k, s=s: maw(s - 1, k), 10) / Rn * 3.94
        out.append(RATIO[s - 1] * eff(am) / (RATIO[s - 2] * eff(bm)))
        L.append(f"  {s-1}->{s}: " + " / ".join(f"x{v:.2f}" for v in out))
    # 지금 공식(bagMul 포함) 해금 직후 8h 보따리 ÷ 숲 판 수입
    cur1 = raw(1, 1, 1, lambda k: 1, lambda k: 0) * 28800
    L.append(f"⑤ 지금 공식 1단계 해금 직후(Lv1·나이테1) 8h 골드 {cur1:,.0f} = 숲 일반 판 중앙 {inc[1][0]:.3g}의 {cur1/inc[1][0]:.1f}판 (최소 판 기준 {cur1/inc[1][1]:.1f}판), 기준 배치 {ref[0]*8:,.0f} = {ref[0]*8/inc[1][0]:.1f}판")
    # 실제 판 간격 민감도: 공식 분모(timeMax+30) vs 실제 판 간격(timeMax+G)
    L.append("⑥ 실제 판 사이 시간이 30초가 아니라 G초일 때 8단계 상한 비율 (timeMax = 간격-30): " +
             ", ".join(f"G={g}: {0.05*4*(iv[8]-30+g)/iv[8]*100:.1f}%" for g in (30, 45, 60, 90)))
    # 운 좋은 판: 같은 구간 판 수입 최대/중앙
    L.append("⑦ 같은 구간 일반 판 수입 최대÷중앙 (운 좋은 판 기준 튐 상한 근사): " + ", ".join(f"{s}:{inc[s][2]/inc[s][0]:.2f}" for s in range(1, 9)))
    txt = "\n".join(L)
    open(os.path.join(HERE, "verify_z2_out.txt"), "w", encoding="utf-8").write(txt)
    print(txt)


if __name__ == "__main__":
    main()
