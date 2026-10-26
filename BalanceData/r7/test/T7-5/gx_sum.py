# T7-5 거대 강제 집계. 사용: python -X utf8 BalanceData/r7/test/T7-5/gx_sum.py TAG [TAG...]
# 등급별(gTier) 처치율, 판 net 합, 처치까지 시간, 시드별 처치율. 거대 hp 열 gHp.
import csv,sys,collections,statistics as S
D='BalanceData/r7/test/T7-5/'
HDR=None
def load(path):
    L=open(path,encoding='utf-8').read().splitlines()
    H=L[0].split(',')+['gTier','gHp','gSpawnT','gKillT','gSp']
    return list(csv.DictReader(L[1:],fieldnames=H))
for tag in sys.argv[1:]:
    p=D+tag+'_rounds.csv'
    try: rows=load(p)
    except FileNotFoundError: rows=load('BalanceData/r7/test/T7-4/'+tag+'_rounds.csv')
    have=[r for r in rows if int(r['gTier'])>=0]
    print('==',tag,'판',len(rows),'거대 나온',len(have),'stage',sorted(set(r['stage'] for r in rows)),'dur',sorted(set(r['dur'] for r in rows)))
    print(' net 합 %.4e'%sum(float(r['net']) for r in rows),' 수확 평균 %.0f'%S.mean(float(r['harvests']) for r in rows))
    tot=0;kk=0
    for t in range(4):
        x=[r for r in have if int(r['gTier'])==t]
        if not x: continue
        k=[float(r['gKillT'])>=0 for r in x]; tot+=len(x); kk+=sum(k)
        kt=[float(r['gKillT'])-float(r['gSpawnT']) for r in x if float(r['gKillT'])>=0]
        per=[]
        for sd in range(1,7):
            y=[r for r in x if int(r['state'])==sd]
            per.append(f"{sum(float(r['gKillT'])>=0 for r in y)}/{len(y)}")
        hp=sorted(set(r['gHp'] for r in x))
        print(f'  등급{t}: n={len(x)} hp {hp[:2]} 처치 {sum(k)}/{len(x)} = {sum(k)/len(x):.3f}  처치시간 평균 {S.mean(kt) if kt else float("nan"):.1f}  시드별 {per}')
    print(f'  전체(자연 섞임 그대로) {kk}/{tot} = {kk/max(1,tot):.3f}')
