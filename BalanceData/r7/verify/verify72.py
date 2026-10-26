# verify 7-2: 이론_7 "7-2 재조정" 독립 재계산 (theory 스크립트 import 없이 원자료만 읽음)
# 실행: python BalanceData/r7/verify/verify72.py  (프로젝트 루트에서)
import json, math, statistics as S
A = 'BalanceData/r6/test/T6-3/'; B = 'BalanceData/r7/test/T7-3/'
OFF = 4; SC = [1, 5, 20, 100]
def load(d, sd):
    ph = json.load(open(f'{d}phase_k06_s{sd}.json'))
    out = []; cum = 0
    for i, r in enumerate(ph['stageR']):
        m = ph['madeT'][r-1][:4]; k = ph['killT'][r-1][:4]
        out.append(dict(i=i, R=r, c=r-OFF, boss=ph['bossR'][i], inc=ph['incR'][i], cum=cum, m=m, k=k))
        cum += ph['buysR'][i]
    return out
D = {lab: {sd: load(d, sd) for sd in range(1, 7)} for lab, d in (('6', A), ('7', B))}
gm = lambda v: math.exp(S.mean(math.log(x) for x in v))
def V(k): return sum(k[t]*SC[t] for t in range(4))/max(1, sum(k))
print('== 1. 등급 비중 (만든 버섯 / 수확), 일반 판, 시드 합')
for name, lo, hi in [('3~6', 3, 6), ('6~13', 6, 13), ('14~20', 14, 20), ('21~25', 21, 25), ('26~30', 26, 30), ('31~40', 31, 40), ('41~55', 41, 55)]:
    row = []
    for lab in '67':
        m = [0]*4; k = [0]*4
        for sd in range(1, 7):
            for x in D[lab][sd]:
                if lo <= x['c'] <= hi and x['boss'] == 0:
                    for t in range(4): m[t] += x['m'][t]; k[t] += x['k'][t]
        row.append(([v/sum(m) for v in m], [v/sum(k) for v in k]))
    (m6, k6), (m7, k7) = row
    sv = [(m6[t]/m7[t])/(m6[0]/m7[0]) if m7[t] > 0 else float('nan') for t in range(4)]
    print(f"{name}: 만든 6 {[round(v,3) for v in m6]} 7 {[round(v,3) for v in m7]} | 수확 레전드리 6/7 {k6[3]:.3f}/{k7[3]:.3f} 유니크+ {k6[2]+k6[3]:.2f}/{k7[2]+k7[3]:.2f} | 거르기 {[round(v,2) for v in sv]}")

print('\n== 2. 같은 상태 (누적 구매 같음): 7회차는 누적 구매를 선형 보간한 가상 판과 비교 + 짝의 표시 스테이지 차')
for name, lo, hi in [('14~20', 14, 20), ('21~30', 21, 30), ('31~55', 31, 55)]:
    ri = []; rv = []; rk = []; dc = []; pairs = []
    for sd in range(1, 7):
        b = [y for y in D['7'][sd] if y['boss'] == 0]
        for x in D['6'][sd]:
            if not (lo <= x['c'] <= hi) or x['boss']: continue
            # 7회차에서 cum이 x['cum']을 감싸는 두 일반 판 사이 로그 보간
            for y0, y1 in zip(b, b[1:]):
                if y0['cum'] <= x['cum'] <= y1['cum'] and y1['cum'] > y0['cum']:
                    w = (x['cum']-y0['cum'])/(y1['cum']-y0['cum'])
                    li = (1-w)*math.log(y0['inc'])+w*math.log(y1['inc'])
                    lv = (1-w)*math.log(V(y0['k']))+w*math.log(V(y1['k']))
                    lk = (1-w)*math.log(max(1, sum(y0['k'])))+w*math.log(max(1, sum(y1['k'])))
                    ri.append(math.exp(li)/x['inc']); rv.append(math.exp(lv)/V(x['k'])); rk.append(math.exp(lk)/max(1, sum(x['k'])))
                    dc.append((1-w)*y0['c']+w*y1['c']-x['c'])
                    pairs.append((math.log(math.exp(li)/x['inc']), math.log(math.exp(lk)/max(1, sum(x['k']))), math.log(math.exp(lv)/V(x['k']))))
                    break
    # 수확당 수입의 V 탄력: log(inc/kill 비) = beta * log(V 비) + a
    ys = [p[0]-p[1] for p in pairs]; xs = [p[2] for p in pairs]
    mx, my = S.mean(xs), S.mean(ys)
    beta = sum((a-mx)*(b-my) for a, b in zip(xs, ys))/sum((a-mx)**2 for a in xs)
    print(f"{name}: 짝 {len(ri)} | 수입 ×{gm(ri):.2f} (중앙 ×{S.median(ri):.2f}) | 수확 ×{gm(rk):.2f} | V ×{gm(rv):.2f} | 7회차 짝이 표시 +{S.mean(dc):.2f}스테이지 늦음 | 수확당 수입의 V 탄력 beta {beta:.2f} 절편 ×{math.exp(my-beta*mx):.2f}")

