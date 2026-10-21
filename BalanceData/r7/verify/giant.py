# -*- coding: utf-8 -*-
"""FindGiantSpot(Effects.cs:107~120)가 빈 자리를 찾는 비율 — 지금(판 시작 + 대기열이 200번 더 채운 상태) vs 제안(판 시작)."""
import math, numpy as np, verify as V
def find(f):
    w = f.w; WW = w['WW']
    for rad in range(0, 800, 40):
        k = max(1, int(np.round(rad / 25)))
        for i in range(k):
            a = i / k * 2 * math.pi + rad; x = WW / 2 + math.cos(a) * rad; y = w['NSY'] / 2 + math.sin(a) * rad * 0.6
            if x - 90 < w['X0'] or x + 90 > w['X1'] or y - 90 < w['Y0'] or y + 90 > w['NSY']: continue
            if f.ok(np.array([x]), np.array([y]), 90.0)[0]: return True
    return False
out = []
for z, L, II, mix in ((1.0, 4, 2, (0.069, 0.235, 0.378, 0.318)), (1.0, 4, 2, (1, 0, 0, 0)), (1.2, 4, 0, (0.58, 0.25, 0.14, 0.03)), (1.5, 3, 0, (0.76, 0.17, 0.07, 0))):
    p = np.array(mix, float); p /= p.sum(); devs = ('stump', 'moss', 'acorn')
    ro = []; rn = []
    for k in range(80):
        f = V.new_field(z, 60000 + k, devs); n = V.maxcol(V.OLD['cc'][L], V.OLD['ii'][II], z, 0.7)
        for j in range(n + 200):
            if len(f.cx) >= n: break
            t = f.rng_np.choice(4, p=p); RR = V.colRR(V.TR[t]); q = f.try_random(RR)
            if q: f.cx.append(q[0]); f.cy.append(q[1]); f.cR.append(RR)
        ro.append(find(f))
        f = V.new_field(z, 70000 + k, devs); n = V.maxcol(V.NEW['cc'][L], V.NEW['ii'][II], z, 0.8)
        V.place_prop(f, z, list(f.rng_np.choice(4, size=n, p=p)), devs); rn.append(find(f))
    s = f"z{z} cols{L}+II{II} 섞임{mix}: 거대 버섯 빈 자리 찾음 지금 {np.mean(ro):.2f} / 제안 {np.mean(rn):.2f}"
    print(s); out.append(s)
open('giant_out.txt', 'w', encoding='utf-8').write('\n'.join(out) + '\n')
