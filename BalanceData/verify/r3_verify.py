# -*- coding: utf-8 -*-
"""verify — 3회차(R3) theory 안 독립 재계산 (theory#5 ①~⑥).

theory의 r3_model.py를 import하지 않고 게임 코드(Game.cs ComputeStats, BulkBuy, ParentOk)를 따로 옮겨 다시 계산한다.
입력: Assets/Resources/DB/db.json(지금 DB), BalanceData/round3_t0(T3-0 가안 실측),
      BalanceData/theory/r3_apply_set.json·r3_apply_prices.json·r3_rare0_rows.json(theory 제안 = 검증 대상).
실행: python BalanceData/verify/r3_verify.py > BalanceData/verify/r3_verify_out.txt  (난수 시드 고정)
"""
import json
import math
import os
import random
import statistics as stt

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DB = json.load(open(os.path.join(ROOT, "Assets", "Resources", "DB", "db.json"), encoding="utf-8"))
T0 = os.path.join(ROOT, "BalanceData", "round3_t0")
TH = os.path.join(ROOT, "BalanceData", "theory")
APPLY_SET = json.load(open(os.path.join(TH, "r3_apply_set.json"), encoding="utf-8"))
APPLY_PRICES = json.load(open(os.path.join(TH, "r3_apply_prices.json"), encoding="utf-8"))
RARE0 = json.load(open(os.path.join(TH, "r3_rare0_rows.json"), encoding="utf-8"))


def jsround(x):          # U.JsRound = Math.floor(x + 0.5)
    return math.floor(x + 0.5)


# ---------------- DB → 노드 표 (Defs.Nodes.cs:226~267 과 같게) ----------------
def load_nodes(proposed, rare0):
    up = {u["Upg_ID"]: dict(u) for u in DB["Upgrade"]}
    cost = {c["Cost_ID"]: dict(c) for c in DB["Upgrade_Cost"]}
    if proposed:
        key2id = {u["Upg_Key"]: i for i, u in up.items()}
        for k, ch in APPLY_SET["Upgrade"].items():
            up[key2id[k]].update(ch)
        for k, ps in APPLY_PRICES.items():
            c = cost[up[key2id[k]]["Cost_ID"]]
            for i, p in enumerate(ps):
                c[f"Cost_Lv{i + 1}"] = p
        if rare0:
            for u in RARE0["Upgrade"]:
                up[u["Upg_ID"]] = dict(u)
            for c in RARE0["Upgrade_Cost"]:
                cost[c["Cost_ID"]] = dict(c)
    nodes = {}
    for u in up.values():
        c = cost[u["Cost_ID"]]
        mx = u["Efx_MaxLv"]
        gold = [c[f"Cost_Lv{i}"] for i in range(1, 5)][:mx]
        gem = [c[f"Gem_Lv{i}"] for i in range(1, 5)][:mx]
        nodes[u["Upg_Key"]] = dict(
            id=u["Upg_ID"], key=u["Upg_Key"], parent=up[u["Upg_Pre"]]["Upg_Key"] if u["Upg_Pre"] else None,
            req=up[u["Upg_Req"]]["Upg_Key"] if u["Upg_Req"] else None, max=mx, tier=u["Tier"], inf=(mx == 0),
            gem=any(g > 0 for g in gem), costs=gold, efx=[u[f"Efx_Lv{i}"] for i in range(5)])
    return nodes


def V(nodes, k, L):
    n = nodes[k]
    return n["efx"][max(0, min(L, n["max"]))]


# ---------------- ComputeStats 일부 (Game.cs:216~258) ----------------
def area(nodes, lv_map):
    z = V(nodes, "md_map", lv_map)
    return (1 / (z * z)) ** 0.7


