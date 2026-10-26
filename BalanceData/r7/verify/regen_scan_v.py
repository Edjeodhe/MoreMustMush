# -*- coding: utf-8 -*-
"""재생 노드 후보별 묶음 판당 버섯 비(새/지금), dyn 동역학(서버 ∞), t_c는 verify dyn 맞춤값."""
import numpy as np, verify as V
TC = {'1~5': (5.23, 6.16), '6~13': (11.58, 4.85), '14~20': (3.02, 1.57), '21~30': (1.15, 1.19), '31~55': (1.16, 0.76)}
CAND = {'B 2.0/1.4/0.9/0.4(제안)': [2.0, 1.4, 0.9, 0.4], 'E 3.0/2.0/0.9/0.3': [3.0, 2.0, 0.9, 0.3], 'F 3.0/2.2/1.2/0.3': [3.0, 2.2, 1.2, 0.3], 'G 2.5/1.8/0.9/0.25': [2.5, 1.8, 0.9, 0.25]}
out = []
for name, (z, L, II, RL, devs, m, th, ob) in V.STATES.items():
    for k, (TT, made, mix) in enumerate((th, ob)):
        tc = TC[name][k]
        n_old = V.maxcol(V.OLD['cc'][L], V.OLD['ii'][II], z, 0.7); n_new = V.maxcol(V.NEW['cc'][L], V.NEW['ii'][II], z, 0.8)
        base, _, _ = V.mean_made(z, n_old, TT, V.OLD['regen'][RL], tc, m, mix, devs, 'now', 8, 8000)
        row = []
        for cn, rg in CAND.items():
            v, _, _ = V.mean_made(z, n_new, TT, rg[RL], tc, m, mix, devs, 'prop', 8, 8000)
            row.append(f"{cn} ×{v / base:.2f}")
        s = f"{name} {'theory입력' if k == 0 else 'T6-3입력'}: " + ' | '.join(row); print(s, flush=True); out.append(s)
open('regen_scan_v_out.txt', 'w', encoding='utf-8').write('\n'.join(out) + '\n')
