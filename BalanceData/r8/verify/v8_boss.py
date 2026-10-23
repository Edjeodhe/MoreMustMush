# -*- coding: utf-8 -*-
"""verify 8회차 — 보스 사람/봇 보정 독립 재계산 (theory boss_fit.py·boss8.py를 import하지 않음).
D = 판 안 피해 / 보스 체력. 실패 = D < 1. 관측: 판당 실패율 q, 실패 판 평균 남은 체력 L → E[D | D<1] = 1 − L.
분포 3가지(로그정규·감마·와이블)로 맞춰 꼬리 가정의 영향을 본다. 새 체력 비 h, 사람 DPS 배율 R/M → D' = D·R/(M·h).
다시 돌리기: python -X utf8 BalanceData/r8/verify/v8_boss.py → v8_boss_out.txt"""
import math, os
from statistics import NormalDist


def bisect(f, a, b, it=200):
    fa = f(a)
    for _ in range(it):
        m = (a + b) / 2; fm = f(m)
        if (fm > 0) == (fa > 0): a, fa = m, fm
        else: b = m
    return (a + b) / 2


def gammainc(k, x):
    """정칙화 하부 불완전 감마 P(k, x) (급수 / 연분수)"""
    if x <= 0: return 0.0
    if x < k + 1:
        t = s = 1 / k; n = k
        for _ in range(2000):
            n += 1; t *= x / n; s += t
            if t < s * 1e-14: break
        return s * math.exp(-x + k * math.log(x) - math.lgamma(k))
    b = x + 1 - k; c = 1e300; d = 1 / b; h = d
    for i in range(1, 2000):
        an = -i * (i - k); b += 2
        d = an * d + b; d = 1 / (d if abs(d) > 1e-300 else 1e-300)
        c = b + an / c; c = c if abs(c) > 1e-300 else 1e-300
        dl = d * c; h *= dl
        if abs(dl - 1) < 1e-14: break
    return 1 - math.exp(-x + k * math.log(x) - math.lgamma(k)) * h


class _G:
    @staticmethod
    def cdf(x, k, scale): return gammainc(k, x / scale)
    @staticmethod
    def ppf(q, k): return bisect(lambda x: gammainc(k, x) - q, 1e-12, 1e4)
    @staticmethod
    def pdf(x, k, scale): return 0.0 if x <= 0 else math.exp((k - 1) * math.log(x / scale) - x / scale - math.lgamma(k)) / scale


class _W:
    @staticmethod
    def cdf(x, c, scale): return 1 - math.exp(-(x / scale) ** c)
    @staticmethod
    def pdf(x, c, scale): return 0.0 if x <= 0 else c / scale * (x / scale) ** (c - 1) * math.exp(-(x / scale) ** c)


class SS: gamma = _G; weibull_min = _W


def _quad(f, a, b, n=4000):
    h = (b - a) / n
    return (f(a + 1e-12) + f(b) + sum((4 if i % 2 else 2) * f(a + i * h) for i in range(1, n))) * h / 3


class IG:
    @staticmethod
    def quad(f, a, b): return (_quad(f, a, b),)


class OP:
    @staticmethod
    def brentq(f, a, b): return bisect(f, a, b, 100)

HERE = os.path.dirname(os.path.abspath(__file__))
N = NormalDist()
# 지표 B (BALANCE_EVIDENCE 2절): 표시 보스, q, L, 판/유저, 유저
B = {10: ('king', 0.43, 0.307), 20: ('cleaner', 0.36, 0.319), 30: ('angel', 0.38, 0.386), 40: ('phantom', 0.84, 0.390), 50: ('mover', 0.50, 0.480)}
HP_NOW = {'king': 1.0, 'cleaner': 0.35, 'angel': 65, 'phantom': 400, 'mover': 200}
REC = {'king': 1.0, 'cleaner': 0.35, 'angel': 30, 'phantom': 100, 'mover': 90}
TARGET = {10: '≤0.15', 20: '≤0.15(요청)/≤0.20(이론표)', 30: '0.40', 40: '0.60', 50: '0.60'}


def fit_logn(q, L):
    z = N.inv_cdf(q)
    def g(s):
        mu = -s * z
        cm = math.exp(mu + s * s / 2) * N.cdf((-mu - s * s) / s) / N.cdf(-mu / s)
        return cm - (1 - L)
    s = OP.brentq(g, 0.02, 5.0)
    return ('logn', -s * z, s)


def fit_gamma(q, L):
    def cm(k):
        th = 1 / SS.gamma.ppf(q, k)            # P(D<1)=q
        num = IG.quad(lambda x: x * SS.gamma.pdf(x, k, scale=th), 0, 1)[0]
        return num / q
    k = OP.brentq(lambda k: cm(k) - (1 - L), 0.05, 200)
    return ('gamma', k, 1 / SS.gamma.ppf(q, k))


def fit_weib(q, L):
    def lam(c):
        return 1 / (-math.log(1 - q)) ** (1 / c)   # P(D<1)=1-exp(-(1/lam)^c)=q
    def cm(c):
        l = lam(c)
        num = IG.quad(lambda x: x * SS.weibull_min.pdf(x, c, scale=l), 0, 1)[0]
        return num / q
    c = OP.brentq(lambda c: cm(c) - (1 - L), 0.2, 30)
    return ('weib', c, lam(c))


def pfail(model, h):
    """체력 h배(또는 DPS 1/h배) → P(D/h < 1) = P(D < h)"""
    kind, a, b = model
    if kind == 'logn':
        return N.cdf((math.log(h) - a) / b)
    if kind == 'gamma':
        return SS.gamma.cdf(h, a, scale=b)
    return SS.weibull_min.cdf(h, a, scale=b)


