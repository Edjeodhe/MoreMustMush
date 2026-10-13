# verify 2회차 manager#7: R2-11(BOSS_SCORE_MUL 0.05, 안 A) · 갱신 체력(K 1.0, Hp_Mul 0.226/0.184/1.89/7.24/99.6) · 판 목표 R20/R88/R172
# 독립: 체력 식, 관문 식(my_gate), 진 판 상한(체력 ÷ 한 타 피해 × 한 타 점수), 트리 완료 갇힘 검사, 대왕송이 날씨별 여유
# 데이터 생성기: theory boss.progression (시나리오는 verify가 고름). 안 A로 돌린다(THEORY_FAIL=A)
import os, sys, io, json, math
os.environ["THEORY_FAIL"] = "A"
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
DB = json.load(open(r"C:/Project/MoreMustMush/Assets/Resources/DB/db.json", encoding="utf-8"))
C = {c["Const_Key"]: c["Value"] for c in DB["Const"]}
FROM = [1, 20, 45, 75, 105]
def zm(s): return 10 ** max(i for i, f in enumerate(FROM) if s >= f) * 1.01 ** (s - 1)
def my_gate(boss, hp, atk, heavy, bossMul, crit, critMul, spd, balls, dur, temp, H0=0.9, KB=0.5, MOVER_H=0.6, TAU=0.2, T0=2.0):
    """verify_r2b.py와 같은 독립 관문 식 (RoundSim·ComputeStats 기준)"""
    A = atk * heavy * bossMul * (1 + crit * (critMul - 1))
    H = H0 * spd * (1 + KB * (balls - 1)) * (MOVER_H if boss == "mover" else 1) * ((1 + TAU) if boss != "angel" and temp else 1)
    T = dur - T0; d = A * H
    if boss == "cleaner":
        r = C["CLEANER_HEAL"] / C["CLEANER_INT"]; t = hp / (d - r * hp) if d > r * hp else math.inf
    elif boss == "phantom": t = 1.99 * hp / d
    else: t = hp / d
    return t, T
import boss as B, plan as P            # noqa: E402

NEW = {"king": 0.226, "cleaner": 0.184, "angel": 1.89, "phantom": 7.24, "mover": 99.6}
ORDER = ["king", "cleaner", "angel", "phantom", "mover"]
HPDB = {s: 40 * 1.0 * zm(s) * NEW[ORDER[i]] for i, s in enumerate((10, 20, 30, 40, 50))}
print("\n\n######## manager#7 ########")
print("== 1. DB 값 체력 ==", {s: round(v, 1) for s, v in HPDB.items()})

hp, _, prices, _ = P.solve()
print("theory plan 체력:", {s: round(v, 1) for s, v in hp.items() if s <= 50}, "· 차:", {s: f"{HPDB[s]/hp[s]-1:+.2%}" for s in HPDB})

def run(gate=None, hpx=None, **kw):
    old = dict(B.GATE)
    if gate: B.GATE.update(gate)
    try:
        cfg = dict(prices=prices, hp=hpx or HPDB, reward_rounds=P.REWARD_ROUNDS, look_tier=P.LOOK_TIER, fail_rounds=None,
                   max_rounds=700, stop_stage=50, snap=True); cfg.update(kw)
        return B.progression(cfg)
    finally:
        B.GATE.clear(); B.GATE.update(old)

def fmt(res):
    lg = res["log"]
    return (f"R{res['rounds']} · {res['minutes']:.0f}분 · 도전 {[lg[s]['tries'] if s in lg else '-' for s in (10,20,30,40,50)]} · "
            f"처치 {[lg[s].get('kill_r') if s in lg else None for s in (10,20,30,40,50)]} · 단계 완료 {res['done']}")

print("\n== 2. 진행·민감도 (DB 값 체력, 제안 가격, 안 A) ==")
W = dict(H0=0.63, MOVER_H=0.4, TAU=0.0)
cases = [("기준", {}), ("H0×0.7", dict(gate=dict(H0=0.63))), ("H0×1.3", dict(gate=dict(H0=1.17))),
         ("최악 H0×0.7+MOVER 0.4+TAU 0", dict(gate=W)), ("최악 + 수입×0.7", dict(gate=W, inc_mul=0.7)), ("최악 + 수입×0.5", dict(gate=W, inc_mul=0.5)),
         ("수입×0.5", dict(inc_mul=0.5)), ("수입×0.7", dict(inc_mul=0.7)), ("수입×1.5", dict(inc_mul=1.5)), ("수입×2", dict(inc_mul=2.0)),
         ("H0×0.7+수입×0.7", dict(gate=dict(H0=0.63), inc_mul=0.7)), ("지금 DB 가격", dict(prices=None))]
