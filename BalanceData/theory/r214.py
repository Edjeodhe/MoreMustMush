# -*- coding: utf-8 -*-
"""R2-14 (manager#12): 스테이지 50(움직이는 버섯) 첫 처치를 R165~175로 옮기는 Hp_Mul.

입력: BalanceData/round2_fit4/balance_phase_s1~3.json(incR·stageR·atkR·gateR·bossR·playR),
      balance.log 마지막 세 실행(핀볼·보유골드·지역).
방법 (결정적, 난수 없음):
 1) 판별 지출 spend_i = gold_{i-1} + incR_i - gold_i, 누적 S. 사는 순서(가장 싼 것부터 + 부모 잠금 + 싱크)는
    수입과 무관하므로 능력치는 S의 함수로 본다(기록 그대로).
 2) 기록의 모든 관문 판(gateR)을 '스테이지 50·Hp_Mul 5.5 움직이는 버섯' 등가 시간 T50으로 바꾼다
    (체력 = 지역 배율 10^(지역-1) × 1.01^(스테이지-1) × 배율, H의 보스별 항, 청소부 회복, 환상 ×1.99).
    잔차 r = T50 × 공격력 × (1 + 0.5(핀볼-1)) 를 S에 대해 보간(나머지 배율: 파쇄·보스피해·치명·속도·임시수확기).
 3) 일반 판 수입을 들판·스테이지 50 기준으로 정규화 I50 = incR / (10^(지역-2) × 1.01^(스테이지-50)), S에 대해 보간.
 4) 반사실: 기록 처치 판(R116 근처)부터 스테이지 50에 머문다고 보고 판을 진행 — 파밍 판은 I50(S)를 모두 지출,
    재도전 판(공격 노드 산 다음 판 또는 5판째, BalanceSim 규칙)은 수입 ≈ 0. 관문: 2 + T50(S)·m/5.5 ≤ TMAX.
    처치 뒤엔 스테이지 +1/판, 지역(75 바다, 105 폐허) 배율 반영, 60~130 보스도 같은 식(Hp_Mul은 마지막 보스 = m).
python r214.py → theory_r214.json, 표 출력
"""
import json, math, os, re, bisect

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
FIT = os.path.join(ROOT, "BalanceData", "round2_fit4")
GATE_T0, MOVER_H, TAU, PH_K, KB = 2.0, 0.6, 0.2, 1.99, 0.5
HEAL = 0.05 / 3.0
M_NOW = {"king": 0.21, "cleaner": 0.184, "angel": 1.13, "phantom": 2.83, "mover": 5.5}
ORDER = ["king", "cleaner", "angel", "phantom", "mover"]
THEMES = [("forest", 1, "숲"), ("night", 20, "달빛 밤"), ("field", 45, "들판"), ("sea", 75, "바다"), ("ruins", 105, "버섯 폐허 도시")]
OVERHEAD = 30.0
TMAX = 39.0     # 스테이지 50 보스 판 제한 시간(기록 37~40s의 중앙)

