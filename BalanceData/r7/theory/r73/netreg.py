# 거대 버섯 등급·살아 있던 시간이 판 net에 주는 영향 (같은 상태·날씨·식 고정효과 회귀)
import sys,math,json
sys.path.insert(0,'BalanceData/r7/theory/r73')
from explore import load
import numpy as np
rows=[]
for tag,form in (('GXnew_m25','new'),('GXold_m25','old')):   # GX_m25(K=6)는 GXnew 앞 6판과 같은 판이라 뺀다
    for r in load(tag):
        if r['gTier']<0: continue
        r['form']=form; rows.append(r)
W=30.0
groups=sorted(set((r['state'],r['weather'],r['form']) for r in rows))
X=[];y=[]
for r in rows:
    fe=[1.0 if (r['state'],r['weather'],r['form'])==g else 0.0 for g in groups]
    A=(r['kt'] if r['kt'] is not None else W)/W   # 살아 있던 시간 비율
    t=r['gTier']
    # 등급별 '살아 있던 시간 비율' 기울기 (일반 거대가 기준)
    xs=[A*(t==k) for k in range(4)]
    X.append(fe+xs); y.append(math.log(r['net']))
X=np.array(X);y=np.array(y)
b,res,rk,sv=np.linalg.lstsq(X,y,rcond=None)
pred=X@b; e=y-pred; s=np.sqrt((e@e)/(len(y)-X.shape[1]))
cov=s*s*np.linalg.inv(X.T@X)
out={}
for k in range(4):
    i=len(groups)+k
    out[f't{k}_alive_slope']=(round(float(b[i]),3),round(float(np.sqrt(cov[i,i])),3))
print('n',len(y),'resid sd',round(float(s),3))
print('ln net 기울기 (살아 있던 시간 비율 0→1), ±1se:',out)
json.dump({'n':len(y),'sd':float(s),'slopes':out},open('BalanceData/r7/theory/r73/netreg_out.json','w'),ensure_ascii=False,indent=1)
