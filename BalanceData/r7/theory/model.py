# -*- coding: utf-8 -*-
"""7회차 군락 배치 이론 모델 (theory). 다시 돌리면 같은 결과(시드 고정).

python model.py  →  model_out.json, model_out.txt

1. 재현: 지금 코드(s=1, 무작위 30번, 격자 없음, 일반 등급만)로 측정표 20칸을 다시 만든다 → 오차
2. 제안 규칙(proposal.py)과 새 DB 값으로 모든 조합(지도 6 × 군락 레벨 7 × 날씨 2 × 장치 0/3) 배치
3. 측정표와 같은 칸의 예상 실제 군락 수
4. 덤(축복·별·꼬마)까지 얹은 최악 조합
5. 판 수입 영향(흐름 근사) — 지금 코드의 '못 놓은 군락 = 0.5초마다 다시 시도하는 대기열' 효과 포함
"""
import json, math
import numpy as np
from place_mc import world, colony_RR
from proposal import run_prop, s_min, S_VIS, S_ABS, COL_PACK, COL_TRIES, COL_GRID, COL_SHRINK

ZOOMS = [('md_map L0', 1.68), ('md_map L1', 1.5), ('md_map L2', 1.35), ('md_map L3', 1.2), ('md_map L4', 1.1), ('md_map2 L1', 1.0)]
DEV3 = ('stump', 'moss', 'acorn')

# ---- DB: 지금 / 제안 ----
OLD = dict(cc=[4, 6, 10, 17, 27], ii=[0, 4, 8], p=0.7, regen=[4.0, 3.2, 2.4, 1.6])
NEW = dict(cc=[4, 6, 8, 10, 11], ii=[0, 2, 3], p=0.8, regen=[2.0, 1.4, 0.9, 0.4])


def jsround(x):
    return math.floor(x + 0.5)


def maxcol(db, L, II, z, rain=False, extra_n=0.0, extra_pct=0.0):
    base = (db['cc'][L] + db['ii'][II] + extra_n) * (1 + extra_pct) * (1.2 if rain else 1)
    return max(1, jsround(math.ceil(base - 1e-9) * (1 / (z * z)) ** db['p']))


def mean(a):
    return float(np.mean(a))


out = {'const': dict(S_VIS=S_VIS, S_ABS=S_ABS, COL_PACK=COL_PACK, COL_TRIES=COL_TRIES, COL_GRID=COL_GRID, COL_SHRINK=COL_SHRINK),
       'db_old': OLD, 'db_new': NEW}
txt = []

# ---- 1. 재현 ----
MEAS = {1.68: [2.0, 2.3, 2.8, 2.3, 2.5], 1.35: [3.0, 4.5, 4.5, 4.8, 4.5], 1.1: [4.0, 7.8, 7.5, 8.0, 8.0], 1.0: [4.0, 8.5, 10.5, 10.3, 10.3]}
CELLS = [(0, 0), (2, 0), (3, 0), (4, 0), (4, 2)]   # (ed_cols, ed_cols2)
rep = {}; err = []
for z, ms in MEAS.items():
    row = []
    for (L, II), m in zip(CELLS, ms):
        n = maxcol(OLD, L, II, z)
        r = run_prop(z, n, (), trials=300, seed=101, fixed_s=1.0, tries=30, grid=0)
        v = mean([k for k, _ in r]); row.append((n, m, round(v, 2))); err.append(v - m)
    rep[z] = row
rmse = float(np.sqrt(np.mean(np.square(err)))); bias = float(np.mean(err)); mx = float(np.max(np.abs(err)))
out['reproduce'] = {str(z): v for z, v in rep.items()}; out['reproduce_err'] = dict(rmse=rmse, bias=bias, max=mx)
txt.append(f'[1] 재현(지금 코드, 일반 등급만, 300판): RMSE {rmse:.2f}개, 치우침 {bias:+.2f}, 최대 {mx:.2f}')
for z, row in rep.items():
    txt.append(f'  z{z}: ' + ' | '.join(f'최대 {n} 측정 {m} 모델 {v}' for n, m, v in row))