def maxcol(code, nodes, stage, lv, rain=False, ab_cols=0.0, tb_colsN=0.0, tb_cols=0.0, preset=(12, 5)):
    """code='cur' 지금(Game.cs:242·254·257) / 'new' 제안(적용안 1·2·3)"""
    L = lv.get("ed_cols", 0)
    if code == "cur":
        colcount = math.ceil(preset[0] + preset[1] * L)                       # Defs.cs:123
    else:
        colcount = int(round(V(nodes, "ed_cols", L)))                          # 적용안 1 (C# Math.Round = 은행가 반올림, 정수라 같음)
    raw = (colcount + V(nodes, "ed_cols2", lv.get("ed_cols2", 0)) + tb_colsN) * (1 + ab_cols + tb_cols) * (1.2 if rain else 1)
    m = max(3 if code == "cur" else 1, math.ceil(raw - 1e-12))
    m = max(4 if code == "cur" else 1, jsround(m * area(nodes, lv.get("md_map", 0))))
    if code == "cur" and 1 <= stage <= 6:
        m = max(1, jsround(m * [0.4, 0.45, 0.5, 0.6, 0.7, 0.85][stage - 1]))
    return m


def regen(code, nodes, stage, lv):
    r = V(nodes, "ed_regen", lv.get("ed_regen", 0))
    if code == "cur" and 1 <= stage <= 6:
        r *= [1.6, 1.5, 1.4, 1.3, 1.2, 1.1][stage - 1]
    return r


def duration(code, nodes, stage, lv, ab_dur=0.0, cold=False):
    base_time = 15 if code == "cur" else 10
    base = 10 if stage <= 4 else base_time + min(10, 0.5 * (stage - 1))
    return base + V(nodes, "ps_dur", lv.get("ps_dur", 0)) + V(nodes, "ps_dur2", lv.get("ps_dur2", 0)) + ab_dur - (3 if cold else 0)


# ---------------- ① 판 수입 근사 다시 맞추기 ----------------
def load_t0():
    pts, st7 = [], []
    cur = load_nodes(False, False)
    for s in (1, 2, 3):
        states = [json.loads(l) for l in open(os.path.join(T0, f"states_s{s}.jsonl"), encoding="utf-8")]
        ph = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))
        for i in range(1, 7):                      # 판 i+1 (스테이지 i+1), 판 시작 노드 = 판 i 끝 상태
            lv = states[i - 1]["nodes"]
            stg = ph["stageR"][i]
            d = dict(seed=s, stage=stg, c=maxcol("cur", cur, stg, lv), T=duration("cur", cur, stg, lv),
                     f=V(cur, "ed_score", lv.get("ed_score", 0)), g=1.1 + V(cur, "ed_price", lv.get("ed_price", 0)) - 1,
                     stump=lv.get("dev_stump", 0), inc=ph["incR"][i], regen=regen("cur", cur, stg, lv),
                     combo=lv.get("md_combo", 0), bar=lv.get("md_bar", 0))
            (pts if stg <= 6 else st7).append(d)
    return pts, st7


def pred(p, kappa, sigma, alpha, tau=0.0):
    return p["T"] * p["g"] * ((1 + p["f"] + tau) * kappa * p["c"] ** alpha + (1 + p["f"]) * sigma * p["stump"])


def golden(fn, a, b, it=80):
    g = (math.sqrt(5) - 1) / 2
    c, d = b - g * (b - a), a + g * (b - a)
    for _ in range(it):
        if fn(c) < fn(d):
            b = d
        else:
            a = c
        c, d = b - g * (b - a), a + g * (b - a)
    return (a + b) / 2


def fit(pts, alpha):
    def sse(kappa, sigma):
        return sum((math.log(pred(p, kappa, sigma, alpha)) - math.log(p["inc"])) ** 2 for p in pts)

    def best_sigma(kappa):
        return golden(lambda s: sse(kappa, s), 0.0, 20.0)

    lk = golden(lambda lk: sse(math.exp(lk), best_sigma(math.exp(lk))), math.log(0.05), math.log(20))
    k = math.exp(lk)
    s = best_sigma(k)
    return dict(alpha=alpha, kappa=k, sigma=s, rmse=math.sqrt(sse(k, s) / len(pts)))


