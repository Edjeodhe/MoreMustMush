# R2-15 싱크 노드(inf_atk 11032 / inf_gold 13032 -> Cost 90088 / 90089) 시작가 재산출 (manager#14)
# 입력: BalanceData/round2_sweep/m15_s{1,2,3}.json (관문 모드 230판, 지금 시작가 3.63e12로 싱크까지 산 기록)
#       BalanceData/round2_fit4/tuned_prices.json (트리 가격)
# 출력: 표(표준출력), BalanceData/theory/r215_out.json, BalanceData/db/r215_apply.json
# 다시 돌려도 같은 결과(난수 없음).
import json, math, os, statistics as st

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
K = 1.076            # 싱크 가격 증가율 (Cost_Lv2/Cost_Lv1)
E_ATK, E_GOLD = 1.05, 1.025
P0_NOW = 3.63e12
T_GATE = 37.0        # 관문 처치 문턱(초): 기록상 처치 최대 37.1, 실패 최소 38.8
CANDS = [("스테이지49 판x3 (옛 규칙 그대로)", 1.3e11), ("부모 마지막 가격 (ps_atk4 Lv3)", 2.5e12),
         ("열림 판 수입x3", None), ("지금", P0_NOW), ("열림 판 수입x5", "x5"), ("트리 최고가 (fest Lv4)", 7.5e12), ("1e13", 1e13)]

def load(s):
    return json.load(open(os.path.join(ROOT, f"BalanceData/round2_sweep/m15_s{s}.json"), encoding="utf-8"))

prices = json.load(open(os.path.join(ROOT, "BalanceData/round2_fit4/tuned_prices.json"), encoding="utf-8"))

def sink_levels_obs(d):
    """기록의 공격력 변화에서 inf_atk 레벨(×1.05 계단)을 센다. atkR[r]는 r판 시작 값 = r-1판 뒤 구매 반영."""
    a = d["atkR"]; L = [0] * len(a); lv = 0; opened = None
    for r in range(1, len(a)):
        q = math.log(a[r] / a[r - 1]) / math.log(E_ATK) if a[r - 1] > 0 else 0
        if opened is None and r >= 140 and 2.9 < q < 3.1:   # ps_atk4 Lv3 (+15 평 = ×1.159) 산 판
            opened = r - 1                                    # 0-기준: 이 판(opened) 정산 뒤 부모 최대
        elif opened is not None and abs(q - round(q)) < 0.05 and q > 0.5:
            lv += round(q)
        L[r] = lv
    return opened, L

def remaining_tree(opened_price=2.5e12):
    # 부모(ps_atk4 Lv3 2.5e12)를 산 순간, 가장 싼 것부터 사는 봇은 그보다 싼 열린 노드를 다 샀다 -> 남은 것은 2.5e12 이상
    rem = [(v, k, i + 1) for k, vs in prices.items() for i, v in enumerate(vs) if v >= opened_price]
    rem = [x for x in rem if not (x[1] == "ps_atk4" and x[2] == 3)]
    return sorted(rem)

def replay(d, opened, P0, Lobs, g_feedback=True):
    """열린 판 다음부터 가장 싼 것부터 사기. 판 수입은 기록값 × 1.025^(후보 gold 레벨 - 기록 gold 레벨)."""
    inc = d["incR"]; n = len(inc)
    tree = [v for v, _, _ in remaining_tree()]
    la = lg = 0; gold = 0.0; tree_done = None
    LA = [0] * n; LG = [0] * n
    for r in range(opened + 1, n):
        lg_obs = Lobs[r]                     # 기록: gold 레벨 ≈ atk 레벨 (같은 가격·번갈아 산다, 최종 state 101/101 확인)
        mul = E_GOLD ** (lg - lg_obs) if g_feedback else 1.0
        gold += inc[r] * mul
        while True:
            pa, pg = P0 * K ** la, P0 * K ** lg
            pt = tree[0] if tree else float("inf")
            c = min(pa, pg, pt)
            if c > gold: break
            gold -= c
            if c == pt: tree.pop(0)
            elif c == pa: la += 1
            else: lg += 1
        if not tree and tree_done is None: tree_done = r + 1
        if r + 1 < n: LA[r + 1] = la; LG[r + 1] = lg
    return LA, LG, tree_done

