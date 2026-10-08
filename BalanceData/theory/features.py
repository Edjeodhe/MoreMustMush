# -*- coding: utf-8 -*-
"""노드 레벨 → 라운드 능력치 (Game.ComputeStats 중 수입에 닿는 부분만, 날씨·별 능력치 제외).
프리셋(버섯 체력·군락 수)은 cfg.py. 공격력 배율(코어 + 별)은 기록 폴더의 balance.log 에서 읽는다."""
import json, math, os, statistics
import cfg

HP0 = cfg.P["hp0"]              # 일반 버섯 체력 (tuned 1, spec 3)
COL0, COLSTEP = cfg.P["col0"], cfg.P["colstep"]
MAP_ZOOM = [1.5, 1.35, 1.2, 1.1, 1.0]
THEME_FROM = [1, 20, 45, 75, 105]
SKILLS = ["accel", "sharpen", "split", "sip", "clear", "burst", "tspore", "clone", "magnet", "bolt", "tornado", "shock", "device", "pierce", "fest"]


def L(lv, k):
    return lv.get(k, 0)


def atk_flat(lv):
    return (1 + L(lv, "ps_atk") + 10 * (L(lv, "ps_atk2") + L(lv, "ps_atk2p")) + 100 * (L(lv, "ps_atk3") + L(lv, "ps_atk3p"))
            + 1000 * L(lv, "ps_atk4"))


def score_flat(lv):
    return (L(lv, "ed_score") + L(lv, "ed_score1p") + 10 * (L(lv, "ed_score2") + L(lv, "ed_score2p"))
            + 100 * (L(lv, "ed_score3") + L(lv, "ed_score3p")))


SPEC_POINTS = [(1, 1.1), (34, 1.1), (43, 1.33), (50, 1.55), (55, 1.83), (97, 2.1), (120, 2.15)]  # 초안(spec) 값
ATK_POINTS = None


def log_atk_points(data=None):
    """balance.log 의 표시 공격력 ÷ 리플레이 고정값 → (판, 배율) 점. 시드별 마지막 실행 블록, 고정값 ≥ 10 만(표시 반올림 때문).
    같은 판은 시드 중앙값. 앞뒤를 (1, 1.1) · (마지막 판 + 30, 마지막 값)으로 막는다."""
    from replay import replay
    path = os.path.join(data or cfg.DATA, "balance.log")
    if not os.path.exists(path):
        return None
    lines = open(path, encoding="utf-8").read().splitlines()
    starts = [i for i, l in enumerate(lines) if l.startswith("R1 |")]
    if len(starts) < 3:
        return None
    pts = {}
    for sd, st in zip((1, 2, 3), starts[-3:]):
        end = starts[starts.index(st) + 1] if st != starts[-1] else len(lines)
        rows = replay(sd)
        for l in lines[st:end]:
            p = [x.strip() for x in l.split("|")]
            if not l.startswith("R") or len(p) < 6 or not p[5].replace(",", "").replace(".", "").isdigit():
                continue
            r = int(p[0][1:])
            f = atk_flat(rows[r - 1]["levels_before"])
            if f >= 10:
                pts.setdefault(r, []).append(float(p[5].replace(",", "")) / f)
    if not pts:
        return None
    out = [(1, 1.1)] + [(r, statistics.median(v)) for r, v in sorted(pts.items())]
    # 별은 줄지 않으므로 단조 증가로
    mono = []
    for r, v in out:
        mono.append((r, max(v, mono[-1][1]) if mono else v))
    mono.append((mono[-1][0] + 30, mono[-1][1]))
    return mono


def dense_points():
    """a4.py 결과(판마다 기록된 표본 밖 시드 4~6)의 판 매듭. 없으면 None"""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), f"theory_a4_{cfg.TAG}.json")
    if not os.path.exists(path):
        return None
    k = json.load(open(path, encoding="utf-8"))["stage"]["knots"]
    pts = [(1, 1.1)] + [(int(a), b) for a, b in k]
    return pts + [(pts[-1][0] + 30, pts[-1][1])]


