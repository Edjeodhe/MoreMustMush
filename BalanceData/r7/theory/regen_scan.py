# 재생 노드(ed_regen) Efx 후보별 판 수입 비(새/지금) — model.py 5절과 같은 흐름 근사, 같은 상태·시드
import json, numpy as np
from proposal import run_prop
import importlib.util, sys
G = 0.175
OLD_REGEN = [4.0, 3.2, 2.4, 1.6]
CAND = {'지금 4/3.2/2.4/1.6': OLD_REGEN, 'A 1.0/0.8/0.6/0.4': [1.0, 0.8, 0.6, 0.4], 'B 2.0/1.4/0.9/0.4': [2.0, 1.4, 0.9, 0.4],
        'C 3.0/2.0/1.0/0.4': [3.0, 2.0, 1.0, 0.4], 'D 2.0/1.2/0.7/0.5': [2.0, 1.2, 0.7, 0.5]}
src = open('model.py', encoding='utf-8').read()
ns = {}
exec(src.split('out = {')[0], ns)          # 상수·maxcol만 읽는다
exec('G=0.175\nDEV3=("stump","moss","acorn")\n' + src[src.index('STATES = ['):src.index('imp = []')], ns)
maxcol, OLD, NEW, STATES = ns['maxcol'], ns['OLD'], ns['NEW'], ns['STATES']
res = {}
pre = []
for name, z, L, II, RL, devs, m, T, made, mix, rb in STATES:
    d = {}
    for rain in (False, True):
        n_old = maxcol(OLD, L, II, z, rain); c_old = float(np.mean([k for k, _ in run_prop(z, n_old, devs, mix, trials=150, seed=5, fixed_s=1.0, tries=30, grid=0)]))
        tr_old = 0.25 if n_old - c_old > 0.5 else OLD['regen'][RL] / (2 if rain else 1)
        if not rain: t_c = max(0.2, c_old * m * T / made - tr_old - G)
        n_new = maxcol(NEW, L, II, z, rain); rr = run_prop(z, n_new, devs, mix, trials=150, seed=6)
        c_new = float(np.mean([k for k, _ in rr])); s_new = float(np.mean([s for _, s in rr]))
        d[rain] = (c_old, tr_old, c_new, s_new)
    pre.append((name, RL, m, T, rb, t_c, d))
for cn, rg in CAND.items():
    row = []
    for name, RL, m, T, rb, t_c, d in pre:
        v = []
        for rain in (False, True):
            c_old, tr_old, c_new, s_new = d[rain]
            tc_new = t_c * (rb + 32) / (rb + 32 * s_new)
            v.append(round((c_new / (tc_new + rg[RL] / (2 if rain else 1) + G)) / (c_old / (t_c + tr_old + G)), 2))
        row.append((name, v))
    res[cn] = row
    print(cn, ' | '.join(f'{n} 맑음×{a} 비×{b}' for n, (a, b) in row))
json.dump(res, open('regen_scan_out.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