base = None
for name, kw in cases:
    r = run(**kw); base = base or r
    print(f"{name:30}", fmt(r))

print("\n== 3. 갇힘: 트리 전부 능력치, 스테이지 50 t/T (verify my_gate) ==")
full = {n["id"]: n["max"] for n in B.GOLD_NODES}
for w in ("storm", "wind", "cold"):
    s = B.stats(full, 50, 400, w)
    row = []
    for name, g in (("기준", {}), ("최악", W)):
        t, T = my_gate("mover", HPDB[50], s["atk"], s["heavy"], s["bossMul"], s["crit"], s["critMul"], s["spd"], s["balls"], s["dur"], s["temp"], **g)
        row.append(f"{name} {t/T:.2f}")
    print(w, " · ".join(row))
# 스테이지 40도 트리 전부 기준 (0.5 여유)
for w in ("storm", "cold"):
    s = B.stats(full, 40, 400, w)
    t, T = my_gate("phantom", HPDB[40], s["atk"], s["heavy"], s["bossMul"], s["crit"], s["critMul"], s["spd"], s["balls"], s["dur"], s["temp"], **W)
    print(f"스테이지 40 트리 전부 최악·{w}: t/T {t/T:.3f}")

print("\n== 4. 대왕송이 첫 도전: 도착 판(R10) 실제 능력치, 날씨별 r = t/T ==")
snap = base["snaps"][base["log"][10]["first_r"] - 1]
for H0 in (0.9, 0.63):
    out = []
    for w in ("storm", "wind", "cold"):
        s = B.stats(snap, 10, 10, w)
        t, T = my_gate("king", HPDB[10], s["atk"], s["heavy"], s["bossMul"], s["crit"], s["critMul"], s["spd"], s["balls"], s["dur"], s["temp"], H0=H0)
        out.append(f"{w} {t:.1f}/{T:.1f}={t/T:.3f}")
    print(f"H0 {H0}: " + " · ".join(out), "| 도착 능력치 atk", round(B.stats(snap, 10, 10, 'wind')["atk"], 3), "spd", B.stats(snap, 10, 10, 'wind')["spd"])
snap20 = base["snaps"][base["log"][20]["first_r"] - 1]
for w in ("storm", "wind", "cold"):
    s = B.stats(snap20, 20, 20, w)
    t, T = my_gate("cleaner", HPDB[20], s["atk"], s["heavy"], s["bossMul"], s["crit"], s["critMul"], s["spd"], s["balls"], s["dur"], s["temp"], H0=0.63)
    print(f"청소부 H0 0.63 {w}: r {t/T:.3f}")

print("\n== 5. 진 판 net 상한 (안 A, 사람 기준, 진 판은 체력을 다 못 깎음 → 타 수 < 유효 체력 ÷ 한 타 피해) ==")
# 한 타 점수 실측: T0/T1a 스테이지 10 진 판 점수골드 5,755, 타 수 ≈ (105 × 9%) ÷ 1.12 = 8.4 → 약 685/타 (콤보·골드 배율 포함)
per10 = 5755 / (105 * 0.09 / 1.12)
for comb in (1.0, 2.0):
    hits = HPDB[10] / 1.12
    fail = hits * per10 * comb * 0.05 * C["BOSS_FAIL_GOLD"]
    print(f"스테이지 10: 타 수 < {hits:.1f}, 한 타 {per10:.0f} × 콤보 추가 ×{comb} → 진 판 < {fail:.0f} · 직전 일반 판 R9 1,977(T1a 시드 1) → 비 < {fail/1977:.2f} · 같은 스테이지 최저 일반 판 679 → < {fail/679:.2f}")
print("BOSS_SCORE_MUL 상한(콤보 ×2, 최저 일반 판 679 기준):", round(679 / (HPDB[10] / 1.12 * per10 * 2 * 0.8), 3))
