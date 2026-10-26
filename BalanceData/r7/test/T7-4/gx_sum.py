import csv,collections,statistics as S
import sys
for tag in (sys.argv[1:] or ('GX_m25','GX_m29','GX_end')):
    H=open(f'BalanceData/r7/test/T7-4/{tag}_rounds.csv',encoding='utf-8').readline().strip().split(',')+['gTier','gHp','gSpawnT','gKillT','gSp']
    rows=list(csv.DictReader(open(f'BalanceData/r7/test/T7-4/{tag}_rounds.csv',encoding='utf-8').read().splitlines()[1:],fieldnames=H))
    print('==',tag,'rows',len(rows),'stage',sorted(set(r['stage'] for r in rows)))
    for w in ('clear','rain'):
        v=[r for r in rows if r['weather']==w]
        have=[r for r in v if int(r['gTier'])>=0]
        tc=collections.Counter(int(r['gTier']) for r in have)
        print(f' {w}: 판 {len(v)} 거대 나온 판 {len(have)} 등급분포 {[tc[t] for t in range(4)]}')
        for t in range(4):
            x=[r for r in have if int(r['gTier'])==t]
            if x:
                k=[float(r['gKillT'])>=0 for r in x]
                print(f'   등급{t}: n={len(x)} hp {sorted(set(r["gHp"] for r in x))[:3]} 처치 {sum(k)}/{len(x)} 처치까지 평균 {S.mean(float(r["gKillT"])-float(r["gSpawnT"]) for r in x if float(r["gKillT"])>=0) if any(k) else None}')
        miss=[r for r in have if float(r['gKillT'])<0]
        print('   못 잡은 판',len(miss),'/',len(have),' 거대 없음',len(v)-len(have),' 대체(giantFallback 합)',sum(int(r['giantFallback']) for r in v),'/',sum(int(r['giantChecked']) for r in v))
