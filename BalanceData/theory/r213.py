# -*- coding: utf-8 -*-
"""R2-13 (manager#10): 단계 노드 줄(공격력·점수·보너스)의 효율 넘김 체인이 1단계 가격을 묶는 문제.

1) 줄마다 EnforceShape 규칙(노드 안 ×4 · 같은 단계 형제 ×4 · 단계 경계 per[t]/per[t-1]×cross)으로
   낼 수 있는 최대 가격 폭을 구한다. 명목(규칙 그대로)과 실효(EnforceShape의 여유 ×1.05 · ×1.06 반영) 둘.
2) "1단계 끝 → 3단계 첫 레벨" 구간(2단계 노드만 사이에 있음)의 허용 폭을, 그 구간에 필요한 수입 폭과 비교한다.
3) 지난 실행(round2_fit2_job.log, m = 0.41·0.44·3.8)의 1단계 줄 가격 바닥을 이 체인으로 재현한다(검산).
4) 고치는 안별로 1단계 줄 바닥·3단계 첫 가격을 낸다.

python r213.py → theory_r213.json (결정적, 난수 없음)
"""
import csv, json, math, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
NODE_STEP = 4.0
EFF_NODE = NODE_STEP / 1.05        # RaiseNode: v[i] >= v[i+1]/4*1.05
EFF_CROSS_MARGIN = 1.06            # need*1.06

LINES = {
    "atk":   (["ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"], [1, 2.5, 2.5, 6, 6, 15], 0.53),
    "score": (["ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p"], [1, 1, 10, 10, 100, 100], 0.66),
    "bonus": (["ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p"], [1, 1, 10, 10, 100, 100], 0.66),
}

db = json.load(open(os.path.join(ROOT, "Assets", "Resources", "DB", "db.json"), encoding="utf-8"))
UC = {r["Cost_ID"]: r for r in db["Upgrade_Cost"]}
ROWS = {r["Upg_Key"]: r for r in db["Upgrade"]}
def info(k):
    r = ROWS[k]; c = UC[r["Cost_ID"]]; n = int(r["Efx_MaxLv"])
    return dict(tier=int(r["Tier"]), max=n, cost=[c["Cost_Lv%d" % i] for i in range(1, n + 1)])

# 수입: R1~R20은 시드 기록(manager#10), 그 뒤는 r212 모델 곡선(일반 판 기준)
SIM_INC = {1: 60, 2: 268, 3: 306, 4: 1736, 20: 45604}
curve = {int(x["r"]): float(x["inc"]) for x in csv.DictReader(open(os.path.join(HERE, "theory_boss_curve_r212.csv"), encoding="utf-8"))}
def inc(r):
    ks = sorted(curve); k = max(x for x in ks if x <= r); return curve[k]


def steps(ids, per, cross, eff):
    """줄의 각 레벨 사이 허용 배율(앞 → 뒤) 목록: (from, to, ratio, kind)"""
    out = []
    for t, k in enumerate(ids):
        n = info(k)["max"]
        if t > 0:
            if per[t] == per[t - 1]:
                out.append((ids[t - 1], k, NODE_STEP / (EFF_CROSS_MARGIN if eff else 1), "형제"))
            else:
                out.append((ids[t - 1], k, per[t] / per[t - 1] * cross / (EFF_CROSS_MARGIN if eff else 1), "단계"))
        for _ in range(n - 1):
            out.append((k, k, EFF_NODE if eff else NODE_STEP, "노드 안"))
    return out


def span(ids, per, cross, eff, a=None, b=None):
    """a 노드 마지막 레벨 → b 노드 첫 레벨 (None이면 줄 처음/끝)"""
    st = steps(ids, per, cross, eff)
    # 레벨 목록을 펼쳐 위치로 자른다
    lv = []
    for k in ids: lv += [(k, i) for i in range(info(k)["max"])]
    i0 = 0 if a is None else max(j for j, x in enumerate(lv) if x[0] == a)
    i1 = len(lv) - 1 if b is None else min(j for j, x in enumerate(lv) if x[0] == b)
    return math.prod(s[2] for s in st[i0:i1])


res = {}
for name, (ids, per, cross) in LINES.items():
    tiers = [info(k)["tier"] for k in ids]
    end1 = [k for k, t in zip(ids, tiers) if t == 1][-1]
    start3 = [k for k, t in zip(ids, tiers) if t == 3][0]
    r = dict(levels=sum(info(k)["max"] for k in ids),
             full_nom=span(ids, per, cross, False), full_eff=span(ids, per, cross, True),
             end1=end1, start3=start3,
             gap13_nom=span(ids, per, cross, False, end1, start3), gap13_eff=span(ids, per, cross, True, end1, start3),
             in3_nom=span(ids, per, cross, False, start3, None) * 1.0,
             boundaries=[(s[0], s[1], round(s[2], 3)) for s in steps(ids, per, cross, False) if s[3] != "노드 안"])
    # 지난 실행 재현: 1단계 줄 끝 바닥 = 3단계 첫 레벨(DB × 3.8) / 실효 폭
    p3 = info(start3)["cost"][0]
    r["db_end1"] = info(end1)["cost"][-1]; r["db_start3"] = p3
    r["floor_end1_m3.8"] = p3 * 3.8 / r["gap13_eff"]
    r["floor_end1_m1"] = p3 / r["gap13_eff"]
    # 1단계 줄 끝을 1e5로 둘 때 3단계 첫 레벨 상한
    r["cap_start3_if_end1_1e5_eff"] = 1e5 * r["gap13_eff"]
    res[name] = r

