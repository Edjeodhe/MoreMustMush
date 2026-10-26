# 7-2: st.stageMul 기울기(Game.cs:249, 지금 0.03)를 바꿨을 때 7회차 판별 등급 섞임 → 수확당 등급점수(V) 변화
# 종 가중치 x = w_t·rareMul·stageMul^t (RoundSim.cs:183). 새 기울기 a면 등급 t 비중 ∝ s_t·f^t, f = (1+a(c−1))/(1+0.03(c−1))
import json,math,sys,statistics as S
sys.path.insert(0,'BalanceData/r7/theory/r72')
from align import rows,A,B
SC=[1,5,20,100]
IV=[('1~5',1,5),('6~13',6,13),('14~20',14,20),('21~30',21,30),('31~55',31,55)]
def f(c,a,c0=1): return (1+0.03*(c-1)+(a-0.03)*max(0,c-c0))/(1+0.03*(c-1))
def Vmix(s): return sum(s[t]*SC[t] for t in range(4))/sum(s)
dec=json.load(open('BalanceData/r7/theory/r72/decomp_out.json'))
cands=[(0.03,1),(0.05,1),(0.06,1),(0.07,1),(0.08,1),(0.06,14),(0.08,14),(0.10,14),(0.09,10),(0.10,12)]
out={}
for a,c0 in cands:
    row={}
    for name,lo,hi in IV:
        r=[]
        for sd in range(1,7):
            for x in rows(B,sd):
                if lo<=x['disp']<=hi and x['boss']==0 and sum(x['killT'][:4])>0:
                    s=[x['killT'][t] for t in range(4)]; k=f(x['disp'],a,c0)
                    s2=[s[t]*k**t for t in range(4)]
                    r.append(Vmix(s2)/Vmix(s))
        g=math.exp(S.mean(math.log(v) for v in r))
        row[name]=dict(Vgain=g, Vratio_vs_r6=dec[name]['v']*g)
    out[f'a{a}_c0{c0}']=row
    print(f'기울기 {a} (c0={c0}): '+' | '.join(f"{n} V×{row[n]['Vgain']:.2f}(대6 {row[n]['Vratio_vs_r6']:.2f})" for n,_,_ in IV))
json.dump(out,open('BalanceData/r7/theory/r72/stagemul_out.json','w'),indent=1)
