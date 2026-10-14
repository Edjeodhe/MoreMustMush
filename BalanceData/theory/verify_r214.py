# -*- coding: utf-8 -*-
"""verify R2-14 (manager#13): mover Hp_Mul 15 반사실을 theory 코드(r214.py) 없이 따로 계산.

다른 점 (theory와 일부러 다르게):
 - 능력치 상태 변수: 누적 지출 C (판 수입 - 보유골드 변화, 로그는 내 파서로 읽음)
 - 보스 공통 위력 Q = 관문 기록에서 코드 공식(BalanceSim.BossGateTime)으로 역산. 사이는 '직전 샘플 Q × 공격력비 × 핀볼항비'(계단),
   theory는 잔차를 log 보간 → 두 방식 차이 = 보간 오차
 - 재도전: 코드 규칙 그대로 (판 시작 때 ++botFarm >= 5 또는 직전 판에 공격 관련 구매) → 간격 5판. theory는 6판
 - 처치 판 수입: 직전 일반 판 × 2 + 처치까지 번 몫(gt/판시간), 패배 판 수입 = 기록 비율(≈0)
 - 변형 'nosea': 기록에서 스테이지 75(바다) 이후 '구매 없이 오른 공격력'(바다 버섯 별·세트)을 빼고 계산 (50에 갇히면 못 얻음)
python verify_r214.py > verify_r214_out.txt
"""
import json, math, os, re, statistics as stx

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FIT = os.path.join(ROOT, "BalanceData", "round2_fit4")
# 코드 상수 (BalanceSim.cs:36, db.json Const/Boss)
T0, KB, MOV_H, TAU, PHK = 2.0, 0.5, 0.6, 0.2, 1.99
HEAL = 0.0500000007451 / 3.0
HPM = {"king": 0.21, "cleaner": 0.184, "angel": 1.13, "phantom": 2.83, "mover": 5.5}
CYC = ["king", "cleaner", "angel", "phantom", "mover"]
FROM = [1, 20, 45, 75, 105]
ZN = {"숲": 0, "달빛 밤": 1, "들판": 2, "바다": 3, "버섯 폐허 도시": 4}
FAIL_GOLD, REWARD = 0.8, 2.0

