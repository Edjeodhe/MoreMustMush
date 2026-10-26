# -*- coding: utf-8 -*-
"""7회차 제안 규칙의 배치 몬테카를로.

제안 규칙 (이론_7.md 4절):
  s_min(z) = max(S_VIS / z, S_ABS)                        # 화면 크기 하한(보이기)
  A_free   = (FX1-FX0)(NSY-FY0) - pi*140^2/2 - 장치 넓이 합
  rho      = sqrt(COL_PACK * A_free / (pi * N))            # 군락 하나가 쓸 수 있는 반지름(여백 반 포함)
  s0       = clamp((rho - 17) / (3.9 * R_REF), S_ABS, 1)    # R_REF = TIERS[0].r (32)
  배치: 종을 N개 먼저 고르고, s에서 자리만 계획(무작위 COL_TRIES번 → 실패 시 COL_GRID 간격 빈 점 중 무작위)
        → 다 놓이면 확정, 아니면 s *= COL_SHRINK 하고 처음부터 다시(s_min(z)를 건너뛰지 않고 한 번 들르며, S_ABS에서 멈추고 놓인 만큼 확정)

run_prop(...) → 판마다 (놓인 수, 최종 s)
"""
import math
import numpy as np
from place_mc import world, colony_RR, place_devices, TIER_R

S_VIS = 0.70        # s*z 하한: 레전드리(r 20) 화면 지름 40*s*z >= 28px, 일반(r 32) >= 45px (1920x1080 기준)
S_ABS = 0.45        # 필드 px 하한: 일반 r 14.4 (기본 핀볼 r 27의 절반 남짓)
COL_PACK = 0.40     # 시작 s 추정용 채움률 (격자 보충 배치의 99% 용량 phi99 0.36~0.47의 가운데)
COL_TRIES = 30
COL_GRID = 8
COL_SHRINK = 0.93
R_REF = TIER_R[0]

DEV_AREA = {'stump': math.pi * 56 ** 2, 'moss': 364 * 70, 'acorn': 3 * math.pi * 18 ** 2}


def s_min(z):
    return max(S_VIS / z, S_ABS)


def s_start(z, n, devs):
    w = world(z)
    A = (w['FX1'] - w['FX0']) * (w['NSY'] - w['FY0']) - math.pi * 140 ** 2 / 2 - sum(DEV_AREA[d] for d in devs)
    rho = math.sqrt(COL_PACK * A / (math.pi * max(n, 1)))
    return min(1.0, max(S_ABS, (rho - 17) / (3.9 * R_REF)))


def plan(rng, w, dsh, ts, s, tries=COL_TRIES, grid=COL_GRID):
    dc = np.array([[sh[1], sh[2], sh[3]] for sh in dsh if sh[0] == 'c']).reshape(-1, 3)
    dr = [sh for sh in dsh if sh[0] == 'r']
    n = len(ts); cx = np.empty(n); cy = np.empty(n); cr = np.empty(n); k = 0
    for t in ts:
        RR = colony_RR(TIER_R[t] * s)
        if w['FX0'] + RR > w['FX1'] - RR or w['FY0'] + RR > w['NSY'] - RR:
            continue

        def okmask(X, Y):
            m = (X - w['LX']) ** 2 + (Y - w['LY']) ** 2 >= (RR + 140) ** 2
            if k:
                m &= ~np.any((cx[None, :k] - X[:, None]) ** 2 + (cy[None, :k] - Y[:, None]) ** 2 < (cr[None, :k] + RR + 10) ** 2, axis=1)
            if len(dc):
                m &= ~np.any((dc[None, :, 0] - X[:, None]) ** 2 + (dc[None, :, 1] - Y[:, None]) ** 2 < (dc[None, :, 2] + RR + 10) ** 2, axis=1)
            for o in dr:
                px = np.clip(X, o[1], o[3]); py = np.clip(Y, o[2], o[4])
                m &= (X - px) ** 2 + (Y - py) ** 2 >= (RR + 10) ** 2
            return m
        xs = rng.uniform(w['FX0'] + RR, w['FX1'] - RR, tries); ys = rng.uniform(w['FY0'] + RR, w['NSY'] - RR, tries)
        idx = np.flatnonzero(okmask(xs, ys))
        if len(idx):
            x, y = xs[idx[0]], ys[idx[0]]
        elif grid > 0:
            gx = np.arange(w['FX0'] + RR, w['FX1'] - RR + 1e-6, grid); gy = np.arange(w['FY0'] + RR, w['NSY'] - RR + 1e-6, grid)
            X, Y = np.meshgrid(gx, gy); X = X.ravel(); Y = Y.ravel()
            idx = np.flatnonzero(okmask(X, Y))
            if not len(idx): continue
            j = idx[rng.integers(len(idx))]; x, y = X[j], Y[j]
        else:
            continue
        cx[k] = x; cy[k] = y; cr[k] = RR; k += 1
    return k


def run_prop(z, n, devs=(), tiers=(1, 0, 0, 0), trials=200, seed=1, floor=None, tries=COL_TRIES, grid=COL_GRID, fixed_s=None):
    """floor: 줄이기 하한(기본 S_ABS). fixed_s: 주면 그 s로 한 번만(지금 코드 재현용: fixed_s=1, grid=0)."""
    rng = np.random.default_rng(seed)
    w = world(z)
    p = np.array(tiers, float); p /= p.sum()
    fl = S_ABS if floor is None else floor
    out = []
    for _ in range(trials):
        dsh = place_devices(rng, w, devs)
        ts = rng.choice(4, size=n, p=p)
        if fixed_s is not None:
            out.append((plan(rng, w, dsh, ts, fixed_s, tries, grid), fixed_s)); continue
        s = s_start(z, n, devs)
        while True:
            k = plan(rng, w, dsh, ts, s, tries, grid)
            if k >= n or s <= fl + 1e-9:
                break
            nxt = s * COL_SHRINK
            sm = s_min(z)
            s = sm if (s > sm + 1e-9 and nxt < sm) else max(fl, nxt)   # s_min(z)에서 한 번 멈춘다
        out.append((k, s))
    return out
