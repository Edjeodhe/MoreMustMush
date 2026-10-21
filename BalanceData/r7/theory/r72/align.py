# 7-2: 6회차(T6-3) vs 7회차(T7-3) 판별 정렬 — 같은 판 / 같은 누적 구매 수
import json,math,statistics as S
A='BalanceData/r6/test/T6-3/'; B='BalanceData/r7/test/T7-3/'
OFF=4
def rows(d,sd):
    ph=json.load(open(f'{d}phase_k06_s{sd}.json'))
    out=[];cum=0
    for i,r in enumerate(ph['stageR']):
        m=ph['madeT'][r-1]; k=ph['killT'][r-1]
        dur=ph['playR'][i]-(ph['playR'][i-1] if i>0 else 0)-30
        out.append(dict(i=i+1,R=r,disp=r-OFF,boss=ph['bossR'][i],inc=ph['incR'][i],cum=cum,buy=ph['buysR'][i],
                        made=sum(m),kill=sum(k),madeT=m,killT=k,dur=dur,atk=ph['atkR'][i]))
        cum+=ph['buysR'][i]
    return out
if __name__=='__main__':
    import sys
    for sd in range(1,7):
        a=rows(A,sd); b=rows(B,sd)
        print(f'== 시드{sd}: 판i | 표시 | 누적구매 6/7 | 수입 6/7 비 | 만든 6/7 | 수확 6/7 | 수확당 골드 6/7 비 | 만든 등급 6 / 7')
        for x,y in zip(a,b):
            if not(14<=x['disp']<=34) or x['boss'] or y['boss']: continue
            vx=x['inc']/max(1,x['kill']); vy=y['inc']/max(1,y['kill'])
            print(f"{x['i']} | {x['disp']}/{y['disp']} | {x['cum']}/{y['cum']} | {x['inc']:.3g}/{y['inc']:.3g} x{y['inc']/x['inc']:.2f} | {x['made']}/{y['made']} | {x['kill']}/{y['kill']} | {vx:.3g}/{vy:.3g} x{vy/vx:.2f} | {x['madeT']} / {y['madeT']}")