def latest(stage): return max(i for i, t in enumerate(THEMES) if stage >= t[1])
def Z(stage): return 10.0 ** latest(stage) * 1.01 ** (stage - 1)
def boss_at(stage): return ORDER[(stage // 10 - 1) % 5]
LOOP = [None]   # None: 60~ 은 마지막 보스(mover) 배율 = 지금 코드. 숫자: 60~ 배율을 따로 고정(적용안 B)
def mul(stage, m):
    mm = dict(M_NOW); mm["mover"] = m
    if stage // 10 > 5: return LOOP[0] if LOOP[0] else mm["mover"]
    return mm[boss_at(stage)]

SUF = {"": 1, "K": 1e3, "M": 1e6, "B": 1e9, "T": 1e12, "Qa": 1e15, "Qi": 1e18}
def num(s):
    s = s.strip().replace(",", ""); m = re.match(r"^([0-9.]+)\s*([A-Za-z]*)$", s)
    return float(m.group(1)) * SUF[m.group(2)]
ZN = {t[2]: i for i, t in enumerate(THEMES)}

def parse_log():
    lines = open(os.path.join(FIT, "balance.log"), encoding="utf-8").read().split("\n")
    starts = [i for i, l in enumerate(lines) if l.startswith("조건:")]
    runs = {}
    for st in starts[-3:]:
        seed = int(re.search(r"시드 (\d+)", lines[st]).group(1))
        rows = []
        end = next((s for s in starts if s > st), len(lines))
        for l in lines[st + 2:end]:
            if not l.startswith("R"): continue
            c = l.split("|")
            rows.append(dict(zone=ZN[c[2].strip()], balls=int(c[4]), gold=num(c[8])))
        runs[seed] = rows
    return runs

def interp(xs, ys, x):
    if x <= xs[0]: return ys[0]
    if x >= xs[-1]: return ys[-1]
    j = bisect.bisect_right(xs, x)
    x0, x1, y0, y1 = xs[j - 1], xs[j], ys[j - 1], ys[j]
    t = (math.log(x) - math.log(x0)) / (math.log(x1) - math.log(x0)) if x0 > 0 else 1
    return y0 + (y1 - y0) * t

def step(xs, ys, x):   # 구간 상수 (x 이하 마지막 값)
    j = bisect.bisect_right(xs, x) - 1
    return ys[max(0, j)]

def seed_model(seed, rows):
    d = json.load(open(os.path.join(FIT, "balance_phase_s%d.json" % seed), encoding="utf-8"))
    n = len(d["incR"]); assert n == len(rows), (seed, n, len(rows))
    S, s, g0 = [], 0.0, 0.0
    for i in range(n):
        sp = max(0.0, g0 + d["incR"][i] - rows[i]["gold"]); s += sp; S.append(s); g0 = rows[i]["gold"]
    Sbefore = [0.0] + S[:-1]            # 판 i를 시작할 때 누적 지출(능력치 상태)
    # 관문 샘플 → T50 등가, 잔차 r
    samp = []
    for i in range(n):
        gt = d["gateR"][i]
        if gt < 0: continue
        stg = d["stageR"][i]; b = boss_at(stg)
        hp = Z(stg) * (5.5 if stg // 10 > 5 else M_NOW[b]); hp50 = Z(50) * 5.5
        t = gt - GATE_T0
        hb = (MOVER_H if b == "mover" else 1.0) * (1.0 if b == "angel" else 1 + TAU)   # 임시 수확기는 50 이후 켜져 있다고 봄
        if b == "cleaner":
            dps_over_hp = 1 / t + HEAL; dps = dps_over_hp * hp
        else:
            dps = (PH_K if b == "phantom" else 1.0) * hp / t
        H_rel = hb                                   # 이 보스의 H/H_공통
        dps0 = dps / H_rel                           # 공통 dps (보스별 항 제외)
        t50 = hp50 / (dps0 * MOVER_H * (1 + TAU))
        ballF = 1 + KB * (rows[i]["balls"] - 1)
        samp.append(dict(i=i + 1, stage=stg, boss=b, gate=round(gt, 2), S=Sbefore[i], atk=d["atkR"][i], balls=rows[i]["balls"],
                         T50=t50, r=t50 * d["atkR"][i] * ballF))
    # 능력치(공격력·핀볼) S 함수
    atkS = (Sbefore, d["atkR"]); ballS = (Sbefore, [r["balls"] for r in rows])
    # 잔차: 스테이지 50 이상 샘플만 (임시 수확기 가정이 맞는 구간)
    late = [x for x in samp if x["stage"] >= 50]
    rx = [x["S"] for x in late]; ry = [math.log(x["r"]) for x in late]
    # 수입 I50 (일반 판, 들판 50 기준)
    ix, iy = [], []
    for i in range(n):
        if d["bossR"][i] != 0 or d["stageR"][i] < 45: continue
        z = rows[i]["zone"]; stg = d["stageR"][i]
        ix.append(Sbefore[i]); iy.append(d["incR"][i] / (10 ** (z - 2) * 1.01 ** (stg - 50)))
    # 이동 중앙값(±3판)으로 잡음 줄이기
    sm = [sorted(iy[max(0, k - 3):k + 4])[len(iy[max(0, k - 3):k + 4]) // 2] for k in range(len(iy))]
    kill = next(i for i in range(n) if d["stageR"][i] == 50 and d["bossR"][i] == 1)
    tree = d["doneR"][3]
    return dict(d=d, rows=rows, S=S, Sb=Sbefore, samp=samp, atkS=atkS, ballS=ballS, rx=rx, ry=ry, ix=ix, iy=sm,
                kill=kill, tree=tree, Stree=S[tree - 1], play=d["playR"])

def T50(M, S):
    atk = step(M["atkS"][0], M["atkS"][1], S); balls = step(M["ballS"][0], M["ballS"][1], S)
    r = math.exp(interp(M["rx"], M["ry"], S))
    return r / (atk * (1 + KB * (balls - 1))), atk

def boss_time(M, S, stage, m):
    """스테이지 stage 보스 관문 예상 시각(초)."""
    t50, _ = T50(M, S)
    b = boss_at(stage); hp_rel = Z(stage) * mul(stage, m) / (Z(50) * 5.5)
    hb = (MOVER_H if b == "mover" else 1.0) * (1.0 if b == "angel" else 1 + TAU)
    base = t50 * MOVER_H * (1 + TAU) / hb * hp_rel      # 힐·환상 전 시간
    if b == "cleaner":
        rate = 1 / base - HEAL
        return GATE_T0 + (1e9 if rate <= 0 else 1 / rate)
    return GATE_T0 + (PH_K if b == "phantom" else 1.0) * base

def run(M, m, tmax=TMAX, inc_k=1.0, upto=260):
    """반사실 진행. 기록의 처치 판 직전(kill-1 판까지)은 같다고 보고 그 다음부터 판 단위로 계산."""
    d = M["d"]; i0 = M["kill"]                  # 0-based: 기록에서 50을 처음 처치한 판
    S = M["Sb"][i0]; stage = 50; minutes = d["playR"][i0 - 1] / 60
    farm = 0; atk_last = T50(M, S)[1]; bossOn = True
    log, k50, tree_r, kills = None, None, None, []
    fails = {}
    for R in range(i0 + 1, upto + 1):
        is_boss = stage % 10 == 0 and bossOn
        if is_boss:
            gt = boss_time(M, S, stage, m)
            if gt <= tmax:
                kills.append((stage, R, round(gt, 1), fails.get(stage, 0)))
                if stage == 50: k50 = (R, minutes + (gt + 1.5 + OVERHEAD) / 60)
                inc = 2 * inc50(M, S) * inc_k * 1.01 ** (stage - 50) * 10 ** (latest(stage) - 2)   # 처치 보상 ≈ 일반 판 ×2
                minutes += (gt + 1.5 + OVERHEAD) / 60
                stage += 1
            else:
                fails[stage] = fails.get(stage, 0) + 1
                inc = 0.0; bossOn = False; farm = 0; atk_last = T50(M, S)[1]
                minutes += (tmax + OVERHEAD) / 60
        else:
            inc = inc50(M, S) * inc_k * 1.01 ** (stage - 50) * 10 ** (latest(stage) - 2)
            minutes += (40 + OVERHEAD) / 60
            if stage % 10 == 0:          # 보스 꺼짐 → 파밍
                farm += 1
            else:
                stage += 1
        S += inc
        if stage % 10 == 0 and not bossOn:
            a = T50(M, S)[1]
            if a > atk_last or farm >= 5: bossOn = True; farm = 0
        if tree_r is None and S >= M["Stree"]: tree_r = (R, minutes)
    return dict(k50=k50, tree=tree_r, kills=kills, stage_end=stage)

def inc50(M, S): return interp(M["ix"], M["iy"], S)

def main():
    runs = parse_log(); out = {"samples": {}, "seeds": {}}
    Ms = {s: seed_model(s, runs[s]) for s in (1, 2, 3)}
    print("== 관문 샘플 → 스테이지50·mover 5.5 등가 시간 T50, 잔차 r = T50·공격력·핀볼항")
    for s, M in Ms.items():
        out["samples"][s] = M["samp"]
        for x in M["samp"]:
            if x["stage"] >= 40:
                print(" s%d R%-3d st%-3d %-7s gate %6.1f  atk %7.0f 핀볼 %d  T50 %7.1f  r %.0f  S %.3g" % (s, x["i"], x["stage"], x["boss"], x["gate"], x["atk"], x["balls"], x["T50"], x["r"], x["S"]))
    # 검산: 현재 m=5.5 반사실이 기록 처치 판을 재현하는지 (기록 처치 판부터 시작하므로 그 판에서 바로 처치되어야 함)
    print("\n== 검산 (m=5.5): 50 처치 판·트리 완료 판 (기록과 비교)")
    for s, M in Ms.items():
        r = run(M, 5.5)
        print(" s%d 기록 50처치 R%d · 트리 R%d  | 모델 50처치 R%s · 트리 R%s · 이후 처치 %s" % (s, M["kill"] + 1, M["tree"], r["k50"][0], r["tree"][0] if r["tree"] else "-", r["kills"][1:9]))
    # 후보 m
    print("\n== m별 반사실 (TMAX %.0fs, 수입 ×1)" % TMAX)
    grid = [5.5, 8, 10, 11, 12, 13, 14, 15, 16, 18, 20]
    res = {}
    for m in grid:
        row = []
        for s, M in Ms.items():
            r = run(M, m); row.append(r)
        res[m] = row
        print(" m %5.1f | " % m + " | ".join("s%d 50:R%s(%s분) 트리:R%s" % (s, r["k50"][0] if r["k50"] else "-", round(r["k50"][1]) if r["k50"] else "-",
                                                                   r["tree"][0] if r["tree"] else "-") for s, r in zip(Ms, row)))
    fine = [13, 14, 14.5, 15, 15.5, 16, 17]
    out["fine"] = {}
    for loop in (None, 5.5):
        LOOP[0] = loop
        print("\n== 세밀 격자 (60~ 배율 %s)" % ("= m (지금 코드)" if loop is None else "고정 5.5 (적용안 B)"))
        for m in fine:
            rr = [run(M, m) for M in Ms.values()]
            k = sorted(r["k50"][0] for r in rr); mn = sorted(round(r["k50"][1]) for r in rr)
            tr = sorted(r["tree"][0] for r in rr)
            k80 = [next((x for x in r["kills"] if x[0] == 80), None) for r in rr]
            print(" m %4.1f | 50처치 R%s 중앙 R%d (%s분 중앙 %d) | 트리 R%s | 80 처치(판,진 횟수) %s" % (m, k, k[1], mn, mn[1], tr, [(x[1], x[3]) if x else "-" for x in k80]))
            out["fine"]["%s_%s" % (loop, m)] = dict(k50=[r["k50"] for r in rr], tree=[r["tree"] for r in rr], kills=[r["kills"][:10] for r in rr])
    LOOP[0] = 5.5
    best = 15.0
    out["best"] = best
    sens = {}
    for lbl, kw in [("기준", {}), ("TMAX 37", {"tmax": 37}), ("TMAX 41", {"tmax": 41}), ("수입 ×0.8", {"inc_k": 0.8}), ("수입 ×1.25", {"inc_k": 1.25})]:
        rr = [run(M, best, **kw) for M in Ms.values()]
        sens[lbl] = [(r["k50"][0], round(r["k50"][1]), r["tree"][0] if r["tree"] else None) for r in rr]
        print(" 민감도 m=%.1f %-10s " % (best, lbl) + " · ".join("50 R%s(%s분) 트리 R%s" % x for x in sens[lbl]))
    out["sens"] = sens
    for lp in (None, 5.5):
        LOOP[0] = lp
        for s_, M in Ms.items():
            r = run(M, best)
            print(" 60~ %s s%d 보스(스테이지, 판, 관문s, 진 횟수): %s" % (lp, s_, r["kills"][:9]))
            out.setdefault("detail", {})["%s_s%d" % (lp, s_)] = r["kills"][:9]
    json.dump(out, open(os.path.join(HERE, "theory_r214.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

if __name__ == "__main__":
    main()