def boss_effect(d, LA, Lobs):
    """스테이지 60~100 보스: 처치 판에 후보 공격력이 문턱을 넘는지. 못 넘으면 넘을 때까지 판(벽 판 수입이 이어진다는 근사)."""
    out = {}
    S, B, G, A = d["stageR"], d["bossR"], d["gateR"], d["atkR"]
    for n in (60, 70, 80, 90, 100):
        rounds = [r for r in range(len(S)) if S[r] == n and B[r] in (1, -1)]
        if not rounds: continue
        req = st.median([A[r] * G[r] / T_GATE for r in rounds])   # 필요 공격력 (처치 시간 ∝ 1/공격력)
        k = [r for r in rounds if B[r] == 1][0]
        first_try = rounds[0]
        atk_c = lambda r: A[r] * E_ATK ** (LA[r] - Lobs[r])
        # 처치 가능한 첫 판 (기록 판 시간축 그대로)
        ok = [r for r in range(first_try, len(S)) if atk_c(r) >= req]
        kc = ok[0] if ok else None
        out[n] = dict(obs=k + 1, cand=(kc + 1) if kc is not None else None, slack=atk_c(first_try) / req)
    return out

res = {}
for s in (1, 2, 3):
    d = load(s); opened, Lobs = sink_levels_obs(d)
    inc = d["incR"]
    I_open = st.median(inc[opened + 1: opened + 11])
    I_farm = st.median([inc[r] for r in range(opened - 20, opened + 1) if d["bossR"][r] == 2])
    I49 = st.median([inc[r] for r in range(len(inc)) if d["stageR"][r] == 49])
    rows = []
    for name, P in CANDS:
        P0 = 3 * I_open if P is None else 5 * I_open if P == "x5" else P
        LA, LG, td = replay(d, opened, P0, Lobs)
        r10 = min(opened + 11, len(inc) - 1)
        be = boss_effect(d, LA, Lobs)
        rows.append(dict(name=name, P0=P0, D0=P0 / I_open, atk10=LA[r10], gold10=LG[r10],
                         atk_done=LA[min(td or 0, len(inc) - 1)] if td else None, tree_done=td,
                         first_atk=next((r for r in range(len(LA)) if LA[r] > 0), None),
                         L200=LA[199], L230=LA[-1], boss={k: v["cand"] for k, v in be.items()},
                         boss_obs={k: v["obs"] for k, v in be.items()}))
    # 지금 시작가 재현 확인 (공격력 싱크 레벨: 기록 vs 재현)
    LAnow, _, tdnow = replay(d, opened, P0_NOW, Lobs)
    chk = [(r + 1, Lobs[r], LAnow[r]) for r in (opened + 11, 184, 190, 200, 215, 229)]
    res[s] = dict(opened=opened + 1, stage_open=d["stageR"][opened], I_open=I_open, I_farm=I_farm, I49=I49,
                  tree_done_obs=d["doneR"][3], tree_done_replay=tdnow, check=chk, rows=rows)

for s, r in res.items():
    print(f"\n== 시드 {s}: 부모 최대 R{r['opened']} (스테이지 {r['stage_open']}) · 열린 뒤 10판 수입 중앙값 {r['I_open']:.2e} · 직전 파밍 {r['I_farm']:.2e} · 스테이지49 {r['I49']:.2e}")
    print(f"   재현 확인(판, 기록 atk싱크Lv, 재현Lv): {r['check']} · 트리 완료 기록 R{r['tree_done_obs']} / 재현 R{r['tree_done']}" if False else
          f"   재현 확인(판, 기록Lv, 재현Lv): {r['check']} · 트리 완료 기록 R{r['tree_done_obs']} / 재현 R{r['tree_done_replay']}")
    for x in r["rows"]:
        print(f"   {x['name']:<26} P0 {x['P0']:.2e} D0 {x['D0']:5.1f}판 | 10판 atk {x['atk10']:3d} gold {x['gold10']:3d} | 첫 atk판 R{x['first_atk']} | 트리완료 R{x['tree_done']} | L@R200 {x['L200']} L@R230 {x['L230']} | 보스 {x['boss']} (기록 {x['boss_obs']})")

json.dump(res, open(os.path.join(ROOT, "BalanceData/theory/r215_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
