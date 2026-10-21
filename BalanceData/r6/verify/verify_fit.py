# -*- coding: utf-8 -*-
"""verify 6회차: K 독립 재적합 (격자 최대우도, 이론 fit_k.py의 황금분할 대신). 입력: 이론 attempts.json (extract.py 출력)"""
import json, math, os
HERE = os.path.dirname(os.path.abspath(__file__))
A = json.load(open(os.path.join(HERE, '..', 'theory', 'attempts.json')))
def Phi(x): return 0.5 * (1 + math.erf(x / math.sqrt(2)))
def need(r, T0=2.0):
    heal = 0.05 / 3 if r['boss'] == 'cleaner' else 0
    g = 1.99 if r['boss'] == 'phantom' else 1
    return r['hp'] * g * (1 / max(0.5, r['T'] - T0) + heal)
def nll(lk, s, rows, T0=2.0):
    v = 0
    for r in rows:
        p = Phi((lk + math.log(r['atk']) - math.log(need(r, T0))) / s)
        p = min(1 - 1e-12, max(1e-12, p)); v -= math.log(p if r['kill'] else 1 - p)
    return v
out = {}
for b in ['king', 'cleaner', 'angel', 'phantom', 'mover']:
    rows = [r for r in A if r['boss'] == b]
    # 분리 확인: 처치 판의 ln(need/atk) 최대 vs 실패 판의 최소
    kz = [math.log(need(r) / r['atk']) for r in rows if r['kill']]
    fz = [math.log(need(r) / r['atk']) for r in rows if not r['kill']]
    best = None
    for si in range(2, 101):
        s = si * 0.01
        for li in range(-200, 1001):
            lk = li * 0.01
            v = nll(lk, s, rows)
            if best is None or v < best[0]: best = (v, s, lk)
    v, s, lk = best
    # K의 프로파일 95% 구간 (Δnll ≤ 1.92, σ 고정)
    ci = [li * 0.01 for li in range(-200, 1001) if nll(li * 0.01, s, rows) - v <= 1.92]
    # 판 시간 T0 1·3초 민감도
    alt = {}
    for T0 in (1.0, 3.0):
        bb = min(((nll(li * 0.01, s, rows, T0), li * 0.01) for li in range(-200, 1001)))
        alt[T0] = round(math.exp(bb[1]), 2)
    out[b] = dict(n=len(rows), kills=len(kz), K=round(math.exp(lk), 3), sigma=s, nll=round(v, 3), K95=[round(math.exp(min(ci)), 2), round(math.exp(max(ci)), 2)],
                  kill_lnratio_max=round(max(kz), 3), fail_lnratio_min=round(min(fz), 3) if fz else None, K_T0_1=alt[1.0], K_T0_3=alt[3.0])
    print(b, out[b])
json.dump(out, open(os.path.join(HERE, 'verify_fit.json'), 'w'), ensure_ascii=False, indent=1)
