# -*- coding: utf-8 -*-
"""R3 개정(R3-11~) 판 수입 근사 — T3-α 실측(군락 수 c × 판 시간 T)으로 포화식을 맞추고, T3-0으로 바깥 확인.

근사식 (0등급만 나오는 스테이지 2~6, 노드 power 1 기준):
    Y(c, T) = A * (T/10)^b * c^p / (c^p + K^p),   K = K0 * (T/10)^e
    판 수입 I = g * [ (1 + f + tau) * rho * Y(c, T) + (1 + f) * ( stump * S(T) + qL * N ) ]
      g  정산 배율 1.1 + (ed_price 효과 - 1)          (Game.SettleBonus)
      f  타격 점수 고정값 (ed_score)                  (RoundSim.Balls.cs:305 (등급 점수 + f))
      tau 높은 등급 몫 (스테이지 7부터)
      rho 재생 보정 (T3-α는 EARLY_REGEN_MUL 1.4/1.2가 걸린 채 측정 — 제안은 삭제. 기준 1.0, 민감도 1.15)
      S(T) 그루터기 몫 (T3-α c=3, 점수 ×5 측정을 ×3으로 환산: ×0.6)
      qL·N 낱개 버섯 N개 몫 (T3-0 튜토리얼 판 = 군락 1 + 낱개 8로 맞춤)
Y는 '판 수입 / (g·(1+f))'. T3-α는 f=1, g=1.1이라 Y = net / 2.2.
실행: python BalanceData/theory/r3b_fit.py → r3b_fit.json (난수 없음)
"""
import json
import math
import os
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ALPHA = os.path.join(ROOT, "BalanceData", "round3_alpha")
T0 = os.path.join(ROOT, "BalanceData", "round3_t0")
G_T3A = 1.1
F_T3A = 1
STUMP_OLD, STUMP_NEW = 5, 3
TUTORIAL_LOOSE = 8
EARLY_COL_MUL_DRAFT = [0.4, 0.45, 0.5, 0.6, 0.7, 0.85]
MAP_ZOOM = [1.5, 1.35, 1.2, 1.1, 1.0]
ED_PRICE_EFX = [1.0, 1.3, 1.6, 1.9]


def nelder_mead(fn, x0, step=0.2, iters=4000, tol=1e-10):
    n = len(x0)
    pts = [list(x0)] + [[x0[j] + (step if i == j else 0) for j in range(n)] for i in range(n)]
    vals = [fn(p) for p in pts]
    for _ in range(iters):
        order = sorted(range(n + 1), key=lambda i: vals[i])
        pts = [pts[i] for i in order]
        vals = [vals[i] for i in order]
        if abs(vals[-1] - vals[0]) < tol:
            break
        cen = [sum(p[j] for p in pts[:-1]) / n for j in range(n)]
        xr = [cen[j] + (cen[j] - pts[-1][j]) for j in range(n)]
        fr = fn(xr)
        if fr < vals[0]:
            xe = [cen[j] + 2 * (cen[j] - pts[-1][j]) for j in range(n)]
            fe = fn(xe)
            pts[-1], vals[-1] = (xe, fe) if fe < fr else (xr, fr)
        elif fr < vals[-2]:
            pts[-1], vals[-1] = xr, fr
        else:
            xc = [cen[j] + 0.5 * (pts[-1][j] - cen[j]) for j in range(n)]
            fc = fn(xc)
            if fc < vals[-1]:
                pts[-1], vals[-1] = xc, fc
            else:
                for i in range(1, n + 1):
                    pts[i] = [pts[0][j] + 0.5 * (pts[i][j] - pts[0][j]) for j in range(n)]
                    vals[i] = fn(pts[i])
    i = min(range(n + 1), key=lambda k: vals[k])
    return pts[i], vals[i]


def Y(par, c, T):
    A, b, p, K0, e = par
    K = K0 * (T / 10) ** e
    return A * (T / 10) ** b * c ** p / (c ** p + K ** p)


