# -*- coding: utf-8 -*-
"""사람 배율 A(n)의 내부 15~23 구간: A(n) = A(13)·d^(n−13). 지표 B 15 548 · 20 1,739 · 23 ≈ 24 보스 판 12,846/2.6 에 맞춘다.
다시 돌리기: python -X utf8 BalanceData/r8/theory/human15.py → human15_out.txt"""
import math, os, json
import numpy as np
from tree import load
from model8 import fit_fe, simulate, t80_rows, a_human_fit
HERE = os.path.dirname(os.path.abspath(__file__))
B = {15: 548, 20: 1739, 23: 12846 / 2.6}

def a_fn_make(a0, sl, d):
    def f(n):
        base = max(0.15, a0 + sl * (min(n, 13) - 1))
        return base * (d ** (n - 13) if n > 13 else 1)
    return f

if __name__ == '__main__':
    nodes, _, _ = load(drop=('homing',))
    fe, _ = fit_fe(nodes, t80_rows())
    a0, sl, _, _ = a_human_fit(nodes, fe)
    best = None
    for d in np.arange(0.70, 1.001, 0.005):
        sim = simulate(nodes, fe, a_fn_make(a0, sl, d), nmax=23)
        e = [math.log(sim[n - 1]['net'] / v) for n, v in B.items()]
        sse = sum(x * x for x in e)
        if best is None or sse < best[0]:
            best = (sse, d, e, sim)
    sse, d, e, sim = best
    out = [f'A0 {a0:.2f} 기울기 {sl:+.4f} · 15~23 d {d:.3f}/판 · 맞춤 오차 ' + ' '.join(f'{n}:{math.exp(x):.2f}' for n, x in zip(B, e)),
           '판 | 사람 net 모델 | A(n) | 수확기 | 공격력 대신 ps_atk 레벨 | 시간']
    f = a_fn_make(a0, sl, d)
    for r in sim:
        out.append(f"{r['n']} | {r['net']:.0f} | {f(r['n']):.3f} | {r['balls']} | ps_atk {r['lv'].get('ps_atk', 0)} power {r['lv'].get('power', 0)} | {r['dur']:.1f}")
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'human15_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    json.dump(dict(a0=a0, slope=sl, d=d), open(os.path.join(HERE, 'human_params.json'), 'w'))
