import csv,sys,statistics as S,collections
tag=sys.argv[1] if len(sys.argv)>1 else 'B'
rows=list(csv.DictReader(open(f'BalanceData/r7/test/{tag}_rounds.csv',encoding='utf-8')))
f=lambda r,k: float(r[k])
by=collections.defaultdict(list)
for r in rows: by[r['weather']].append(r)
print('rows',len(rows))
for w,v in by.items():
    print(f'== {w} n={len(v)} stage {sorted(set(r["stage"] for r in v))[:3]}')
    print(' 최대',S.mean(f(r,'max') for r in v),'판 시작',S.mean(f(r,'start') for r in v),'(최소',min(f(r,'start') for r in v),'최대',max(f(r,'start') for r in v),')','colS',S.mean(f(r,'colS') for r in v),'(최소',min(f(r,'colS') for r in v),')')
    print(' 판 중 평균 군락',S.mean(f(r,'avgCols') for r in v),'계획ms 평균',S.mean(f(r,'planMs') for r in v),'최대',max(f(r,'planMs') for r in v))
    print(' net 중앙',S.median(f(r,'net') for r in v),'평균',S.mean(f(r,'net') for r in v),'수확',S.mean(f(r,'harvests') for r in v),'만든',S.mean(f(r,'made') for r in v))
    print(' 재생 시도/판',S.mean(f(r,'attempts') for r in v),'실패/판',S.mean(f(r,'fails') for r in v),'탄생/판',S.mean(f(r,'births') for r in v))
    print(' 프레임ms 최대평균',S.mean(f(r,'frameMax') for r in v),'최악',max(f(r,'frameMax') for r in v),'p99평균',S.mean(f(r,'frameP99') for r in v),'시도프레임 평균',S.mean(f(r,'attFrameMean') for r in v if f(r,'attempts')>0),'시도프레임 최대',max(f(r,'attFrameMax') for r in v))
    print(' 재생지연 평균',S.mean(f(r,'delayMean') for r in v),'추가지연(−재생값)',S.mean(f(r,'extraDelayMean') for r in v))
    print(' 판 시간',S.mean(f(r,'dur') for r in v))
# 상태 시드별 net 중앙
for w,v in by.items():
    d=collections.defaultdict(list)
    for r in v: d[r['state']].append(f(r,'net'))
    print(w,'시드별 net 평균',{k:f'{S.mean(x):.3g}' for k,x in d.items()})
if len(by)>1:
    base=by['clear']
    for w in by:
        if w=='clear': continue
        d0=collections.defaultdict(list); d1=collections.defaultdict(list)
        for r in base: d0[(r['state'],r['k'])].append(f(r,'net'))
        for r in by[w]: d1[(r['state'],r['k'])].append(f(r,'net'))
        ratios=[d1[k][0]/d0[k][0] for k in d0 if k in d1]
        print(w,'/clear net 비 (같은 상태·시드 쌍) 중앙',S.median(ratios),'평균',S.mean(ratios), '합 비', sum(f(r,'net') for r in by[w])/sum(f(r,'net') for r in base))
