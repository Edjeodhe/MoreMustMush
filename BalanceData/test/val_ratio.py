# -*- coding: utf-8 -*-
"""verify#2: 판별 버섯 가치(val = RoundResult.value, 상점 판매 가치) ÷ 판 수입(inc = 점수골드 + 보너스).
사용: python BalanceData/test/val_ratio.py <폴더> [시드...]   (phase json에 val 배열이 있는 기록만; BalanceSim.PhaseLog.val)
창: R10·R40·R70·R100 근처 = 그 판 ±2판(5판) — 각 판 비의 중앙값, 5판 val 합 ÷ 5판 inc 합 둘 다 출력. 이 기록은 판매 수입을 골드에 넣지 않은 시뮬이다."""
import json, os, sys, statistics as st
sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1]; seeds = [int(x) for x in sys.argv[2:]] or [4, 5, 6]
def f(x):
    for u, s in ((1e12, "T"), (1e9, "B"), (1e6, "M"), (1e3, "K")):
        if x >= u: return f"{x/u:.3g}{s}"
    return f"{x:.0f}"
for sd in seeds:
    p = json.load(open(os.path.join(d, f"balance_phase_s{sd}.json"), encoding="utf-8"))
    inc, val = p["inc"], p["val"]
    print(f"시드 {sd} 완료 R{max(p['done'][1:])}")
    for c in (10, 40, 70, 100):
        a, b = c - 3, c + 2   # 0-index 슬라이스: R(c-2)~R(c+2)
        ri = [val[i] / inc[i] for i in range(a, b) if inc[i] > 0]
        print(f"  R{c-2}~R{c+2}: 판 inc {f(st.median(inc[a:b]))} · val {f(st.median(val[a:b]))} · 비(중앙) {st.median(ri):.2f} (최소 {min(ri):.2f} · 최대 {max(ri):.2f}) · 합비 {sum(val[a:b])/sum(inc[a:b]):.2f}")
