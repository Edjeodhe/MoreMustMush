import json,glob,collections,math,statistics as S
d=[]
for f in sorted(glob.glob('alpha_s*.jsonl')): d+=[json.loads(l) for l in open(f)]
def alpha(pts):
    xs=[math.log(c) for c,_ in pts]; ys=[math.log(v) for _,v in pts]
    mx=S.mean(xs); my=S.mean(ys)
    return sum((x-mx)*(y-my) for x,y in zip(xs,ys))/sum((x-mx)**2 for x in xs)
def table(filt,label):
    print('##',label)
    for st in (3,5):
        pts=[];ptsH=[]
        print(f'stage {st} (dur {set(r["dur"] for r in d if r["stage"]==st)}) c | n | net mean | net median | harv | made | kill-total | actual cols')
        for c in (1,2,3,4,6,8,12):
            v=[r for r in d if r['stage']==st and r['c']==c and r['stump']==0 and filt(r)]
            if not v: continue
            m=S.mean(r['net'] for r in v); md=S.median(r['net'] for r in v)
            h=S.mean(r['harv'] for r in v); mk=S.mean(r['made'] for r in v)
            print(f'{c} | {len(v)} | {m:.1f} | {md:.1f} | {h:.2f} | {mk:.2f} | {S.mean(sum(r["kill"]) for r in v):.2f} | {S.mean(r["cols"] for r in v):.2f}')
            pts.append((c,m)); ptsH.append((c,h))
        for lo,hi in ((1,12),(1,3),(3,6),(6,12),(2,4),(4,12)):
            p=[x for x in pts if lo<=x[0]<=hi]; q=[x for x in ptsH if lo<=x[0]<=hi]
            if len(p)>=2: print(f'  alpha(net) c{lo}-{hi}: {alpha(p):.2f}  alpha(harv): {alpha(q):.2f}')
    # per seed
table(lambda r:True,'전체(날씨 무작위 그대로)')
table(lambda r:r['weather']=='clear','날씨 clear만')
table(lambda r:r['weather']!='rain','rain 제외(군락 수 그대로)')
print('날씨 분포',collections.Counter(r['weather'] for r in d))
print('## 시드별 net 평균 (전체) stage c: s1/s2/s3')
for st in (3,5):
    for c in (1,2,3,4,6,8,12):
        print(st,c,[round(S.mean(r['net'] for r in d if r['stage']==st and r['c']==c and r['stump']==0 and r['seed']==s),1) for s in (1,2,3)])
print('## stump c=3')
for st in (3,5):
    for sp in (0,1):
        for lab,f in (('전체',lambda r:True),('clear',lambda r:r['weather']=='clear'),('rain제외',lambda r:r['weather']!='rain')):
            v=[r for r in d if r['stage']==st and r['c']==3 and r['stump']==sp and f(r)]
            print(st,'stump',sp,lab,len(v),'net mean %.1f med %.1f harv %.2f'%(S.mean(r['net'] for r in v),S.median(r['net'] for r in v),S.mean(r['harv'] for r in v)))
    for s in (1,2,3):
        a=[r['net'] for r in d if r['stage']==st and r['c']==3 and r['stump']==0 and r['seed']==s]
        b=[r['net'] for r in d if r['stage']==st and r['c']==3 and r['stump']==1 and r['seed']==s]
        print(st,'seed',s,'stump 없음 %.1f / 있음 %.1f 비 %.2f'%(S.mean(a),S.mean(b),S.mean(b)/S.mean(a)))
