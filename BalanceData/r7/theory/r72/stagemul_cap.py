# 7-2: stageMul = min(1 + a·(c−1), CAP) 후보 (지금 1 + 0.03·(c−1), 상한 없음). 7회차 판별 수확 등급 섞임에 f^t를 곱해 V 변화
import json,math,sys,statistics as S
sys.path.insert(0,'BalanceData/r7/theory/r72')
from align import rows,B
SC=[1,5,20,100]
IV=[('6~13',6,13),('14~20',14,20),('21~30',21,30),('31~40',31,40),('41~55',41,55),('31~55',31,55)]
def Vmix(s): return sum(s[t]*SC[t] for t in range(4))/sum(s)
dec=json.load(open('BalanceData/r7/theory/r72/decomp_out.json'))
out={}
for a in (0.05,0.06,0.07,0.08):
    for cap in (2.0,2.2,2.4,9):
        row={}
        for name,lo,hi in IV:
            r=[]
            for sd in range(1,7):
                for x in rows(B,sd):
                    c=x['disp']
                    if lo<=c<=hi and x['boss']==0 and sum(x['killT'][:4])>0:
                        f=max(1.0,min(1+a*(c-1),cap))/(1+0.03*(c-1)); f=max(f,1.0)
                        s=x['killT'][:4]; r.append(Vmix([s[t]*f**t for t in range(4)])/Vmix(s))
            row[name]=math.exp(S.mean(math.log(v) for v in r))
        out[f'a{a}_cap{cap}']=row
        print(f'a {a} 상한 {cap}: '+' | '.join(f'{n} ×{v:.2f}' for n,v in row.items()))
json.dump(out,open('BalanceData/r7/theory/r72/stagemul_cap_out.json','w'),indent=1)