def latest(st): return max(i for i, f in enumerate(FROM) if st >= f)
def zmul(zi, st): return 10.0 ** zi * 1.01 ** (st - 1)        # Defs.World.cs:59-65 (ZONE_RAMP 0)
def boss(st): return CYC[(st // 10 - 1) % 5]
def hpmul(st, m, loop):                                        # Defs.Boss.cs:53 (+ 안 B)
    if st // 10 > 5: return loop if loop is not None else m
    return m if boss(st) == "mover" else HPM[boss(st)]
def hfac(b): return (MOV_H if b == "mover" else 1.0) * (1.0 if b == "angel" else 1 + TAU)

U = {"": 1, "K": 1e3, "M": 1e6, "B": 1e9, "T": 1e12, "Qa": 1e15, "Qi": 1e18}
def pnum(x):
    x = x.strip().replace(",", ""); g = re.fullmatch(r"([0-9.]+)\s*([A-Za-z]*)", x); return float(g.group(1)) * U[g.group(2)]

def read_log():
    L = open(os.path.join(FIT, "balance.log"), encoding="utf-8").read().splitlines()
    heads = [i for i, l in enumerate(L) if l.startswith("조건:")][-3:]
    out = {}
    for k, h in enumerate(heads):
        sd = int(re.search(r"시드 (\d+)", L[h]).group(1)); end = heads[k + 1] if k + 1 < len(heads) else len(L)
        out[sd] = [dict(zone=ZN[c[2].strip()], balls=int(c[4]), gold=pnum(c[8])) for c in (l.split("|") for l in L[h + 2:end] if re.match(r"R\d+ \|", l))]
    return out

def build(sd, rows):
    d = json.load(open(os.path.join(FIT, "balance_phase_s%d.json" % sd), encoding="utf-8"))
    n = len(d["incR"]); assert len(rows) == n
    C0, c, g = [], 0.0, 0.0                 # C0[i] = 판 i 시작 때 누적 지출
    for i in range(n):
        C0.append(c); c += max(0.0, g + d["incR"][i] - rows[i]["gold"]); g = rows[i]["gold"]
    Cend = C0[1:] + [c]
    # 관문 샘플 → 공통 위력 Q (보스 항 뺀 A·H, 체력 단위 = TIERS[3].hp)
    smp = []
    for i in range(n):
        gt = d["gateR"][i]
        if gt < 0: continue
        st = d["stageR"][i]; b = boss(st); hp = zmul(latest(st), st) * hpmul(st, 5.5, None); t = gt - T0
        dps = hp * (1 / t + HEAL) if b == "cleaner" else (PHK if b == "phantom" else 1) * hp / t
        smp.append((C0[i], dps / hfac(b), d["atkR"][i], 1 + KB * (rows[i]["balls"] - 1), i))
    # 바다 이후 구매 없이 오른 공격력 (트리 완료 전) — 누적 지출 위치와 배율
    f75 = next(i for i in range(n) if d["stageR"][i] >= 75)
    seaj = [(C0[i], d["atkR"][i] / d["atkR"][i - 1]) for i in range(f75 + 1, n)
            if d["buysR"][i - 1] == 0 and d["atkR"][i] > d["atkR"][i - 1] * 1.0001 and i < d["doneR"][3]]
    # 정규화 수입 (들판 스테이지 50 기준, 실제 고른 지역 배율) — 일반 판 + 보스 스테이지 파밍 판
    inc = [(C0[i], d["incR"][i] / zmul(rows[i]["zone"], d["stageR"][i]) * zmul(2, 50)) for i in range(n)
           if d["bossR"][i] in (0, 2) and d["stageR"][i] >= 45]
    lg = [math.log(v) for _, v in inc]
    sm = [math.exp(sum(lg[max(0, k - 3):k + 4]) / len(lg[max(0, k - 3):k + 4])) for k in range(len(lg))]   # ±3판 기하평균
    # 지역 프리미엄: 지역 배율 ×10으로 나눈 뒤에도 75·105 진입 판에 수입이 계단으로 뛴다(바다·폐허 버섯 등) → 50에 갇히면 못 받음
    zr = [(rows[i]["zone"], d["incR"][i] / zmul(rows[i]["zone"], d["stageR"][i])) for i in range(n) if d["bossR"][i] == 0 and d["stageR"][i] >= 45]
    def prem(z):
        a = [v for zz, v in zr if zz == z - 1][-5:]; b = [v for zz, v in zr if zz == z][:5]
        return stx.mean(b) / stx.mean(a)
    P3, P4 = prem(3), prem(4)
    zk = [1.0, 1.0, 1.0, P3, P3 * P4]
    incz = [(C0[i], d["incR"][i] / zmul(rows[i]["zone"], d["stageR"][i]) * zmul(2, 50) / zk[rows[i]["zone"]]) for i in range(n)
            if d["bossR"][i] in (0, 2) and d["stageR"][i] >= 45]
    lz = [math.log(v) for _, v in incz]
    smz = [math.exp(sum(lz[max(0, k - 3):k + 4]) / len(lz[max(0, k - 3):k + 4])) for k in range(len(lz))]
    kill = next(i for i in range(n) if d["stageR"][i] == 50 and d["bossR"][i] == 1)
    fails = [d["incR"][i] for i in range(n) if d["bossR"][i] == -1 and d["stageR"][i] == 50]
    farm = [d["incR"][i] for i in range(n) if d["bossR"][i] == 2 and d["stageR"][i] == 50]
    dur_farm = stx.median([(d["playR"][i] - d["playR"][i - 1]) for i in range(1, n) if d["bossR"][i] == 2])
    return dict(d=d, rows=rows, C0=C0, Cend=Cend, smp=smp, seaj=seaj, incC=[x for x, _ in inc], incV=sm, incVz=smz, zk=zk, kill=kill,
                Ctree=Cend[d["doneR"][3] - 1], f75=f75, fail_ratio=stx.median(fails) / stx.median(farm), dur_farm=dur_farm,
                gaps=[smp[k + 1][4] - smp[k][4] for k in range(len(smp) - 1) if smp[k][4] < kill])

def last_le(xs, x):
    lo = 0
    for k, v in enumerate(xs):
        if v <= x: lo = k
    return lo

def state(M, C, nosea=False):
    """누적 지출 C에서 (공격력, 핀볼항, Q)."""
    k = last_le(M["C0"], C); atk = M["d"]["atkR"][k]; bf = 1 + KB * (M["rows"][k]["balls"] - 1)
    if nosea:
        for cj, f in M["seaj"]:
            if cj <= C: atk /= f
    s = M["smp"][last_le([x[0] for x in M["smp"]], C)]
    satk = s[2]
    if nosea:
        for cj, f in M["seaj"]:
            if cj <= s[0]: satk /= f
    return atk, bf, s[1] * (atk / satk) * (bf / s[3])

PREM = [False]   # True: 지역 프리미엄 보정 (기록 바다·폐허 판 수입을 들판 기준으로 낮추고, 반사실에서 75·105 진입 때 다시 곱함)
def income(M, C):
    return M["incVz" if PREM[0] else "incV"][last_le(M["incC"], C)]

def gate(M, C, st, m, loop, nosea):
    _, _, Q = state(M, C, nosea); b = boss(st)
    hp = zmul(latest(st), st) * hpmul(st, m, loop); A = Q * hfac(b)
    if b == "cleaner":
        r = A / hp - HEAL; return T0 + (1e9 if r <= 0 else 1 / r)
    return T0 + (PHK if b == "phantom" else 1) * hp / A

def run(M, m, loop=None, tmax=39.0, ik=1.0, nosea=False, upto=320, stop_tree=True):
    d = M["d"]; i0 = M["kill"]; C = M["C0"][i0]; st = 50; mins = d["playR"][i0 - 1] / 60
    bossOn, farmN, flag = True, 0, False; lastN = income(M, C) * ik
    k50 = tree = None; kills = []; nf = 0; nfail = {}
    for R in range(i0 + 1, upto + 1):
        # 판 시작: 코드 BalanceSim.cs:207 재도전 규칙
        if st % 10 == 0 and not bossOn and (flag or (farmN := farmN + 1) >= 5): bossOn, farmN, flag = True, 0, False
        atk_before = state(M, C, nosea)[:2]
        zf = zmul(latest(st), st) / zmul(2, 50) * (M["zk"][latest(st)] if PREM[0] else 1.0)
        if st % 10 == 0 and bossOn:
            gt = gate(M, C, st, m, loop, nosea)
            if gt <= tmax:
                g = REWARD * lastN;   # 보스 판엔 일반 버섯이 없어 이번 판 골드 ≈ 0 → 2 × 직전 일반 판 (기록 R116: 1.77e12 / 8.85e11 = 2.0)
                mins += (gt + 1.5 + 30) / 60
                kills.append((st, R, round(gt, 1), nfail.get(st, 0)))
                if st == 50: k50 = (R, round(mins))
                st += 1
            else:
                g = lastN * M["fail_ratio"]; mins += (tmax + 30) / 60; bossOn = False; flag = False; farmN = 0
                nfail[st] = nfail.get(st, 0) + 1
        else:
            g = income(M, C) * ik * zf; lastN = g; mins += M["dur_farm"] / 60
            if st % 10 != 0: st += 1
        C += g
        if not bossOn and state(M, C, nosea)[:2] > atk_before: flag = True   # 공격 관련 구매 (공격력·핀볼로 근사)
        if tree is None and C >= M["Ctree"]: tree = (R, round(mins))
        if stop_tree and tree and k50: break
    return dict(k50=k50, tree=tree, kills=kills)

def main():
    logs = read_log(); Ms = {s: build(s, logs[s]) for s in (1, 2, 3)}
    print("== 기록 확인")
    for s, M in Ms.items():
        d = M["d"]
        print(" s%d 50처치 R%d · 트리 R%d(%.0f분) · 바다 R%d · 패배판 수입/파밍판(중앙) %.1e · 파밍 판 길이 %.0fs · 50 재도전 간격 %s"
              % (s, M["kill"] + 1, d["doneR"][3], d["doneMin"][3], M["f75"] + 1, M["fail_ratio"], M["dur_farm"], sorted(set(M["gaps"]))))
        print("    바다 이후 구매 없이 오른 공격력(트리 전): %s  누적 ×%.3f" % ([("%.2e" % c, round(f, 3)) for c, f in M["seaj"]], math.prod(f for _, f in M["seaj"])))
    # 처치 문턱: 50 mover m 의 gate ≤ 39 를 처음 만족하는 누적 지출
    print("\n== mover 15 처치 문턱 (gate ≤ 39s) 누적 지출 vs 기록 위치")
    for s, M in Ms.items():
        for ns in (False, True):
            Cs = sorted(set(M["C0"]))
            th = next((c for c in Cs if c >= M["C0"][M["kill"]] and gate(M, c, 50, 15, None, ns) <= 39), None)
            ri = last_le(M["C0"], th) if th else None
            print(" s%d %s 문턱 C %.3g (기록에서 R%s · 스테이지 %s) · 트리 C %.3g · 바다 진입 C %.3g" %
                  (s, "nosea" if ns else "기록 ", th or -1, ri + 1 if ri is not None else "-", M["d"]["stageR"][ri] if ri is not None else "-", M["Ctree"], M["C0"][M["f75"]]))
    print("\n== 반사실 (판, 분)  — 시드1 · 시드2 · 시드3")
    def show(lbl, **kw):
        rr = [run(M, **kw) for M in Ms.values()]
        print(" %-34s 50: %s | 트리: %s" % (lbl, " · ".join("R%d(%d분)" % r["k50"] if r["k50"] else "-" for r in rr),
                                            " · ".join("R%d(%d분)" % r["tree"] if r["tree"] else "-" for r in rr)))
        return rr
    show("검산 m 5.5", m=5.5)
    for m in (12, 14, 15, 16, 17):
        show("m %g" % m, m=m)
    for lbl, kw in [("m15 TMAX 37", dict(tmax=37)), ("m15 TMAX 41", dict(tmax=41)), ("m15 수입 ×0.5", dict(ik=0.5)),
                    ("m15 수입 ×0.8", dict(ik=0.8)), ("m15 수입 ×1.25", dict(ik=1.25)), ("m15 수입 ×2", dict(ik=2.0)),
                    ("m15 nosea", dict(nosea=True)), ("m15 nosea 수입 ×0.8", dict(nosea=True, ik=0.8)), ("m 5.5 nosea(검산)", dict(nosea=True))]:
        show(lbl, m=15 if "m15" in lbl else 5.5, **kw)
    print("\n== 지역 프리미엄 (×10 정규화 뒤 75·105 진입 계단, 전 5판 → 후 5판 평균비): " + " · ".join("s%d 바다 ×%.2f 폐허 ×%.2f" % (s, M["zk"][3], M["zk"][4] / M["zk"][3]) for s, M in Ms.items()))
    PREM[0] = True
    for lbl, kw in [("[보정] m 5.5 (검산)", dict(m=5.5)), ("[보정] m 12", dict(m=12)), ("[보정] m 14", dict(m=14)), ("[보정] m 15", dict(m=15)),
                    ("[보정] m 15 수입 ×0.5", dict(m=15, ik=0.5)), ("[보정] m 15 수입 ×0.8", dict(m=15, ik=0.8)),
                    ("[보정] m 15 수입 ×1.25", dict(m=15, ik=1.25)), ("[보정] m 15 수입 ×2", dict(m=15, ik=2.0))]:
        show(lbl, **kw)
    PREM[0] = False
    print("\n== 60~ 보스 (스테이지, 판, 관문s, 진 횟수) m 15")
    for loop in (None, 5.5):
        for s, M in Ms.items():
            r = run(M, 15, loop=loop, stop_tree=False, upto=260)
            print(" 60~ %s s%d %s" % ("=m(지금 코드)" if loop is None else "5.5(안 B)", s, r["kills"][:9]))

if __name__ == "__main__":
    main()
