# T7-5 대 T7-4 봇(T5-2, 시드 1~6, SKILL 0.6): 표시 스테이지별 일반 판 수입 시드별 비, 구간 비, 처치 분, 도전 수, 앞 구간 일치 여부
import json,statistics as S,math,hashlib
A='BalanceData/r7/test/T7-4/'; B='BalanceData/r7/test/T7-5/'
def ld(d,sd): return json.load(open(f'{d}phase_k06_s{sd}.json'))
def inc(d,sd):
    ph=ld(d,sd); return {r-4:ph['incR'][i] for i,r in enumerate(ph['stageR']) if ph['bossR'][i]==0 and r-4>=1}
same={}
for sd in range(1,7):
    a=ld(A,sd); b=ld(B,sd)
    n=0
    for i,(x,y) in enumerate(zip(a['incR'],b['incR'])):
        if x==y and a['buysR'][i]==b['buysR'][i]: n+=1
        else: break
    same[sd]=(n,a['stageR'][n]-4 if n<len(a['stageR']) else None, len(a['incR']),len(b['incR']))
print('시드별 처음 달라지는 판 인덱스, 그때 표시 스테이지, 총 판 수(T7-4,T7-5):',same)
print('\n구간 | 시드별 평균 수입 T7-5/T7-4 | 시드 평균의 중앙 비 | 시드별 비의 중앙')
IV=[('1~5',1,5),('6~13',6,13),('14~20',14,20),('21~30',21,30),('21~25',21,25),('26~30',26,30),('31~55',31,55)]
for nm,lo,hi in IV:
    ma=[];mb=[]
    for sd in range(1,7):
        a=inc(A,sd); b=inc(B,sd)
        ma.append(S.mean(v for k,v in a.items() if lo<=k<=hi)); mb.append(S.mean(v for k,v in b.items() if lo<=k<=hi))
    print(f'{nm} | '+' '.join(f'{y/x:.2f}' for x,y in zip(ma,mb))+f' | ×{S.median(mb)/S.median(ma):.3f} | ×{S.median([y/x for x,y in zip(ma,mb)]):.3f}  (평균 {S.median(ma):.4g}→{S.median(mb):.4g}; 시드 평균끼리 ×{S.mean(mb)/S.mean(ma):.3f})')
print('\n표시 스테이지별 중앙 수입 T7-4 → T7-5 (비)')
for c in list(range(13,56)):
    a=[inc(A,sd).get(c) for sd in range(1,7)]; b=[inc(B,sd).get(c) for sd in range(1,7)]
    a=[x for x in a if x]; b=[x for x in b if x]
    if a and b: print(f' {c}: {S.median(a):.3e} → {S.median(b):.3e} ×{S.median(b)/S.median(a):.2f}')
