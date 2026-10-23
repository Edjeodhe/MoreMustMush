# T8-2 vs T8-1. python -X utf8 BalanceData/r8/test/t82_sum.py > BalanceData/r8/test/T8-2/t82_sum_out.txt
import csv,glob,statistics as S
B='BalanceData/r8/test/'
def load(d):
    return {int(r[0]['seed']):r for r in (list(csv.DictReader(open(f,encoding='utf-8'))) for f in sorted(glob.glob(B+d+'rounds_s*.csv')))}
def med(x): return S.median(x) if x else float('nan')
g=lambda x:f'{x:.4g}'
for sk,d1,d2 in (('0.6','T8-1/','T8-2/'),('1.0','T8-1/skill10/','T8-2/skill10/')):
    a=load(d1);b=load(d2);print('=== SKILL',sk)
    for name,lo,hi in [('1~4',1,4),('5~13',5,13),('15~23',15,23),('25~33',25,33),('35~48',35,48),('35~43',35,43),('45~53',45,53)]:
        o=[]
        for sd in (a,b):
            v=[];z=[];n=0
            for rows in sd.values():
                x=[r for r in rows if lo<=int(r['stage'])<=hi and not r['boss'] and r['bossFlag']=='0']
                v.append((S.mean(float(r['net']) for r in x),S.mean(int(r['buys']) for r in x)))
                n+=sum(1 for r in x if int(r['buys'])==0); z.append(len(x))
            o.append((med([c[0] for c in v]),med([c[1] for c in v]),n/sum(z)))
        print(f"내부 {name}: net T1 {g(o[0][0])} → T2 {g(o[1][0])} (x{o[1][0]/o[0][0]:.2f}) | 구매/판 {o[0][1]:.2f}→{o[1][1]:.2f} | 0구매 판 {o[0][2]:.0%}→{o[1][2]:.0%}")
    for nm,sd in (('T8-1',a),('T8-2',b)):
        for bs in (34,44,54):
            tr=[];lf=[]
            for rows in sd.values():
                rs=[r for r in rows if int(r['stage'])==bs and r['boss']]; tr.append(len(rs)); lf+=[round(float(r['bossLeft']),2) for r in rs if r['bossRes']=='실패']
            print(f"{nm} 보스 {bs} 도전 {tr} 진 판 남은 체력 {lf}")
        e=[float(r[-1]['playMin']) for r in sd.values()]; print(nm,'표시 50 처치 분',[round(x,1) for x in e],'중앙',round(med(e),1),'판수',[len(r) for r in sd.values()])
    print('판1 net (T8-1/T8-2) 중앙',g(med([float(r[0]['net']) for r in a.values()])),g(med([float(r[0]['net']) for r in b.values()])),' 판1 <14 시드',sum(float(r[0]['net'])<14 for r in a.values()),sum(float(r[0]['net'])<14 for r in b.values()))
    print('판 13 net 중앙',g(med([float([x for x in r if int(x['stage'])==13][0]['net']) for r in a.values()])),g(med([float([x for x in r if int(x['stage'])==13][0]['net']) for r in b.values()])))
print('\n=== M·DPS/maxHp (SKILL 1.0, homing 0, T8-2 m.csv; lag0=봇 자신, lag8=사람 근사)')
rows=list(csv.DictReader(open(B+'T8-2/m.csv',encoding='utf-8')))
for bs in (34,44,54):
    for lag in (0,4,8):
        x=[r for r in rows if int(r['boss'])==bs and int(r['lag'])==lag and r['homing']=='0' and r['hpScale']=='1']
        y=[r for r in rows if int(r['boss'])==bs and int(r['lag'])==lag and r['homing']=='0' and r['hpScale']=='50']
        h=[r for r in rows if int(r['boss'])==bs and int(r['lag'])==lag and r['homing']=='4' and r['hpScale']=='50']
        mh=S.mean(float(r['maxHp']) for r in x)
        print(f"보스 {bs} lag{lag}: maxHp {g(mh)} 총DPS(지금 체력) {g(S.mean(float(r['grossDps']) for r in x))} DPS/maxHp {S.mean(float(r['grossDps']) for r in x)/mh:.2f}/s · 처치율 {sum(int(r['cleared']) for r in x)}/{len(x)} 남은체력 중앙 {med([float(r['leftPct']) for r in x]):.2f} 처치시간 중앙 {med([float(r['fightT']) for r in x if int(r['cleared'])]):.1f}s | DPS(체력x50) {g(S.mean(float(r['grossDps']) for r in y))} → 유도4 M x{S.mean(float(r['grossDps']) for r in h)/S.mean(float(r['grossDps']) for r in y):.2f}")
