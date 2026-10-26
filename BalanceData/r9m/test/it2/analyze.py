import csv,sys,collections
for s in (1,2,3):
    R=list(csv.DictReader(open(f'rounds_k06_s{s}.csv',encoding='utf-8')))
    f=lambda r,k:float(r[k] or 0)
    print(f'== seed {s} rows {len(R)}')
    first={}
    for r in R:
        d=int(r['stage'])-2
        for m in range(10,101,10):
            if d>=m and m not in first: first[m]=(int(r['i']),round(f(r,'playMin'),1))
    print('first reach disp stage:',first)
    L=R[-1]
    print('last: i',L['i'],'dispstage',int(L['stage'])-2,'playMin',round(f(L,'playMin'),1),'atk',L['atk'],'balls',L['balls'],'gold',L['gold'],'net',L['net'])
    fail=collections.Counter(); 
    for r in R:
        if r['boss'] and f(r,'bossLeft')>0: fail[int(r['stage'])-2]+=1
    print('boss fails by disp stage:',dict(fail))
    nb=sum(1 for r in R if r['bought']); print('rounds with buy',nb,'of',len(R),'avg buys/round',round(sum(f(r,'buys') for r in R)/len(R),3))
    inf=[(r['i'],r['bought']) for r in R if 'inf_' in r['bought']]
    print('inf_ rows',len(inf),inf[:3],'first i',inf[0][0] if inf else None)
    # last 200 rounds buys
    print('buys last200',sum(f(r,'buys') for r in R[-200:]),'last buy round',max((int(r['i']) for r in R if f(r,'buys')>0),default=0))
    print('gold last rounds',[r['gold'] for r in R[-5:]])
    # progression every 100 rounds
    print([(r['i'],int(r['stage'])-2,round(f(r,'playMin')),r['atk'],r['balls'],r['gold']) for r in R[::150]])