def load_alpha(seeds=(1, 2, 3)):
    rows = []
    for s in seeds:
        rows += [json.loads(l) for l in open(os.path.join(ALPHA, f"alpha_s{s}.jsonl"), encoding="utf-8")]
    return rows


def cells(rows, stump=0):
    d = defaultdict(list)
    for r in rows:
        if r["stump"] == stump:
            d[(r["stage"], r["c"])].append(r)
    out = []
    for (stg, c), v in sorted(d.items()):
        out.append(dict(stage=stg, c_set=c, c=sum(r["cols"] for r in v) / len(v), T=v[0]["dur"],
                        Y=sum(r["net"] for r in v) / len(v) / (G_T3A * (1 + F_T3A)), n=len(v)))
    return out


def fit_hill(pts):
    def err(x):
        par = [math.exp(x[0]), x[1], math.exp(x[2]), math.exp(x[3]), x[4]]
        return sum((math.log(Y(par, q["c"], q["T"])) - math.log(q["Y"])) ** 2 for q in pts)
    best = None
    for x0 in ([math.log(60), 1.0, math.log(1.5), math.log(4), 0.5], [math.log(80), 1.2, math.log(2.0), math.log(6), 0.0],
               [math.log(50), 0.8, math.log(1.2), math.log(3), 1.0]):
        x, v = nelder_mead(err, x0)
        x, v = nelder_mead(err, x, step=0.05)
        if best is None or v < best[1]:
            best = (x, v)
    x, v = best
    par = [math.exp(x[0]), x[1], math.exp(x[2]), math.exp(x[3]), x[4]]
    return par, math.sqrt(v / len(pts))


def stump_gain(rows):
    """그루터기 몫 / (g(1+f)) — c=3, 점수 ×5 실측. 시드별 값도 낸다"""
    out = {}
    for stg in (3, 5):
        a = [r for r in rows if r["stage"] == stg and r["c"] == 3]
        on = [r["net"] for r in a if r["stump"] == 1]
        off = [r["net"] for r in a if r["stump"] == 0]
        T = a[0]["dur"]
        seeds = {}
        for s in (1, 2, 3):
            on_s = [r["net"] for r in a if r["stump"] == 1 and r["seed"] == s]
            off_s = [r["net"] for r in a if r["stump"] == 0 and r["seed"] == s]
            seeds[s] = (sum(on_s) / len(on_s) - sum(off_s) / len(off_s)) / (G_T3A * (1 + F_T3A))
        out[T] = dict(mean=(sum(on) / len(on) - sum(off) / len(off)) / (G_T3A * (1 + F_T3A)), seeds=seeds)
    return out


def area(map_lv):
    z = MAP_ZOOM[map_lv]
    return (1 / (z * z)) ** 0.7


def js_round(x):
    return math.floor(x + 0.5)


def maxcol_draft(stage, nodes):
    base = max(3, math.ceil(12 + 5 * nodes.get("ed_cols", 0)))
    c = max(4, js_round(base * area(nodes.get("md_map", 0))))
    return max(1, js_round(c * EARLY_COL_MUL_DRAFT[stage - 1])) if stage <= 6 else c


def time_draft(stage, nodes):
    base = 10 if stage <= 4 else 15 + min(10, 0.5 * (stage - 1))
    return base + [0, 2, 4, 6][min(3, nodes.get("ps_dur", 0))]


def stump_S(sg, T, mul):
    """그루터기 몫 S(T): 10초·17초 두 점 사이 직선, 아래는 0. mul = 새 배율/5"""
    t10, t17 = sg[10]["mean"], sg[17]["mean"]
    slope = (t17 - t10) / 7
    return max(0.0, (t10 + slope * (T - 10)) * mul)


