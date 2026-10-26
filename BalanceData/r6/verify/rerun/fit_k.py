# -*- coding: utf-8 -*-
"""봇의 보스 판 유효 DPS 배율 K(= DPS / 공격력)를 보스별로 맞춘다 (probit 최대우도).
처치 ⇔ ln(K·atk) − ln(need) + σ·z ≥ 0,  need = 체력 × G_hp × (1/(T − T0) + 회복률)
 - T0 = 2초(발사·첫 타격), 회복률 = 청소부 CLEANER_HEAL/CLEANER_INT (지금 0.05/3), 환상 G_hp = 1.99(분신 66%·33%)
 - K에는 봇 조준·수확기 수·스킬·치명·무더기·보스 피해·기믹(천사 정화·움직임)이 모두 들어간다.
입력 attempts.json (extract.py). 출력 fit_k.json"""
import json, math, os
HERE = os.path.dirname(os.path.abspath(__file__))
A = json.load(open(os.path.join(HERE, 'attempts.json')))
T0 = 2.0; HEAL = 0.05 / 3; GHP = {'phantom': 1.99}
def need(r, heal=HEAL):
    return r['hp'] * GHP.get(r['boss'], 1) * (1 / max(0.5, r['T'] - T0) + (heal if r['boss'] == 'cleaner' else 0))
def Phi(x): return 0.5 * (1 + math.erf(x / math.sqrt(2)))
def nll(lnk, sig, rows):
    s = 0
    for r in rows:
        m = (lnk + math.log(r['atk']) - math.log(need(r))) / sig
        p = min(1 - 1e-9, max(1e-9, Phi(m)))
        s -= math.log(p if r['kill'] else 1 - p)
    return s
def fit(rows, sigs):
    best = None
    for sig in sigs:
        lo, hi = -5, 15
        # 황금분할로 lnk
        g = (math.sqrt(5) - 1) / 2
        a, b = lo, hi
        for _ in range(80):
            c = b - g * (b - a); d = a + g * (b - a)
            if nll(c, sig, rows) < nll(d, sig, rows): b = d
            else: a = c
        lk = (a + b) / 2; v = nll(lk, sig, rows)
        if best is None or v < best[0]: best = (v, sig, lk)
    return best
if __name__ == '__main__':
    out = {}
    sigs = [0.05, 0.1, 0.15, 0.2, 0.3, 0.4, 0.5, 0.7]
    # 공통 σ: 보스별 K, σ 하나
    tot = {}
    for sig in sigs:
        t = 0; ks = {}
        for b in ['king', 'cleaner', 'angel', 'phantom', 'mover']:
            rows = [r for r in A if r['boss'] == b]
            v, _, lk = fit(rows, [sig]); t += v; ks[b] = lk
        tot[sig] = (t, ks)
    sig = min(tot, key=lambda s: tot[s][0])
    print('공통 σ 후보 nll:', {s: round(v[0], 2) for s, v in tot.items()}, '→ σ', sig)
    for b, lk in tot[sig][1].items():
        rows = [r for r in A if r['boss'] == b]
        # 보스 판 도착 공격력(T5-2 첫 도전) 중앙값과 그때 DPS
        first = sorted([r['atk'] for r in rows if r['set'] == 'T5-2' and r['rnd'] == min(x['rnd'] for x in rows if x['set'] == 'T5-2' and x['seed'] == r['seed'])])
        a0 = first[len(first) // 2]
        # 각 보스별 σ 따로
        v2, s2, lk2 = fit(rows, sigs)
        out[b] = dict(K=math.exp(lk), lnK=lk, sigma_common=sig, K_own=math.exp(lk2), sigma_own=s2, n=len(rows), kills=sum(r['kill'] for r in rows),
                      atk_arrive_T52=a0, dps_arrive_T52=math.exp(lk) * a0)
        print(f"{b:8s} K={math.exp(lk):9.2f} (자기 σ {s2}: K={math.exp(lk2):9.2f})  n={len(rows)} 처치 {out[b]['kills']}  도착 공격력 {a0:.1f} → DPS {math.exp(lk)*a0:,.0f}")
    json.dump(out, open(os.path.join(HERE, 'fit_k.json'), 'w'), ensure_ascii=False, indent=1)
