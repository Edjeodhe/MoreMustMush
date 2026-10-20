# 막혀서 파밍할 때 재도전 사이 공격력 성장 (T5-2 봇, 보스 도전 2회 이상 시드). 출처: BalanceData/r5/test/T5-2/phase_k06_s*.json, t51_k06_s*.log
import json,re,statistics as S
D='BalanceData/r5/test/T5-2/'
print('보스 | 시드 | 도전수 | 첫 도전 atk | 마지막 atk | 사이 판수 | atk 배율/재도전 | 판당 atk 증가율 | 도전별 atk')
res={}
for b in (10,20,30,40,50):
    for sd in range(1,7):
        ph=json.load(open(D+f'phase_k06_s{sd}.json'))
        stg,atk,br=ph['stageR'],ph['atkR'],ph['bossR']
        rb=b+4
        idx=[i for i,s in enumerate(stg) if s==rb and br[i] in (1,-1)]
        n=len(idx)
        if n<2: continue
        a0,a1=atk[idx[0]],atk[idx[-1]]
        rounds=idx[-1]-idx[0]
        gr=(a1/a0)**(1/(n-1)); gp=(a1/a0)**(1/rounds)
        res.setdefault(b,[]).append((gr,gp,n,rounds,a1/a0))
        print(b,sd,n,'%.1f'%a0,'%.1f'%a1,rounds,'x%.3f'%gr,'%.2f%%'%((gp-1)*100),[round(atk[i],1) for i in idx])
print('--- 보스별 중앙값 (재도전당 atk 배율 / 판당 증가율)')
for b,v in res.items():
    print(b,len(v),'시드: 재도전당 x%.3f'%S.median(x[0] for x in v),'판당 %.2f%%'%((S.median(x[1] for x in v)-1)*100))
