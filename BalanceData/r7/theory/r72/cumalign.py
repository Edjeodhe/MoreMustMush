# 7-2: 같은 누적 구매 수(= 거의 같은 노드 상태, 가장 싼 것부터 사므로 순서가 같다)에서 6회차 vs 7회차
# 6회차 판을 기준으로, 7회차에서 누적 구매 수가 가장 가까운 일반 판(차 ≤3)과 비교
import json,math,sys,statistics as S
sys.path.insert(0,'BalanceData/r7/theory/r72')
from align import rows,A,B
SC=[1,5,20,100]
def V(x): return sum(x['killT'][t]*SC[t] for t in range(4))/max(1,x['kill'])
IV=[('14~20',14,20),('21~30',21,30),('31~55',31,55)]
out={}
for name,lo,hi in IV:
    rm=[];ri=[];rv=[];rr=[]
    for sd in range(1,7):
        a=[x for x in rows(A,sd) if x['boss']==0]; b=[x for x in rows(B,sd) if x['boss']==0]
        for x in a:
            if not(lo<=x['disp']<=hi): continue
            y=min(b,key=lambda y:abs(y['cum']-x['cum']))
            if abs(y['cum']-x['cum'])>3: continue
            rm.append(y['made']/x['made']); ri.append(y['inc']/x['inc']); rv.append(V(y)/V(x)); rr.append((y['inc']/max(1,y['kill'])/V(y))/(x['inc']/max(1,x['kill'])/V(x)))
    g=lambda v: math.exp(S.mean(math.log(t) for t in v))
    out[name]=dict(n=len(rm),made=g(rm),inc=g(ri),V=g(rv),rest=g(rr))
    print(f"{name}: 짝 {len(rm)} | 만든 버섯 ×{g(rm):.2f} | 수입 ×{g(ri):.2f} | 수확당 등급점수 ×{g(rv):.2f} | 나머지 ×{g(rr):.2f}")
json.dump(out,open('BalanceData/r7/theory/r72/cumalign_out.json','w'),indent=1)
