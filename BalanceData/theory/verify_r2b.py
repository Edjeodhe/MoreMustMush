# verify 2회차 2단계: theory 제안안(이론.md R2-1~R2-9) 재계산·민감도
# 독립 부분: 체력 식·관문 식·트리 완료 여유·가격 모양·싱크 정상 상태는 코드/DB에서 직접 계산 (theory 함수 안 씀)
# 시나리오 부분: 판 단위 진행은 theory boss.progression을 "데이터 생성기"로 쓰되, 시나리오(조합 민감도·현재 가격)는 verify가 고름
import json, math, sys, io, os, statistics
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
DB = json.load(open(r"C:/Project/MoreMustMush/Assets/Resources/DB/db.json", encoding="utf-8"))
C = {c["Const_Key"]: c["Value"] for c in DB["Const"]}
FROM = [1, 20, 45, 75, 105]
def zm(s): return 10 ** max(i for i, f in enumerate(FROM) if s >= f) * 1.01 ** (s - 1)
PROP = {"king": 0.29, "cleaner": 0.157, "angel": 1.89, "phantom": 10.1, "mover": 142}
ORDER = ["king", "cleaner", "angel", "phantom", "mover"]

print("== 1. 제안 Hp_Mul × BOSS_HP_K 1.0 → 체력 (Defs.Boss.cs:50 식) vs theory cfg.hp ==")
plan_b = json.load(open(os.path.join(HERE, "theory_boss_plan.json"), encoding="utf-8"))
for i, s in enumerate((10, 20, 30, 40, 50)):
    hp = 40 * 1.0 * zm(s) * PROP[ORDER[i]]; th = plan_b["cfg"]["hp"][str(s)]
    print(f"{s} {ORDER[i]}: DB에 넣을 값으로 {hp:,.1f} · theory 모델 {th:,.1f} · 차 {hp/th-1:+.2%}")

# 독립 관문 식 (RoundSim·ComputeStats 기준, theory R2-1 문구대로)
def my_gate(boss, hp, atk, heavy, bossMul, crit, critMul, spd, balls, dur, temp, H0=0.9, KB=0.5, MOVER_H=0.6, TAU=0.2, T0=2.0):
    A = atk * heavy * bossMul * (1 + crit * (critMul - 1))
    H = H0 * spd * (1 + KB * (balls - 1)) * (MOVER_H if boss == "mover" else 1) * ((1 + TAU) if boss != "angel" and temp else 1)
    T = dur - T0; d = A * H
    if boss == "cleaner":
        r = C["CLEANER_HEAL"] / C["CLEANER_INT"]; t = hp / (d - r * hp) if d > r * hp else math.inf
    elif boss == "phantom": t = 1.99 * hp / d
    else: t = hp / d
    return t, T

import boss as B, plan as P
print("\n== 2. 관문 식 대조 (같은 능력치 입력, theory gate vs verify my_gate) ==")
mx = 0
for st in (10, 20, 30, 40, 50):
    for w in ("storm", "wind", "cold"):
        lv = P.lv_at({10: 10, 20: 20, 30: 44, 40: 89, 50: 170}[st])
        s = B.stats(lv, st, 100, w); b = P.boss_def(st); hp = plan_b["cfg"]["hp"][str(st)]
        _, t1, T1, _ = B.gate(s, b, hp)
        t2, T2 = my_gate(b["id"], hp, s["atk"], s["heavy"], s["bossMul"], s["crit"], s["critMul"], s["spd"], s["balls"], s["dur"], s["temp"])
        mx = max(mx, abs(t1 - t2) / t2 if math.isfinite(t2) else 0, abs(T1 - T2))
print("최대 상대 차:", f"{mx:.2e}")

print("\n== 3. 스테이지 50 갇힘 검사: 트리 전부(골드 노드 최대) 능력치로 처치 여유 ==")
full = {n["id"]: n["max"] for n in B.GOLD_NODES}
for w in ("storm", "wind", "cold"):
    s = B.stats(full, 50, 400, w)
    for name, g in (("기준", {}), ("H0×0.7", dict(H0=0.63)), ("MOVER_H 0.4", dict(MOVER_H=0.4)), ("TAU 0", dict(TAU=0.0)),
                    ("H0×0.7+MOVER 0.4", dict(H0=0.63, MOVER_H=0.4)), ("H0×0.7+MOVER 0.4+TAU 0", dict(H0=0.63, MOVER_H=0.4, TAU=0.0))):
        t, T = my_gate("mover", plan_b["cfg"]["hp"]["50"], s["atk"], s["heavy"], s["bossMul"], s["crit"], s["critMul"], s["spd"], s["balls"], s["dur"], s["temp"], **g)
        print(f"{w:5} {name:24} t/T = {t/T:.2f} {'처치' if t <= T else '**불가(트리 다 사도)**'}")
print("트리 전부 능력치(포자 바람):", {k: round(v, 3) if isinstance(v, float) else v for k, v in B.stats(full, 50, 400, 'wind').items() if k in ('atk','heavy','crit','critMul','spd','balls','dur','temp')})

