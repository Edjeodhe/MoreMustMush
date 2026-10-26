import math, numpy as np, sys
from proposal import run_prop, s_min
Z=[1.68,1.5,1.35,1.2,1.1,1.0]
def jsround(x): return math.floor(x+0.5)
def maxcol(B, z, p, rain): return max(1, jsround(math.ceil(B*(1.2 if rain else 1)) * (1/(z*z))**p))
for B,p in [(14,0.8),(12,0.7),(16,0.8)]:
    print('B',B,'p',p)
    for z in Z:
        n=maxcol(B,z,p,True)
        r=run_prop(z,n,('stump','moss','acorn'),trials=150,seed=3,floor=s_min(z))
        pa=np.mean([k>=n for k,_ in r]); ms=np.mean([s for _,s in r])
        r2=run_prop(z,n,('stump','moss','acorn'),trials=150,seed=4)
        pv=np.mean([s < s_min(z)-1e-9 for _,s in r2]); pa2=np.mean([k>=n for k,_ in r2])
        print(f'  z{z} N{n} smin{s_min(z):.3f} P(all|floor smin)={pa:.3f} mean s={ms:.3f} | ladder to S_ABS: P(all)={pa2:.3f} P(s<smin)={pv:.3f}', flush=True)
