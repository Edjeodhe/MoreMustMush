import csv,statistics as S,collections,sys
d=sys.argv[1]
rows=list(csv.reader(open(d+'out.csv')))[1:]
hd="hpset,stage,lag,x,skill,seed,boss,weather,dur,atk,balls,maxHp,hpLeftPct,cleared,killT,fightT,netDmgPerS,grossDmgPerS,heal".split(',')
rows=[dict(zip(hd,r)) for r in rows]
g=collections.defaultdict(list)
for r in rows: g[(int(r['stage']),int(r['lag']),r['hpset'])].append(r)
for k in sorted(g,key=lambda k:(k[0],k[1],k[2]!='before')):
    v=g[k]; f=lambda c:S.median([float(x[c]) for x in v])
    kt=[float(x['killT']) for x in v if x['cleared']=='1']
    print(k,v[0]['boss'],'n',len(v),'atk %.1f'%f('atk'),'dur %.0f'%f('dur'),'hp %.3g'%f('maxHp'),'net %.1f'%f('netDmgPerS'),'gross %.1f'%f('grossDmgPerS'),'left med %.0f%%'%f('hpLeftPct'),'kill %d/%d'%(sum(int(x['cleared']) for x in v),len(v)),'killT med %.1f'%(S.median(kt) if kt else -1))