def tau_inv(p, m):
    base = p["T"] * p["g"] * m["kappa"] * p["c"] ** m["alpha"]
    return (p["inc"] - p["T"] * p["g"] * (1 + p["f"]) * m["sigma"] * p["stump"]) / base - (1 + p["f"])


# ---------------- ④ 가장 싼 것부터 (Game.BulkBuy:156~175, ParentOk:125) ----------------
def parent_ok(nodes, lv, k):
    n = nodes[k]
    return (n["parent"] is None or lv.get(n["parent"], 0) >= 1) and (n["req"] is None or lv.get(n["req"], 0) >= nodes[n["req"]]["max"])


def bulk_buy(nodes, lv, gold):
    bought = []
    while True:
        best, bc = None, float("inf")
        for k, n in nodes.items():
            if n["inf"] or n["gem"] or lv.get(k, 0) >= n["max"] or not parent_ok(nodes, lv, k):
                continue
            c = math.ceil(n["costs"][lv.get(k, 0)])
            if c < bc:
                best, bc = k, c
        if best is None or gold < bc:
            return gold, bought
        gold -= bc
        lv[best] = lv.get(best, 0) + 1
        bought.append(f"{best}{lv[best]}")


def income(code, nodes, stage, lv, m, tau7, rare_node):
    if stage == 1:
        return 18.0                                     # 튜토리얼 판(lone 1 + 낱개 8): T3-0 16·21·18
    c = maxcol(code, nodes, stage, lv)
    T = duration(code, nodes, stage, lv)
    f = V(nodes, "ed_score", lv.get("ed_score", 0))
    g = 1.1 + V(nodes, "ed_price", lv.get("ed_price", 0)) - 1
    w = V(nodes, "ed_rare0", lv.get("ed_rare0", 0)) if (code == "new" and rare_node) else 1.0
    tau = tau7 * w if stage >= 7 else 0.0
    return T * g * ((1 + f + tau) * m["kappa"] * c ** m["alpha"] + (1 + f) * m["sigma"] * lv.get("dev_stump", 0))


def run(code, nodes, m, tau7, rare_node, mul=1.0, rounds=12, noise=None, rng=None):
    lv, gold, out = {}, 0.0, []
    for r in range(1, rounds + 1):
        inc = income(code, nodes, r, lv, m, tau7, rare_node) * mul
        if noise:
            inc *= math.exp(rng.gauss(0, noise))
        before = dict(lv)
        gold, b = bulk_buy(nodes, lv, gold + inc)
        # P5: 구매 1건씩 다음 판(스테이지 r+1) 수입 배율
        p5 = []
        for x in b:
            k = x.rstrip("0123456789")
            one = dict(before)
            one[k] = one.get(k, 0) + 1
            i0 = income(code, nodes, r + 1, before, m, tau7, rare_node)
            i1 = income(code, nodes, r + 1, one, m, tau7, rare_node)
            p5.append((x, i1 / i0))
            before = one
        out.append(dict(r=r, inc=inc, n=len(b), b=b, p5=p5, gold=gold, lv=dict(lv)))
    return out


