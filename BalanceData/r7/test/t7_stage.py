# 표시 스테이지별 일반 판 수입의 시드 중앙값, 6회차 vs 7회차
import json,statistics as S
A='BalanceData/r6/test/T6-3/'; B='BalanceData/r7/test/T7-3/'
def stages(d):
    out={}
    for sd in range(1,7):
        ph=json.load(open(f'{d}phase_k06_s{sd}.json'))
        for i,r in enumerate(ph['stageR']):
            if ph['bossR'][i]==0 and r-4>=1: out.setdefault(r-4,[]).append(ph['incR'][i])
    return out
a=stages(A); b=stages(B)
print('표시 | 6회차 중앙 | 7회차 중앙 | 비 | (n6,n7)')
for s in sorted(a):
    if s in b and a[s] and b[s]:
        x,y=S.median(a[s]),S.median(b[s]); print(f'{s} | {x:.4g} | {y:.4g} | ×{y/x:.2f} | ({len(a[s])},{len(b[s])})')
