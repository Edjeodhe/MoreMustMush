# -*- coding: utf-8 -*-
"""6회차 이론 — 보스 체력 하향안 (사람 기준). 실행: python BalanceData/r6/theory/model.py
결과: model_out.json · model_out.txt · dps_curve.csv (난수 시드 고정, 다시 돌리면 같은 값)

1) 보스 체력 (Defs.BossHp, 실제 게임): 40 × BOSS_HP_K(1) × ZoneMul(LatestTheme, 표시 s) × Hp_Mul
   ZoneMul = 10^지역 × 1.01^(s−1), 지역 = 표시 20부터 밤(1), 45부터 들판(2)  ← 보스 체력은 고른 지역이 아니라 '열린 최신 지역'
2) 봇 DPS = K(s) × 공격력(s). K는 fit_k.py가 T5-1·T5-2·T5-3② 보스 도전 판(206판)에서 보스별로 역산 (probit).
   기믹 몫 G: 천사 0.8(임시 수확기 정화, 가정) · 움직이는 버섯 0.6(BalanceSim 관문 MOVER_H) · 환상은 체력 ×1.99로 따로.
   K_state(s) = K_보스 / G_보스, 보스 사이 표시 스테이지는 ln K_state를 직선 보간. 공격력은 T5-2 표시 스테이지별 첫 판 중앙값.
3) 사람 시나리오 = 봇 곡선을 표시 스테이지로 늦춘 것 (지연 lag):
     하한 L: lag 8  — 표시 20에서 공격력 4.4~5(ps_atk 3레벨 = 4.95)·DPS 9 → 사용자 보고("공격 노드 3회")와 같은 수준
     중간 M: lag 4
     상한 U: lag 0  — 봇(T5-2) 그대로
   재도전 사이 성장: 사람은 봇 곡선을 판당 HG(0.5) 스테이지씩 따라간다(봇은 1). 재도전 = 지고 1판 파밍 뒤(2판마다).
   판 시간: 10 + min(10, 0.5(s−1)) + 시간 노드(지연된 봇 상태의 값). 나쁜 날씨 한파 −3초는 1/3 확률.
4) 처치 ⇔ ln D − ln need + σ z ≥ 0,  need = 체력 × G_hp × (1/(T − 2) + 회복률(청소부)),  σ = 0.3
"""
import csv, json, math, os, random, glob, statistics as S

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
FIT = json.load(open(os.path.join(HERE, 'fit_k.json')))
ORDER = ['king', 'cleaner', 'angel', 'phantom', 'mover']
NOW = dict(king=1.75, cleaner=20.0, angel=740.0, phantom=1650.0, mover=1100.0, loop=220.0)
G = dict(king=1.0, cleaner=1.0, angel=0.8, phantom=1.0, mover=0.6, loop=1.0)
GHP = dict(phantom=1.99)
T0, SIGMA, HG, RETRY_GAP, MC, CAP = 2.0, 0.3, 0.5, 2, 4000, 30
LAG = dict(L=8, M=4, U=0)
TARGET_MEAN = 2.35   # 하한 L의 평균 도전 횟수 목표 (사용자: 첫 판에도 잡을 가능성, 2~3판이면 잡힘 → 1번 ≥10%·3번 안 ≥90%)
HEAL_NOW = (0.05, 3.0)


def zone(s): return 2 if s >= 45 else 1 if s >= 20 else 0
def base_hp(s): return 40 * 10 ** zone(s) * 1.01 ** (s - 1)
def boss_of(s): return 'loop' if s // 10 > 5 else ORDER[s // 10 - 1]


# ---- 봇 곡선 (T5-2) ----
def bot_curve():
    by = {}
    for p in glob.glob(os.path.join(ROOT, 'BalanceData', 'r5', 'test', 'T5-2', 'phase_k06_s*.json')):
        ph = json.load(open(p)); seen = set(); prev = 0
        for st, atk, pl in zip(ph['stageR'], ph['atkR'], ph['playR']):
            T = pl - prev - 30; prev = pl; d = st - 4
            if d in seen or d < 1: continue
            seen.add(d); by.setdefault(d, []).append((atk, T))
    atk = {d: S.median(a for a, _ in v) for d, v in by.items()}
    T = {d: S.median(t for _, t in v) for d, v in by.items()}
    return atk, T


ATK, TBOT = bot_curve()
KS = {10 * (i + 1): FIT[b]['K_own'] / G[b] for i, b in enumerate(ORDER)}   # K_state 닻
# 시간 노드(초) = 봇 판 시간 − 10 − StageTime (봇 상태의 ps_dur·ps_dur2 몫)
DUR = {d: max(0.0, TBOT[d] - 10 - min(10, 0.5 * (d - 1))) for d in TBOT}


