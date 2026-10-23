# lag 8 사람 근사 집계. python -X utf8 BalanceData/r9/test/t90_lag.py > BalanceData/r9/test/T9-0/sum_lag.txt
import csv,glob,statistics as S
for tag in ('k06','k10'):
    print('==',tag)
    rows=[]
    for f in sorted(glob.glob(f'BalanceData/r9/test/T9-0/lag_{tag}_s*.csv')): rows+=list(csv.DictReader(open(f)))
    for b in (60,70,80,90,100):
        x=[r for r in rows if r['boss']==str(b)]
        cl=sum(int(r['cleared']) for r in x); d=[float(r['dpsOverMax']) for r in x]; lf=[float(r['leftPct']) for r in x if r['cleared']=='0']; ft=[float(r['fightT']) for r in x]
        print(f"보스 {b} {x[0]['bossId']}: n={len(x)} 처치 {cl}/{len(x)} · DPS/maxHp 중앙 {S.median(d):.3f} 최소 {min(d):.3f} · 전투시간 중앙 {S.median(ft):.1f}s · 진 판 남은체력 {[round(v,2) for v in lf]}")
