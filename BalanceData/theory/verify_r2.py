# verify 2회차 1단계: 보스 체력·관문 시간·기믹 배율을 코드 공식대로 독립 계산 (theory 코드 안 씀)
# 근거: Defs.Boss.cs:50 BossHp, Defs.World.cs:59-65 ZoneMul, Defs.cs:71-77 TIERS, Defs.cs:106-107 BASE_TIME/StageTime,
#       Game.cs:225 duration, Game.cs:217-218 atk, RoundSim.Balls.cs:292-298 Damage, RoundSim.Boss.cs 기믹, BalanceSim.cs:36-43 관문
import json, math, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
DB = json.load(open(r"C:/Project/MoreMustMush/Assets/Resources/DB/db.json", encoding="utf-8"))
C = {c["Const_Key"]: c["Value"] for c in DB["Const"]}
BOSS = [(b["Boss_Key"], b["Hp_Mul"], b["Look"]) for b in DB["Boss"]]
THEME_FROM = [1, 20, 45, 75, 105]           # Defs.World.cs:43-47
LEG_HP = {"tuned": 40, "spec": 100}         # Defs.cs:90-91 프리셋 hp[3]
LOOK_TIER = {"송이버섯": 3, "선녀낙엽버섯": 1, "독우산광대버섯": 2, "광대버섯": 1, "동충하초": 3}  # Defs.cs:153,183,204,195,179
TIER_SCORE = [1, 5, 20, 100]                 # Defs.cs:73-76 (프리셋이 score는 안 바꿈)

def latest_idx(s): return max(i for i, f in enumerate(THEME_FROM) if s >= f)
def zone_mul(s): return C["ZONE_STEP"] ** latest_idx(s) * C["STAGE_GROWTH"] ** (s - 1)
def boss_at(s): return BOSS[(s // int(C["BOSS_EVERY"]) - 1) % len(BOSS)]
def boss_hp(s, preset="tuned", k=None):
    b = boss_at(s); return LEG_HP[preset] * (k or C["BOSS_HP_K"]) * zone_mul(s) * b[1]
def dur(s, extra=0, cold=False): return 15 + min(10, 0.5 * (s - 1)) + extra - (3 if cold else 0)

HEAL, INT = C["CLEANER_HEAL"], C["CLEANER_INT"]
def cleaner_T(T0):   # 연속 근사: 피해 d·t = HP(1 + HEAL·t/INT) → t = T0/(1 − HEAL·T0/INT)
    x = 1 - HEAL * T0 / INT
    return T0 / x if x > 0 else math.inf
def cleaner_T_discrete(T0):  # 회복은 INT초마다 계단 (RoundSim.Boss.cs:74-80)
    t, n = T0, 0
    for _ in range(1000):
        n2 = math.floor(t / INT); t2 = T0 * (1 + HEAL * n2)
        if abs(t2 - t) < 1e-9: return t2
        t = t2
    return math.inf
def phantom_mul(hits):   # 본체 1 + 66% 시점 분신(그때 체력) + 33% 시점 분신. hits = 본체를 잡는 타 수
    p = 1 / hits; hp, clones = 1.0, []
    for th in (0.66, 0.33):
        while hp > th: hp -= p
        clones.append(max(hp, 0))
    return 1 + sum(clones)

print("== 보스 체력 (tuned, BOSS_HP_K=%.2f) ==" % C["BOSS_HP_K"])
print("스테이지 | 보스 | 지역 | ZoneMul | 체력 | 판 시간(기본/한파) | H=1.2 첫 판 처치 공격력(기본/한파) | 이전 보스 대비 체력")
prev = None
for s in range(10, 101, 10):
    b = boss_at(s); hp = boss_hp(s); D, Dc = dur(s), dur(s, cold=True)
    eff = 1.0
    if b[0] == "phantom": eff = phantom_mul(20)
    if b[0] == "cleaner":   # 판 시간 D 안 처치 조건: T0 ≤ D/(1 + HEAL·D/INT)
        need = lambda D_: hp / (1.2 * (D_ / (1 + HEAL * D_ / INT)))
    else:
        need = lambda D_: hp * eff / (1.2 * D_)
    print(f"{s} | {b[0]} | {latest_idx(s)} | {zone_mul(s):.3f} | {hp:,.1f} | {D:.1f}/{Dc:.1f} | {need(D):,.2f}/{need(Dc):,.2f} | {'' if prev is None else f'x{hp/prev:.2f}'}")
    prev = hp

print("\n검산: 스테이지 10 대왕송이 공격력 5.5 H=1.2 →", round(boss_hp(10) / (5.5 * 1.2), 2), "초 (주석 '약 16초')")
print("spec 프리셋이면 체력 ×", LEG_HP["spec"] / LEG_HP["tuned"])

print("\n== 청소부 회복: 무회복 처치 시간 T0 → 실제 처치 시간 ==")
for T0 in (5, 10, 15, 20, 30, 45, 59):
    print(f"T0 {T0}s → 연속 {cleaner_T(T0):.1f}s (×{cleaner_T(T0)/T0:.2f}) · 계단 {cleaner_T_discrete(T0):.1f}s")

print("\n== 환상 분신: 본체 처치 타 수 → 유효 체력 배율 (분신 체력 = 분신 순간 본체 체력) ==")
for n in (1, 2, 3, 5, 10, 20, 40, 100):
    print(f"{n}타 → ×{phantom_mul(n):.3f}  (×Hp_Mul 0.6 = {0.6*phantom_mul(n):.3f})")

print("\n== 보스 판 타격당 점수 기본값 (TIERS[look 등급].score + scoreFlat) × 3 ==")
for k, m, look in BOSS:
    t = LOOK_TIER[look]; print(f"{k}: look {look} 등급 {t} → 기본 점수 {TIER_SCORE[t]} (대왕송이 대비 ×{TIER_SCORE[t]/100:.2f}, scoreFlat=0일 때)")

print("\n== H 민감도: 첫 판 처치에 필요한 공격력 (기본 날씨 판 시간, 보스 피해 배율 1) ==")
for s in (10, 20, 30, 40, 50):
    b = boss_at(s); hp = boss_hp(s); D = dur(s)
    row = []
    for H in (0.84, 1.2, 1.56):
        if b[0] == "cleaner": a = hp / (H * D / (1 + HEAL * D / INT))
        elif b[0] == "phantom": a = hp * phantom_mul(20) / (H * D)
        else: a = hp / (H * D)
        row.append(f"H{H}: {a:,.2f}")
    print(s, b[0], " | ".join(row))

print("\n== 공격력 상한 (Game.cs:217-218, db.json) ==")
U = {u["Upg_Key"]: u for u in DB["Upgrade"]}
flat = 1 + sum(U[k][f"Efx_Lv{U[k]['Efx_MaxLv']}"] for k in ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4")) + 3
print("atkFlat 최대(독버섯 세트 +3 포함) =", flat, "→ atk = atkFlat × (1.1 + 별 atk) × gm_atk(최대 1.3)")
for k in ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"):
    u = U[k]; print(k, [u[f"Efx_Lv{i}"] for i in range(u["Efx_MaxLv"] + 1)])