def kstate(x):
    xs = sorted(KS)
    if x <= xs[0]:   # 표시 10 앞: 공격력 말고는 거의 같다고 둔다 (수확기 1~2개)
        return KS[xs[0]]
    if x >= xs[-1]:
        return KS[xs[-1]]
    for a, b in zip(xs, xs[1:]):
        if a <= x <= b:
            w = (x - a) / (b - a)
            return math.exp(math.log(KS[a]) * (1 - w) + math.log(KS[b]) * w)


def interp(tab, x):
    x = max(1.0, min(max(tab), x)); a = int(math.floor(x)); b = min(max(tab), a + 1); w = x - a
    return math.exp(math.log(tab[a]) * (1 - w) + math.log(tab[b]) * w) if tab[a] > 0 and tab[b] > 0 else tab[a] * (1 - w) + tab[b] * w


def dps_state(x):   # 보스 기믹 빼고, 표시 스테이지 x의 봇 상태
    return kstate(x) * interp(ATK, x)


def dur_state(x):
    x = max(1, min(max(DUR), int(round(x))))
    return DUR[x]


def need(hp, s, T, heal):
    b = boss_of(s)
    return hp * GHP.get(b, 1) * (1 / max(0.5, T - T0) + (heal[0] / heal[1] if b == 'cleaner' else 0))


def boss_hp(s, mul): return base_hp(s) * mul


def first_try(s, mul, sc, heal=HEAL_NOW):
    """시나리오의 첫 도전: DPS, 판 시간, 필요 DPS, 판 끝 남은 체력 비율(결정적, z=0, 맑은 날씨)"""
    b = boss_of(s); x = s - LAG[sc]
    D = dps_state(x) * G[b]
    T = 10 + min(10, 0.5 * (s - 1)) + dur_state(x)
    hp = boss_hp(s, mul) * GHP.get(b, 1)
    h = heal[0] / heal[1] if b == 'cleaner' else 0
    net = D - h * boss_hp(s, mul)          # 청소부 회복은 본체 최대 체력 기준
    dealt = max(0.0, net) * (T - T0)
    return dict(D=D, T=T, need=need(boss_hp(s, mul), s, T, heal), left=max(0.0, 1 - dealt / hp), heal_dps=h * boss_hp(s, mul))


