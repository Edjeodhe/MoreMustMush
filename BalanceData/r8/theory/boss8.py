# -*- coding: utf-8 -*-
"""8회차 보스 체력 (사람 기준) — boss_fit.py(지표 B로 맞춘 판당 피해 비율 D 분포)에
  ① 유도 제거 배율 M (34 이후만; 14·24 때 사람은 유도를 못 샀다),
  ② 초반 수치안(E2)으로 사람 상태가 바뀐 몫 R_dps (14·24만, 34 이후는 1로 둔다 — T2 가격 벽에서 진행이 다시 모인다는 가정),
  ③ 목표 판당 실패율 q* 를 넣어 새 Hp_Mul을 낸다.
D = 판 안에 준 피해 / 보스 체력, ln D ~ N(mu, s). 체력 ×h → D/h. 실패 = D < 1.
사람 보스 DPS ∝ 공격력 × 영구 수확기^BETA_BALL × (판 시간 − 2) (가정, T6-1 사람 상태 측정과 같은 꼴).
다시 돌리기: python -X utf8 BalanceData/r8/theory/boss8.py → boss8_out.txt · boss8_out.json"""
import json, math, os
from statistics import NormalDist
from boss_fit import fit, B as BDATA, HP_NOW
from tree import load
from model8 import fit_fe, smooth_fe, simulate, t80_rows, feats
from design8 import FINAL, PLANS, run, fe_with_loose, HUMAN

HERE = os.path.dirname(os.path.abspath(__file__))
N = NormalDist()
BETA_BALL = 0.7
# 목표 판당 실패율 (평균 도전 = 1/(1−q)). 14·24 쉬운 허들, 34 지금 수준 유지(B 38%), 44 지금(84%)은 과함 → 60%, 54 지금(50%) 근처 60%
TARGET = {10: 0.15, 20: 0.20, 30: 0.40, 40: 0.60, 50: 0.60}
# 유도 제거 배율 M: 사람 보스 DPS(유도 있음)/(유도 없음). homing_mc 기하 배율 (M_mc − 1)에 실측 비율 0.5를 곱함
#   (T6-1: homing1+burst2 DPS ×1.54~1.64 vs MC homing1·power1 ×2.04 → 실측/MC 초과분 ≤ 0.6, 폭발 몫이 섞여 0.5로 둠)
M_MC = {30: 3.4, 40: 4.7, 50: 4.7}          # 유도 2·북돋기 4 / 유도 4·북돋기 4·바 스킬 3
REAL = 0.5
M = {10: 1.0, 20: 1.0, **{b: 1 + REAL * (m - 1) for b, m in M_MC.items()}}
M_LO = {10: 1.0, 20: 1.0, 30: 1 + 0.3 * (3.4 - 1), 40: 1 + 0.3 * (4.7 - 1), 50: 1 + 0.3 * (4.7 - 1)}
M_HI = {10: 1.0, 20: 1.0, 30: 1 + 0.8 * (3.4 - 1), 40: 1 + 0.8 * (4.7 - 1), 50: 1 + 0.8 * (4.7 - 1)}
BOSS_ROUND = {10: 14, 20: 24}
# 봇(T8-0) 도전 기록: (도전 판, 처치, 실패 판 남은 체력 목록) — 테스트_8.md T8-0
BOT = {10: (7, 6, [0.03]), 20: (6, 6, []), 30: (6, 6, []), 40: (7, 6, [0.01]), 50: (13, 6, [0.10, 0.19, 0.0, 0.28, 0.17, 0.18, 0.03])}


def atk_of(nodes, lv):
    E = lambda k: nodes[k]['efx'][min(lv.get(k, 0), nodes[k]['max'])] if k in nodes else 0
    return (1 + E('ps_atk') + E('ps_atk2') + E('ps_atk2p')) * 1.1


def dps_state(nodes, sim, n):
    lv = sim[n - 2]['lv']              # 보스 판 직전 판에 산 뒤 상태
    f = feats(nodes, lv, n)
    return atk_of(nodes, lv) * f['balls'] ** BETA_BALL * (f['dur'] - 2), atk_of(nodes, lv), f['balls'], f['dur']


def tries(mu, s, h, cap=30):
    """체력 ×h에서 판당 실패율·평균 도전 (도전 사이 성장 없음, 보수적)"""
    q = N.cdf((math.log(h) - mu) / s)
    return q, (1 / (1 - q) if q < 0.999 else cap)


