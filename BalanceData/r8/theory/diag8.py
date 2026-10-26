# -*- coding: utf-8 -*-
"""8회차 지표 진단: 초반 노드 레벨별 '판 수입 배율 m · 가격 · m/가격' (사람 내부 6판 상태 기준, model8 탄력)과
판 1~13 사람 진행표(지금 vs E2)에 눈에 보이는 것(군락·수확기·점수·정산·시간·버섯 수 추정)을 붙인다.
다시 돌리기: python -X utf8 BalanceData/r8/theory/diag8.py → diag8_out.txt"""
import math, os, json, statistics as S
from tree import price, parent_ok
from model8 import fit_fe, smooth_fe, simulate, t80_rows, lnF, feats
from design8 import FINAL, PLANS, run, fe_with_loose, HUMAN

HERE = os.path.dirname(os.path.abspath(__file__))
VISIBLE = {'ed_cols': '군락 +', 'ps_ball': '수확기 +1', 'md_kid': '꼬마 수확기', 'ed_score': '점수 숫자 +1', 'ed_price': '골드 +30%p',
           'ps_dur': '시간 +초', 'md_map': '필드 넓어짐(군락 +, 밀도 −)', 'power': '바 스킬(수입 0)', 'ps_atk': '공격력(초반 수입 0)',
           'md_bar': '바 길어짐', 'dev_stump': '장치 1개', 'dev_moss': '장치 1개', 'md_combo': '콤보 계수', 'ed_regen': '재생 빨라짐',
           'ed_rare0': '희귀 버섯', 'ps_size': '수확기 커짐', 'ps_spd': '빨라짐', 'ed_bonus': '수확 개수(골드 0)'}


def item_table(nodes, lv, n, label):
    rows = []
    for k in VISIBLE:
        nd = nodes.get(k)
        if not nd:
            continue
        for L in range(nd['max']):
            lv2 = dict(lv); lv2[k] = L
            lv3 = dict(lv2); lv3[k] = L + 1
            for p in ('power', 'ed_score', 'md_bar', 'ps_atk'):
                lv2.setdefault(p, 1); lv3.setdefault(p, 1)
            m = math.exp(lnF(nodes, lv3, n) - lnF(nodes, lv2, n))
            c = price(nd, L)
            if c > 1000:
                continue
            rows.append((c, f'{k}{L + 1}', m, VISIBLE[k]))
    rows.sort()
    out = [f'[{label}] 가격 | 노드 | 판 수입 배율 m | 보이는 것']
    out += [f'{c} | {k} | ×{m:.2f} | {v}' for c, k, m, v in rows]
    ms = [m for c, k, m, v in rows if c <= 600]
    out.append(f'가격 600 이하 {len(ms)}개: m 기하 평균 ×{math.exp(S.mean(math.log(x) for x in ms)):.3f} · m ≤ 1.03(수입이 거의 안 오름) {sum(1 for x in ms if x <= 1.03)}개')
    return out


def prog(sim, nodes, label, made_scale):
    out = [f'[{label}] 판 | 판 수입 | ×전판 | 산 것(개수) | 다음 가격/판 수입 | 군락 | 수확기 | 타격 점수 | 정산 | 시간 | 만든 버섯(추정)']
    prev = None
    for r in sim[:13]:
        f = feats(nodes, r['lv'], r['n'])
        made = made_scale[r['n']] * 1  # 기준선 만든 버섯 (참고)
        out.append(f"{r['n']} | {r['net']:.0f} | {('×%.2f' % (r['net'] / prev)) if prev else '-'} | {' '.join(k + str(l) for k, l, c in r['bought'])} ({r['buys']}) | "
                   f"{r['ratio']:.2f} | {r['cols']} | {r['balls']} | {r['score']:.0f} | {r['goldMul']:.2f} | {r['dur']:.1f} | {made:.0f}")
        prev = r['net']
    return out


if __name__ == '__main__':
    base = run({})
    fe = smooth_fe(fit_fe(base, t80_rows())[0])
    plan = run(PLANS[FINAL])
    hnow = simulate(base, fe, HUMAN, nmax=13, boss_rounds=())
    hnew = simulate(plan, fe_with_loose(fe, PLANS[FINAL]['_loose']), HUMAN, nmax=13, boss_rounds=())
    made = {n: S.median([r['made'] for r in t80_rows() if r['stage'] == n and r['bossFlag'] == 0]) for n in range(1, 14)}
    out = item_table(base, {}, 6, '지금 트리 · 내부 6판 · 빈 상태에서 한 레벨씩')
    out += [''] + item_table(plan, {}, 6, FINAL + ' · 내부 6판')
    out += [''] + prog(hnow, base, '사람 · 지금', made) + [''] + prog(hnew, plan, '사람 · ' + FINAL, made)
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'diag8_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
