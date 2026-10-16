# -*- coding: utf-8 -*-
"""verify — 제안 사다리에 BalanceSim.EnforceShape 줄 규칙(BalanceSim.cs:458~506)을 한 번 돌리면 타격 점수 줄이 어떻게 바뀌나.
LINE_CAP(BalanceJob 이분이 켬, CapLines)와 올림 모드(기본) 둘 다. STEP_FIX·RaiseNode 전체는 빼고 줄 규칙만."""
import math
import r3_verify as v

def nice(x):
    if x < 10: return max(1, round(x))
    e = 10 ** (math.floor(math.log10(x)) - 1); return round(x / e) * e
def nice_down(x):
    if x < 10: return max(1, math.floor(x))
    e = 10 ** (math.floor(math.log10(x)) - 1); return math.floor(x / e + 1e-9) * e

ids = ["ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p"]
per = [1, 1, 10, 10, 100, 100]; cross = 0.66; NS = 4
for name, nodes in (("지금", v.load_nodes(False, False)), ("제안", v.load_nodes(True, True))):
    pr = {k: list(nodes[k]["costs"]) for k in ids}
    cap = {k: list(p) for k, p in pr.items()}
    for t in range(1, len(ids)):           # CapLines
        pa, pb = cap[ids[t - 1]], cap[ids[t]]
        last = pa[-1]
        c = last * NS if per[t] == per[t - 1] else last * per[t] / per[t - 1] * cross
        pb[0] = max(last, min(pb[0], nice_down(c / 1.06)))
        for i in range(1, len(pb)): pb[i] = max(pb[i - 1], min(pb[i], nice_down(pb[i - 1] * NS / 1.05)))
    rs = {k: list(p) for k, p in pr.items()}
    for t in range(len(ids) - 1, 0, -1):   # 올림 모드
        pa, pb = rs[ids[t - 1]], rs[ids[t]]
        need = pb[0] / NS if per[t] == per[t - 1] else pb[0] / per[t] * per[t - 1] / cross
        if pa[-1] < need: pa[-1] = nice(need * 1.06)
        for i in range(len(pa) - 2, -1, -1): pa[i] = max(pa[i], nice(pa[i + 1] / NS * 1.05))
    print(name)
    for k in ids: print(f"  {k:<11} DB {pr[k]}  CapLines {cap[k]}  올림 {rs[k]}")
