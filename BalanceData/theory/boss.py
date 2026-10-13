# -*- coding: utf-8 -*-
"""2회차(2026-10-13) 보스 관문 모델 + 판 단위 진행.

- 게임 정의는 Assets/Resources/DB/db.json(엑셀 DB 내보내기)에서 직접 읽는다(Assets/는 읽기만).
- 판 수입(일반 판)은 1회차 근사식(theory_fit_tuned.json 계수, RMSE 0.24 log10)을 쓴다. 공격력은 들어가지 않는다.
- 보스 판은 관문 공식(gate)으로 처치 여부·처치 시간을 정하고, 수입은 보스 타수 × 타당 점수로 근사한다.
- 결정적: 날씨는 보스 도전 순서대로 storm → wind → cold 를 돌린다(난수 없음).

python boss.py              # 지금 DB 값으로 진행 + 보스 표
python boss.py --plan       # 2회차 제안안(PLAN)으로 진행
결과: theory_boss_now.json / theory_boss_plan.json, theory_boss_curve_*.csv
"""
import csv, json, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DB = json.load(open(os.path.join(ROOT, "Assets", "Resources", "DB", "db.json"), encoding="utf-8"))

# ---------- 게임 상수 (코드 그대로) ----------
THEME_FROM = [1, 20, 45, 75, 105]
TIER_HP = [1, 4, 12, 40]          # tuned 프리셋 (Defs.cs:90)
TIER_SCORE = [1, 5, 20, 100]
COL0, COLSTEP = 12, 5             # tuned
MAP_ZOOM = [1.5, 1.35, 1.2, 1.1, 1.0]
OVERHEAD = 30.0                   # 한 판 = 라운드 시간 + 30초
BOSS_EVERY = 10
LOOK_TIER = {"king": 3, "cleaner": 1, "angel": 2, "phantom": 1, "mover": 3}   # 보스 그림 버섯 등급 (verify 2회차 A: 송이3·선녀낙엽1·독우산광대2·광대1·동충하초3)

CONST = {r["Const_Key"]: r["Value"] for r in DB["Const"]}
BOSSES = [dict(id=r["Boss_Key"], mul=r["Hp_Mul"]) for r in DB["Boss"]]
SKILL_KEYS = [s["Skill_Key"] for s in DB["Skill"]]

# ---------- 관문 공식 파라미터 (현황/theory.md "공유할 결과") ----------
SCORE_MUL = 1.0   # Const BOSS_SCORE_MUL (R2-11). plan.py가 정한다
GATE = dict(H0=0.9, KB=0.5, MOVER_H=0.6, TAU=0.2, T0=2.0, PHANTOM=1.99)
BOSS_WEATHERS = ["storm", "wind", "cold"]

# ---------- 판 수입 근사 (1회차 3판 계수) ----------
FIT = json.load(open(os.path.join(HERE, "theory_fit_tuned.json"), encoding="utf-8"))["params"]
# 공격력 배율(1.1 + 별 공격력)을 판의 함수로: 2회차 T1a v2(관문 모드, 시드 1) 판별 atkR ÷ 리플레이 고정값. 62판 뒤는 1회차 상한 2.21
A4 = [(1, 1.1), (10, 1.12), (15, 1.12), (20, 1.24), (25, 1.31), (30, 1.36), (35, 1.49), (40, 1.78), (45, 2.01), (55, 2.07), (60, 2.18), (80, 2.21), (400, 2.21)]
# 판 수입 보정(log10, 지역별): 1회차 근사식을 2회차 T1a v2(게임 지역 규칙 + 보스 관문)에 맞춘 평균 잔차. 숲 n25 · 밤 n23 · 들판 n5, 보정 뒤 RMSE 0.30
INC_BIAS = [0.218, 0.485, 0.542, 0.542, 0.542]