def tries(s, mul, sc, heal=HEAL_NOW, rng=None, runs=MC):
    b = boss_of(s); x0 = s - LAG[sc]; hg = 1.0 if sc == 'U' else HG
    hp = boss_hp(s, mul)
    out = []
    for _ in range(runs):
        t = 0; n = 0
        while True:
            n += 1
            x = x0 + hg * t
            T = 10 + min(10, 0.5 * (s - 1)) + dur_state(x) - (3 if rng.random() < 1 / 3 else 0)
            D = dps_state(x) * G[b]
            if math.log(D) - math.log(need(hp, s, T, heal)) + SIGMA * rng.gauss(0, 1) >= 0 or n >= CAP:
                break
            t += RETRY_GAP
        out.append(n)
    out.sort()
    return dict(mean=round(sum(out) / len(out), 2), med=out[len(out) // 2], p1=round(sum(v <= 1 for v in out) / len(out), 2),
                p3=round(sum(v <= 3 for v in out) / len(out), 2), p90=out[int(len(out) * 0.9)], cap=round(sum(v >= CAP for v in out) / len(out), 3))


def solve_mul(s, sc, target_mean, heal=HEAL_NOW):
    """시나리오 sc의 평균 도전 횟수 = target_mean 이 되는 Hp_Mul (이분, ln 공간)"""
    lo, hi = math.log(1e-3), math.log(1e5)
    for _ in range(40):
        mid = (lo + hi) / 2
        p = tries(s, math.exp(mid), sc, heal, random.Random(s * 7 + 1), 1500)['mean']
        if p < target_mean: lo = mid
        else: hi = mid
    return math.exp((lo + hi) / 2)


def nice(v):
    e = math.floor(math.log10(v)); m = v / 10 ** e
    steps = [1, 1.1, 1.2, 1.3, 1.5, 1.6, 1.8, 2, 2.2, 2.5, 2.8, 3, 3.5, 4, 4.5, 5, 5.5, 6, 7, 8, 8.5, 9, 10]
    return round(min(steps, key=lambda q: abs(math.log(q / m))) * 10 ** e, 6)


def main():
    lines, res = [], {}
    P = lambda *a: lines.append(' '.join(str(x) for x in a))
    P('K 닻(K_state = K_보스/G):', {k: round(v, 2) for k, v in KS.items()})
    P('봇 상태 DPS (기믹 전) 표시 2·6·10·12·16·20·22·26·30·32·36·40·42·46·50·52:',
      {x: round(dps_state(x), 1) for x in (2, 6, 10, 12, 16, 20, 22, 26, 30, 32, 36, 40, 42, 46, 50, 52)})
    # 보정 확인: 지금 Hp에서 U(봇) 예측 vs T5-2
    T52 = {10: '2(1~2)', 20: '2(1~2)', 30: '2~5', 40: '3~9', 50: '1~5'}
    P('\n[보정 확인] 지금 Hp, 봇(U) 예측 도전 vs T5-2 측정')
    for s in (10, 20, 30, 40, 50):
        r = tries(s, NOW[boss_of(s)], 'U', rng=random.Random(s))
        P(f'  {s} {boss_of(s)}: 예측 평균 {r["mean"]} 중앙 {r["med"]} (p90 {r["p90"]}) · 측정 {T52[s]}')
    # 지금 수치: 시나리오별
    P('\n[지금] 보스 체력과 시나리오별 첫 도전·도전 횟수')
    cur = {}
    for s in (10, 20, 30, 40, 50, 60):
        b = boss_of(s); mul = NOW[b]
        row = dict(boss=b, hp=boss_hp(s, mul), hp_forest_quote=40 * 1.01 ** (s - 1) * mul)
        for sc in ('L', 'M', 'U'):
            f = first_try(s, mul, sc); t = tries(s, mul, sc, rng=random.Random(100 + s))
            row[sc] = dict(first=f, tries=t)
        cur[s] = row
        P(f'  {s} {b} Hp_Mul {mul}: 체력 {row["hp"]:.4g} (숲 기준이라면 {row["hp_forest_quote"]:.4g})')
        for sc in ('L', 'M', 'U'):
            f, t = row[sc]['first'], row[sc]['tries']
            P(f'     {sc}: DPS {f["D"]:.4g} 판 {f["T"]:.0f}s 필요 {f["need"]:.4g} (비 {f["D"]/f["need"]:.3g}) 회복 {f["heal_dps"]:.3g}/s 첫 판 남은 {f["left"]:.0%} · 도전 평균 {t["mean"]} 중앙 {t["med"]} 3번 안 {t["p3"]:.0%} 상한 {t["cap"]:.0%}')
    res['current'] = cur
    # 제안: 하한 L의 평균 도전 = TARGET_MEAN (사용자: 하한도 2~3번) → 보기 좋은 값으로 반올림
    P(f'\n[제안] 하한 L의 평균 도전 {TARGET_MEAN}번에 맞춘 Hp_Mul (회복 3%/3초 안 포함)')
    heals = {'now(5%/3s)': (0.05, 3.0), 'B(3%/3s)': (0.03, 3.0), 'C(4%/4s)': (0.04, 4.0)}
    prop = {}
    for s in (10, 20, 30, 40, 50, 60):
        b = boss_of(s)
        for hk, hv in heals.items():
            if b != 'cleaner' and hk != 'now(5%/3s)':
                continue
            m = solve_mul(s, 'L', TARGET_MEAN, hv)
            prop[(s, hk)] = m
            P(f'  {s} {b} [{hk}] 계산 {m:.4g} → 반올림 {nice(m)}')
    res['solve'] = {f'{s}|{h}': v for (s, h), v in prop.items()}
    # 최종 표 (반올림 값으로 다시 계산)
    FINAL = dict(king=None, cleaner=None, angel=None, phantom=None, mover=None, loop=None)
    for s in (10, 20, 30, 40, 50, 60):
        FINAL[boss_of(s)] = nice(prop[(s, 'B(3%/3s)' if boss_of(s) == 'cleaner' else 'now(5%/3s)')])
    FINAL['king'] = NOW['king'] if FINAL['king'] >= NOW['king'] else FINAL['king']   # 10은 하향만 (올리지 않음)
    res['loop_model'] = FINAL['loop']
    if FINAL['loop'] > NOW['loop']:
        FINAL['loop'] = NOW['loop']   # 60~ 루프는 '하향' 요청 범위 밖이라 유지 (모델값은 참고로만, 표시 55 밖 외삽)
    res['final_raw'] = dict(FINAL)
    P('\n[최종안 확인] 반올림 값, 청소부 회복 3%/3초')
    fin = {}
    for s in (10, 20, 30, 40, 50, 60):
        b = boss_of(s); mul = FINAL[b]; heal = (0.03, 3.0) if b == 'cleaner' else HEAL_NOW
        row = dict(boss=b, mul_now=NOW[b], mul=mul, hp=boss_hp(s, mul), ratio=mul / NOW[b])
        for sc in ('L', 'M', 'U'):
            row[sc] = dict(first=first_try(s, mul, sc, heal), tries=tries(s, mul, sc, heal, random.Random(200 + s)))
        fin[s] = row
        P(f'  {s} {b}: {NOW[b]} → {mul} (×{mul/NOW[b]:.3g}) 체력 {row["hp"]:.4g}')
        for sc in ('L', 'M', 'U'):
            f, t = row[sc]['first'], row[sc]['tries']
            P(f'     {sc}: 비 {f["D"]/f["need"]:.3g} 첫 판 남은 {f["left"]:.0%} · 도전 평균 {t["mean"]} 중앙 {t["med"]} 1번 {t["p1"]:.0%} 3번 안 {t["p3"]:.0%} p90 {t["p90"]}')
    res['final'] = fin
    # 청소부만: 회복 그대로 두고 Hp만 낮추는 안
    s = 20; m_now_heal = nice(prop[(20, 'now(5%/3s)')])
    P(f'\n[청소부 대안] 회복 5%/3초 유지 + Hp_Mul {m_now_heal}')
    for sc in ('L', 'M', 'U'):
        f = first_try(s, m_now_heal, sc); t = tries(s, m_now_heal, sc, rng=random.Random(300 + s))
        P(f'     {sc}: 비 {f["D"]/f["need"]:.3g} 회복 {f["heal_dps"]:.3g}/s vs DPS {f["D"]:.3g} 첫 판 남은 {f["left"]:.0%} · 도전 평균 {t["mean"]} 3번 안 {t["p3"]:.0%}')
    # "아무것도 못 하는" 문턱: DPS ≤ 회복
    for hk, hv in heals.items():
        for mul in (20, 1.0, 0.8, 0.6):
            hpv = boss_hp(20, mul); P(f'  청소부 Hp_Mul {mul} [{hk}]: 회복 {hpv*hv[0]/hv[1]:.3g}/s = 공격력 4.95·수확기 2개 DPS {dps_state(12):.3g}의 {hpv*hv[0]/hv[1]/dps_state(12):.0%}')
    # 민감도: 하한 시나리오 정의(lag 6·8·10), σ 0.2·0.45, HG 0.25·1.0, G 천사 0.6·1.0
    P('\n[민감도] 최종안에서 하한 L의 3번 안 처치 확률·평균 도전')
    global HG, SIGMA
    base = (dict(LAG), HG, SIGMA, dict(G))
    sens = {}
    for name, fn in [('lag6', lambda: LAG.update(L=6)), ('lag10', lambda: LAG.update(L=10)), ('HG0.25', lambda: None), ('HG1.0', lambda: None),
                     ('sigma0.2', lambda: None), ('sigma0.45', lambda: None), ('G_angel0.6', lambda: G.update(angel=0.6)), ('G_angel1.0', lambda: G.update(angel=1.0)),
                     ('G_mover0.4', lambda: G.update(mover=0.4)), ('G_mover0.8', lambda: G.update(mover=0.8))]:
        LAG.clear(); LAG.update(base[0]); HG = base[1]; SIGMA = base[2]; G.clear(); G.update(base[3])
        fn()
        if name == 'HG0.25': HG = 0.25
        if name == 'HG1.0': HG = 1.0
        if name == 'sigma0.2': SIGMA = 0.2
        if name == 'sigma0.45': SIGMA = 0.45
        row = {}
        for s in (20, 30, 40, 50, 60):
            b = boss_of(s); heal = (0.03, 3.0) if b == 'cleaner' else HEAL_NOW
            t = tries(s, FINAL[b], 'L', heal, random.Random(400 + s), 1500)
            row[s] = (t['p3'], t['mean'])
        sens[name] = row
        P(f'  {name:11s} ' + ' · '.join(f'{s}: {v[0]:.0%}/{v[1]}' for s, v in row.items()))
    LAG.clear(); LAG.update(base[0]); HG = base[1]; SIGMA = base[2]; G.clear(); G.update(base[3])
    res['sens'] = sens
    # 곡선
    with open(os.path.join(HERE, 'dps_curve.csv'), 'w', newline='', encoding='utf-8') as fp:
        w = csv.writer(fp); w.writerow(['disp', 'bot_atk', 'K_state', 'bot_dps_state', 'L_dps(lag8)', 'M_dps(lag4)', 'dur_nodes'])
        for x in range(1, 56):
            w.writerow([x, round(interp(ATK, x), 3), round(kstate(x), 3), round(dps_state(x), 3), round(dps_state(x - 8), 3) if x > 8 else '', round(dps_state(x - 4), 3) if x > 4 else '', round(dur_state(x), 2)])
    res['params'] = dict(T0=T0, SIGMA=SIGMA, HG=HG, RETRY_GAP=RETRY_GAP, LAG=LAG, G=G, KS=KS)
    json.dump(res, open(os.path.join(HERE, 'model_out.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
    open(os.path.join(HERE, 'model_out.txt'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
