# -*- coding: utf-8 -*-
"""7회차 군락 배치 몬테카를로 (RoundSim.Start → PlaceDevice → SpawnColony(true) × maxCol 를 그대로 옮김).

- 필드: SetWorld(z) — WW=round(1920/z), WH=round(1080/z), FX0=40, FX1=WW-40, FY0=40, NSY=WH-180, LAUNCH=(WW/2, WH-150) 원 140(여백 0)
- 군락: r = TIERS[t].r * s, rc = round(1.9 r), ring = rc + r + 6, RR = ring + r + 6, 군락끼리 여백 10
- 장치: stump 원 56(Pos 여백 70) · moss 364x70 또는 70x364(여백 196) · acorn 원 18 x3 간격 98(여백 154), 40번 시도
- 난수는 numpy(시드 고정). 게임(UnityEngine.Random)과 수열은 다르고 분포만 같다.

run(z, s, tries, tiers, devs, n_req, trials, seed) → 판마다 [놓인 수, 첫 실패 순번] 목록.
"""
import math
import numpy as np

TIER_R = [32, 28, 22, 20]        # round(BASE_R × rMul 1.25), Mathf.Round(은행가 반올림): 26→32, 22→28, 18→22, 16→20
DEV_SCALE = 1.4


def unity_round(x):
    # Mathf.Round = 짝수 쪽 반올림
    return float(np.round(x))


def world(z):
    WW = unity_round(1920 / z); WH = unity_round(1080 / z)
    return dict(WW=WW, WH=WH, FX0=40.0, FX1=WW - 40, FY0=40.0, NSY=WH - 180, LX=WW / 2, LY=WH - 150)


def colony_RR(r):
    rc = unity_round(1.9 * r)
    ring = rc + r + 6
    return ring + r + 6


def circ_rect_overlap(cx, cy, cr, x0, y0, x1, y1, m):
    px = min(max(cx, x0), x1); py = min(max(cy, y0), y1)
    return (cx - px) ** 2 + (cy - py) ** 2 < (cr + m) ** 2


def place_devices(rng, w, devs):
    """장치 모양 목록: ('c', x, y, r) 또는 ('r', x0, y0, x1, y1)"""
    shapes = []

    def free(shs):
        for sh in shs:
            if sh[0] == 'c':
                _, x, y, r = sh
                if (x - w['LX']) ** 2 + (y - w['LY']) ** 2 < (r + 140) ** 2: return False
                if y + r > w['NSY']: return False   # areaFloor: y0 = NSY 아래 금지 (원-사각 겹침 여백 0)
                for o in shapes:
                    if o[0] == 'c' and (x - o[1]) ** 2 + (y - o[2]) ** 2 < (r + o[3] + 10) ** 2: return False
                    if o[0] == 'r' and circ_rect_overlap(x, y, r, o[1], o[2], o[3], o[4], 10): return False
            else:
                _, x0, y0, x1, y1 = sh
                if circ_rect_overlap(w['LX'], w['LY'], 140, x0, y0, x1, y1, 0): return False
                if y1 > w['NSY']: return False
                for o in shapes:
                    if o[0] == 'c' and circ_rect_overlap(o[1], o[2], o[3], x0, y0, x1, y1, 10): return False
                    if o[0] == 'r' and x0 < o[3] + 10 and x1 + 10 > o[1] and y0 < o[4] + 10 and y1 + 10 > o[2]: return False
        return True

    def pos(m):
        return rng.uniform(w['FX0'] + m, w['FX1'] - m), rng.uniform(w['FY0'] + m, w['NSY'] - m)

    for d in devs:
        for a in range(40):
            if d == 'stump':
                x, y = pos(50 * DEV_SCALE); shs = [('c', x, y, 40 * DEV_SCALE)]
            elif d == 'moss':
                x, y = pos(140 * DEV_SCALE); hz = rng.random() < 0.5
                ww = (260 if hz else 50) * DEV_SCALE; hh = (50 if hz else 260) * DEV_SCALE
                shs = [('r', x - ww / 2, y - hh / 2, x + ww / 2, y + hh / 2)]
            else:
                x, y = pos(110 * DEV_SCALE); hz = rng.random() < 0.6; g = 70 * DEV_SCALE
                shs = [('c', x + (k * g if hz else 0), y + (0 if hz else k * g), 18) for k in (-1, 0, 1)]
            if free(shs):
                shapes.extend(shs); break
    return shapes