# ---- 2. 모든 조합 ----
combos = []
LEVELS = [(L, 0) for L in range(5)] + [(4, 1), (4, 2)]
for name, z in ZOOMS:
    for L, II in LEVELS:
        for rain in (False, True):
            for devs in ((), DEV3):
                n = maxcol(NEW, L, II, z, rain)
                r = run_prop(z, n, devs, trials=120, seed=1000 + L * 10 + II + (5 if rain else 0) + (50 if devs else 0))
                ks = [k for k, _ in r]; ss = [s for _, s in r]
                combos.append(dict(map=name, z=z, cols=L, cols2=II, rain=rain, dev=len(devs), n=n,
                                   p_all=mean([k >= n for k in ks]), placed=mean(ks), s_mean=mean(ss), s_lo=float(np.percentile(ss, 5)),
                                   p_below_smin=mean([s < s_min(z) - 1e-9 for s in ss]), s_min=s_min(z)))
out['combos'] = combos
worst = min(combos, key=lambda c: c['p_all'])
vis = max(combos, key=lambda c: c['p_below_smin'])
txt.append(f'[2] 제안 모든 조합 {len(combos)}개: P(전부 놓임) 최소 {worst["p_all"]:.3f} ({worst["map"]} cols{worst["cols"]}+II{worst["cols2"]} 비{worst["rain"]} 장치{worst["dev"]}), '
           f's<s_min 판 비율 최대 {vis["p_below_smin"]:.3f} ({vis["map"]} cols{vis["cols"]}+II{vis["cols2"]} 비{vis["rain"]} 장치{vis["dev"]})')

# ---- 3. 측정표 칸 ----
tab = {}
for name, z in [ZOOMS[0], ZOOMS[2], ZOOMS[4], ZOOMS[5]]:
    row = []
    for L, II in CELLS:
        c = [x for x in combos if x['z'] == z and x['cols'] == L and x['cols2'] == II and not x['rain'] and x['dev'] == 0][0]
        cd = [x for x in combos if x['z'] == z and x['cols'] == L and x['cols2'] == II and x['rain'] and x['dev'] == 3][0]
        row.append(dict(n=c['n'], placed=round(c['placed'], 2), s=round(c['s_mean'], 2), n_worst=cd['n'], placed_worst=round(cd['placed'], 2), s_worst=round(cd['s_mean'], 2)))
    tab[name] = row
out['table'] = tab
txt.append('[3] 측정표 칸(맑음·장치 없음 / 비·장치 3): 최대→실제(s)')
for name, row in tab.items():
    txt.append(f'  {name}: ' + ' | '.join(f'{c["n"]}→{c["placed"]}(s{c["s"]}) / {c["n_worst"]}→{c["placed_worst"]}(s{c["s_worst"]})' for c in row))

# ---- 4. 덤 최악 (축복 군락지 +1.5개, 별·꼬마 +30%) ----
ext = []
for name, z in ZOOMS:
    n = maxcol(NEW, 4, 2, z, True, extra_n=1.5, extra_pct=0.30)
    r = run_prop(z, n, DEV3, trials=120, seed=777)
    ext.append(dict(map=name, z=z, n=n, p_all=mean([k >= n for k, _ in r]), placed=mean([k for k, _ in r]), s_mean=mean([s for _, s in r])))
out['extras_worst'] = ext
txt.append('[4] 덤 최악(II2·비·장치3·축복+1.5·별/꼬마+30%): ' + ' | '.join(f'{e["map"]} {e["n"]}→{e["placed"]:.1f} s{e["s_mean"]:.2f}' for e in ext))