# 필요한 폭: 1단계 끝(R19~20) → 3단계 첫 구매(R62 모델 / R88 목표). 가격 ≈ 판 수입 × f 로 같은 f를 쓰면 폭 = 수입 비
need = dict(
    sim_R20=SIM_INC[20], model_R19=inc(19), model_R62=inc(62), model_R88=inc(88), model_R165=inc(165),
    need_R20_to_R62_sim=inc(62) / SIM_INC[20], need_R20_to_R88_sim=inc(88) / SIM_INC[20],
    need_R19_to_R88_model=inc(88) / inc(19), need_R1_to_R165=inc(165) / SIM_INC[1],
)

# 안별 수치
atk = res["atk"]
opts = {
    "now(m3=3.8)": dict(start3=info("ps_atk3")["cost"][0] * 3.8, end1_floor=atk["floor_end1_m3.8"]),
    "a(경계 보고만)": dict(start3=info("ps_atk3")["cost"][0] * 3.8, end1_floor=None),
    "d(3단계 첫 가격 상한, 끝1=1e5)": dict(start3=atk["cap_start3_if_end1_1e5_eff"], end1_floor=1e5,
                                    start3_rounds_R88=atk["cap_start3_if_end1_1e5_eff"] / inc(88),
                                    start3_rounds_R62=atk["cap_start3_if_end1_1e5_eff"] / inc(62)),
    "e(2단계 레벨 +3, ×64)": dict(gap13_eff=atk["gap13_eff"] * EFF_NODE ** 3, start3_cap=1e5 * atk["gap13_eff"] * EFF_NODE ** 3),
}

out = dict(lines=res, need=need, options=opts)
json.dump(out, open(os.path.join(HERE, "theory_r213.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=float)

def g(x): return "%.3g" % x
for n, r in res.items():
    print(n, "레벨", r["levels"], "전체 명목", g(r["full_nom"]), "실효", g(r["full_eff"]),
          "|", r["end1"], "→", r["start3"], "명목", g(r["gap13_nom"]), "실효", g(r["gap13_eff"]),
          "| 3단계 안 명목", g(r["in3_nom"]),
          "| DB", g(r["db_end1"]), g(r["db_start3"]), "비", g(r["db_start3"] / r["db_end1"]),
          "| 바닥(m3 3.8)", g(r["floor_end1_m3.8"]), "(m3 1)", g(r["floor_end1_m1"]),
          "| 상한(끝1 1e5)", g(r["cap_start3_if_end1_1e5_eff"]))
    print("   경계:", r["boundaries"])
print({k: g(v) for k, v in need.items()})
for k, v in opts.items(): print(k, {a: (g(b) if isinstance(b, float) else b) for a, b in v.items()})


# 안 d(줄 상한 모드) 시뮬: DB × m[t] 가격에서 줄을 앞에서부터 상한(앞 끝 × 허용 배율)으로 내린다
def cap_mode(m, eff=True):
    out = {}
    for name, (ids, per, cross) in LINES.items():
        prev = None; tab = {}
        for t, k in enumerate(ids):
            I = info(k); v = [c * m[I["tier"]] for c in I["cost"]]
            if prev is not None:
                ratio = (NODE_STEP if per[t] == per[t - 1] else per[t] / per[t - 1] * cross) / (EFF_CROSS_MARGIN if eff else 1)
                v[0] = min(v[0], prev * ratio)
            for i in range(1, len(v)):
                v[i] = min(max(v[i], v[i - 1]), v[i - 1] * (EFF_NODE if eff else NODE_STEP))
            tab[k] = v; prev = v[-1]
        out[name] = tab
    return out

M = {1: 0.3, 2: 0.44, 3: 3.8}
cm = cap_mode(M)
print("\n안 d, m =", M)
for name, tab in cm.items():
    for k, v in tab.items():
        db_m = [c * M[info(k)["tier"]] for c in info(k)["cost"]]
        print("  %-11s" % k, [g(x) for x in v], "DB×m 대비", [round(x / y, 3) for x, y in zip(v, db_m)])
# R21 이후 한 판 수입으로 바로 살 수 있는 공격력 레벨 수(모델 R21 수입, 시드 기록 ×10·×100)
for lab, I21 in (("모델 R21", inc(21)), ("시드 R20×10", SIM_INC[20] * 10), ("시드 R20×100", SIM_INC[20] * 100)):
    tot = 0; nlv = 0; eff_add = 0
    for t, k in enumerate(LINES["atk"][0]):
        if info(k)["tier"] != 2: continue
        for x in cm["atk"][k]:
            if tot + x <= I21: tot += x; nlv += 1; eff_add += LINES["atk"][1][t]
    print("  %s %.3g: 2단계 공격력 %d레벨(+%.1f) 한 판에" % (lab, I21, nlv, eff_add))
out["cap_mode"] = dict(m=M, prices={n: {k: v for k, v in t.items()} for n, t in cm.items()})
json.dump(out, open(os.path.join(HERE, "theory_r213.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1, default=float)
