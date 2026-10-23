# -*- coding: utf-8 -*-
"""8회차 노드 무관 목표 곡선 (내부 판 1~64) + 권장안 예측.
목표(사람 기준)는 '판 n에서 판 수입·구매 수·다음 가격/판 수입·공격력·수확기·군락·보스 체력(사람 유효 DPS 대비)·도전 수'.
  - 1~23: 사람 = model8 진행(권장안 E4) — 목표선과 같이 적는다
  - 24~64: 봇 = T8-0 중앙(판 23에서 권장안 봇과 T8-0이 다시 모임: 1.88e5 vs 1.87e5), 사람 = 봇 × H_RATIO (지표 B 내부 34: 사람/봇 ≈ 0.2)
  - 55~64: T8-0 기록 없음 → 54 값에 판당 ×1.15 (가정)
보스: boss8_out.json 권장 Hp → 체력 절대값 = 40 × ZoneMul × Hp_Mul, 사람 중앙 D(판 안 피해/체력), 평균 도전.
다시 돌리기: python -X utf8 BalanceData/r8/theory/target8.py → target_curve.csv · target8_out.txt"""
import csv, json, math, os, statistics as S
from statistics import NormalDist
from model8 import fit_fe, smooth_fe, simulate, t80_rows, feats
from design8 import FINAL, PLANS, run, fe_with_loose, HUMAN
from boss_fit import fit, B as BDATA

HERE = os.path.dirname(os.path.abspath(__file__))
N = NormalDist()
H_RATIO = 0.2
# 목표 판당 배율 (사람): 튜토리얼·초반 ×1.5, 표시 5~9 ×1.35, 15~23 ×1.25, 밤(25~) ×1.25, 지역 해금 판(25·49) ×10 뜀은 그대로
def target_growth(n):
    if n <= 8: return 1.5
    if n <= 13: return 1.35
    if n <= 33: return 1.25
    if n <= 48: return 1.15
    return 1.12
ANCHOR = (25, 49)   # 지역 해금 판(밤·들판): 목표선을 그 판 예측값(사람)에서 다시 잡는다 (지역 ×10은 노드와 무관한 구조)
TARGET_BUYS = lambda n: '2~3' if n <= 13 else ('2 이상' if n <= 33 else '1 이상')
TARGET_RATIO = lambda n: '≤ 0.6' if n <= 13 else ('≤ 0.8' if n <= 33 else '≤ 1.5')
BOSS = {14: ('king', 10, 'forest'), 24: ('cleaner', 20, 'night'), 34: ('angel', 30, 'night'), 44: ('phantom', 40, 'night'), 54: ('mover', 50, 'field')}
ZI = {'forest': 0, 'night': 1, 'field': 2}


def zone(cst, z):
    return 10 ** ZI[z] * 1.01 ** (cst - 1)


if __name__ == '__main__':
    base = run({}); plan = run(PLANS[FINAL])
    fe = smooth_fe(fit_fe(base, t80_rows())[0])
    fe_p = fe_with_loose(fe, PLANS[FINAL]['_loose'])
    hum = {r['n']: r for r in simulate(plan, fe_p, HUMAN, nmax=23)}
    bot = {r['n']: r for r in simulate(plan, fe_p, nmax=23)}
    rows = t80_rows()
    med = lambda n, k: S.median([r[k] for r in rows if r['stage'] == n and r['bossFlag'] in (0, 1)]) if any(r['stage'] == n for r in rows) else None
    bj = json.load(open(os.path.join(HERE, 'boss8_out.json'), encoding='utf-8'))
    fitp = {st: fit(q, L) for st, k, q, L, rpu, n in BDATA}
    out, csvrows = [], []
    tgt = None
    last_bot = None
    for n in range(1, 65):
        disp = n - 4
        if n <= 23:
            h = hum[n]; b = bot[n]
            hn, bn, hb, bb = h['net'], b['net'], h['buys'], b['buys']
            hr, br = h['ratio'], b['ratio']
            balls, cols, dur = h['balls'], h['cols'], h['dur']
            atk = (1 + sum(plan[k]['efx'][min(h['lv'].get(k, 0), plan[k]['max'])] for k in ('ps_atk', 'ps_atk2'))) * 1.1
        else:
            m = med(n, 'net')
            if m is None:
                m = last_bot * 1.15
            bn = m; last_bot = m
            hn = bn * H_RATIO
            bb = med(n, 'buys') if med(n, 'buys') is not None else 1
            hb, hr, br = None, None, None
            balls = med(n, 'balls') or 4; cols = med(n, 'colsMax') or 18; dur = med(n, 'dur') or 39.5
            atk = med(n, 'atk') or 115
        if n == 1:
            tgt = 16
        elif n in ANCHOR:
            tgt = hn
        elif n in BOSS:
            tgt = tgt          # 보스 판은 목표선을 잇기만 한다
        else:
            tgt = tgt * (target_growth(n) if (n - 1) not in BOSS else target_growth(n) ** 2)
        boss_txt = ''
        if n in BOSS:
            key, st, z = BOSS[n]
            hp = 40 * zone(st, z) * bj[key]['rec']
            mu, s = fitp[st]
            mu_new = mu + math.log(bj[key]['R_dps']) - math.log(bj[key]['M']) - math.log(bj[key]['rec'] / bj[key]['hp_now'])
            q = N.cdf(-mu_new / s)
            boss_txt = f"{key} 체력 {hp:.3g} · 사람 중앙 D {math.exp(mu_new):.2f} (판 시간의 {min(100, 100 / math.exp(mu_new)):.0f}%에 처치) · 판당 실패 {q:.2f} · 평균 도전 {1 / (1 - q):.2f} · 봇 {bj[key]['rec_bot_tries']:.2f}"
        csvrows.append(dict(내부=n, 표시=disp, 목표_사람_판수입=round(tgt, 1), 예측_사람_판수입=round(hn, 1), 예측_봇_판수입=round(bn, 1),
                            목표_판당배율=('-' if n == 1 else ('지역' if n in ANCHOR else ('보스' if n in BOSS else target_growth(n)))), 목표대비=('보스' if n in BOSS else ('미달' if hn < tgt * 0.95 else '충족')), 목표_구매수=TARGET_BUYS(n), 예측_사람_구매수=hb, 예측_봇_구매수=bb,
                            목표_다음가격비=TARGET_RATIO(n), 예측_사람_다음가격비=(round(hr, 2) if hr else ''), 공격력=round(atk, 2), 수확기=balls, 군락=cols, 판시간=dur, 보스=boss_txt))
    with open(os.path.join(HERE, 'target_curve.csv'), 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(csvrows[0])); w.writeheader(); w.writerows(csvrows)
    for r in csvrows:
        out.append(' | '.join(str(v) for v in r.values()))
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'target8_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
