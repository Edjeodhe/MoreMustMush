# -*- coding: utf-8 -*-
"""verify R2-12 (manager#9 · theory#2) — theory r212.py와 따로 만든 독립 재계산.

독립: 능력치(Game.cs:217~ ComputeStats 식을 db.json에서 직접), 관문 식(verify_r2b my_gate),
      판 진행(BulkBuy·재도전·정산 식을 코드 기준으로 새로 작성), 보스 체력(Defs.Boss.cs:51),
      싱크, 날씨(결정적 순환 + 무작위 시드), 수입-공격력 결합 민감도.
데이터로만 theory 것을 씀: 가격표(plan.solve 결과 — 가격 맞추기 전 모델 가격), 판 수입 근사식 계수(FIT·INC_BIAS), A4(별 공격력 배율 표).
python verify_r212.py
"""
import os, sys, io, json, math, random, statistics
os.environ["THEORY_FAIL"] = "A"
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
DB = json.load(open(r"C:/Project/MoreMustMush/Assets/Resources/DB/db.json", encoding="utf-8"))
C = {c["Const_Key"]: c["Value"] for c in DB["Const"]}
FROM = [1, 20, 45, 75, 105]
def zi(s): return max(i for i, f in enumerate(FROM) if s >= f)
def zm(s): return C["ZONE_STEP"] ** zi(s) * C["STAGE_GROWTH"] ** (s - 1)
TIER_HP, TIER_SCORE = [1, 4, 12, 40], [1, 5, 20, 100]
UP = {u["Upg_Key"]: u for u in DB["Upgrade"]}
ID2K = {u["Upg_ID"]: u["Upg_Key"] for u in DB["Upgrade"]}
COST = {c["Cost_ID"]: c for c in DB["Upgrade_Cost"]}
ATK = ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4")
TIER_OF = {"ps_atk": "I", "ps_atk2": "II", "ps_atk2p": "II+", "ps_atk3": "III", "ps_atk3p": "III+", "ps_atk4": "IV"}
CURVES = {"old": None, "A": {"ps_atk": 1, "ps_atk2": 2.5, "ps_atk2p": 2.5, "ps_atk3": 6, "ps_atk3p": 6, "ps_atk4": 15},
          "B": {"ps_atk": 1, "ps_atk2": 3, "ps_atk2p": 3, "ps_atk3": 6, "ps_atk3p": 6, "ps_atk4": 10}}
MAXLV = {k: int(u["Efx_MaxLv"]) for k, u in UP.items()}
GOLD = [k for k, u in UP.items() if max(COST[u["Cost_ID"]]["Gem_Lv%d" % i] for i in range(1, MAXLV[k] + 1)) <= 0]
PARENT = {k: ID2K.get(UP[k]["Upg_Pre"]) for k in UP}; REQ = {k: ID2K.get(UP[k]["Upg_Req"]) for k in UP}
SKILLS = [s["Skill_Key"] for s in DB["Skill"]]
CUR = "A"

def efx(k, L):
    L = min(max(L, 0), MAXLV[k])
    c = CURVES[CUR]
    if c and k in c: return c[k] * L
    return UP[k]["Efx_Lv%d" % L]

