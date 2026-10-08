# -*- coding: utf-8 -*-
"""A4(공격력 배율 = 코어 + 별) 다시 맞추기. 판마다 기록이 남은 balance.log(every = 1)에서
표시 공격력 ÷ 리플레이 고정값을 판·별 수·보유 레벨 수와 나란히 뽑는다.
사용: python a4.py → theory_a4_tuned.json (점, 각 설명변수별 적합 오차)"""
import json, os, re, sys, statistics
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cfg
from replay import replay
from features import atk_flat

SOURCES = [  # (기록 폴더, 시드 목록 = 로그의 마지막 실행 블록 순서)
    (os.path.join(cfg.ROOT, "BalanceData", "test", "before_1414"), [4, 5, 6]),   # T2 가격·tuned·skill 0.6 (표본 밖 시드)
]


def blocks(path, seeds):
    lines = open(path, encoding="utf-8").read().splitlines()
    st = [i for i, l in enumerate(lines) if l.startswith("R1 |")]
    st = st[-len(seeds):]
    out = {}
    for sd, a in zip(seeds, st):
        b = st[st.index(a) + 1] if a != st[-1] else len(lines)
        out[sd] = [l for l in lines[a:b] if l.startswith("R")]
    return out


def points():
    pts = []
    for data, seeds in SOURCES:
        for sd, ls in blocks(os.path.join(data, "balance.log"), seeds).items():
            rows = replay(sd, data=data)
            for l in ls:
                p = [x.strip() for x in l.split("|")]
                r = int(p[0][1:])
                atk = float(p[5].replace(",", ""))
                star = int(re.search(r"별 (\d+)", l).group(1))
                lvb = rows[r - 1]["levels_before"]
                f = atk_flat(lvb)
                if f >= 10:
                    pts.append(dict(seed=sd, stage=r, mul=atk / f, star=star, levels=sum(lvb.values())))
    return pts


def main():
    pts = points()
    res = dict(n=len(pts), sources=[s[0] for s in SOURCES])
    y = np.array([p["mul"] for p in pts])
    for key in ("stage", "star", "levels"):
        x = np.array([p[key] for p in pts], float)
        # 구간 선형(10개 분위 매듭) 대신 단조 계단 평균: x 를 정렬해 묶음 중앙값
        order = np.argsort(x)
        xs, ys = x[order], y[order]
        knots = []
        for q in np.linspace(0, 1, 9):
            i = int(q * (len(xs) - 1))
            lo, hi = max(0, i - 8), min(len(xs), i + 9)
            knots.append((float(xs[i]), float(np.median(ys[lo:hi]))))
        mono = []
        for xv, yv in knots:
            if mono and xv <= mono[-1][0]:
                continue
            mono.append((xv, max(yv, mono[-1][1]) if mono else yv))
        pred = np.interp(x, [k[0] for k in mono], [k[1] for k in mono])
        res[key] = dict(knots=mono, rmse=float(np.sqrt(np.mean((pred - y) ** 2))), max_abs=float(np.max(np.abs(pred - y))))
    json.dump(res, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "theory_a4_tuned.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps({k: (v if not isinstance(v, dict) else {"rmse": round(v["rmse"], 3), "max": round(v["max_abs"], 3), "knots": [(round(a, 1), round(b, 3)) for a, b in v["knots"]]}) for k, v in res.items()}, ensure_ascii=False, indent=0))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