def atk_points():
    global ATK_POINTS
    if ATK_POINTS is None:
        ATK_POINTS = (SPEC_POINTS if cfg.PRESET == "spec" else None) or dense_points() or log_atk_points() or SPEC_POINTS
    return ATK_POINTS


def atk_mul(stage):
    """1 + 코어 0.1 + 별 능력치(공격력)를 판의 함수로 보간 (가정 A4)."""
    pts = atk_points()
    for (a, va), (b, vb) in zip(pts, pts[1:]):
        if stage <= b:
            return va + (vb - va) * (stage - a) / (b - a)
    return pts[-1][1]


ZONE_RAMP = int(os.environ.get("THEORY_RAMP", "0"))      # (환경변수 THEORY_RAMP) 구조 실험: >0 이면 지역 배율 ×10 을 해금 뒤 ZONE_RAMP 판에 걸쳐 나눠 올린다(체력·보상 같이)
ZONE_STEP = 10.0   # Defs.ZONE_STEP


def zone_mul(z, stage):
    base = ZONE_STEP ** z
    if ZONE_RAMP > 0 and z > 0:
        k = min(1.0, (stage - THEME_FROM[z] + 1) / ZONE_RAMP)
        base = ZONE_STEP ** (z - 1 + k)
    return base * 1.01 ** (stage - 1)


def pick_zone(lv, stage, hp0=None):
    """BalanceSim.PickZone: 열린 지역 중 공격력 ≥ 0.5 × 일반 체력 × 지역 배율인 가장 높은 곳"""
    hp0 = HP0 if hp0 is None else hp0
    atk = atk_flat(lv) * atk_mul(stage)
    z = 0
    for k, f in enumerate(THEME_FROM):
        if stage >= f and atk >= 0.5 * hp0 * zone_mul(k, stage):
            z = k
    return z


def duration(lv, stage):
    return 15 + min(10, 0.5 * (stage - 1)) + 2 * L(lv, "ps_dur") + 3 * L(lv, "ps_dur2")


def balls(lv):
    return 1 + L(lv, "ps_ball") + L(lv, "ps_ball2") + L(lv, "ps_ball3")


def cols(lv):
    c = math.ceil(COL0 + COLSTEP * L(lv, "ed_cols")) + 4 * L(lv, "ed_cols2")
    area = (1 / MAP_ZOOM[L(lv, "md_map")] ** 2) ** 0.7
    return max(4, round(max(3, c) * area))


def combo_k(lv):
    return 0.1 + 0.05 * L(lv, "md_combo") + 0.1 * L(lv, "md_combo2")


def skills_on(lv):
    return sum(1 for s in SKILLS if L(lv, s) > 0)


def skill_lv(lv):
    return sum(L(lv, s + "_rate") + L(lv, s + "_pow") for s in SKILLS)


def stats(lv, stage):
    z = pick_zone(lv, stage)
    return dict(stage=stage, zone=z, Z=zone_mul(z, stage), sflat=score_flat(lv), atk=atk_flat(lv) * atk_mul(stage),
                dur=duration(lv, stage), balls=balls(lv), cols=cols(lv), comboK=combo_k(lv), skills=skills_on(lv),
                skill_lv=skill_lv(lv), crit=0.05 * L(lv, "ps_crit"), critMul=3 + L(lv, "ps_crit2"),
                kid=0.12 * L(lv, "md_kid"), size=1 + 0.15 * L(lv, "ps_size"), spd=1 + 0.12 * L(lv, "ps_spd"),
                bar=1 + 0.25 * L(lv, "md_bar"), rare=1 + 0.5 * L(lv, "ed_rare"), giant=L(lv, "ed_giant"),
                gold=L(lv, "ed_gold"), fest=L(lv, "fest") * (1 + 0.75 * L(lv, "fest_pow")))