def main():
    rng = random.Random(20261016)
    P = print
    pts, st7 = load_t0()
    P("=== ① T3-0 맞춤점 (스테이지 2~6, 15점) ===")
    for p in pts:
        P(f"  s{p['seed']} st{p['stage']} c{p['c']} T{p['T']} f{p['f']:.0f} g{p['g']:.2f} stump{p['stump']} regen{p['regen']:.2f} combo{p['combo']} bar{p['bar']} inc {p['inc']:.0f}")
    P("  f 값이 0이 아닌 점:", sum(1 for p in pts if p["f"] > 0), "· g≠1.1:", sum(1 for p in pts if abs(p["g"] - 1.1) > 1e-9),
      "· stump 있는 점:", [(p['seed'], p['stage']) for p in pts if p["stump"]])
    models = {}
    P("  alpha 프로필 (자유 κ·σ, 로그 오차):")
    for a in [0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 1.0]:
        m = fit(pts, a)
        models[a] = m
        P(f"   alpha {a:.1f}: kappa {m['kappa']:.3f} sigma {m['sigma']:.3f} RMSE {m['rmse']:.4f}")
    # 시드 하나 빼고 맞춰 그 시드 예측 (교차검증)
    P("  시드 하나 빼기 (alpha 0.3):")
    for s in (1, 2, 3):
        tr = [p for p in pts if p["seed"] != s]
        te = [p for p in pts if p["seed"] == s]
        m = fit(tr, 0.3)
        e = [math.log(pred(p, m["kappa"], m["sigma"], 0.3) / p["inc"]) for p in te]
        P(f"   빼낸 시드 {s}: kappa {m['kappa']:.2f} sigma {m['sigma']:.2f} 예측 오차(ln) {['%.2f' % x for x in e]}")
    m3 = models[0.3]
    P("  잔차 (alpha 0.3, 예측/실측):")
    res = []
    for p in pts:
        r = pred(p, m3["kappa"], m3["sigma"], 0.3) / p["inc"]
        res.append(r)
        P(f"   s{p['seed']} st{p['stage']}: {r:.2f}")
    P(f"   |ln| 중앙 {stt.median(abs(math.log(x)) for x in res):.3f} 최대 {max(abs(math.log(x)) for x in res):.3f}")
    # 그루터기: 스테이지 6 판만 stump=1 → sigma는 3점이 정한다. 같은 판 stump 없을 때 예측 대비 배율
    P("  그루터기 배율 (스테이지 6 실측 / stump 없는 예측, alpha 0.3):")
    for p in pts:
        if p["stump"]:
            q = dict(p, stump=0)
            P(f"   s{p['seed']}: ×{p['inc'] / pred(q, m3['kappa'], 0, 0.3):.2f}")
    P("  바깥 확인 2회차 판 1(c 7, 15초, 노드 0, 관측 57~65, 튜토리얼 없던 때): 예측", {a: round(15 * 1.1 * models[a]['kappa'] * 7 ** a, 1) for a in (0.1, 0.3, 0.5, 0.7, 1.0)})

    P("\n=== ① tau7 역산 (스테이지 7 판, 시드별) ===")
    for a in (0.1, 0.3, 0.5):
        P(f"  alpha {a}: " + ", ".join(f"s{p['seed']} c{p['c']} T{p['T']} f{p['f']:.0f} inc{p['inc']:.0f} → tau {tau_inv(p, models[a]):.2f}" for p in st7))
    taus = [tau_inv(p, m3) for p in st7]
    tau_mean = sum(taus) / 3
    P(f"  alpha 0.3 평균 {tau_mean:.2f}")

    cur = load_nodes(False, False)
    new = load_nodes(True, True)
    new_nr = load_nodes(True, False)

    P("\n=== ② 다 산 뒤 지금과 같은가 (군락·판 시간) ===")
    bad = []
    full_cur = {"ed_cols": 3, "ps_dur": 3}
    full_new = {"ed_cols": 4, "ps_dur": 4}
    cnt = 0
    for stage in range(1, 301):
        for mp in range(5):
            for c2 in range(3):
                for rain in (False, True):
                    for abc in (0.0, 0.1, 0.45):
                        for cn in (0, 2):
                            a = maxcol("cur", cur, stage, dict(full_cur, md_map=mp, ed_cols2=c2), rain, abc, cn)
                            b = maxcol("new", new, stage, dict(full_new, md_map=mp, ed_cols2=c2), rain, abc, cn)
                            cnt += 1
                            if a != b and stage >= 7:
                                bad.append(("col", stage, mp, c2, rain, abc, cn, a, b))
        for d2 in range(3):
            for cold in (False, True):
                a = duration("cur", cur, stage, dict(full_cur, ps_dur2=d2), 0, cold)
                b = duration("new", new, stage, dict(full_new, ps_dur2=d2), 0, cold)
                if abs(a - b) > 1e-9:
                    bad.append(("time", stage, d2, cold, a, b))
    P(f"  군락 비교 {cnt}조합(스테이지 1~300 × md_map 0~4 × ed_cols2 0~2 × 비 × 별 능력 cols × 꼬마 colsN) — 7 이상 다른 곳 {sum(1 for x in bad if x[0]=='col')}")
    tb = [x for x in bad if x[0] == "time"]
    P(f"  판 시간 다른 곳 {len(tb)}: 스테이지 {sorted(set(x[1] for x in tb))[:10]} 예 {tb[:2]}")
    P("  스테이지 1~6 다 산 뒤 군락(md_map 4): 지금", [maxcol('cur', cur, s, dict(full_cur, md_map=4)) for s in range(1, 7)], "제안", [maxcol('new', new, s, dict(full_new, md_map=4)) for s in range(1, 7)])
    P("  레벨별 군락 (md_map 0, 스테이지 7+): 지금 L0~3", [maxcol('cur', cur, 9, {'ed_cols': L}) for L in range(4)], "제안 L0~4", [maxcol('new', new, 9, {'ed_cols': L}) for L in range(5)])
    P("  레벨별 군락 (md_map 4): 지금", [maxcol('cur', cur, 9, {'ed_cols': L, 'md_map': 4}) for L in range(4)], "제안", [maxcol('new', new, 9, {'ed_cols': L, 'md_map': 4}) for L in range(5)])
    P("  스테이지 2~6 가안(노드 0) 군락", [maxcol('cur', cur, s, {}) for s in range(2, 7)], "제안(노드 0)", [maxcol('new', new, s, {}) for s in range(2, 7)])
    P("  레벨별 판 시간 스테이지 9: 지금 L0~3", [duration('cur', cur, 9, {'ps_dur': L}) for L in range(4)], "제안 L0~4", [duration('new', new, 9, {'ps_dur': L}) for L in range(5)])
    P("  옛 저장(ed_cols 3·ps_dur 3, md_map 4, 스테이지 30) 불러올 때: 군락", maxcol('cur', cur, 30, {'ed_cols': 3, 'md_map': 4}), "→", maxcol('new', new, 30, {'ed_cols': 3, 'md_map': 4}),
      "· 판 시간", duration('cur', cur, 30, {'ps_dur': 3}), "→", duration('new', new, 30, {'ps_dur': 3}),
      "· ed_cols2·ps_dur2 req 최대 충족:", parent_ok(new, {'ed_cols': 3, 'ed_score': 1, 'power': 1}, 'ed_cols2'), parent_ok(new, {'ps_dur': 3, 'power': 1}, 'ps_dur2'))
    P("  spec 프리셋(col0 3·colStep 3) 다 산 뒤 군락: 지금", maxcol('cur', cur, 30, {'ed_cols': 3, 'md_map': 4}, preset=(3, 3)), "제안(DB만 읽음)", maxcol('new', new, 30, {'ed_cols': 4, 'md_map': 4}))

    P("\n=== ③ 가격 사다리 ===")
    def levels(nodes, tiers=(1, 2, 3)):
        out = []
        for k, n in nodes.items():
            if n["inf"] or n["gem"] or n["tier"] not in tiers:
                continue
            for i, p in enumerate(n["costs"]):
                out.append((p, k, i + 1))
        return sorted(out)
    for name, nd in (("지금", cur), ("제안+ed_rare0", new), ("제안, ed_rare0 없음", new_nr)):
        for lim, tiers in ((20000, (1,)), (3e5, (1, 2, 3)), (1e15, (1, 2, 3))):
            seq = [x for x in levels(nd, tiers) if x[0] <= lim]
            j = [(seq[i][0] / seq[i - 1][0], seq[i - 1], seq[i]) for i in range(1, len(seq))]
            jm = max(j)
            P(f"  {name} · 단계{tiers} ≤{lim:g}: 레벨 {len(seq)}, 바로 앞 대비 최대 ×{jm[0]:.2f} ({jm[1][1]}{jm[1][2]} {jm[1][0]:g} → {jm[2][1]}{jm[2][2]} {jm[2][0]:g}), ×2 넘는 곳 {sum(1 for x in j if x[0] > 2)}, 평균 ×{sum(x[0] for x in j)/len(j):.3f}")
        t1 = levels(nd, (1,))
        P(f"    1단계 레벨 {len(t1)} · 가격 합 {sum(x[0] for x in t1):,.0f}")
    P("  노드 안 (바뀐 노드만): 다음 레벨 배율 · 마지막/첫 (P3 원문 ≤×4) · 최소 배율 (P3 ≥×1.15)")
    for k in list(APPLY_PRICES) + ["ed_rare0"]:
        c = new[k]["costs"]
        r = [c[i] / c[i - 1] for i in range(1, len(c))]
        P(f"   {k}: {c} 배율 {['%.2f' % x for x in r]} 마지막/첫 ×{c[-1] / c[0]:.1f}" + (" (지금 ×%.1f)" % (cur[k]['costs'][-1] / cur[k]['costs'][0]) if k in cur and len(cur[k]['costs']) > 1 else ""))
    P("  줄 규칙 (BalanceSim.cs:300 TIER_LINES, 형제 I→I+ 첫 레벨 ≤ 앞 끝 ×4 NODE_STEP_MAX, :483):")
    for line in (["ed_score", "ed_score1p"], ["ed_bonus", "ed_bonus1p"], ["ps_atk", "ps_atk2"]):
        a, b = line
        for name, nd in (("지금", cur), ("제안", new)):
            P(f"   {name} {a} 끝 {nd[a]['costs'][-1]:g} → {b} 첫 {nd[b]['costs'][0]:g} = ×{nd[b]['costs'][0] / nd[a]['costs'][-1]:.1f}")
    P("  단계 경계 (1단계 끝 → 2단계 req 첫 레벨, 같은 능력):")
    for a, b in (("ed_cols", "ed_cols2"), ("ps_dur", "ps_dur2"), ("md_combo", "md_combo2"), ("ed_price", "ed_price2")):
        P(f"   {a} 끝 지금 {cur[a]['costs'][-1]:g} / 제안 {new[a]['costs'][-1]:g} → {b} {new[b]['costs'][0]:g}")
    P("  부모가 바뀐 ps_dur 아래 노드 (먼저 열림):", [(k, n['costs'][:1], n['tier']) for k, n in new.items() if n['parent'] == 'ps_dur'])

    P("\n=== ④ 판 1~12 가장 싼 것부터 (독립 구현) ===")
    cases = []
    for a in (0.1, 0.3, 0.5, 0.7, 1.0):
        cases.append((f"alpha {a}", models[a], None, 1.0, True))
    for mul in (0.5, 0.7, 1.3, 2.0):
        cases.append((f"수입 ×{mul}", m3, None, mul, True))
    cases += [("tau 최소", m3, min(taus), 1.0, True), ("tau 최대", m3, max(taus), 1.0, True),
              ("ed_rare0 없음", m3, None, 1.0, False), ("ed_rare0 없음 tau 최대", m3, max(taus), 1.0, False),
              ("alpha 1.0 수입 ×0.7", models[1.0], None, 0.7, True), ("alpha 0.1 수입 ×1.3", models[0.1], None, 1.3, True)]
    for name, m, t, mul, rn in cases:
        t = tau_mean if t is None else t
        for a in (0.1, 0.3, 0.5, 0.7, 1.0):
            if m is models[a]:
                break
        tt = [tau_inv(p, m) for p in st7]
        t = (sum(tt) / 3) if name.startswith("alpha") or name.startswith("alpha") else t
        log = run("new", new if rn else new_nr, m, t, rn, mul)
        n16 = [x["n"] for x in log[:6]]
        p5 = max((y for x in log for y in x["p5"]), key=lambda y: y[1])
        P(f"  {name:<22} 판1~6 {n16} 판7~12 {[x['n'] for x in log[6:]]} 수입 1→6→7 {log[0]['inc']:.0f}→{log[5]['inc']:.0f}→{log[6]['inc']:.0f} P5 최대 {p5[0]} ×{p5[1]:.2f}")
    P("  기준 경우 구매 내역:")
    for x in run("new", new, m3, tau_mean, True):
        P(f"   R{x['r']} inc {x['inc']:.0f} 산 것 {x['b']} 남은 {x['gold']:.0f} · P5 {[(a, round(b, 2)) for a, b in x['p5']]}")
    P("  되돌려 맞추기(backtest): 같은 근사로 가안 코드 + 지금 가격 → T3-0 실측과 비교")
    t0b = {1: [1, 2, 1, 0, 2, 2, 9], 2: [1, 2, 1, 1, 1, 4, 7], 3: [1, 2, 1, 1, 1, 1, 13]}
    bt = run("cur", cur, m3, tau_mean, False)
    P(f"   모델 {[x['n'] for x in bt[:7]]} 수입 {[round(x['inc']) for x in bt[:7]]}")
    P(f"   실측 시드1~3 {list(t0b.values())}")
    P("  몬테카를로 (판마다 수입 ×exp(N(0,0.19)) = 맞춤 RMSE, 2,000회): 판 1~6 판별 구매 수 분포")
    for name, m, mul, rn in (("기준", m3, 1.0, True), ("alpha 1.0", models[1.0], 1.0, True), ("alpha 0.1", models[0.1], 1.0, True), ("수입 ×0.5", m3, 0.5, True), ("수입 ×2", m3, 2.0, True)):
        dist = [[0] * 8 for _ in range(6)]
        bad_runs = 0
        for _ in range(2000):
            tt = rng.choice(taus)
            log = run("new", new if rn else new_nr, m, tt, rn, mul, rounds=6, noise=0.19, rng=rng)
            ok = True
            for i, x in enumerate(log):
                dist[i][min(7, x["n"])] += 1
                if not 1 <= x["n"] <= 2:
                    ok = False
            bad_runs += not ok
        P(f"   {name}: 1~6판 모두 1~2개인 비율 {1 - bad_runs / 2000:.0%}")
        for i in range(6):
            d = dist[i]
            P(f"     판{i + 1}: 0개 {d[0] / 20:.0f}% · 1~2개 {(d[1] + d[2]) / 20:.0f}% · 3개+ {sum(d[3:]) / 20:.0f}%")

    P("\n=== ⑤ 2회차 목표 영향 (같은 근사로 가안 vs 제안 비교 → 근사 편향 상쇄) ===")
    for name, m, mul in (("기준", m3, 1.0), ("alpha 1.0", models[1.0], 1.0), ("alpha 0.1", models[0.1], 1.0), ("수입 ×0.7", m3, 0.7)):
        lo = run("cur", cur, m, tau_mean, False, mul, rounds=12)
        ln = run("new", new, m, tau_mean, True, mul, rounds=12)
        co = sum(x["inc"] for x in lo)
        cn = sum(x["inc"] for x in ln)
        P(f"  {name}: 판 12 수입 가안 {lo[-1]['inc']:.0f} / 제안 {ln[-1]['inc']:.0f} (×{ln[-1]['inc'] / lo[-1]['inc']:.2f}) · 누적 {co:.0f} / {cn:.0f} · 판12까지 산 레벨 가안 {sum(lo[-1]['lv'].values())} / 제안 {sum(ln[-1]['lv'].values())}")
    # T3-0 판 8~16 판 수입 증가율 (중앙) → 판 12 수입 비를 판 지연으로 환산
    gr = []
    for s in (1, 2, 3):
        inc = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))["incR"]
        gr += [inc[i] / inc[i - 1] for i in range(8, 13)]
    P(f"  T3-0 판 9~13 판당 수입 배율 중앙 ×{stt.median(gr):.2f} (범위 ×{min(gr):.2f}~×{max(gr):.2f})")
    for ratio in (0.5, 0.7, 1.0):
        P(f"   판 수입 비 {ratio} → 지연 {math.log(1 / ratio) / math.log(stt.median(gr)):.1f}판" if ratio < 1 else "   비 1 → 지연 0")
    # 판 시간: 판 5~12 시간 차 (기준 경우 구매 시점)
    ln = run("new", new, m3, tau_mean, True)
    lvs = [{}] + [x["lv"] for x in ln[:-1]]
    t3 = json.load(open(os.path.join(T0, "states_s1.jsonl"), encoding="utf-8")) if False else None
    dsum = 0
    for r in range(1, 13):
        a = duration("new", new, r, lvs[r - 1])
        b = duration("cur", cur, r, {"ps_dur": 2 if r >= 8 else 0})     # T3-0 시드1: ps_dur 2는 판 8 뒤
        dsum += a - b
    P(f"  판 1~12 판 시간 합 차 (제안 − T3-0 시드1 근사): {dsum:+.1f}초")

    P("\n=== ⑥ ed_rare0 ===")
    # 스테이지 7 숲 풀 가중치 (RoundSim.cs:158~166): w(t) = TIERS.w × 숲 선호 3 × rareMul(t>=1) × stageMul^t
    sm = 1 + 0.03 * 6
    commons = {"양송이": 1, "느타리": 3, "구름버섯": 3, "치마버섯": 1, "무당버섯": 1, "노란다발": 3}
    gen2 = {1: {"표고": 3, "간버섯": 1, "흰목이": 1, "노란각시버섯": 1}, 2: {"표고": 3, "간버섯": 1}, 3: {"표고": 3, "팽이버섯": 1, "간버섯": 1}}
    for s, g2 in gen2.items():
        wc = 50 * (sum(commons.values()) + sum(g2.values()))
        for r in (0.2, 0.45, 0.7, 1.0):
            we, wu = 25 * sm * r * 2, 15 * sm * sm * r
            P(f"  시드{s} 풀 rareMul {r}: 에픽 {we / (wc + we + wu):.2%} 유니크 {wu / (wc + we + wu):.2%} (rareMul 1 대비 유니크 몫 ×{(wu / (wc + we + wu)) / ((15 * sm * sm) / (wc + 25 * sm * 2 + 15 * sm * sm)):.3f})")
    P("  초반 6종 등급:", "모두 0등급 → ed_rare0는 스테이지 1~6에 영향 없음")
    P("  ID 충돌: Upg_ID 13033", 13033 in {u['Upg_ID'] for u in DB['Upgrade']}, "· Cost_ID 90090", 90090 in {c['Cost_ID'] for c in DB['Upgrade_Cost']},
      "· Str 303033/203033", [s['Str_ID'] for s in DB['String'] if s['Str_ID'] in (303033, 203033)], "· Str_Group 23033", [s['Str_ID'] for s in DB['String'] if s.get('Str_Group_ID') == 23033])
    P("  자동 계단 6→7 (노드 그대로: ed_score f, stump 1, ed_cols 2, ps_dur 2, ed_rare0 0, md_map 0), alpha 0.3:")
    for fl in (0, 2, 3):
        lv = {"ed_score": fl, "dev_stump": 1, "ed_cols": 2, "ps_dur": 2}
        a6 = income("new", new, 6, lv, m3, tau_mean, True)
        a7 = income("new", new, 7, lv, m3, tau_mean, True)
        b7 = income("new", new_nr, 7, lv, m3, tau_mean, False)
        a7x = income("new", new, 7, lv, m3, max(taus), True)
        P(f"   f={fl}: 제안+ed_rare0 ×{a7 / a6:.2f} (tau 최대 ×{a7x / a6:.2f}) · ed_rare0 없음 ×{b7 / a6:.2f}")


if __name__ == "__main__":
    main()
