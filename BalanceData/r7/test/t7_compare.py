# 6회차(T6-3) vs 7회차(T7-3) 봇 T5-2 조건: 구간별 일반 판 수입·만든 버섯·판 시간·내부 판 1~9 구매 수·50 처치 분
import json,statistics as S,re
A='BalanceData/r6/test/T6-3/'; B='BalanceData/r7/test/T7-3/'
OFF=4
IV=[('1~5',1,5),('6~13',6,13),('14~20',14,20),('21~30',21,30),('31~55',31,55)]
def ld(d,sd): return json.load(open(f'{d}phase_k06_s{sd}.json'))
def per(d,sd,lo,hi):
    ph=ld(d,sd); net=[];made=[];dur=[]
    for i,r in enumerate(ph['stageR']):
        disp=r-OFF
        if lo<=disp<=hi and ph['bossR'][i]==0:
            net.append(ph['incR'][i]); made.append(sum(ph['madeT'][r-1]))
            dur.append(ph['playR'][i]-(ph['playR'][i-1] if i>0 else 0))
    return net,made,dur
def gm(v): 
    import math; return math.exp(sum(math.log(max(x,1e-9)) for x in v)/len(v)) if v else float('nan')
print('구간 | 수입 평균(시드 평균의 중앙) 6회차 → 7회차 비 | 수입 기하평균 비 | 만든 버섯/판 6→7 비 | 판 시간(초) 6→7')
res={}
for name,lo,hi in IV:
    row={}
    for lab,d in (('r6',A),('r7',B)):
        mn=[];gmn=[];mk=[];du=[];n=0
        for sd in range(1,7):
            a,b,c=per(d,sd,lo,hi); mn.append(S.mean(a)); gmn.append(gm(a)); mk.append(S.mean(b)); du.append(S.mean(c)); n+=len(a)
        row[lab]=(S.median(mn),S.median(gmn),S.median(mk),S.median(du),n/6, mn, gmn, mk)
    r6,r7=row['r6'],row['r7']
    print(f'{name} | 평균 {r6[0]:.4g} → {r7[0]:.4g} ×{r7[0]/r6[0]:.2f} | 기하 ×{r7[1]/r6[1]:.2f} | 만든 {r6[2]:.0f} → {r7[2]:.0f} ×{r7[2]/r6[2]:.2f} | 판시간 {r6[3]:.1f} → {r7[3]:.1f} | 판수/시드 {r6[4]:.1f} → {r7[4]:.1f}')
    res[name]=row
# 내부 판 1~9 판별 구매 수
print('내부 판 1~9 구매 수 (시드별), 6회차 / 7회차')
for lab,d in (('r6',A),('r7',B)):
    for sd in range(1,7): print(lab,sd,ld(d,sd)['buysR'][:9])
for lab,d in (('r6',A),('r7',B)):
    mx=[max(ld(d,sd)['buysR'][:9]) for sd in range(1,7)]; print(lab,'판당 최대 구매 수',mx)
# 50 처치 분, 도전 수
def boss_info(d,sd):
    log=open(f'{d}t51_k06_s{sd}.log',encoding='utf-8').read().splitlines()
    ph=ld(d,sd); out={}
    for b in (10,20,30,40,50):
        rb=b+OFF
        lines=[l for l in log if l.startswith(f'  보스 R{rb} ')]
        idx=[i for i,s in enumerate(ph['stageR']) if s==rb]
        kill=any('처치' in l for l in lines); last=idx[-1]
        out[b]=(len(lines),kill,ph['playR'][last]/60)
    return out
for lab,d in (('r6',A),('r7',B)):
    bi=[boss_info(d,sd) for sd in range(1,7)]
    for b in (10,20,30,40,50):
        print(lab,'보스',b,'도전',[x[b][0] for x in bi],'처치',[x[b][1] for x in bi],'분',[round(x[b][2],1) for x in bi],'중앙 분',round(S.median([x[b][2] for x in bi]),1),'도전 중앙',S.median([x[b][0] for x in bi]))
