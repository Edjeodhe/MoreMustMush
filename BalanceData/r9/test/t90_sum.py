# T9-0 집계. python -X utf8 BalanceData/r9/test/t90_sum.py k06|k10 > BalanceData/r9/test/T9-0/sum_<tag>.txt
import csv,glob,re,sys,statistics as S
tag=sys.argv[1]; B='BalanceData/r9/test/T9-0/'; TUT=2
med=lambda x:S.median(x) if x else float('nan')
g=lambda x:f'{x:.3g}'
seeds={}
for f in sorted(glob.glob(B+f'rounds_{tag}_s*.csv')):
    rows=list(csv.DictReader(open(f,encoding='utf-8'))); sd=int(rows[0]['seed']); lv={}
    for r in rows:
        d=0
        for p in filter(None,r['bought'].split(';')):
            k,t=p.split(':'); a,b=t.split('>'); d+=int(b)-int(a); lv[k]=int(b)
        r['d']=int(r['stage'])-TUT; r['allb']=d; r['ia']=lv.get('inf_atk',0); r['ig']=lv.get('inf_gold',0)
    seeds[sd]=rows
print('시드',sorted(seeds),'판 수',[len(v) for v in seeds.values()],'끝 표시',[v[-1]['d'] for v in seeds.values()],[v[-1]['bossRes'] for v in seeds.values()])
def normal(r): return not r['boss'] and r['bossFlag']=='0'
print('\n== 구간별 일반 판(시드 평균의 중앙) / 구간 직전 대비')
prev=None
for lo,hi in [(41,50),(51,60),(61,70),(71,80),(81,90),(91,100)]:
    nets=[];by=[];z=0;n=0;ab=[]
    for rows in seeds.values():
        x=[r for r in rows if lo<=r['d']<=hi and normal(r)]
        if not x: continue
        nets.append(S.mean(float(r['net']) for r in x)); ab.append(S.mean(r['allb'] for r in x)); z+=sum(1 for r in x if r['allb']==0); n+=len(x)
    m=med(nets); print(f"표시 {lo}~{hi}: net {g(m)}"+(f" (×{m/prev:.2f})" if prev else '')+f" | 구매/판 {med(ab):.2f} | 0구매 {z}/{n}={z/n:.0%}"); prev=m
print('\n== 지역별 일반 판 수입(시드 평균의 중앙)')
for z in ('field','sea'):
    nets=[]
    for rows in seeds.values():
        x=[float(r['net']) for r in rows if r['d']>=51 and normal(r) and r['zone']==z]
        if x: nets.append(S.mean(x))
    print(z,g(med(nets)),'시드',len(nets))
print('\n== 지역 전환(표시)')
for sd,rows in seeds.items():
    ch=[];p=None
    for r in rows:
        if r['zone']!=p: ch.append(f"{r['zone']}@{r['d']}"); p=r['zone']
    print(f"s{sd}",' '.join(ch))
print('\n== 바다 판(표시>=75, 일반) 처치율(잡음/생김 합) · 판 수입 / 들판 마지막 판(표시 74 일반) 대비 (시드별)')
for sd,rows in seeds.items():
    sea=[r for r in rows if r['zone']=='sea' and normal(r) and int(r['made'])>0]
    fld=[r for r in rows if r['zone']=='field' and normal(r) and int(r['made'])>0]
    if not sea: print(f's{sd}: 바다 없음'); continue
    kr=sum(int(r['kill']) for r in sea)/sum(int(r['made']) for r in sea)
    kf=sum(int(r['kill']) for r in fld[-5:])/sum(int(r['made']) for r in fld[-5:])
    last=float(fld[-1]['net']); sn=S.mean(float(r['net']) for r in sea[:5]); sa=S.mean(float(r['net']) for r in sea)
    print(f"s{sd}: 바다 처치율 {kr:.4f}(들판 끝 5판 {kf:.4f}) · 바다 첫 5판 net/들판 마지막 판 ×{sn/last:.2f} · 바다 전체 평균/들판 마지막 ×{sa/last:.2f} · 바다 첫 판 표시 {sea[0]['d']}")
print('\n== 보스 60~100 (표시): 시드별 도전 수 / 진 판 남은 체력 / 보스 판 DPS÷maxHp(첫 도전 처치 판 중앙)')
for bd in (60,70,80,90,100):
    tr=[];lf=[];dp=[]
    for rows in seeds.values():
        rs=[r for r in rows if r['d']==bd and r['boss']]; tr.append(len(rs)); lf+=[round(float(r['bossLeft']),2) for r in rs if r['bossRes']=='실패']
        dp+=[float(r['grossDps'])/float(r['bossMaxHp']) for r in rs if float(r['bossMaxHp'])>0]
    print(f"보스 {bd}: 도전 {tr} · 진 판 남은 체력 {lf} · DPS/max 중앙 {g(med(dp))} (최소 {g(min(dp))})")
print('\n== 트리 완료 판 / 무한 첫 구매 / inf 레벨')
for sd,rows in seeds.items():
    last=0;first=None
    for r in rows:
        if [p for p in r['bought'].split(';') if p and not p.startswith('inf_')]: last=r['d']
        if first is None and 'inf_' in r['bought']: first=r['d']
    print(f"s{sd}: 트리 완료 표시 {last} · 무한 첫 구매 {first}")
for d in (60,70,80,90,100):
    print(f"inf {d}:",' '.join(f"{[r for r in rows if r['d']<=d][-1]['ia']}/{[r for r in rows if r['d']<=d][-1]['ig']}" for rows in seeds.values()))
print('\n== 도달 분')
for d in (60,80,100):
    o=[round(float([r for r in rows if r['d']<d][-1]['playMin']),1) for rows in seeds.values()]
    print(f"표시 {d}:",o,'중앙',round(med(o),1))
print('\n== 막힘: 같은 보스 5번 초과 실패 / 0구매 10판 연속 / 판 수입 직전 일반판 대비 <0.5 (51~100)')
for sd,rows in seeds.items():
    fails={};run=0;mx=0;mxd=0;drops=[]
    pn=None
    for r in rows:
        if r['boss'] and r['bossRes']=='실패': fails[r['d']]=fails.get(r['d'],0)+1
        if 51<=r['d']<=100 and normal(r) and float(r['made'])>0:
            run=run+1 if r['allb']==0 else 0
            if run>mx: mx=run;mxd=r['d']
            nn=float(r['net'])
            if pn and nn<0.5*pn: drops.append((r['d'],round(nn/pn,2),r['zone']))
            pn=nn
    print(f"s{sd}: 보스 실패 {fails or '없음'} · 최장 0구매 연속 {mx}(끝 {mxd}) · 수입 급락 {drops[:6]}")
print('\n== 판 수입 배율 51~100(일반 첫 판→끝): ',end='')
for sd,rows in seeds.items():
    x=[float(r['net']) for r in rows if 51<=r['d']<=100 and normal(r) and float(r['made'])>0]
    print(f"s{sd} ×{x[-1]/x[0]:.1f}",end=' ')
print()
