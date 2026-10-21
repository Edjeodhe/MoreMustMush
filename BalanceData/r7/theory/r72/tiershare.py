# 7-2: 등급별 생긴 버섯 비중 6회차/7회차 (구간·시드 합) → 예전 대기열의 '작은 등급 거르기' 배율 추정
import json,sys
sys.path.insert(0,'BalanceData/r7/theory/r72')
from align import rows,A,B
IV=[('6~13',6,13),('14~20',14,20),('21~25',21,25),('26~30',26,30),('31~40',31,40),('41~55',41,55)]
out={}
for name,lo,hi in IV:
    sh={}
    for lab,d in (('r6',A),('r7',B)):
        m=[0]*4;k=[0]*4
        for sd in range(1,7):
            for x in rows(d,sd):
                if lo<=x['disp']<=hi and x['boss']==0:
                    for t in range(4): m[t]+=x['madeT'][t]; k[t]+=x['killT'][t]
        sm=sum(m); sh[lab]=[v/sm for v in m]
        sh[lab+'k']=[k[t]/max(1,m[t]) for t in range(4)]
    # 거르기 배율: (r6 비중/r7 비중) 을 일반 등급 기준으로
    rel=[(sh['r6'][t]/sh['r7'][t])/(sh['r6'][0]/sh['r7'][0]) if sh['r7'][t]>0 else float('nan') for t in range(4)]
    out[name]=dict(r6=sh['r6'],r7=sh['r7'],sieve=rel,kill6=sh['r6k'],kill7=sh['r7k'])
    print(name,'r6',[f'{v:.2f}' for v in sh['r6']],'r7',[f'{v:.2f}' for v in sh['r7']],'거르기(일반=1)',[f'{v:.2f}' for v in rel],'처치율6',[f'{v:.2f}' for v in sh['r6k']],'7',[f'{v:.2f}' for v in sh['r7k']])
json.dump(out,open('BalanceData/r7/theory/r72/tiershare_out.json','w'),indent=1)