if __name__ == '__main__':
    base = run({})
    fe = smooth_fe(fit_fe(base, t80_rows())[0])
    now = simulate(base, fe, HUMAN, nmax=23, boss_rounds=(14,))
    plan = run(PLANS[FINAL])
    new = simulate(plan, fe_with_loose(fe, PLANS[FINAL]['_loose']), HUMAN, nmax=23, boss_rounds=(14,))
    out = ['표시 보스 | B 실패율·남은 HP·판/유저 | mu s | 사람 상태 지금→권장안 (공격력·수확기·시간) R_dps | M (범위) | 목표 q* | h | Hp_Mul 지금 → 새 (M 범위) | 새 값에서 사람 실패율·평균 도전 (권장안 상태) | 같은 체력, 지금 트리 상태 사람 | 봇 예상 도전']
    js = {}
    for st, key, q, L, rpu, n in BDATA:
        mu, s = fit(q, L)
        if st in BOSS_ROUND:
            d0, a0, b0, t0 = dps_state(base, now, BOSS_ROUND[st])
            d1, a1, b1, t1 = dps_state(plan, new, BOSS_ROUND[st])
            R = d1 / d0
            stxt = f'{a0:.2f}·{b0}·{t0:.1f} → {a1:.2f}·{b1}·{t1:.1f} ×{R:.2f}'
        else:
            R, stxt = 1.0, '(34 이후 같다고 둠) ×1.00'
        h = math.exp(mu + s * N.inv_cdf(TARGET[st]))
        hp_new = HP_NOW[key] * h * R / M[st]
        rng = (HP_NOW[key] * h * R / M_HI[st], HP_NOW[key] * h * R / M_LO[st])
        # 새 체력에서 사람(권장안 상태): D_new = D_B · R / M / (hp_new/hp_now)
        mu_new = mu + math.log(R) - math.log(M[st])
        qn, an = tries(mu_new, s, hp_new / HP_NOW[key])
        qo, ao = tries(mu - math.log(M[st]), s, hp_new / HP_NOW[key])
        # 봇: T8-0 지금 체력에서 실패율 qb, 실패 남은 HP → 같은 꼴로 맞춤 (실패 1건 이하면 s = 0.3 가정)
        att, win, lefts = BOT[st]
        qb = (att - win) / att
        if len(lefts) >= 3:
            mub, sb = fit(qb, sum(lefts) / len(lefts))
        else:
            sb = 0.3
            mub = -sb * N.inv_cdf(max(qb, 0.02))
        qbn, abn = tries(mub, sb, hp_new / HP_NOW[key])
        out.append(f'{st} {key} | {q:.2f}·{L:.2f}·{rpu} | {mu:+.2f} {s:.2f} | {stxt} | {M[st]:.2f} ({M_LO[st]:.2f}~{M_HI[st]:.2f}) | {TARGET[st]:.2f} | ×{h:.2f} | '
                   f'{HP_NOW[key]} → {hp_new:.3g} ({rng[0]:.3g}~{rng[1]:.3g}) | {qn:.2f}·{an:.2f}번 | {qo:.2f}·{ao:.2f}번 | {abn:.2f}번 (판당 실패 {qbn:.2f})')
        js[key] = dict(stage=st, hp_now=HP_NOW[key], hp_new=hp_new, range=rng, q_target=TARGET[st], R_dps=R, M=M[st], human_tries=an, bot_tries=abn)
    # ---- 권장값 (불확실성에 강한 값): 사람 R_dps 낮은 쪽(14: ps_ball을 못 삼 · 24: 수확기 하나 적음)과 가운데, 봇 상태 배율까지 넣어 평가
    REC = {'king': 1.0, 'cleaner': 0.20, 'angel': 30, 'phantom': 100, 'mover': 90}
    botnow = simulate(base, fe, nmax=23, boss_rounds=(14,))
    botnew = simulate(plan, fe_with_loose(fe, PLANS[FINAL]['_loose']), nmax=23, boss_rounds=(14,))
    out.append('\n권장 Hp_Mul | 사람 권장안 실패율 (R_dps 낮은 쪽~가운데) · 평균 도전 | 사람 초반안 없이 같은 Hp | 봇 R_dps · 봇 평균 도전(권장안)')
    for st, key, q, L, rpu, n in BDATA:
        mu, s_ = fit(q, L)
        hr = REC[key] / HP_NOW[key]
        if st in BOSS_ROUND:
            rb = BOSS_ROUND[st]
            d0 = dps_state(base, now, rb)[0]
            d1 = dps_state(plan, new, rb)[0]
            lv = new[rb - 2]['lv']
            f = feats(plan, lv, rb)
            d_lo = atk_of(plan, lv) * max(1, f['balls'] - 1) ** BETA_BALL * (f['dur'] - 2)
            Rm, Rl = d1 / d0, max(1.0, d_lo / d0)
            Rb = dps_state(plan, botnew, rb)[0] / dps_state(base, botnow, rb)[0]
        else:
            Rm = Rl = Rb = 1.0
        qm, am = tries(mu + math.log(Rm) - math.log(M[st]), s_, hr)
        ql, al = tries(mu + math.log(Rl) - math.log(M[st]), s_, hr)
        q0, a0 = tries(mu - math.log(M[st]), s_, hr)
        att, win, lefts = BOT[st]
        qb = (att - win) / att
        if len(lefts) >= 3:
            mub, sb = fit(qb, sum(lefts) / len(lefts))
        else:
            sb = 0.3
            mub = -sb * N.inv_cdf(max(qb, 0.02))
        qbn, abn = tries(mub + math.log(Rb), sb, hr)
        out.append(f'{st} {key} {HP_NOW[key]} → {REC[key]} | {ql:.2f}~{qm:.2f} · {al:.2f}~{am:.2f}번 | {q0:.2f} · {a0:.2f}번 | ×{Rb:.2f} · {abn:.2f}번')
        js[key].update(rec=REC[key], rec_human_q=(ql, qm), rec_human_tries=(al, am), rec_bot_tries=abn, rec_human_no_early=(q0, a0))
    out.append('\n주: "같은 체력, 지금 트리 상태 사람" = 초반 수치안을 넣지 않고 Hp만 바꿨을 때(14·24는 사람이 더 약해 어려움).')
    txt = '\n'.join(out); print(txt)
    open(os.path.join(HERE, 'boss8_out.txt'), 'w', encoding='utf-8').write(txt + '\n')
    json.dump(js, open(os.path.join(HERE, 'boss8_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
