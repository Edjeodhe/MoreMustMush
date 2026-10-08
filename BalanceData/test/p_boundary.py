# -*- coding: utf-8 -*-
"""지역 경계(R20·R45·R75) 5판 창 비 3개: [b-5,b-1]→[b,b+4] · [b,b+4]→[b+5,b+9] · [b+5,b+9]→[b+10,b+14] (inc 5판 중앙값 비).
사용: python BalanceData/test/p_boundary.py <폴더> [시드...]. 조사.md P6 '해금 뒤 10판(S1)' 허용(중반 ≤ ×18 · 후반 ≤ ×12)과 비교용."""
import json, os, sys, statistics as st
sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1]; seeds = [int(x) for x in sys.argv[2:]] or [1, 2, 3]
m = lambda inc, a: st.median(inc[a - 1:a + 4])
for sd in seeds:
    inc = json.load(open(os.path.join(d, f"balance_phase_s{sd}.json"), encoding="utf-8"))["inc"]
    out = []
    for b in (20, 45, 75):
        r = [m(inc, b) / m(inc, b - 5), m(inc, b + 5) / m(inc, b), m(inc, b + 10) / m(inc, b + 5)]
        out.append(f"R{b}: " + " · ".join(f"×{x:.1f}" for x in r) + f" (3창 곱 ×{r[0]*r[1]*r[2]:.0f})")
    print(f"시드 {sd}: " + " | ".join(out))
