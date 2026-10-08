# -*- coding: utf-8 -*-
"""시뮬 기록(balance_phase_s{1,2,3}.json)에서 구매 사이 판 수 분포·단계 구간·판 수입 변화를 뽑는다.

사용: python BalanceData/test/phase_stats.py [폴더]   (기본: BalanceData)
입력 필드(BalanceSim.PhaseLog, Assets/Editor/BalanceSim.cs 104~116줄):
  buys[i] = 스테이지 i+1 정산 뒤 일괄 강화로 산 레벨 수
  inc[i]  = 스테이지 i+1의 판 수입(점수골드 + 보너스)
  first/done[t] = 단계 t(1 초반, 2 중반, 3 후반)를 처음 산 / 다 산 스테이지
  firstMin/doneMin[t] = 그때까지의 추정 플레이 시간(분, 판마다 라운드 시간 + 30초)
정의:
  구매 판 = buys > 0 인 판. 구매 간격 = 연속한 구매 판 번호의 차(1 = 매 판 구매).
  간격 통계는 트리 완료 판(max(done)) 이하의 구매 판끼리만 센다.
  무구매 연속 = 트리 완료 판까지 buys == 0 이 이어진 길이.
  단계 구간(겹침 반영): A = R1~초반 완료, B = 초반 완료+1~중반 완료, C = 중반 완료+1~후반 완료.
"""
import json, os, sys, statistics as st

sys.stdout.reconfigure(encoding="utf-8")
d = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")


def fmt(x):
    for u, s in ((1e12, "T"), (1e9, "B"), (1e6, "M"), (1e3, "K")):
        if x >= u:
            return f"{x / u:.3g}{s}"
    return f"{x:.0f}"


def gaps_of(rounds):
    return [b - a for a, b in zip(rounds, rounds[1:])]


def dist(g):
    return "{" + ", ".join(f"{k}판:{g.count(k)}" for k in sorted(set(g))) + "}"


all_gaps = []
for sd in (1, 2, 3):
    path = os.path.join(d, f"balance_phase_s{sd}.json")
    p = json.load(open(path, encoding="utf-8"))
    buys, inc, f, dn = p["buys"], p["inc"], p["first"], p["done"]
    fin = max(dn[1:])
    br = [i + 1 for i, b in enumerate(buys) if b > 0]
    br_fin = [r for r in br if r <= fin]
    g = gaps_of(br_fin)
    all_gaps += g
    streaks, s, start = [], 0, 0
    for i, b in enumerate(buys[:fin]):
        if b == 0:
            if s == 0:
                start = i + 1
            s += 1
        else:
            if s:
                streaks.append((s, start))
            s = 0
    print(f"== 시드 {sd} ({os.path.relpath(path)})")
    print(f"  단계 첫 구매 R{f[1]}/R{f[2]}/R{f[3]} ({p['firstMin'][1]:.0f}/{p['firstMin'][2]:.0f}/{p['firstMin'][3]:.0f}분)"
          f" · 완료 R{dn[1]}/R{dn[2]}/R{dn[3]} ({p['doneMin'][1]:.0f}/{p['doneMin'][2]:.0f}/{p['doneMin'][3]:.0f}분)")
    print(f"  산 판 {p['buyRounds']}/{p['rounds']} · 트리 완료 R{fin}까지 산 판 {len(br_fin)}/{fin} · 산 레벨 합 {sum(buys)}")
    print(f"  구매 간격(완료 전): 중앙 {st.median(g)} · 평균 {st.mean(g):.2f} · 최대 {max(g)} · 분포 {dist(g)}")
    top = sorted(streaks, reverse=True)[:3]
    print(f"  무구매 연속 최장: " + (", ".join(f"{n}판(R{a}~R{a + n - 1})" for n, a in top) if top else "없음"))
    segs = [("A 초반 완료까지", 1, dn[1]), ("B 중반 완료까지", dn[1] + 1, dn[2]), ("C 후반 완료까지", dn[2] + 1, dn[3])]
    for name, a, b in segs:
        if b < a:
            print(f"  {name}: 구간 없음 (R{a}>R{b})")
            continue
        rr = [r for r in br if a <= r <= b]
        gg = gaps_of(rr)
        print(f"  {name} R{a}~R{b} ({b - a + 1}판): 산 판 {len(rr)} · 레벨 {sum(buys[a - 1:b])}"
              f" · 간격 중앙 {st.median(gg) if gg else '-'} 최대 {max(gg) if gg else '-'}"
              f" · 판 수입 {fmt(inc[a - 1])} → {fmt(inc[b - 1])}")
    j = [(inc[i] / inc[i - 1], i + 1) for i in range(1, fin) if inc[i - 1] > 0]
    up, dw = max(j), min(j)
    big = [(r, x) for x, r in j if x >= 5 or x <= 0.2]
    print(f"  판 수입 전판 대비(완료 전): 최대 ×{up[0]:.1f}(R{up[1]}) · 최소 ×{dw[0]:.2f}(R{dw[1]})"
          f" · ×5 이상/×0.2 이하 판: " + ", ".join(f"R{r} ×{x:.2g}" for r, x in big))
    many = sorted(((b, i + 1) for i, b in enumerate(buys)), reverse=True)[:4]
    print("  한 판에 가장 많이 산 레벨: " + ", ".join(f"R{r} {b}레벨" for b, r in many))
    print("  판별 산 레벨(9 이상은 9): " + "".join(str(min(b, 9)) for b in buys))
print(f"== 시드 1~3 합: 구매 간격 {len(all_gaps)}개 · 중앙 {st.median(all_gaps)} · 평균 {st.mean(all_gaps):.2f}"
      f" · 최대 {max(all_gaps)} · 분포 {dist(all_gaps)}")