print("=" * 70, "\n1. 공격력 노드 표 (Game.cs:217 atkFlat = 1 + Σ NF.ps_atkX(L) + poison 3)\n" + "=" * 70)
for cv in ("old", "A", "B"):
    CUR = cv
    cum, seq, ends = 1.0, [], {}
    for k in ATK:
        for L in range(MAXLV[k]):
            d = efx(k, L + 1) - efx(k, L); seq.append((k, L + 1, d, d / cum)); cum += d
        ends[TIER_OF[k]] = cum
    e = [ends["I"], ends["II+"], ends["III+"], ends["IV"]]
    per = [efx(k, 1) for k in ("ps_atk", "ps_atk2", "ps_atk3", "ps_atk4")]
    print(f"[{cv}] 최대 {MAXLV['ps_atk']}·{MAXLV['ps_atk2']}·{MAXLV['ps_atk2p']}·{MAXLV['ps_atk3']}·{MAXLV['ps_atk3p']}·{MAXLV['ps_atk4']} · 다 사면 {cum}")
    print(f"   레벨당 {per} · 단계 사이 ×{per[1]/per[0]:.2f} ×{per[2]/per[1]:.2f} ×{per[3]/per[2]:.2f}")
    print(f"   단계 끝 누적 {e} · 비 ×{e[1]/e[0]:.2f} ×{e[2]/e[1]:.2f} ×{e[3]/e[2]:.2f} · 단계 증분 {[round(e[0]-1,1)]+[round(e[i]-e[i-1],1) for i in (1,2,3)]}")
    print("   레벨별 +%:", " ".join(f"{TIER_OF[k]}{L}:{p*100:.0f}" for k, L, d, p in seq))
    # 효과 쪽 넘김: 다음 단계 첫 레벨 % ÷ 앞 단계 마지막 레벨 %
    firsts = {}
    for i, (k, L, d, p) in enumerate(seq):
        if L == 1 and i > 0: firsts[k] = (p, seq[i - 1][3], p / seq[i - 1][3])
    print("   넘김(첫 레벨 % ÷ 직전 레벨 %):", {TIER_OF[k]: round(v[2], 2) for k, v in firsts.items()})
CUR = "A"

print("\n" + "=" * 70, "\n2. 가격표 (theory plan.solve, 곡선 A, MARGIN_FIRST 0.65, MARGIN_LAST 0.4909) — 데이터로만 사용\n" + "=" * 70)
import boss as B, plan as P, r212 as R    # noqa
R.patch(R.CURVES["A"]); P.MARGIN_FIRST = 0.65; P.MARGIN_LAST = 0.4909
hp_raw, _, PRICES, _ = P.solve()
print("theory hp_raw:", {k: round(v, 1) for k, v in hp_raw.items()})
# 효율 넘김(가격 포함): 공격력 % 증가 ÷ 가격, 다음 단계 첫 레벨 vs 앞 단계 마지막 레벨 (P7 ≥ ×1.5)
cum, prev = 1.0, None
for k in ATK:
    for L in range(MAXLV[k]):
        d = efx(k, L + 1) - efx(k, L); eff = (d / cum) / PRICES[k][L]
        if L == 0 and prev:
            print(f"P7(공격력 DPS 기준) {TIER_OF[prev[0]]}{prev[1]}→{TIER_OF[k]}1: 효율 비 ×{eff/prev[2]:.3g} (가격 {prev[3]:.3g} → {PRICES[k][0]:.3g})")
        prev = (k, L + 1, eff, PRICES[k][L]); cum += d
seqp = sorted(p for k in GOLD for p in PRICES[k])
print("가격 모양: 정렬 최대 뜀 ×%.1f · 노드 안 최대 ×%.1f" % (max(b / a for a, b in zip(seqp, seqp[1:])), max(PRICES[k][L + 1] / PRICES[k][L] for k in GOLD for L in range(MAXLV[k] - 1))))

# ---------------------------------------------------------------- 독립 진행 모델
FIT = json.load(open(os.path.join(HERE, "theory_fit_tuned.json"), encoding="utf-8"))["params"]
INC_BIAS = [0.218, 0.485, 0.542, 0.542, 0.542]
A4 = [(1, 1.1), (10, 1.12), (15, 1.12), (20, 1.24), (25, 1.31), (30, 1.36), (35, 1.49), (40, 1.78), (45, 2.01), (55, 2.07), (60, 2.18), (80, 2.21), (400, 2.21)]
def a4(r):
    for (a, va), (b, vb) in zip(A4, A4[1:]):
        if r <= b: return va + (vb - va) * (r - a) / (b - a)
    return 2.21