def run(z, s, tries=30, tiers=(1, 0, 0, 0), devs=(), n_req=120, trials=200, seed=1, s_by_tier=None, grid=0):
    """grid>0: 무작위 tries번이 다 실패하면 grid 간격 격자의 빈 점 중 하나를 무작위로 고른다(제안 규칙 COL_GRID)."""
    rng = np.random.default_rng(seed)
    w = world(z)
    p = np.array(tiers, float); p /= p.sum()
    RRt = [colony_RR(TIER_R[t] * s) for t in range(4)]
    out = []
    for _ in range(trials):
        dsh = place_devices(rng, w, devs)
        dc = np.array([[sh[1], sh[2], sh[3]] for sh in dsh if sh[0] == 'c']).reshape(-1, 3)
        dr = [sh for sh in dsh if sh[0] == 'r']
        cx = np.empty(n_req); cy = np.empty(n_req); cr = np.empty(n_req); k = 0
        first_fail = n_req + 1; placed = 0
        for i in range(n_req):
            t = rng.choice(4, p=p)
            RR = RRt[t]
            ok = False
            if not (w['FX0'] + RR > w['FX1'] - RR or w['FY0'] + RR > w['NSY'] - RR):
                xs = rng.uniform(w['FX0'] + RR, w['FX1'] - RR, tries)
                ys = rng.uniform(w['FY0'] + RR, w['NSY'] - RR, tries)
                okm = (xs - w['LX']) ** 2 + (ys - w['LY']) ** 2 >= (RR + 140) ** 2
                # areaFloor(y>=NSY 사각)는 후보 범위가 NSY-RR까지라 늘 비어 있음
                if k:
                    okm &= ~np.any((cx[None, :k] - xs[:, None]) ** 2 + (cy[None, :k] - ys[:, None]) ** 2 < (cr[None, :k] + RR + 10) ** 2, axis=1)
                if len(dc):
                    okm &= ~np.any((dc[None, :, 0] - xs[:, None]) ** 2 + (dc[None, :, 1] - ys[:, None]) ** 2 < (dc[None, :, 2] + RR + 10) ** 2, axis=1)
                for o in dr:
                    px = np.clip(xs, o[1], o[3]); py = np.clip(ys, o[2], o[4])
                    okm &= (xs - px) ** 2 + (ys - py) ** 2 >= (RR + 10) ** 2
                idx = np.flatnonzero(okm)
                if len(idx):
                    ok = True; x = xs[idx[0]]; y = ys[idx[0]]
            if not ok and grid > 0 and not (w['FX0'] + RR > w['FX1'] - RR or w['FY0'] + RR > w['NSY'] - RR):
                gx = np.arange(w['FX0'] + RR, w['FX1'] - RR + 1e-6, grid); gy = np.arange(w['FY0'] + RR, w['NSY'] - RR + 1e-6, grid)
                X, Y = np.meshgrid(gx, gy); X = X.ravel(); Y = Y.ravel()
                okm = (X - w['LX']) ** 2 + (Y - w['LY']) ** 2 >= (RR + 140) ** 2
                for j in range(k):
                    okm &= (X - cx[j]) ** 2 + (Y - cy[j]) ** 2 >= (cr[j] + RR + 10) ** 2
                for d in dc:
                    okm &= (X - d[0]) ** 2 + (Y - d[1]) ** 2 >= (d[2] + RR + 10) ** 2
                for o in dr:
                    px = np.clip(X, o[1], o[3]); py = np.clip(Y, o[2], o[4])
                    okm &= (X - px) ** 2 + (Y - py) ** 2 >= (RR + 10) ** 2
                idx = np.flatnonzero(okm)
                if len(idx):
                    j = idx[rng.integers(len(idx))]; ok = True; x = X[j]; y = Y[j]
            if ok:
                cx[k] = x; cy[k] = y; cr[k] = RR; k += 1; placed += 1
            elif first_fail > n_req:
                first_fail = i + 1
        out.append((placed, first_fail))
    return out


def stats_for_n(res, n):
    """요청 N개일 때(앞 N번만 본다): 평균 놓인 수 근사와 N개 다 놓일 확률.
    순차 배치라 앞 N번의 결과는 N에서 멈춘 판과 같다. 놓인 수는 '첫 실패 순번'까지로는 모르니
    여기서는 P(전부)만 정확하고, 평균은 run(n_req=N)을 따로 돌려 얻는다."""
    return np.mean([ff > n for _, ff in res])
