# 용량 격자: z × s × 시도 수 × 장치(없음/3종) → 요청 120개일 때 놓인 수 분포, P(앞 N개 전부 놓임)
import numpy as np, json, sys
from place_mc import run
Z = [1.68, 1.5, 1.35, 1.2, 1.1, 1.0]
S = [0.35,0.4,0.45,0.5,0.55,0.6,0.65,0.7,0.75,0.8,0.85,0.9,1.0]
TRIES = [30, 60, 100]
DEVS = {'없음': (), '3종': ('stump','moss','acorn')}
out = []
for dn, dv in DEVS.items():
    for z in Z:
        for s in S:
            for tr in TRIES:
                res = run(z, s, tr, (1,0,0,0), dv, n_req=80, trials=150, seed=11)
                placed = np.array([p for p,_ in res]); ff = np.array([f for _,f in res])
                # N개 다 놓일 확률이 0.99·0.95 이상인 가장 큰 N
                def nq(q):
                    n=0
                    while n < 80 and np.mean(ff > n+1) >= q: n+=1
                    return n
                rec = dict(dev=dn, z=z, s=s, tries=tr, mean=float(placed.mean()), p05=float(np.percentile(placed,5)),
                           n99=nq(0.99), n95=nq(0.95), n90=nq(0.90))
                out.append(rec); print(rec, flush=True)
json.dump(out, open('capacity_out.json','w',encoding='utf-8'), ensure_ascii=False, indent=0)
