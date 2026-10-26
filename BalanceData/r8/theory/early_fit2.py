# -*- coding: utf-8 -*-
"""early_fit.py 보조: 내부 판 1~13(+15) 일반 판 판 수입의 노드별 탄력. 점수·정산은 구조상 1로 고정(일반 버섯 위주, 골드 = 점수 × 정산).
다시 돌리기: python -X utf8 BalanceData/r8/theory/early_fit2.py → early_fit2_out.txt"""
import math, os, json
import numpy as np
from early_fit import rounds, feats, nodes
from tree import efx

HERE = os.path.dirname(os.path.abspath(__file__))
allr = [r for s in range(1, 7) for r in rounds(s)]

def X_of(r):
    f = feats(r); lv = r['lv']; E = lambda k: efx(nodes, lv, k)
    zoom = {0: 1.68, 1: 1.5, 2: 1.35, 3: 1.2, 4: 1.1}[lv.get('md_map', 0)]
    return dict(dur=f['dur'], cols=f['cols'], bar=E('md_bar'), comboK=E('md_combo'), atk=f['atk'],
                zoom=zoom, stump=1 + 0.5 * (lv.get('dev_stump', 0) + lv.get('dev_moss', 0)),
                power=1 + 0.2 * lv.get('power', 0), regen=1 / E('ed_regen'), rare=E('ed_rare0'),
                species=min(r['stage'], 6))

def run(lo, hi, keys, out):
    rs = [r for r in allr if r['bossFlag'] == 0 and lo <= r['stage'] <= hi]
    y = np.array([math.log(r['net']) - math.log(feats(r)['score']) - math.log(feats(r)['price']) - math.log(feats(r)['zone']) for r in rs])
    X = np.array([[1.0] + [math.log(X_of(r)[k]) for k in keys] for r in rs])
    c, *_ = np.linalg.lstsq(X, y, rcond=None)
    res = y - X @ c
    # 부트스트랩(시드 단위) 표준오차
    rng = np.random.default_rng(8)
    seeds = np.array([r['seed'] for r in rs]); bs = []
    for _ in range(300):
        pick = rng.choice(np.arange(1, 7), 6)
        idx = np.concatenate([np.where(seeds == s)[0] for s in pick])
        cb, *_ = np.linalg.lstsq(X[idx], y[idx], rcond=None); bs.append(cb)
    se = np.std(np.array(bs), axis=0)
    out.append(f'내부 {lo}~{hi} n {len(rs)} RMSE(ln) {np.sqrt(np.mean(res**2)):.3f}: ' + ' · '.join(f'{k} {b:+.2f}±{s:.2f}' for k, b, s in zip(['상수'] + keys, c, se)))
    return c

out = []
run(1, 13, ['dur', 'cols', 'bar', 'comboK', 'zoom', 'species'], out)
run(1, 13, ['dur', 'cols', 'bar', 'comboK', 'zoom', 'atk', 'power', 'stump', 'species'], out)
run(1, 13, ['dur', 'cols'], out)
run(5, 13, ['dur', 'cols', 'bar', 'comboK', 'zoom'], out)
# 판 1~13 시드별 '산 것 → 다음 판 net 배율' 평균 (같은 판 함께 산 것은 나눠 가짐)
from collections import defaultdict
gain = defaultdict(list)
for s in range(1, 7):
    rs = [r for r in allr if r['seed'] == s]
    for a, b in zip(rs, rs[1:]):
        if a['stage'] > 13 or a['bossFlag'] or b['bossFlag'] or b['stage'] == a['stage']:
            continue
        items = [x.split(':')[0] + x.split('>')[1] for x in (a['bought'] or '').split(';') if x]
        ratio = (b['net'] / a['net']) ** (1 / max(1, len(items)))
        for it in items:
            gain[it].append(ratio)
out.append('\n산 것별 다음 판 net 배율(같은 판 여럿이면 기하 평균으로 나눔, 판 1~13) — 잡음 큼(판당 ×1.35)')
for k, v in sorted(gain.items(), key=lambda kv: -np.exp(np.mean(np.log(kv[1])))):
    out.append(f'  {k:12s} ×{np.exp(np.mean(np.log(v))):.2f} (n {len(v)})')
txt = '\n'.join(out); print(txt)
open(os.path.join(HERE, 'early_fit2_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
