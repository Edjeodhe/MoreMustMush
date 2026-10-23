# -*- coding: utf-8 -*-
"""8회차: T8-0(유도 없음, 봇 SKILL 0.6, 시드 1~6) 판별 기록으로 판 수입 근사식을 맞춘다.
ln(net) = c + Σ b_k · ln(x_k)   (일반 판만, 내부 판 범위 지정)
x: 판 시간 dur · 타격 점수 (1+scoreFlat) · 정산 (1+0.1+ed_price-1+ed_price2) · 군락 최대 colsMax · 영구 수확기 balls · 공격력 atk ·
   콤보 (1+comboK·3) · 지역 배율 zone(=ZoneMul, 표시 스테이지) · 수확량(1+Σed_bonus) — 수확량은 버섯(골드 아님)이라 기대 0
다시 돌리기: python -X utf8 BalanceData/r8/theory/early_fit.py → early_fit_out.txt, rounds_levels.json"""
import csv, json, math, os, sys
import numpy as np
from tree import load, efx

HERE = os.path.dirname(os.path.abspath(__file__))
T80 = os.path.join(HERE, '..', 'test', 'T8-0')
nodes, consts, boss = load()


def rounds(seed):
    rows = list(csv.DictReader(open(os.path.join(T80, f'rounds_s{seed}.csv'), encoding='utf-8')))
    lv = {}
    out = []
    for r in rows:
        r = dict(r)
        r['lv'] = dict(lv)       # 판 시작 때 레벨 (bought = 이 판 뒤에 산 것)
        for b in (r['bought'] or '').split(';'):
            if ':' in b:
                k, ab = b.split(':')
                lv[k] = int(ab.split('>')[1])
        for k in ('stage', 'bossFlag', 'balls', 'colsMax', 'made', 'harvests', 'buys'):
            r[k] = int(float(r[k]))
        for k in ('net', 'atk', 'dur', 'playMin', 'gold', 'colS'):
            r[k] = float(r[k])
        r['seed'] = seed
        out.append(r)
    return out


def zone(stage):
    cst = max(1, stage - 4)
    i = 0 if cst < 20 else (1 if cst < 45 else 2)
    return 10 ** i * 1.01 ** (cst - 1)


def feats(r):
    lv = r['lv']
    E = lambda k: efx(nodes, lv, k)
    score = 1 + E('ed_score') + E('ed_score1p') + E('ed_score2') + E('ed_score2p') + E('ed_score3') + E('ed_score3p')
    price = 1 + 0.1 + (E('ed_price') - 1 if lv.get('ed_price') else 0) + E('ed_price2')
    combo = 1 + 3 * (E('md_combo') + E('md_combo2'))
    harv = 1 + E('ed_bonus') + E('ed_bonus1p') + E('ed_bonus2') + E('ed_bonus2p') + E('ed_bonus3') + E('ed_bonus3p')
    return dict(dur=r['dur'], score=score, price=price, cols=r['colsMax'], balls=r['balls'], atk=r['atk'], combo=combo,
                zone=zone(r['stage']), harv=harv)


def fit(rs, keys, fixed=None):
    fixed = fixed or {}
    y = np.array([math.log(max(1, r['net'])) for r in rs])
    for k, b in fixed.items():
        y = y - b * np.array([math.log(feats(r)[k]) for r in rs])
    X = np.array([[1.0] + [math.log(feats(r)[k]) for k in keys] for r in rs])
    coef, *_ = np.linalg.lstsq(X, y, rcond=None)
    res = y - X @ coef
    return coef, float(np.sqrt(np.mean(res ** 2))), res


if __name__ == '__main__':
    allr = [r for s in range(1, 7) for r in rounds(s)]
    json.dump([{k: v for k, v in r.items()} for r in allr], open(os.path.join(HERE, 'rounds_levels.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    out = []
    norm = [r for r in allr if r['bossFlag'] == 0]
    for lo, hi in ((1, 13), (5, 23), (1, 23), (15, 53)):
        rs = [r for r in norm if lo <= r['stage'] <= hi]
        for keys, fixed in ((['dur', 'score', 'price', 'cols', 'balls', 'atk', 'combo'], {'zone': 1.0}),
                            (['dur', 'cols', 'balls', 'atk', 'combo'], {'zone': 1.0, 'score': 1.0, 'price': 1.0})):
            keys = [k for k in keys if len(set(feats(r)[k] for r in rs)) > 1]
            c, rmse, _ = fit(rs, keys, fixed)
            out.append(f'내부 {lo}~{hi} (n {len(rs)}) 고정 {fixed} | 상수 {c[0]:+.2f} ' + ' '.join(f'{k} {b:+.2f}' for k, b in zip(keys, c[1:])) + f' | RMSE(ln) {rmse:.3f} (×{math.exp(rmse):.2f})')
    # 판별 표: 내부 1~16 시드별 net, 산 것, 특징
    out.append('\n시드 1~6 내부 1~16: 판 net | 판 시작 특징 | 이 판 뒤 산 것')
    for r in allr:
        if r['stage'] > 16:
            continue
        f = feats(r)
        out.append(f"s{r['seed']} R{r['stage']:2d}{'B' if r['bossFlag'] else ' '} net {r['net']:7.0f} gold {r['gold']:6.0f} | dur {f['dur']:.1f} sc {f['score']:.0f} pr {f['price']:.2f} cols {f['cols']} atk {f['atk']:.2f} combo {f['combo']:.2f} made {r['made']} | {r['bought']}")
    txt = '\n'.join(out)
    open(os.path.join(HERE, 'early_fit_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    print(txt[:6000])
