# -*- coding: utf-8 -*-
"""T5(판매 전부 · 지역 이동 게임 규칙) 처치율·판매 몫.
사용: python BalanceData/test/t5_stats.py <phase폴더> [balance.log] [시드...]
처치율 = phase json `kill`(수확한 버섯 ÷ 그 판에 생긴 버섯, BalanceSim.cs:113,179), 지역 = `zone`.
창: 해금 직후 5판 R20~24 · R45~49 · R75~79, 중앙값·최저. 지역별: 그 지역으로 기록된 판의 중앙값·최저.
판매 몫(근사) = (inc − 점수골드) ÷ inc. inc = 점수골드 + 보너스 + 판매(`BalanceSim.cs:179`), 점수골드는 balance.log 열 7(U.Fmt 3자리 반올림이라 근사, 보너스 포함)."""
import json, os, sys, statistics as st, re
sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1]
logp = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].isdigit() else None
seeds = [int(x) for x in sys.argv[2:] if x.isdigit()] or [1, 2, 3]
def unfmt(s):
    s = s.strip().replace(",", ""); m = re.fullmatch(r"([\d.]+)([KMBT]?)", s)
    return float(m.group(1)) * {"": 1, "K": 1e3, "M": 1e6, "B": 1e9, "T": 1e12}[m.group(2)] if m else None
# balance.log: 마지막 3개 시드 블록(= 마지막 반복)의 판별 점수골드
logs = {}
if logp:
    blocks = []; cur = None
    for line in open(logp, encoding="utf-8"):
        c = [x.strip() for x in line.split("|")]
        if len(c) > 7 and re.fullmatch(r"R\d+", c[0]):
            r = int(c[0][1:])
            if r == 1: cur = {}; blocks.append(cur)
            cur[r] = unfmt(c[6])
    for i, sd in enumerate(seeds): logs[sd] = blocks[len(blocks) - len(seeds) + i]
zn = {"forest": "숲", "night": "달빛 밤", "field": "들판", "sea": "바다"}
for sd in seeds:
    p = json.load(open(os.path.join(d, f"balance_phase_s{sd}.json"), encoding="utf-8"))
    k, z, inc = p["kill"], p["zone"], p["inc"]; fin = max(p["done"][1:])
    print(f"시드 {sd} 완료 R{fin} ({p['doneMin'][3]:.0f}분) 산 판 {p['buyRounds']}/{p['rounds']}")
    first = {}
    for i, t in enumerate(z): first.setdefault(t, i + 1)
    print("  지역 첫 판:", " · ".join(f"{zn.get(t,t)} R{r}" for t, r in first.items()))
    for t in first:
        v = [k[i] for i in range(len(k)) if z[i] == t and i + 1 <= fin]
        if v: print(f"  처치율 {zn.get(t,t)}: 판 {len(v)} · 중앙 {st.median(v):.2f} · 최저 {min(v):.2f} · 평균 {st.mean(v):.2f}")
    for b in (20, 45, 75):
        v = k[b - 1:b + 4]; print(f"  해금 직후 R{b}~{b+4}: 처치율 {[round(x,2) for x in v]} → 중앙 {st.median(v):.2f} · 최저 {min(v):.2f} · 지역 {[zn.get(t,t) for t in z[b-1:b+4]]}")
    if sd in logs:
        sh = {}
        for r, sg in logs[sd].items():
            if r <= len(inc) and inc[r - 1] > 0 and sg is not None: sh[r] = max(0.0, (inc[r - 1] - sg) / inc[r - 1])
        for c in (10, 40, 70, 100):
            w = [sh[r] for r in range(c - 2, c + 3) if r in sh]
            if w: print(f"  판매 몫(근사) R{c-2}~R{c+2}: 중앙 {st.median(w):.2f} · 최소 {min(w):.2f} · 최대 {max(w):.2f}")
