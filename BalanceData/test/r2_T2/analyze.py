import json,sys,statistics as st
def load(p): return json.load(open(p))
def kill50(d):
    for i,(s,b) in enumerate(zip(d['stageR'],d['bossR'])):
        if b==1 and s==50: return i+1, d['playR'][i]/60
    return None,None
def med(x): return st.median(x) if x else float('nan')
def stats(prefix, rng=(45,50)):
    out=[]
    for sd in (1,2,3):
        d=load(f'{prefix}_s{sd}.json'); k=kill50(d)
        sel=lambda lo,hi:[d['incR'][i] for i in range(d['rounds']) if lo<=d['stageR'][i]<=hi and d['bossR'][i] in (0,2)]
        out.append(dict(seed=sd,k50=k,done=d['doneR'],defeat=sum(1 for b in d['bossR'] if b==-1),
          defeat80=sum(1 for i,b in enumerate(d['bossR']) if b==-1 and d['stageR'][i]>=80),
          maxstage=max(d['stageR']),rounds=d['rounds'],
          inc={s:med([d['incR'][i] for i in range(d['rounds']) if d['stageR'][i]==s and d['bossR'][i] in (0,2)]) for s in range(45,51)},
          n={s:sum(1 for i in range(d['rounds']) if d['stageR'][i]==s and d['bossR'][i] in (0,2)) for s in range(45,51)},
          atk={s:med([d['atkR'][i] for i in range(d['rounds']) if d['stageR'][i]==s]) for s in range(45,51)},
          inc4550=med(sel(45,50)),inc_all=med([x for x,b in zip(d['incR'],d['bossR']) if b in (0,2)])))
    return out
if __name__=='__main__':
    for p in sys.argv[1:]:
        print('==',p)
        for o in stats(p): print(o)
