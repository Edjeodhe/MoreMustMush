import csv,collections,statistics as S,math
T='BalanceData/r7/test/T7-4/'
def load(tag):
    L=open(T+tag+'_rounds.csv',encoding='utf-8').read().splitlines()
    H=L[0].split(',')+['gTier','gHp','gSpawnT','gKillT','gSp']
    out=[]
    for l in L[1:]:
        r=dict(zip(H,l.split(',')))
        for k in ('net','harvests','dur','gHp','gSpawnT','gKillT'): r[k]=float(r[k])
        r['gTier']=int(r['gTier']); r['tag']=tag
        r['kt']=r['gKillT']-r['gSpawnT'] if r['gKillT']>=0 else None
        out.append(r)
    return out
if __name__=='__main__':
  for tag in ('GX_m25','GXnew_m25','GXold_m25','GX_m29','GX_end'):
      rows=load(tag)
      print('==',tag)
      for s in '123456':
          v=[r for r in rows if r['state']==s]
          line=[]
          for t in range(4):
              x=[r for r in v if r['gTier']==t]
              k=[r['kt'] for r in x if r['kt'] is not None]
              line.append(f"t{t} {len(k)}/{len(x)} {('%.1f'%S.median(k)) if k else '-'}")
          print(' s'+s, ' | '.join(line), ' net med %.3g'%S.median(r['net'] for r in v))