print('\n== 3. R1 등급 이득: 만든 섞임에 f^t (생성 쪽에서 바로 곱함) → 수확 섞임은 등급별 처치율 유지 가정')
def f_of(c, a=0.05, cap=2.4): return max(1+0.03*(c-1), min(1+a*(c-1), cap))/(1+0.03*(c-1))
for a, cap in [(0.05, 2.4), (0.06, 2.4), (0.05, 2.2), (0.045, 2.4)]:
    out = []
    for name, lo, hi in [('6~13', 6, 13), ('14~20', 14, 20), ('21~30', 21, 30), ('31~40', 31, 40), ('41~55', 41, 55)]:
        g = []; gl = []; gu = []; g0 = []; gu0 = []
        for sd in range(1, 7):
            for x in D['7'][sd]:
                if lo <= x['c'] <= hi and x['boss'] == 0 and sum(x['m']) > 0:
                    f = f_of(x['c'], a, cap)
                    kr = [x['k'][t]/x['m'][t] if x['m'][t] else 0 for t in range(4)]
                    m2 = [x['m'][t]*f**t for t in range(4)]; s2 = sum(m2); m2 = [v/s2*sum(x['m']) for v in m2]
                    k2 = [m2[t]*kr[t] for t in range(4)]
                    if sum(k2) == 0: continue
                    vr = V(k2)/V(x['k']); kc = sum(k2)/max(1, sum(x['k']))
                    g.append(vr*kc); gl.append(k2[3]/sum(k2)); gu.append((k2[2]+k2[3])/sum(k2)); g0.append(x['k'][3]/max(1,sum(x['k']))); gu0.append((x['k'][2]+x['k'][3])/max(1,sum(x['k'])))
        out.append(f"{name} V·수확 ×{gm(g):.2f} 수확 레전드리 {S.mean(g0):.2f}→{S.mean(gl):.2f} 유니크+ {S.mean(gu0):.2f}→{S.mean(gu):.2f}")
    print(f"a {a} 상한 {cap}: " + ' | '.join(out))

print('\n== 4. stageMul 값 (지금 / R1)')
for c in [1, 3, 6, 10, 14, 20, 25, 29, 35, 40, 47, 48, 55, 60, 80, 100]:
    print(c, round(1+0.03*(c-1), 3), round(max(1+0.03*(c-1), min(1+0.05*(c-1), 2.4)), 3))

print('\n== 5. 폭풍 R3: B_sum 측정(표시 60, 맑음 regen 0.25)으로 보정한 순환식')
# 탄생/판: 맑음 719.6, 폭풍 480.1 (군락 최대 18, 40초) → 순환 시간
born_c, born_s, ncol, T = 719.6, 480.1, 18, 40
cyc_c = ncol*T/born_c; cyc_s = ncol*T/born_s
dl_c, dl_s = 0.2008, 0.4313   # 재생지연 평균 (명목 0.25 / 0.5)
net_per_born = (0.6214)/(born_s/born_c)
print(f"순환 맑음 {cyc_c:.3f}s 폭풍 {cyc_s:.3f}s | 지연 비 실측/명목 {dl_c/0.25:.2f}·{dl_s/0.5:.2f} | net/탄생 비 {net_per_born:.3f}")
for mul in [2, 1.6, 1.4, 1.2, 1.0]:
    cyc = cyc_s - dl_s + dl_s*mul/2
    born = ncol*T/cyc
    print(f"폭풍 재생 ×{mul}: 순환 {cyc:.3f}s → 탄생/맑음 {born/born_c:.3f} → net/맑음 약 {born/born_c*net_per_born:.3f}  (theory 흐름식: {(0.57+0.25+0.175)/(0.57/0.8+0.25*mul+0.175):.3f})")
