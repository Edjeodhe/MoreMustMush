# -*- coding: utf-8 -*-
"""조사.md 원칙 P1·P6·P10 수치. 사용: python BalanceData/test/p_stats.py <폴더> [시드...]  (기본 시드 1 2 3)
P6 정의(조사.md:84 '5판 중앙값'): 구간 증가율 = (끝 5판 inc 중앙값 ÷ 처음 5판 중앙값)^(1/거리), 거리 = 두 창 시작 판의 차
  초반 R1~19: 창 R1~5 → R15~19 (거리 14) · 중반 R30~44: R30~34 → R40~44 (10) · 후반 R55~74: R55~59 → R70~74 (15)
  해금 직후 5판 비 = 중앙값(R_b..R_b+4) ÷ 중앙값(R_b-5..R_b-1), b = 20 · 45 · 75 (지역 해금 판)
  정체: 5판 창 연속(겹침 없이) 두 번 비 < 1.3 (트리 완료 후 제외, 완료 전 5판 창 R1~R(완료)에서 5판 간격으로 비교)
P1: 구매 판(buys>0) 간 간격의 중앙값 · 90% 분위(정렬 후 ceil(0.9n)번째) · 최대, 완료 전 산 판 비율
"""
import json, os, sys, math, statistics as st
sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1]; seeds = [int(x) for x in sys.argv[2:]] or [1, 2, 3]
med = lambda inc, a, b: st.median(inc[a - 1:b])
for sd in seeds:
    p = json.load(open(os.path.join(d, f"balance_phase_s{sd}.json"), encoding="utf-8"))
    inc, buys, dn = p["inc"], p["buys"], p["done"]; fin = max(dn[1:])
    g = lambda a1, a2, b1, b2, dist: (med(inc, b1, b2) / med(inc, a1, a2)) ** (1 / dist)
    early, mid, late = g(1, 5, 15, 19, 14), g(30, 34, 40, 44, 10), g(55, 59, 70, 74, 15)
    jumps = {b: med(inc, b, b + 4) / med(inc, b - 5, b - 1) for b in (20, 45, 75)}
    # 5판 창 비(겹침 없는 연속 창), 완료 전
    wins = [(s, med(inc, s, s + 4)) for s in range(1, fin - 3, 5) if s + 4 <= fin]
    ratios = [(wins[i][0], wins[i + 1][1] / wins[i][1]) for i in range(len(wins) - 1)]
    low = [(s, round(r, 2)) for s, r in ratios if r < 1.3]
    br = [i + 1 for i, b in enumerate(buys) if b > 0 and i + 1 <= fin]
    gaps = sorted(y - x for x, y in zip(br, br[1:]))
    p90 = gaps[math.ceil(0.9 * len(gaps)) - 1]
    print(f"시드 {sd}: 완료 R{fin} ({p['doneMin'][3]:.0f}분) | P1 간격 중앙 {st.median(gaps)} · 90% {p90} · 최대 {max(gaps)} · 완료 전 산 판 {len(br)}/{fin} ({len(br)/fin:.0%})")
    print(f"   P6 판당 증가율 초반 ×{early:.2f} · 중반 ×{mid:.2f} · 후반 ×{late:.2f} | 해금 직후 5판 비 R20 ×{jumps[20]:.1f} · R45 ×{jumps[45]:.1f} · R75 ×{jumps[75]:.1f}")
    print(f"   5판 창 비 < ×1.3 인 창 시작(겹침 없음): {low}")
    print(f"   5판 창 비 최대 ×{max(r for _, r in ratios):.1f} (시작 R{max(ratios, key=lambda x: x[1])[0]})")