def load_nodes():
    cost = {c["Cost_ID"]: c for c in DB["Upgrade_Cost"]}
    by_id = {u["Upg_ID"]: u["Upg_Key"] for u in DB["Upgrade"]}
    out = []
    for u in DB["Upgrade"]:
        c = cost[u["Cost_ID"]]
        mx = int(u["Efx_MaxLv"])
        out.append(dict(id=u["Upg_Key"], br=u["Branch"], tier=u["Tier"], max=mx,
                        efx=[u["Efx_Lv%d" % i] for i in range(5)],
                        gold=[c["Cost_Lv%d" % i] for i in range(1, mx + 1)],
                        gem=[c["Gem_Lv%d" % i] for i in range(1, mx + 1)],
                        parent=by_id.get(u["Upg_Pre"]), req=by_id.get(u["Upg_Req"])))
    return out


NODES = load_nodes()
NODE = {n["id"]: n for n in NODES}
GOLD_NODES = [n for n in NODES if max(n["gem"]) <= 0]


def E(lv, k):
    n = NODE[k]
    return n["efx"][min(max(lv.get(k, 0), 0), n["max"])]


def a4(r):
    for (a, va), (b, vb) in zip(A4, A4[1:]):
        if r <= b:
            return va + (vb - va) * (r - a) / (b - a)
    return A4[-1][1]


def zone_of(stage):
    return max(i for i, f in enumerate(THEME_FROM) if stage >= f)


def zone_mul(stage):
    return CONST["ZONE_STEP"] ** zone_of(stage) * CONST["STAGE_GROWTH"] ** (stage - 1)