def t0_validate(par, sg, qL):
    """T3-0 판 2~6 (가안 코드, 시드 1~3) — 맞춤에 안 쓴 자료로 바깥 확인. 판 1(튜토리얼)은 qL 맞춤에 씀"""
    pts = []
    for s in (1, 2, 3):
        st = [json.loads(l) for l in open(os.path.join(T0, f"states_s{s}.jsonl"), encoding="utf-8")]
        ph = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))
        for i in range(1, 6):
            n = st[i - 1]["nodes"]
            stg = ph["stageR"][i]
            T = time_draft(stg, n)
            c = maxcol_draft(stg, n)
            g = 1.1 + ED_PRICE_EFX[n.get("ed_price", 0)] - 1
            f = n.get("ed_score", 0)
            pred = g * (1 + f) * (Y(par, c, T) + n.get("dev_stump", 0) * stump_S(sg, T, 1.0))
            pts.append(dict(seed=s, stage=stg, c=c, T=T, f=f, stump=n.get("dev_stump", 0),
                            md_bar=n.get("md_bar", 0), md_combo=n.get("md_combo", 0), obs=ph["incR"][i], pred=round(pred, 1),
                            lnerr=round(math.log(pred / ph["incR"][i]), 3)))
    e = [p["lnerr"] for p in pts]
    return pts, dict(rmse=round(math.sqrt(sum(x * x for x in e) / len(e)), 3), bias=round(sum(e) / len(e), 3),
                     max_abs=round(max(abs(x) for x in e), 3))


def tutorial_qL(par):
    incs = []
    for s in (1, 2, 3):
        ph = json.load(open(os.path.join(T0, f"phase_s{s}.json"), encoding="utf-8"))
        incs.append(ph["incR"][0])
    inc = sum(incs) / len(incs)
    col = 1.1 * Y(par, 1, 10)
    return (inc - col) / (TUTORIAL_LOOSE * 1.1), dict(tutorial_inc=incs, mean=round(inc, 2), colony_part=round(col, 2))


def loso(rows):
    """시드 하나 빼고 맞춘 뒤 빼낸 시드의 칸 평균 예측 오차(ln)"""
    out = {}
    for s in (1, 2, 3):
        tr = [r for r in rows if r["seed"] != s]
        te = [r for r in rows if r["seed"] == s]
        par, _ = fit_hill(cells(tr))
        errs = [math.log(Y(par, q["c"], q["T"]) / q["Y"]) for q in cells(te)]
        out[s] = dict(rms=round(math.sqrt(sum(x * x for x in errs) / len(errs)), 3), max=round(max(abs(x) for x in errs), 3))
    return out


def piecewise_alpha(par):
    out = {}
    for T in (10, 12, 17):
        out[T] = {f"{a}-{b}": round(math.log(Y(par, b, T) / Y(par, a, T)) / math.log(b / a), 2) for a, b in ((1, 2), (2, 3), (3, 6), (6, 10), (10, 15))}
    return out


def main():
    rows = load_alpha()
    pts = cells(rows)
    par, rmse = fit_hill(pts)
    sg = stump_gain(rows)
    qL, tut = tutorial_qL(par)
    val, vstat = t0_validate(par, sg, qL)
    within = [math.log(max(1, r["net"])) for r in rows]
    out = dict(
        params=dict(A=round(par[0], 3), b=round(par[1], 3), p=round(par[2], 3), K0=round(par[3], 3), e=round(par[4], 3)),
        fit_rmse_ln=round(rmse, 3), fit_points=[dict(stage=q["stage"], c=round(q["c"], 2), T=q["T"], Y=round(q["Y"], 2),
                                                       pred=round(Y(par, q["c"], q["T"]), 2)) for q in pts],
        loso=loso(rows), piecewise_alpha=piecewise_alpha(par),
        stump=sg, stump_S_new={T: round(stump_S(sg, T, STUMP_NEW / STUMP_OLD), 2) for T in (10, 11, 12, 14, 17)},
        qL=round(qL, 3), tutorial=tut, t0_validation=vstat, t0_points=val)
    json.dump(out, open(os.path.join(HERE, "r3b_fit.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps({k: v for k, v in out.items() if k not in ("t0_points",)}, ensure_ascii=False, indent=0))
    for p in val:
        print(p)
    return out


if __name__ == "__main__":
    main()
