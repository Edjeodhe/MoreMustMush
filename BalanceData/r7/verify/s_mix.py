# -*- coding: utf-8 -*-
"""s0를 등급 섞임에 맞추면(ρ를 계획한 종의 실제 RR로) s가 얼마까지 커질 수 있나 — s0=1에서 시작해 줄이기만."""
import numpy as np, verify as V
devs = ('stump', 'moss', 'acorn')
for z, n, mix in ((1.0, 14, (0.069, 0.235, 0.378, 0.318)), (1.0, 17, (0.069, 0.235, 0.378, 0.318)), (1.2, 8, (0.58, 0.25, 0.14, 0.03)), (1.0, 14, (1, 0, 0, 0))):
    p = np.array(mix, float); p /= p.sum(); base = []; up = []
    orig = V.s0
    for k in range(40):
        f = V.new_field(z, 81000 + k, devs); ts = list(f.rng_np.choice(4, size=n, p=p))
        V.s0 = orig; c, s, _ = V.place_prop(f, z, ts, devs); base.append(s)
        f = V.new_field(z, 81000 + k, devs); ts = list(f.rng_np.choice(4, size=n, p=p))
        V.s0 = lambda z_, n_, d_: 1.0; c2, s2, it = V.place_prop(f, z, ts, devs); up.append((s2, c2 >= n))
    V.s0 = orig
    print(f"z{z} N{n} 섞임{mix}: 지금 공식 s {np.mean(base):.2f} | 1에서 줄이기 s {np.mean([a for a, _ in up]):.2f} (전부 {np.mean([b for _, b in up]):.2f})")