# 초반: R2 적용 L0 3.0, t_c 4.5 → 폭풍
for tc, rg, nm in [(4.5, 3.0, '1~5(L0 3.0)'), (5.8, 2.2, '6~13(L1 2.2)'), (1.1, 0.25, '21~30')]:
    base = 1/(tc+rg+0.175)
    print(nm, ' '.join(f"×{m}:{(1/(tc/0.8+rg*m+0.175))/base:.2f}" for m in (2, 1.4, 1.0)))

print('\n== 6. R1 뒤 수확 등급 비중 (구간·시드 합 기준, theory T7-4 합격선과 같은 기준)')
for name, lo, hi in [('21~30', 21, 30), ('31~55', 31, 55)]:
    K0 = [0]*4; K1 = [0]*4
    for sd in range(1, 7):
        for x in D['7'][sd]:
            if lo <= x['c'] <= hi and x['boss'] == 0 and sum(x['m']) > 0:
                f = f_of(x['c']); kr = [x['k'][t]/x['m'][t] if x['m'][t] else 0 for t in range(4)]
                m2 = [x['m'][t]*f**t for t in range(4)]; s2 = sum(m2); m2 = [v/s2*sum(x['m']) for v in m2]
                for t in range(4): K0[t] += x['k'][t]; K1[t] += m2[t]*kr[t]
    print(f"{name}: 레전드리 {K0[3]/sum(K0):.3f}→{K1[3]/sum(K1):.3f} | 유니크+ {(K0[2]+K0[3])/sum(K0):.3f}→{(K1[2]+K1[3])/sum(K1):.3f}")

print('\n== 7. 탄력 반영 R1 수입 이득 (V 이득^beta, beta는 2절 같은 상태 회귀)')
for name, vg, beta, st in [('14~20', 1.14, 0.47, 0.94), ('21~30', 1.30, 0.89, 0.68), ('31~40', 1.20, 0.39, 1.00), ('41~55', 1.02, 0.39, 1.00)]:
    print(f"{name}: V ×{vg} → 수입 ×{vg**beta:.2f} · 같은 상태 {st} → ×{st*vg**beta:.2f} (theory 같은 상태 기준 0.75 쓰면 ×{0.75*vg**beta:.2f})" if name=='21~30' else f"{name}: V ×{vg} → 수입 ×{vg**beta:.2f}")

print('\n== 8. 기울기 0.06 상한 2.4: 수확 등급 비중(합 기준)·탄력 반영 수입')
for name, lo, hi, beta, st in [('14~20', 14, 20, 0.47, None), ('21~30', 21, 30, 0.89, (0.68, 0.75)), ('31~55', 31, 55, 0.39, None)]:
    K0 = [0]*4; K1 = [0]*4; g = []
    for sd in range(1, 7):
        for x in D['7'][sd]:
            if lo <= x['c'] <= hi and x['boss'] == 0 and sum(x['m']) > 0:
                f = f_of(x['c'], 0.06); kr = [x['k'][t]/x['m'][t] if x['m'][t] else 0 for t in range(4)]
                m2 = [x['m'][t]*f**t for t in range(4)]; s2 = sum(m2); m2 = [v/s2*sum(x['m']) for v in m2]
                k2 = [m2[t]*kr[t] for t in range(4)]
                for t in range(4): K0[t] += x['k'][t]; K1[t] += k2[t]
                if sum(x['k']): g.append(V(k2)/V(x['k'])*sum(k2)/sum(x['k']))
    vg = gm(g)
    extra = f" · 같은 상태 {st[0]}~{st[1]} → ×{st[0]*vg**beta:.2f}~{st[1]*vg**beta:.2f}" if st else ''
    print(f"{name}: 레전드리 {K0[3]/sum(K0):.3f}→{K1[3]/sum(K1):.3f} 유니크+ {(K0[2]+K0[3])/sum(K0):.3f}→{(K1[2]+K1[3])/sum(K1):.3f} | V·수확 ×{vg:.2f} → 수입 ×{vg**beta:.2f}{extra}")
