# 측정표(에디터, 장치 없음, 맑음) 재현 확인
import numpy as np, json
from place_mc import run
MEAS = {1.68: ([2,5,8,13,17],[2.0,2.3,2.8,2.3,2.5]), 1.35: ([3,7,11,18,23],[3.0,4.5,4.5,4.8,4.5]),
        1.1: ([4,9,15,24,31],[4.0,7.8,7.5,8.0,8.0]), 1.0: ([4,10,17,27,35],[4.0,8.5,10.5,10.3,10.3])}
MIX = {'t0만': (1,0,0,0), '초반(희귀포자0)': (50,5,3,2), '후반(희귀포자3)': (50,25,15,10)}
out = {}
for mname, mix in MIX.items():
    for z,(ns,ms) in MEAS.items():
        row=[]
        for n,m in zip(ns,ms):
            res = run(z, 1.0, 30, mix, (), n_req=n, trials=300, seed=7)
            row.append(round(float(np.mean([p for p,_ in res])),2))
        out[f'{mname} z{z}'] = dict(n=ns, meas=ms, mc=row)
        print(mname, z, ns, ms, row)
json.dump(out, open('validate_out.json','w',encoding='utf-8'), ensure_ascii=False, indent=1)