def stats(lv, stage, rounds, weather="wind", gm=None):
    gm = gm or {}
    atk_flat = 1 + sum(E(lv, k) for k in ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"))
    spd = E(lv, "ps_spd") * (0.8 if weather == "storm" else 1)
    c = math.ceil(COL0 + COLSTEP * E(lv, "ed_cols")) + E(lv, "ed_cols2")
    area = (1 / MAP_ZOOM[lv.get("md_map", 0)] ** 2) ** 0.7
    return dict(
        atk=atk_flat * a4(rounds) * gm.get("atk", 1), atk_flat=atk_flat,
        crit=E(lv, "ps_crit"), critMul=E(lv, "ps_crit2"), heavy=E(lv, "ps_heavy"), bossMul=gm.get("boss", 1),
        spd=spd, balls=1 + E(lv, "ps_ball") + E(lv, "ps_ball2") + E(lv, "ps_ball3"),
        dur=15 + min(10, 0.5 * (stage - 1)) + E(lv, "ps_dur") + E(lv, "ps_dur2") - (3 if weather == "cold" else 0),
        temp=lv.get("md_kid", 0) > 0 or lv.get("split", 0) > 0 or lv.get("clone", 0) > 0 or lv.get("barclone", 0) > 0,
        cols=max(4, round(max(3, c) * area)), comboK=E(lv, "md_combo") + E(lv, "md_combo2"),
        sflat=sum(E(lv, k) for k in ("ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p")),
        nsk=sum(1 for k in SKILL_KEYS if lv.get(k, 0) > 0), Z=zone_mul(stage), zone=zone_of(stage))


def income(s):
    """일반 판 정산 골드 (1회차 근사식, 지역 상수는 게임 규칙 지역 = 스테이지로 연 최신 지역)"""
    c = FIT["coef"]
    z = [0, c[4], c[5], c[6], c[6]][s["zone"]]
    lg = (math.log10(s["Z"] * (FIT["sbar"] + s["sflat"]) * s["dur"] * s["comboK"]) + c[0]
          + c[1] * math.log10(s["balls"]) + c[2] * math.log10(s["cols"]) + c[3] * s["nsk"] + z + INC_BIAS[s["zone"]])
    return 10 ** lg


def boss_hp(boss, stage, k=None, extra=None):
    """Defs.BossHp: 레전드리 체력 × BOSS_HP_K × ZoneMul(최신 지역) × Hp_Mul (× 제안안의 스테이지 항)"""
    k = CONST["BOSS_HP_K"] if k is None else k
    hp = TIER_HP[3] * k * zone_mul(stage) * boss["mul"]
    return hp * (extra(stage) if extra else 1)


def gate(s, boss, hp, g=GATE):
    """관문 판정. 반환 (처치 여부, 처치 시간 t(초, 불가면 inf), 싸울 수 있는 시간 T, DPS)"""
    A = s["atk"] * s["heavy"] * s["bossMul"] * (1 + s["crit"] * (s["critMul"] - 1))
    H = g["H0"] * s["spd"] * (1 + g["KB"] * (s["balls"] - 1))
    if boss["id"] == "mover":
        H *= g["MOVER_H"]
    if boss["id"] != "angel" and s["temp"]:
        H *= 1 + g["TAU"]
    dps = A * H
    T = s["dur"] - g["T0"]
    if boss["id"] == "cleaner":
        r = CONST["CLEANER_HEAL"] / CONST["CLEANER_INT"]
        t = hp / (dps - r * hp) if dps > r * hp else math.inf
    elif boss["id"] == "phantom":
        t = g["PHANTOM"] * hp / dps
    else:
        t = hp / dps
    return t <= T, t, T, dps


def need_atk(boss, hp, dur, spd=1.36, balls=1, temp=False, g=GATE):
    """처치에 필요한 실효 공격력 A(= atk×heavy×bossMul×치명 기대값)"""
    T = dur - g["T0"]
    H = g["H0"] * spd * (1 + g["KB"] * (balls - 1)) * (g["MOVER_H"] if boss["id"] == "mover" else 1) * (1 + g["TAU"] if boss["id"] != "angel" and temp else 1)
    if boss["id"] == "cleaner":
        r = CONST["CLEANER_HEAL"] / CONST["CLEANER_INT"]
        return hp * (1 / T + r) / H
    return (g["PHANTOM"] if boss["id"] == "phantom" else 1) * hp / (T * H)


def boss_income(s, boss, hits, killed, look_tier=None):
    """보스 판 골드: 보스만 나온다(군락 없음). 타당 점수 = (그림 등급 점수 + 고정값) × 지역 × 보스 ×3 × 콤보(평균 5회) [근사]"""
    per = (TIER_SCORE[LOOK_TIER[boss["id"]] if look_tier is None else look_tier] + s["sflat"]) * s["Z"] * 3 * (1 + 5 * s["comboK"])
    g = hits * per * SCORE_MUL
    return g * (1 + CONST["BOSS_REWARD_MUL"]) if killed else g * CONST["BOSS_FAIL_GOLD"]


def bulk_buy(lv, gold, prices=None):
    """Game.BulkBuy: 살 수 있는 다음 레벨 중 가장 싼 것부터 전부 (골드 노드만)"""
    bought = []
    while True:
        best = None
        for n in GOLD_NODES:
            L = lv.get(n["id"], 0)
            if L >= n["max"]:
                continue
            if n["parent"] and lv.get(n["parent"], 0) < 1:
                continue
            if n["req"] and lv.get(n["req"], 0) < NODE[n["req"]]["max"]:
                continue
            p = (prices or {}).get(n["id"], n["gold"])[L]
            if best is None or p < best[0]:
                best = (p, n["id"])
        if best is None or best[0] > gold:   # Game.BulkBuy: 가장 싼 것을 못 사면 멈춘다
            return gold, bought
        gold -= best[0]
        lv[best[1]] = lv.get(best[1], 0) + 1
        bought.append(best[1])


ATK_KEYS = ("ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4", "ps_crit", "ps_crit2", "ps_heavy", "ps_spd", "ps_ball", "ps_ball2", "ps_ball3", "ps_dur", "ps_dur2")


def progression(cfg):
    """판 단위 진행. cfg: k(BOSS_HP_K), mul{boss:Hp_Mul}, extra(stage→배율), prices, inc_mul, gems(bool), retry(판), stop_stage, max_rounds"""
    lv, gold, gem, t, stage, r = {}, 0.0, 0, 0.0, 1, 0
    gm = {"atk": 1.0, "boss": 1.0}
    gm_lv = {"gm_boss": 0, "gm_atk": 0}
    boss_on, last_try, wcount, last_norm = True, 0, 0, 0.0
    bosses = [dict(b, mul=cfg.get("mul", {}).get(b["id"], b["mul"])) for b in BOSSES]
    log, rec, snaps, done = {}, [], [{}], {}
    while r < cfg.get("max_rounds", 400):
        r += 1
        is_boss = stage % BOSS_EVERY == 0
        b = bosses[(stage // BOSS_EVERY - 1) % len(bosses)] if is_boss else None
        if is_boss and boss_on:
            w = BOSS_WEATHERS[wcount % 3]; wcount += 1
            s = stats(lv, stage, r, w, gm)
            hp = cfg["hp"][stage] if stage in cfg.get("hp", {}) else boss_hp(b, stage, cfg.get("k"), cfg.get("extra"))
            ok, tk, T, dps = gate(s, b, hp)
            hits = (min(tk, T) * dps) / max(1e-9, s["atk"] * s["heavy"] * s["bossMul"] * (1 + s["crit"] * (s["critMul"] - 1)))
            inc = boss_income(s, b, hits, ok, cfg.get("look_tier")) * cfg.get("inc_mul", 1)
            if ok and cfg.get("reward_rounds"):   # 제안 (c): 처치 보상 = 직전 일반 판 골드 × BOSS_REWARD_ROUNDS
                inc = max(inc, last_norm * cfg["reward_rounds"])
            e = log.setdefault(stage, dict(boss=b["id"], hp=hp, tries=0, first_r=r, first_min=t / 60))
            e["tries"] += 1
            last_try = r
            if ok:
                sec = tk + GATE["T0"] + 1.5 + OVERHEAD
                e.update(kill_r=r, kill_min=(t + sec) / 60, atk=s["atk"], dps=dps, t=tk, T=T, inc_kill=inc)
                gem += int(CONST["BOSS_GEM_BASE"] + CONST["BOSS_GEM_STEP"] * (stage // BOSS_EVERY))
                stage += 1
            else:
                sec = s["dur"] + OVERHEAD
                boss_on = False
                if cfg.get("fail_rounds") is not None:   # R2-11 안 B: 진 판 net = 직전 일반 판 × BOSS_FAIL_GOLD
                    inc = last_norm * cfg["fail_rounds"]
            kind = "kill" if ok else "fail"
        else:
            s = stats(lv, stage, r, "clear", gm)
            inc = income(s) * cfg.get("inc_mul", 1)
            last_norm = inc
            sec = s["dur"] + OVERHEAD
            kind = "farm" if is_boss else "run"
            if not is_boss:
                stage += 1
        t += sec
        gold += inc
        gold, bought = bulk_buy(lv, gold, cfg.get("prices"))
        if cfg.get("gems"):   # 균사석은 보스 피해 → 공격력 순으로 (gm 가격: DB 그대로)
            for key in ("gm_boss", "gm_atk", "gm_boss", "gm_atk", "gm_boss", "gm_atk"):
                L = gm_lv[key]
                if L < 3 and gem >= NODE[key]["gem"][L]:
                    gem -= int(NODE[key]["gem"][L]); gm_lv[key] += 1
            gm = {"atk": NODE["gm_atk"]["efx"][gm_lv["gm_atk"]], "boss": NODE["gm_boss"]["efx"][gm_lv["gm_boss"]]}
        cur_boss = stage % BOSS_EVERY == 0
        if cur_boss and not boss_on:
            b2 = bosses[(stage // BOSS_EVERY - 1) % len(bosses)]
            s2 = stats(lv, stage, r + 1, "wind", gm)
            hp2 = cfg["hp"][stage] if stage in cfg.get("hp", {}) else boss_hp(b2, stage, cfg.get("k"), cfg.get("extra"))
            ok2 = gate(s2, b2, hp2)[0]
            if ok2 or r - last_try >= cfg.get("retry", 5):
                boss_on = True
        if cur_boss and stage not in log:
            boss_on = True
        rec.append(dict(r=r, stage=stage, kind=kind, inc=inc, gold=gold, min=t / 60, bought=len(bought),
                        atk=stats(lv, stage, r, "clear", gm)["atk"], levels=sum(lv.values())))
        if cfg.get("snap"):
            snaps.append(dict(lv))
        for tr in (1, 2, 3):
            if tr not in done and all(lv.get(n["id"], 0) >= n["max"] for n in GOLD_NODES if n["tier"] == tr):
                done[tr] = r
        if stage > cfg.get("stop_stage", 50):
            break
    tiers = {}
    for n in GOLD_NODES:
        tiers.setdefault(n["tier"], [0, 0])
        tiers[n["tier"]][0] += lv.get(n["id"], 0); tiers[n["tier"]][1] += n["max"]
    return dict(log=log, rec=rec, snaps=snaps, done=done, lv=lv, gem=gem, gm=gm_lv, tiers=tiers, rounds=r, minutes=t / 60, stage=stage)


def boss_table(cfg, atk_by_stage=None):
    rows = []
    for st in (10, 20, 30, 40, 50):
        b = BOSSES[(st // BOSS_EVERY - 1) % len(BOSSES)]
        b = dict(b, mul=cfg.get("mul", {}).get(b["id"], b["mul"]))
        hp = boss_hp(b, st, cfg.get("k"), cfg.get("extra"))
        dur = 15 + min(10, 0.5 * (st - 1)) + 6   # ps_dur 최대(+6) 가정
        rows.append(dict(stage=st, boss=b["id"], zone=zone_of(st), hp=round(hp, 1), dur=dur,
                         need_A=round(need_atk(b, hp, dur), 2)))
    return rows


def summarize(res):
    out = []
    for st, e in sorted(res["log"].items()):
        out.append(dict(stage=st, boss=e["boss"], hp=round(e["hp"]), first_r=e["first_r"], first_min=round(e["first_min"], 1),
                        tries=e["tries"], kill_r=e.get("kill_r"), kill_min=round(e["kill_min"], 1) if "kill_min" in e else None,
                        farm=(e["kill_r"] - e["first_r"] - (e["tries"] - 1)) if "kill_r" in e else None,
                        atk=round(e.get("atk", 0), 1), t=round(e.get("t", 0), 1), T=e.get("T")))
    return out


def write(tag, cfg, res):
    json.dump(dict(cfg={k: v for k, v in cfg.items() if k not in ("extra", "prices")}, bosses=summarize(res),
                   table=boss_table(cfg), rounds=res["rounds"], minutes=round(res["minutes"], 1), tiers=res["tiers"], gm=res["gm"]),
              open(os.path.join(HERE, f"theory_boss_{tag}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    with open(os.path.join(HERE, f"theory_boss_curve_{tag}.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(res["rec"][0].keys()))
        w.writeheader(); w.writerows(res["rec"])


def show(tag, cfg):
    res = progression(cfg)
    print(f"== {tag}: 끝 R{res['rounds']} · {res['minutes']:.0f}분 · 스테이지 {res['stage']} · 단계 {res['tiers']} · gm {res['gm']}")
    for row in summarize(res):
        print("  ", row)
    write(tag, cfg, res)
    return res


if __name__ == "__main__":
    for row in boss_table({}):
        print(row)
    show("now", dict(gems=False))
