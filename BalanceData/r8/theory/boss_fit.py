# -*- coding: utf-8 -*-
"""8회차 보스: 사용자 지표(B, BALANCE_EVIDENCE 2절)로 판당 '피해 비율' D = (판 안에 준 피해 / 보스 체력)의 분포를 맞추고,
목표 실패율에 필요한 체력 배율과 유도 제거 보정을 낸다.
가정: ln D ~ N(mu, s). 실패 = D < 1. 관측: 실패율 q, 실패 판의 평균 남은 체력 L → E[D | D<1] = 1 - L.
체력을 h배로 하면 D → D/h. 목표 실패율 q* → h = exp(mu + s*z(1-q*)) 를 푼다 (P(D/h<1) = q*).
다시 돌리기: python -X utf8 BalanceData/r8/theory/boss_fit.py → boss_fit_out.txt"""
import math, os
from statistics import NormalDist

N = NormalDist()

def cond_mean_below1(mu, s):
    # E[D | D<1] for lognormal = exp(mu + s^2/2) * Phi((0 - mu - s^2)/s) / Phi((0-mu)/s)
    return math.exp(mu + s * s / 2) * N.cdf((-mu - s * s) / s) / N.cdf(-mu / s)

def fit(q, left):
    """실패율 q, 실패 판 평균 남은 체력 left(0~1) → (mu, s)"""
    target = 1 - left
    best = None
    for i in range(1, 400):
        s = i * 0.01
        mu = -s * N.inv_cdf(q)          # P(lnD<0)=q → -mu/s = z_q
        err = abs(cond_mean_below1(mu, s) - target)
        if best is None or err < best[0]:
            best = (err, mu, s)
    return best[1], best[2]

def h_for(mu, s, q_target):
    # P(lnD - ln h < 0) = q_target → ln h = mu + s * z(q_target)... P(lnD < ln h) = q → ln h = mu + s*inv(q)
    return math.exp(mu + s * N.inv_cdf(q_target))

# 표시 보스: (이름, 실패율, 실패 판 남은 체력, 판/유저, 유저 수)  — BALANCE_EVIDENCE 2절 B
B = [(10, 'king', 0.43, 0.307, 1.91, 11), (20, 'cleaner', 0.36, 0.319, 1.56, 9), (30, 'angel', 0.38, 0.386, 1.63, 8),
     (40, 'phantom', 0.84, 0.390, 4.75, 8), (50, 'mover', 0.50, 0.480, 2.00, 5)]
# 목표 판당 실패율 (도전 수 A ≈ 1/(1-q) 근사)
TARGET = {10: 0.10, 20: 0.20, 30: 0.45, 40: 0.65, 50: 0.65}
HP_NOW = {'king': 1.0, 'cleaner': 0.35, 'angel': 65, 'phantom': 400, 'mover': 200}
# 유도 제거로 사용자 보스 DPS가 줄어드는 배율(1/M). 근거 homing_mc_out.txt + T6-1 보정(초과분 x0.4). 사용자가 유도를 가졌을 법한 보스만
M_USER = {10: 1.0, 20: 1.0, 30: 1.6, 40: 2.0, 50: 2.0}
M_RANGE = {30: (1.2, 2.5), 40: (1.4, 3.0), 50: (1.4, 3.0)}

if __name__ == '__main__':
    out = ['표시 보스 | 실패율 q · 남은 L · 판/유저 | 맞춘 mu s | 1/(1-q) | 목표 q* → 체력 배율 h | 유도 M | 새 Hp_Mul = 지금 × h / M (M 범위)']
    for st, k, q, L, rpu, n in B:
        mu, s = fit(q, L)
        h = h_for(mu, s, TARGET[st])
        M = M_USER[st]
        new = HP_NOW[k] * h / M
        lo, hi = M_RANGE.get(st, (M, M))
        out.append(f'{st} {k} | {q:.2f} · {L:.3f} · {rpu} (n {n}) | {mu:+.3f} {s:.2f} | {1/(1-q):.2f} | {TARGET[st]:.2f} → x{h:.2f} | {M} | '
                   f'{HP_NOW[k]} → {new:.3g} ({HP_NOW[k]*h/hi:.3g}~{HP_NOW[k]*h/lo:.3g})')
    txt = '\n'.join(out)
    print(txt)
    open(os.path.join(os.path.dirname(__file__), 'boss_fit_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