# ---- 5. 판 수입 영향 (흐름 근사) ----
# 한 군락 자리의 순환 = 비우는 시간 t_c + 다시 나는 시간 t_r + 자라서 맞을 수 있게 되는 0.175초
# 판 수확 ≈ c · m · T / (t_c + t_r + 0.175)  (자리마다 정상 상태, 처음 한 벌 무시)
# 지금 코드: 최대치 > 놓인 수면 못 놓은 군락이 0.5초마다 다시 시도 → t_r ≈ 0.25(대기열). 아니면 t_r = st.regen.
# t_c는 T6-3(6회차 적용 상태 봇) 판당 만든 버섯 수에서 거꾸로 구한다(지금 코드 상태 가정).
# 작아진 버섯: 맞을 단면 (r_b + 32) / (r_b + 32 s) 만큼 t_c가 길어진다 (r_b = 27 × ps_size).
G = 0.175
STATES = [
    # 이름, z, cols, II, regenL, 장치, m(군락당 버섯), T, 관측 made, 등급 섞임, r_b
    ('표시 1~5', 1.68, 1, 0, 0, (), 5, 12, 28, (1, 0, 0, 0), 27),
    ('표시 6~13', 1.5, 3, 0, 1, ('stump',), 5, 17, 39, (1, 0, 0, 0), 27),
    ('표시 14~20', 1.2, 4, 0, 2, DEV3, 5, 28, 320, (50, 5, 3, 2), 32),
    ('표시 21~30', 1.0, 4, 1, 3, DEV3, 6, 31, 1600, (50, 25, 15, 10), 35),
    ('표시 31~55', 1.0, 4, 2, 3, DEV3, 8, 35, 2550, (50, 25, 15, 10), 40),
]
imp = []
for name, z, L, II, RL, devs, m, T, made, mix, rb in STATES:
    row = dict(stage=name, z=z, cols=L, cols2=II, regenL=RL, T=T, made_obs=made)
    for rain in (False, True):
        n_old = maxcol(OLD, L, II, z, rain)
        r_old = run_prop(z, n_old, devs, mix, trials=150, seed=5, fixed_s=1.0, tries=30, grid=0)
        c_old = mean([k for k, _ in r_old])
        tr_old = 0.25 if n_old - c_old > 0.5 else OLD['regen'][RL] / (2 if rain else 1)
        if not rain:
            t_c = max(0.2, c_old * m * T / made - tr_old - G)   # 맑음 기준으로 t_c 역산
            row['t_c'] = round(t_c, 2)
        n_new = maxcol(NEW, L, II, z, rain)
        r_new = run_prop(z, n_new, devs, mix, trials=150, seed=6)
        c_new = mean([k for k, _ in r_new]); s_new = mean([s for _, s in r_new])
        tc_new = t_c * (rb + 32) / (rb + 32 * s_new)
        H_old = c_old * m * T / (t_c + tr_old + G)
        for tag, db in (('R0', OLD), ('R1', NEW)):   # R0 = 재생 노드 지금 값, R1 = 제안 값
            tr_new = db['regen'][RL] / (2 if rain else 1)
            H_new = c_new * m * T / (tc_new + tr_new + G)
            row[f'{"비" if rain else "맑음"}_{tag}'] = round(H_new / H_old, 2)
        row[f'{"비" if rain else "맑음"}_군락'] = f'{n_old}→{c_old:.1f} / {n_new}→{c_new:.1f} (s{s_new:.2f})'
    # t_c 민감도 (맑음, R1): ×0.5, ×2
    sens = []
    for f in (0.5, 2.0):
        tc = row['t_c'] * f
        n_old = maxcol(OLD, L, II, z); c_old = mean([k for k, _ in run_prop(z, n_old, devs, mix, trials=150, seed=5, fixed_s=1.0, tries=30, grid=0)])
        tr_old = 0.25 if n_old - c_old > 0.5 else OLD['regen'][RL]
        n_new = maxcol(NEW, L, II, z); rr = run_prop(z, n_new, devs, mix, trials=150, seed=6)
        c_new = mean([k for k, _ in rr]); s_new = mean([s for _, s in rr])
        sens.append(round((c_new / (tc * (rb + 32) / (rb + 32 * s_new) + NEW['regen'][RL] + G)) / (c_old / (tc + tr_old + G)), 2))
    row['맑음_R1_tc×0.5/×2'] = sens
    imp.append(row)
out['impact'] = imp
txt.append('[5] 판 수입 비(새/지금), 흐름 근사. R0 = 재생 노드 그대로, R1 = 재생 노드 제안값')
for r in imp:
    txt.append(f"  {r['stage']}: t_c {r['t_c']}s | 맑음 {r['맑음_군락']} R0 ×{r['맑음_R0']} R1 ×{r['맑음_R1']} (t_c ×0.5/×2: {r['맑음_R1_tc×0.5/×2']}) | 비 {r['비_군락']} R0 ×{r['비_R0']} R1 ×{r['비_R1']}")

json.dump(out, open('model_out.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
open('model_out.txt', 'w', encoding='utf-8').write('\n'.join(txt) + '\n')
print('\n'.join(txt))
