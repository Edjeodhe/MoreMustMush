# T8-4 후반 집계. python -X utf8 BalanceData/r8/test/t84_sum.py > BalanceData/r8/test/T8-4/t84_sum_out.txt
import csv,glob,json,re,statistics as S
B='BalanceData/r8/test/T8-4/late/'; TUT=2
def med(x): return S.median(x) if x else float('nan')
g=lambda x:f'{x:.3g}'
seeds={}
for f in sorted(glob.glob(B+'rounds_s*.csv')):
    rows=list(csv.DictReader(open(f,encoding='utf-8'))); sd=int(rows[0]['seed'])
    lv={}
    for r in rows:
        d=0
        for part in filter(None,r['bought'].split(';')):
            k,t=part.split(':'); a,b=t.split('>'); d+=int(b)-int(a); lv[k]=int(b)
        r['d']=int(r['stage'])-TUT; r['allbuys']=d; r['inf_atk']=lv.get('inf_atk',0); r['inf_gold']=lv.get('inf_gold',0)
    ph=json.load(open(f.replace('rounds_s','phase_s').replace('.csv','.json')))
    for r,z in zip(rows,ph['zone']): r['zone']=z
    seeds[sd]=rows
print('시드',sorted(seeds),'판 수',[len(v) for v in seeds.values()],'마지막 표시',[v[-1]['d'] for v in seeds.values()],'처치',[v[-1]['bossRes'] for v in seeds.values()])
print('\n== 10단위 구간별 일반 판(보스 판 제외) — 시드 중앙(시드별 평균의 중앙)')
prev=None
for lo,hi in [(41,50),(51,60),(61,70),(71,80),(81,90),(91,100)]:
    nets=[];by=[];zz=0;n=0;ab=[]
    for rows in seeds.values():
        x=[r for r in rows if lo<=r['d']<=hi and not r['boss'] and r['bossFlag']=='0']
        if not x: continue
        nets.append(S.mean(float(r['net']) for r in x)); by.append(S.mean(int(r['buys']) for r in x)); ab.append(S.mean(r['allbuys'] for r in x))
        zz+=sum(1 for r in x if r['allbuys']==0); n+=len(x)
    m=med(nets); print(f"표시 {lo}~{hi}: net {g(m)}" + (f" (직전 구간 ×{m/prev:.2f})" if prev else '') + f" | 골드노드 구매/판 {med(by):.2f} · 무한 포함 {med(ab):.2f} | 0구매 판(무한 포함) {zz}/{n}={zz/n:.0%}"); prev=m
print('\n== 보스 도전(내부 R = 표시+2)')
for bd in (60,70,80,90,100):
    tr=[];lf=[]
    for rows in seeds.values():
        rs=[r for r in rows if r['d']==bd and r['boss']]; tr.append(len(rs)); lf+=[round(float(r['bossLeft']),2) for r in rs if r['bossRes']=='실패']
    print(f"보스 {bd}: 시드별 도전 수 {tr} · 진 판 남은 체력 {lf}")
print('\n== 보스 50 도전 수 참고(전 구간 연속):',[len([r for r in rows if r['d']==50 and r['boss']]) for rows in seeds.values()])
print('\n== 지역 전환(표시 스테이지, 봇이 고른 지역)')
for sd,rows in seeds.items():
    ch=[];p=None
    for r in rows:
        if r['zone']!=p: ch.append(f"{r['zone']}@{r['d']}"); p=r['zone']
    print(f"s{sd}",' '.join(ch))
print('\n== 골드 트리 완료 판(마지막 무한 아닌 구매 표시 스테이지) / 무한 노드 첫 구매 판')
for sd,rows in seeds.items():
    last=0;first=None
    for r in rows:
        ns=[p for p in r['bought'].split(';') if p and not p.startswith('inf_')]
        if ns: last=r['d']
        if first is None and re.search(r'inf_',r['bought']): first=r['d']
    print(f"s{sd}: 비무한 마지막 구매 표시 {last} · 무한 첫 구매 표시 {first}")
print('\n== inf_atk / inf_gold 레벨(표시 d 판 끝) 시드별')
for d in (60,70,80,90,100):
    o=[]
    for rows in seeds.values():
        x=[r for r in rows if r['d']<=d]; o.append(f"{x[-1]['inf_atk']}/{x[-1]['inf_gold']}")
    print(f"표시 {d}:",' '.join(o))
print('\n== 도달 분(표시 d에 처음 들어선 판 시작 시점 = 직전 판 playMin)')
for d in (60,80,100):
    o=[]
    for rows in seeds.values():
        x=[r for r in rows if r['d']<d]; o.append(round(float(x[-1]['playMin']),1))
    print(f"표시 {d}:",o,'중앙',round(med(o),1))
print('\n== 막힘: 같은 보스 5번 초과 실패 / 0구매 연속(무한 포함) 10판 초과 / 표시 51~100')
for sd,rows in seeds.items():
    fails={};run=0;mx=0;mxd=0
    for r in rows:
        if r['boss'] and r['bossRes']=='실패': fails[r['d']]=fails.get(r['d'],0)+1
        if 51<=r['d']<=100 and not r['boss'] and r['bossFlag']=='0':
            run=run+1 if r['allbuys']==0 else 0
            if run>mx: mx=run;mxd=r['d']
    print(f"s{sd}: 보스 실패 {fails or '없음'} · 최장 0구매 연속 {mx}판(끝 표시 {mxd})")
print('\n== 판 수입 배율: 인접 일반 판(51~100) net 비 중앙 / 판당 평균 ×')
for sd,rows in seeds.items():
    x=[float(r['net']) for r in rows if 51<=r['d']<=100 and not r['boss'] and r['bossFlag']=='0']
    print(f"s{sd}: 51판 net {g(x[0])} → 100 부근 {g(x[-1])} ×{x[-1]/x[0]:.1f} (판당 ×{(x[-1]/x[0])**(1/(len(x)-1)):.3f})")
