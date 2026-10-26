import csv,collections,statistics as st
f=lambda r,k:float(r[k] or 0)
OLD={'41~50':2.26e11,'51~60':1.30e12,'61~70':2.78e12,'71~80':5.65e13,'81~90':1.73e14,'91~100':2.38e14}
for s in (1,2,3):
    R=list(csv.DictReader(open(f'rounds_k06_s{s}.csv',encoding='utf-8')))
    print(f'== seed {s} rows {len(R)}')
    first={}
    for r in R:
        d=int(r['stage'])-2
        for m in range(10,101,10):
            if d>=m and m not in first: first[m]=(int(r['i']),round(f(r,'playMin'),1))
    print('first reach:',first)
    L=R[-1]
    print('last: i',L['i'],'disp',int(L['stage'])-2,'playMin',round(f(L,'playMin'),1),'atk',L['atk'],'balls',L['balls'],'gold',L['gold'],'net',L['net'])
    fail=collections.Counter()
    for r in R:
        if r['boss'] and f(r,'bossLeft')>0: fail[int(r['stage'])-2]+=1
    print('boss fails:',dict(fail))
    print('rounds with buy',sum(1 for r in R if r['bought']),'avg buys',round(sum(f(r,'buys') for r in R)/len(R),3))
    inf=[(r['i'],int(r['stage'])-2,r['bought']) for r in R if 'inf_' in r['bought']]
    print('inf_ buys',len(inf),'first',inf[:1])
    for k in ('md_grand','ed_legend','ps_tools'):
        print(k,[(r['i'],int(r['stage'])-2,[b for b in r['bought'].split(';') if b.startswith(k)]) for r in R if k in r['bought']][:6])
    print('last buy round',max((int(r['i']) for r in R if f(r,'buys')>0),default=0),'buys last200',sum(f(r,'buys') for r in R[-200:]))
    b=collections.defaultdict(list)
    for r in R:
        d=int(r['stage'])-2
        if d>10: b[(d-1)//10*10+1].append(f(r,'net'))
    for lo in sorted(b):
        k=f'{lo}~{lo+9}'; print('net median',k,f'{st.median(b[lo]):.3g}','n',len(b[lo]),'old',OLD.get(k))
    seen=set()
    for r in R:
        d=int(r['stage'])-2
        if r['boss'] and d in range(10,101,10) and d not in seen:
            seen.add(d); print('first boss try disp',d,'i',r['i'],r['boss'],'left',round(f(r,'bossLeft'),3),'fightT',round(f(r,'fightT'),2),'maxHp',r['bossMaxHp'])