print("\n== 4. 조합 민감도 (theory 진행 모델에 verify 시나리오, 제안 가격·체력 고정) ==")
hp, _, prices, _ = P.solve()
def run(gate=None, **kw):
    old = dict(B.GATE)
    if gate: B.GATE.update(gate)
    try:
        cfg = dict(prices=prices, hp=hp, reward_rounds=P.REWARD_ROUNDS, look_tier=P.LOOK_TIER, max_rounds=600, stop_stage=50); cfg.update(kw)
        res = B.progression(cfg)
    finally:
        B.GATE.clear(); B.GATE.update(old)
    lg = res["log"]
    tries = [lg[s]["tries"] if s in lg else None for s in (10, 20, 30, 40, 50)]
    kills = [lg[s].get("kill_r") if s in lg else None for s in (10, 20, 30, 40, 50)]
    return f"R{res['rounds']} · {res['minutes']:.0f}분 · 스테이지 {res['stage']} · 도전 {tries} · 처치 판 {kills}"
cases = [("기준", {}), ("수입×0.5", dict(inc_mul=0.5)), ("수입×0.7", dict(inc_mul=0.7)), ("수입×1.5", dict(inc_mul=1.5)), ("수입×2", dict(inc_mul=2.0)),
         ("H0×0.7+MOVER 0.4", dict(gate=dict(H0=0.63, MOVER_H=0.4))), ("H0×0.7+MOVER 0.4+TAU 0", dict(gate=dict(H0=0.63, MOVER_H=0.4, TAU=0.0))),
         ("수입×0.5+H0×0.7", dict(inc_mul=0.5, gate=dict(H0=0.63))), ("수입×0.7+H0×0.7", dict(inc_mul=0.7, gate=dict(H0=0.63))),
         ("H0×1.3+수입×1.5", dict(inc_mul=1.5, gate=dict(H0=1.17)))]
for name, kw in cases: print(f"{name:24}", run(**kw))
print("\n-- 지금 DB 가격 + 제안 체력 (측정 순서 ①과 같은 조건) --")
for name, kw in (("지금 가격", dict(prices=None)), ("지금 가격 H0×0.7", dict(prices=None, gate=dict(H0=0.63)))):
    print(f"{name:24}", run(**kw))

print("\n== 5. 가격 수준 (R2-7): 지금 DB 가격 × 단계 배율 0.51/0.14/4.2 의 모양 ==")
U = {u["Upg_Key"]: u for u in DB["Upgrade"]}; CO = {c["Cost_ID"]: c for c in DB["Upgrade_Cost"]}
gold = [(n["id"], n["tier"], [CO[U[n["id"]]["Cost_ID"]][f"Cost_Lv{i}"] for i in range(1, n["max"] + 1)]) for n in B.GOLD_NODES]
def shape(scale):
    seq = sorted((p * scale[t], t) for _, t, ps in gold for p in ps)
    jumps = [(b[0] / a[0], a[1], b[1]) for a, b in zip(seq, seq[1:])]
    top = sorted(jumps, reverse=True)[:3]
    tmax = {t: max(p * scale[t] for _, tt, ps in gold if tt == t for p in ps) for t in (1, 2, 3)}
    tmin = {t: min(p * scale[t] for _, tt, ps in gold if tt == t for p in ps) for t in (1, 2, 3)}
    over = sum(1 for _, t, ps in gold for p in ps for _2 in [0] if False)
    return top, tmin, tmax
for name, sc in (("지금", {1: 1, 2: 1, 3: 1}), ("배율 적용", {1: 0.51, 2: 0.14, 3: 4.2})):
    top, tmin, tmax = shape(sc)
    print(name, "정렬 가격 최대 점프 3개:", [(round(j, 1), f"{a}→{b}") for j, a, b in top])
    print("   단계별 최소~최대:", {t: f"{tmin[t]:.3g}~{tmax[t]:.3g}" for t in (1, 2, 3)},
          "· 단계3 최소 / 단계2 최대 =", f"{tmin[3]/tmax[2]:.3g}", "· 단계2 최소 / 단계1 최대 =", f"{tmin[2]/tmax[1]:.3g}")
pl = json.load(open(os.path.join(HERE, "theory_plan.json"), encoding="utf-8"))
print("theory 레벨별 가격(plan) 모양:", pl.get("shape"))

print("\n== 6. 싱크 정상 상태 (R2-8) ==")
IM, IC = 1.05, 1.155
print("지역 ×10, 골드 싱크 되먹임 있음: 싱크마다 +", round(math.log(10) / (math.log(IC) - math.log(IM)), 1), "레벨 → 공격력·보스 피해 곱 ×", round(IM ** (2 * math.log(10) / (math.log(IC) - math.log(IM))), 2))
print("골드 싱크 되먹임 없음(inf_gold 안 삼):  +", round(math.log(10) / math.log(IC), 1), "레벨 → ×", round(IM ** (2 * math.log(10) / math.log(IC)), 2))
d = math.log(1.01 ** 10) / (math.log(IC) - math.log(IM)); print("같은 지역 보스 사이 레벨 d =", round(d, 3), "· DPS ×", round(IM ** (2 * d), 4), "vs ZoneMul ×", round(1.01 ** 10, 4))
for loop in (1.0, 1.05):
    # 보스 n이 정상 상태보다 더 요구하는 DPS 배율 loop^(n-5) → 싱크 2개 레벨 Δ = (n-5)·ln(loop)/(2 ln IM), 비용 ×IC^Δ
    g = IC ** (math.log(loop) / (2 * math.log(IM))) if loop > 1 else 1
    print(f"BOSS_HP_LOOP {loop}: 보스마다 추가 필요 골드 ×{g:.3f} → 10마리 뒤 ×{g**10:.2f} · 30마리 뒤 ×{g**30:.1f}")