def tries(q):
    return 1 / (1 - q) if q < 0.999 else float('inf')


if __name__ == '__main__':
    out = []
    fits = {}
    out.append('[1] 분포 맞춤 (theory 로그정규 mu·s와 대조)')
    for st, (k, q, L) in B.items():
        f = [fit_logn(q, L), fit_gamma(q, L), fit_weib(q, L)]
        fits[st] = f
        out.append(f'  {st} {k}: 로그정규 mu {f[0][1]:+.3f} s {f[0][2]:.3f} · 감마 k {f[1][1]:.2f} · 와이블 c {f[2][1]:.2f}')
    # 14 대안: 8회차_요청 표(55판 중 실패 12 = 22%), 남은 체력은 같다고 둠
    fits['10alt'] = [fit_logn(12 / 55, 0.307), fit_gamma(12 / 55, 0.307), fit_weib(12 / 55, 0.307)]

    out.append('\n[2] 권장 Hp에서 사람 판당 실패율 (세 분포: 로그정규/감마/와이블). h = 권장/지금 × M / R')
    def row(st, lab, R, M, key=None, f=None):
        k = key or B[st][0]
        hr = REC[k] / HP_NOW[k]
        h = hr * M / R
        qs = [pfail(m, h) for m in (f or fits[st])]
        return f'  {lab}: R {R:.2f} · M {M:.2f} → 실패 ' + ' / '.join(f'{x:.2f}' for x in qs) + f' · 평균 도전 {tries(qs[0]):.2f}'
    out.append(' 14 king (목표 ≤0.15) — R: 초반안 없음 1.0 · 수확기 못 삼 1.65 · theory 2.69 · 내 모델 2.69')
    for R in (1.0, 1.65, 2.0, 2.69):
        out.append(row(10, '14', R, 1.0))
    out.append('  (B 대안: 55판 중 실패 12 = 22%)')
    for R in (1.0, 1.65, 2.69):
        out.append(row(10, '14 대안', R, 1.0, f=fits['10alt']))
    out.append(' 24 cleaner (요청 ≤0.15) — R: theory 2.21 · 수확기 하나 적음 1.66 · 내 모델(ps_atk2 1레벨 못 삼) 1.48 · 둘 다 1.11; M_24: 유도 없음 1.0 / 재도전자 일부 1.3 / T6-1 homing1+burst2 실측 1.64')
    for R in (1.11, 1.48, 1.66, 2.21):
        for M in (1.0, 1.3, 1.64):
            out.append(row(20, '24', R, M))
    out.append(' 34·44·54 (목표 0.40·0.60·0.60) — M: theory 2.2/2.85 (범위 1.72~2.92 / 2.11~3.96), 사람 조준 보정 낮은 값 1.4/1.8, 더 낮은 1.2/1.4; R: 1.0 (theory) / 1.5 (초반안 몫이 남음)')
    for st, Ms in ((30, (1.2, 1.4, 1.72, 2.2, 2.92)), (40, (1.4, 1.8, 2.11, 2.85, 3.96)), (50, (1.4, 1.8, 2.11, 2.85, 3.96))):
        for R in (1.0, 1.5):
            for M in Ms:
                out.append(row(st, f'{st + 4}', R, M))
    # 3) 필요한 Hp_Mul (목표 실패율 맞추기) — 같은 분포, 시나리오별
    out.append('\n[3] 목표 실패율에 필요한 Hp_Mul (로그정규) — 시나리오별')
    def need(st, qt, R, M):
        k = B[st][0]; _, mu, s = fits[st][0]
        return HP_NOW[k] * math.exp(mu + s * N.inv_cdf(qt)) * R / M
    for R, M in ((2.69, 1.0), (1.65, 1.0)):
        out.append(f'  king 실패 0.15: R {R} M {M} → {need(10, 0.15, R, M):.2f}')
    for R, M in ((2.21, 1.0), (1.48, 1.0), (1.48, 1.64), (1.11, 1.64)):
        out.append(f'  cleaner 실패 0.15: R {R} M {M} → {need(20, 0.15, R, M):.3f} · 0.20 → {need(20, 0.20, R, M):.3f}')
    for st, qt in ((30, 0.40), (40, 0.60), (50, 0.60)):
        out.append(f'  {B[st][0]} {qt}: ' + ' · '.join(f'M {M} → {need(st, qt, 1.0, M):.3g}' for M in ((1.4, 1.72, 2.2, 2.92) if st == 30 else (1.8, 2.11, 2.85, 3.96))))
    # 4) 사람 조준 배율 a (지표 B 24 vs T6-1 봇 같은 상태)
    # T6-1: 공격력 5·수확기 2·유도 없음 총 DPS 4.5(18초 판)·4.8(20초). 지표 B 24 사람 중앙 D = exp(mu)
    _, mu24, s24 = fits[20][0]
    hp24 = 169.0
    human_dps = math.exp(mu24) * hp24 / (30.5 - 2)
    out.append(f'\n[4] 지표 B 24 사람 중앙 D {math.exp(mu24):.2f} → 30.5초 판이면 사람 유효 DPS {human_dps:.1f}/초 (판 19.5초면 {math.exp(mu24) * hp24 / 17.5:.1f}) · '
               f'T6-1 봇 같은 노드 상태 유도 없음 4.5~4.8, 유도+폭발 7.4 → 사람/봇(유도 없음) a = {human_dps / 4.65:.2f}~{math.exp(mu24) * hp24 / 17.5 / 4.65:.2f}. '
               '사람이 유도 없이 봇+유도 수준이면, 사람에게 유도의 덧 배율 M은 봇 기준 M보다 작다(위 [2] 낮은 M 줄).')
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'v8_boss_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
