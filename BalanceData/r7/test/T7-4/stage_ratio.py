# 표시 스테이지별 일반 판 수입의 시드 중앙값: 6회차(T6-3) / 7-1(T7-3) / 7-2후(T7-4); 구간별 단계 중앙비의 기하평균
import json,statistics as S,math
A='BalanceData/r6/test/T6-3/'; B='BalanceData/r7/test/T7-3/'; C='BalanceData/r7/test/T7-4/'
def stages(d):
    out={}
    for sd in range(1,7):
        ph=json.load(open(f'{d}phase_k06_s{sd}.json'))
        for i,r in enumerate(ph['stageR']):
            if ph['bossR'][i]==0 and r-4>=1: out.setdefault(r-4,[]).append(ph['incR'][i])
    return out
a=stages(A); b=stages(B); c=stages(C)
print('표시 | 6회차 중앙 | 7-1 중앙 | 7-2후 중앙 | 7-2후/6 | 7-2후/7-1')
rat={}
for s in sorted(a):
    if s in b and s in c:
        x,y,z=S.median(a[s]),S.median(b[s]),S.median(c[s]); rat[s]=z/x
        print(f'{s} | {x:.4g} | {y:.4g} | {z:.4g} | ×{z/x:.2f} | ×{z/y:.2f}')
for n,lo,hi in [('1~5',1,5),('6~13',6,13),('14~20',14,20),('21~30',21,30),('31~55',31,55)]:
    v=[rat[s] for s in rat if lo<=s<=hi]
    print(n,'스테이지 중앙비의 기하평균 ×%.2f 중앙 ×%.2f'%(math.exp(S.mean(map(math.log,v))),S.median(v)))
