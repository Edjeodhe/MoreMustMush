# 제안 배치(무작위 30번 + 실패 시 격자 8px 빈 점 고르기)의 용량: P(N개 전부) 0.99·0.95와 평균(막힘)
import numpy as np, json
from place_mc import run
Z = [1.68, 1.5, 1.35, 1.2, 1.1, 1.0]
S = [0.4,0.45,0.5,0.55,0.6,0.65,0.7,0.75,0.8,0.85,0.9,1.0]
DEVS = {'none': (), 'dev3': ('stump','moss','acorn')}
out = []
for dn, dv in DEVS.items():
    for z in Z:
        for s in S:
            res = run(z, s, 30, (1,0,0,0), dv, n_req=70, trials=100, seed=21, grid=8)
            placed = np.array([p for p,_ in res]); ff = np.array([f for _,f in res])
            def nq(q):
                n=0
                while n < 70 and np.mean(ff > n+1) >= q: n+=1
                return n
            rec = dict(dev=dn, z=z, s=s, mean=float(placed.mean()), p01=float(np.percentile(placed,1)), n99=nq(0.99), n95=nq(0.95))
            out.append(rec); print(rec, flush=True)
json.dump(out, open('capacity_grid_out.json','w'), indent=0)