BOSS_KEYS = [b["Boss_Key"] for b in DB["Boss"]]
DEF_MUL = {"king": 0.21, "cleaner": 0.184, "angel": 1.13, "phantom": 2.83, "mover": 5.5}
def boss_of(st): return BOSS_KEYS[min(st // 10, len(BOSS_KEYS)) - 1]
def boss_hp(st, mul): return TIER_HP[3] * C["BOSS_HP_K"] * zm(st) * mul[boss_of(st)]   # Defs.Boss.cs:51~54 (60~는 마지막 Hp_Mul)

def stats(lv, st, r, w, o):
    L = lambda k: lv.get(k, 0)
    flat = 1 + sum(efx(k, L(k)) for k in ATK) + (3 if o.get("poison_r") and r >= o["poison_r"] else 0)
    atk = flat * a4(r) * o.get("a4_mul", 1) * 1.05 ** L("inf_atk")
    col = max(3, math.ceil(12 + 5 * efx("ed_cols", L("ed_cols")) + efx("ed_cols2", L("ed_cols2"))))
    area = (1 / [1.5, 1.35, 1.2, 1.1, 1.0][L("md_map")] ** 2) ** 0.7
    return dict(flat=flat, atk=atk, heavy=efx("ps_heavy", L("ps_heavy")), crit=efx("ps_crit", L("ps_crit")), cm=efx("ps_crit2", L("ps_crit2")),
                spd=efx("ps_spd", L("ps_spd")) * (0.8 if w == "storm" else 1), balls=1 + efx("ps_ball", L("ps_ball")) + efx("ps_ball2", L("ps_ball2")) + efx("ps_ball3", L("ps_ball3")),
                dur=15 + min(10, 0.5 * (st - 1)) + efx("ps_dur", L("ps_dur")) + efx("ps_dur2", L("ps_dur2")) - (3 if w == "cold" else 0),
                temp=any(L(k) > 0 for k in ("md_kid", "split", "clone", "barclone")),
                cols=max(4, round(col * area)), comboK=efx("md_combo", L("md_combo")) + efx("md_combo2", L("md_combo2")),
                sflat=sum(efx(k, L(k)) for k in ("ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p")),
                nsk=sum(1 for k in SKILLS if L(k) > 0), Z=zm(st), zone=zi(st), ginf=1.025 ** L("inf_gold"))

def income(s):
    c = FIT["coef"]; z = [0, c[4], c[5], c[6], c[6]][s["zone"]]
    return 10 ** (math.log10(s["Z"] * (FIT["sbar"] + s["sflat"]) * s["dur"] * s["comboK"]) + c[0] + c[1] * math.log10(s["balls"])
                  + c[2] * math.log10(s["cols"]) + c[3] * s["nsk"] + z + INC_BIAS[s["zone"]]) * s["ginf"]

def gate(boss, hp, s, g):
    A = s["atk"] * s["heavy"] * (1 + s["crit"] * (s["cm"] - 1))
    H = g["H0"] * s["spd"] * (1 + 0.5 * (s["balls"] - 1)) * (g["MOVER_H"] if boss == "mover" else 1) * ((1 + g["TAU"]) if boss != "angel" and s["temp"] else 1)
    T, d = s["dur"] - 2.0, A * H
    if boss == "cleaner":
        rr = C["CLEANER_HEAL"] / C["CLEANER_INT"]; t = hp / (d - rr * hp) if d > rr * hp else math.inf
    elif boss == "phantom": t = 1.99 * hp / d
    else: t = hp / d
    return t, T, A

def buy(lv, gold, prices, sink):
    while True:
        best = None
        for k in GOLD + (["inf_atk", "inf_gold"] if sink else []):
            Lk = lv.get(k, 0)
            if k.startswith("inf_"):
                if lv.get({"inf_atk": "ps_atk4", "inf_gold": "ed_score3p"}[k], 0) < MAXLV[{"inf_atk": "ps_atk4", "inf_gold": "ed_score3p"}[k]]: continue
                p = sink * 1.076 ** Lk
            else:
                if Lk >= MAXLV[k] or (PARENT[k] and lv.get(PARENT[k], 0) < 1) or (REQ[k] and lv.get(REQ[k], 0) < MAXLV[REQ[k]]): continue
                p = prices[k][Lk]
            if best is None or p < best[0]: best = (p, k)
        if best is None or best[0] > gold: return gold
        gold -= best[0]; lv[best[1]] = lv.get(best[1], 0) + 1

def prog(mul=None, g=None, inc_mul=1.0, retry=5, seed=None, sink=None, stop=50, maxr=700, inc_fn=None, **o):
    """seed=None: 보스 날씨 storm→wind→cold 순환(theory와 같은 규칙). seed=n: 보스 판마다 3종 중 무작위(Game.RollWeather)."""
    mul = mul or DEF_MUL; g = dict(dict(H0=0.9, MOVER_H=0.6, TAU=0.2), **(g or {}))
    rng = random.Random(seed); lv, gold, t, st, r, wc = {}, 0.0, 0.0, 1, 0, 0
    boss_on, last_try, last_norm, log, done, rec = True, 0, 0.0, {}, {}, []
    while r < maxr:
        r += 1
        isb = st % 10 == 0
        if isb and boss_on:
            w = ["storm", "wind", "cold"][wc % 3] if seed is None else rng.choice(["storm", "wind", "cold"]); wc += 1
            s = stats(lv, st, r, w, o); b = boss_of(st); hp = boss_hp(st, mul)
            tk, T, A = gate(b, hp, s, g); ok = tk <= T
            hits = min(tk, T) * (A * g["H0"]) / max(A, 1e-9)   # 근사: 타 수 (보스 타격 점수만 씀, 0.05배라 결과에 거의 무관)
            sc = hits * (TIER_SCORE[3] + s["sflat"]) * s["Z"] * 3 * (1 + 5 * s["comboK"]) * C["BOSS_SCORE_MUL"] * inc_mul
            e = log.setdefault(st, dict(first_r=r, first_min=t / 60, tries=0, atk_first=s["atk"], r1=tk / T)); e["tries"] += 1; last_try = r
            if ok:
                inc = sc + max(sc, last_norm) * C["BOSS_REWARD_MUL"]          # RoundSim.Effects.cs:351
                sec = tk + 2.0 + 1.5 + 30; t += sec
                e.update(kill_r=r, kill_min=t / 60, atk_kill=s["atk"]); st += 1
            else:
                inc = sc * C["BOSS_FAIL_GOLD"]; sec = s["dur"] + 30; t += sec; boss_on = False
        else:
            s = stats(lv, st, r, "clear", o); inc = income(s) * inc_mul * (inc_fn(s, lv, st, r) if inc_fn else 1)
            last_norm = inc                                                       # RoundSim.Effects.cs:356 (!BossRound)
            t += s["dur"] + 30
            if not isb: st += 1
        gold = buy(lv, gold + inc, PRICES, sink)
        if st % 10 == 0 and not boss_on:
            s2 = stats(lv, st, r + 1, "wind", o)
            if gate(boss_of(st), boss_hp(st, mul), s2, g)[0] <= gate(boss_of(st), boss_hp(st, mul), s2, g)[1] or r - last_try >= retry: boss_on = True
        if st % 10 == 0 and st not in log: boss_on = True
        rec.append((r, st, inc, s["atk"], dict(lv)))
        for tr in (1, 2, 3):
            if tr not in done and all(lv.get(k, 0) >= MAXLV[k] for k in GOLD if UP[k]["Tier"] == tr): done[tr] = r
        if st > stop: break
    return dict(log=log, done=done, r=r, min=t / 60, rec=rec, lv=lv)

def fmt(res, st=50):
    e = res["log"].get(st, {})
    return f"R{e.get('kill_r', '갇힘')} · {e.get('kill_min', res['min']):.0f}분" if e.get("kill_r") else f"갇힘(R{res['r']}까지)"

print("\n" + "=" * 70, "\n3. 독립 진행 — 보스별 (Hp_Mul king 0.21 · cleaner 0.184 · angel 1.13 · phantom 2.83 · mover 5.5)\n" + "=" * 70)
base = prog()
th = json.load(open(os.path.join(HERE, "theory_r212.json"), encoding="utf-8"))["bosses"]
print("| 스테이지 | 보스 | 체력 | 첫 도전 R·분·공격력 | 처치 R·분 | 도전 | W | 처치 공격력 | r₁(바람) | theory 처치 R·분·도전 |")
for i, st in enumerate((10, 20, 30, 40, 50)):
    e = base["log"][st]; tt = th[i]
    print(f"| {st} | {boss_of(st)} | {boss_hp(st, DEF_MUL):,.1f} | R{e['first_r']} · {e['first_min']:.1f} · {e['atk_first']:.1f} | R{e.get('kill_r')} · {e.get('kill_min', 0):.1f} | {e['tries']} | {e.get('kill_r', 0) - e['first_r']} | {e.get('atk_kill', 0):.1f} | {e['r1']:.2f} | R{tt['kill_r']} · {tt['kill_min']} · {tt['tries']} |")
full = prog(mul=dict(DEF_MUL), stop=60, maxr=300, g=None)
print("단계 완료 판(스테이지 60 보스 무한 체력 대신 stop 60):", full["done"])

print("\n-- 3b. 첫 도전 10·20: 도착 판 능력치, 날씨 3종 × H0 0.9/0.63 → r = t/T --")
for st in (10, 20):
    e = base["log"][st]; lv = base["rec"][e["first_r"] - 2][4]
    for H0 in (0.9, 0.63):
        rs = [gate(boss_of(st), boss_hp(st, DEF_MUL), stats(lv, st, e["first_r"], w, {}), dict(H0=H0, MOVER_H=0.6, TAU=0.2)) for w in ("storm", "wind", "cold")]
        print(f"  {st} H0 {H0}: " + " · ".join(f"{w} {t/T:.3f}" for w, (t, T, _) in zip(("storm", "wind", "cold"), rs)))
    for pr in (8, 13, 18):   # poison 세트가 일찍 모인 경우: 공격력 +3 → 첫 도전 판에 의미 없음(이미 처치), 벽에 영향
        pass

print("\n-- 3c. 무작위 보스 날씨(Game.RollWeather, 시드 200개) --")
K = {st: [] for st in (10, 20, 30, 40, 50)}; first_ok = {10: 0, 20: 0}
for sd in range(200):
    rr = prog(seed=sd)
    for st in K: K[st].append(rr["log"][st].get("kill_r", 999))
    for st in (10, 20): first_ok[st] += rr["log"][st]["tries"] == 1
q = lambda v, p: sorted(v)[int(p * (len(v) - 1))]
for st in K: print(f"  {st}: 처치 판 p10 {q(K[st], .1)} · 중앙 {q(K[st], .5)} · p90 {q(K[st], .9)}")
print("  첫 도전 처치율:", {st: f"{v/200:.0%}" for st, v in first_ok.items()})

print("\n" + "=" * 70, "\n4. mover Hp_Mul 계단 (5.0~6.5, 0.1 간격) — 결정적 순환 / 무작위 날씨 중앙(시드 60)\n" + "=" * 70)
for m10 in range(50, 66):
    m = m10 / 10; mm = dict(DEF_MUL, mover=m)
    d = prog(mul=mm)["log"][50]; ks = [prog(mul=mm, seed=sd)["log"][50].get("kill_r", 999) for sd in range(60)]
    print(f"  {m:.1f}: 순환 R{d.get('kill_r')} ({d.get('kill_min', 0):.0f}분) · 무작위 p10/중앙/p90 R{q(ks, .1)}/{q(ks, .5)}/{q(ks, .9)}")

print("\n" + "=" * 70, "\n5. 민감도 (스테이지 50 첫 처치) — 싱크 없음 / 싱크 있음(c0 = 스테이지 50 직전 일반 판 × 3)\n" + "=" * 70)
W = dict(H0=0.63, MOVER_H=0.4)
cases = [("기준", {}), ("H0×0.7", dict(g=dict(H0=0.63))), ("H0×1.3", dict(g=dict(H0=1.17))), ("MOVER_H 0.4", dict(g=dict(MOVER_H=0.4))),
         ("H0×0.7+MOVER 0.4", dict(g=W)), ("H0×0.7+MOVER 0.4+TAU 0", dict(g=dict(W, TAU=0.0))),
         ("수입×0.5", dict(inc_mul=0.5)), ("수입×0.7", dict(inc_mul=0.7)), ("수입×1.5", dict(inc_mul=1.5)), ("수입×2", dict(inc_mul=2.0)),
         ("A4×0.85(별 느림)", dict(a4_mul=0.85)), ("poison +3 R30부터", dict(poison_r=30)), ("poison +3 R10부터", dict(poison_r=10))]
C0 = 3 * [x[2] for x in base["rec"] if x[1] == 50 and x[0] < base["log"][50]["kill_r"]][-1]
print(f"싱크 시작가 c0 = 스테이지 50 마지막 일반(파밍) 판 × 3 = {C0:.3g} (theory 3.63e12)")
for name, kw in cases:
    a, b = prog(**kw), prog(sink=C0, **kw)
    print(f"  {name:26} {fmt(a):22} | 싱크 {fmt(b):18} 싱크 레벨 {b['lv'].get('inf_atk', 0)}/{b['lv'].get('inf_gold', 0)} · 도전 {[a['log'][s]['tries'] if s in a['log'] else '-' for s in (10, 20, 30, 40, 50)]}")

print("\n" + "=" * 70, "\n6. 수입-공격력 결합 민감도 (관리자 지적). 일반 판 수입 × m, m = clip((h_old/h_new)^α, floor, 1)\n"
      "   h = 흔함 버섯 타수 = 체력 1×ZoneMul ÷ (공격력 × heavy). h_old는 같은 레벨을 옛 Efx로 계산\n" + "=" * 70)
def make_fn(alpha, floor, only_zone=None):
    def fn(s, lv, st, r):
        global CUR
        if only_zone is not None and s["zone"] < only_zone: return 1.0
        CUR = "old"; f_old = 1 + sum(efx(k, lv.get(k, 0)) for k in ATK); CUR = "A"
        f_new = 1 + sum(efx(k, lv.get(k, 0)) for k in ATK)
        h_new = max(1.0, zm(st) / (f_new * a4(r))); h_old = max(1.0, zm(st) / (f_old * a4(r)))
        return max(floor, min(1.0, (h_old / h_new) ** alpha))
    return fn
for name, fn in (("α 0.25 하한 0.67(피버 ×1.5 잃음)", make_fn(0.25, 0.67)), ("α 0.5 하한 0.5(피버 ×2 잃음)", make_fn(0.5, 0.5)),
                 ("α 1 하한 0.5", make_fn(1.0, 0.5)), ("들판만 ×0.67 고정", lambda s, lv, st, r: 0.67 if s["zone"] >= 2 else 1.0),
                 ("들판만 ×0.5 고정", lambda s, lv, st, r: 0.5 if s["zone"] >= 2 else 1.0)):
    a, b = prog(inc_fn=fn), prog(inc_fn=fn, sink=C0)
    aw = prog(inc_fn=fn, g=W)
    ms = [round(fn(stats(x[4], x[1], x[0], "clear", {}), x[4], x[1], x[0]), 2) for x in base["rec"] if x[0] in (20, 30, 45, 60, 85, 120, 160)]
    print(f"  {name:30} 기준 {fmt(a):16} · 싱크 {fmt(b):16} · 최악H {fmt(aw):18} · m(R20/30/45/60/85/120/160) {ms}")
print("  흔함 타수 h_new / h_old (기준 진행):", [(x[0], round(max(1, zm(x[1]) / (x[3])), 2)) for x in base["rec"] if x[0] in (20, 30, 45, 60, 85, 120, 160)])

print("\n-- 6b. 결합이 있어도 가격을 다시 맞추면(관리자 순서: 곡선 → 가격 맞추기 → Hp_Mul 재산출) 흡수되는가: 단계 3 가격 × s --")
P0 = {k: list(v) for k, v in PRICES.items()}
for name, fn in (("들판 ×0.67", lambda s_, lv, st, r: 0.67 if s_["zone"] >= 2 else 1.0), ("들판 ×0.5", lambda s_, lv, st, r: 0.5 if s_["zone"] >= 2 else 1.0)):
    for sc in (1.0, 0.67, 0.5):
        for k in GOLD:
            PRICES[k] = [x * (sc if UP[k]["Tier"] == 3 else 1) for x in P0[k]]
        a = prog(inc_fn=fn); f = prog(inc_fn=fn, stop=60, maxr=400)
        aw = prog(inc_fn=fn, g=W, sink=C0)
        print(f"  {name} · 단계3 가격 ×{sc}: 스테이지 50 {fmt(a)} · 단계 완료 {f['done']} · 최악H+싱크 {fmt(aw)}")
for k in GOLD: PRICES[k] = list(P0[k])
print("  (참고) 결합 없음 + 최악H + 싱크:", fmt(prog(g=W, sink=C0)))

print("\n" + "=" * 70, "\n7. 시뮬 기록으로 결합을 잴 수 있나 (round2_t1a 시드 1, 옛 곡선)\n" + "=" * 70)
d = json.load(open(r"C:/Project/MoreMustMush/BalanceData/round2_t1a/phase_s1_k0.6.json"))
ks = d["kill"]
print(f"  처치율 {len(ks)}개: 0.9 이상 {sum(k >= 0.9 for k in ks)} · 0.3~0.9 {sum(0.3 <= k < 0.9 for k in ks)} (숲 R1~9) · 0 {sum(k == 0 for k in ks)}")
print("  → 옛 곡선 기록은 R12 이후 처치율 ≥ 0.9뿐이라 '처치율이 낮을 때 수입'을 볼 표본이 없다(측정 필요)")

print("\n" + "=" * 70, "\n8. 가격 맞추기 판정 창 (BalanceJob.cs:21,29 FINISH_MIN 0.8 · TIER_LATE 1.1) vs 로그의 시드 퍼짐\n" + "=" * 70)
for t, e in ((1, 20), (2, 88), (3, 172)):
    print(f"  단계 {t}: 창 R{math.ceil(e*0.8)}~R{int(e*1.1)} (폭 {int(e*1.1)-math.ceil(e*0.8)}판)")
print("  fit1 로그 단계 1 퍼짐: 반복 1 R25~31(6) · 3 R26~30(4) · 5 R19~27(8) · 7 R20~25(5) · 9 R22~26(4)")
print("  고정점 배율 1 − d(1 + k): k 2.6 → %.2f · k 3.3 → %.2f · d 0.3, k 3.3 → %.2f" % (1 - 0.5 * 3.6, 1 - 0.5 * 4.3, 1 - 0.3 * 4.3))

print("\n" + "=" * 70, "\n9. 갇힘: 트리 전부(골드 노드 최대, A4 2.21) 능력치로 스테이지 50 t/T (싱크 없음)\n" + "=" * 70)
FULL = {k: MAXLV[k] for k in GOLD}
for w in ("storm", "wind", "cold"):
    s = stats(FULL, 50, 400, w, {})
    out = []
    for name, g in (("기준", dict(H0=0.9, MOVER_H=0.6, TAU=0.2)), ("H0×0.7", dict(H0=0.63, MOVER_H=0.6, TAU=0.2)), ("MOVER 0.4", dict(H0=0.9, MOVER_H=0.4, TAU=0.2)),
                    ("H0×0.7+MOVER 0.4", dict(H0=0.63, MOVER_H=0.4, TAU=0.2)), ("+TAU 0", dict(H0=0.63, MOVER_H=0.4, TAU=0.0))):
        t, T, _ = gate("mover", boss_hp(50, DEF_MUL), s, g); out.append(f"{name} {t/T:.2f}")
    print(f"  {w:5} 공격력 {s['atk']:.1f}: " + " · ".join(out))
print("  싱크로 푸는 데 필요한 inf_atk 레벨(바람, H0×0.7+MOVER 0.4): t/T ÷ 1.05^L ≤ 1 →", end=" ")
s = stats(FULL, 50, 400, "wind", {}); t, T, _ = gate("mover", boss_hp(50, DEF_MUL), s, dict(H0=0.63, MOVER_H=0.4, TAU=0.2))
print(math.ceil(math.log(t / T) / math.log(1.05)) if t > T else 0)
