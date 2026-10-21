import json,re,sys,statistics as S
D='BalanceData/r7/test/T7-3/'; D0='BalanceData/r5/test/T5-0/'
OFF=4
def load(pref,tag,sd):
    d=D if pref=='t51' else D0
    log=open(d+f'{pref}_{tag}.log' if pref=='t51' else d+f't50_s{sd}.log',encoding='utf-8').read().splitlines()
    ph=json.load(open(d+(f'phase_{tag}.json' if pref=='t51' else f'phase_s{sd}.json')))
    st=json.load(open(d+(f'state_{tag}.json' if pref=='t51' else f'state_s{sd}.json')))
    return log,ph,st
def run(pref,tag,sd,off):
    log,ph,st=load(pref,tag,sd)
    inc,stg,play,atk,br=ph['incR'],ph['stageR'],ph['playR'],ph['atkR'],ph['bossR']
    r={'n':len(inc),'min':play[-1]/60,'buy':f"{ph['buyRounds']}/{ph['rounds']}",'cond':log[0]}
    for b in (10,20,30,40,50):
        rb=b+off
        lines=[l for l in log if l.startswith(f'  보스 R{rb} ')]
        idx=[i for i,s in enumerate(stg) if s==rb]
        if not idx: r[f'b{b}']=None; continue
        kill=any('처치' in l for l in lines); last=idx[-1]
        r[f'b{b}']=dict(arr=idx[0]+1,atk=atk[idx[0]],ch=len(lines),kill=last+1 if kill else None,min=play[last]/60)
    for s in (15,29,31,45,55):
        v=[inc[i] for i,x in enumerate(stg) if x==s+off and br[i]==0]
        r[f'n{s}']=v[0] if v else None
    rows=[l.split(' | ') for l in log if re.match(r'R\d+ \|',l)]
    prev=None;ch=[]
    for r_ in rows:
        n=int(r_[4])
        if n!=prev: ch.append((int(r_[0][1:]),int(r_[0][1:])-off,n)); prev=n
    r['ball']=ch
    r['mark']=[(re.match(r'R(\d+)',l).group(1),l.split('←')[1].strip()) for l in log if '←' in l]
    r['last']=rows[-1][-3] if rows else ''
    r['nodes']=(sum(1 for v in st['nodes'].values() if v>0),sum(st['nodes'].values()))
    return r
def med(v):
    v=[x for x in v if x is not None]; return S.median(v) if v else None
if __name__=='__main__':
    sets=[('t51','k06',range(1,7),OFF)]
    for pref,tag,sds,off in sets:
        print('=====',pref,tag)
        res=[]
        for sd in sds:
            r=run(pref,f'{tag}_s{sd}',sd,off); res.append(r)
            print(f'--- 시드{sd}: {r["n"]}판 {r["min"]:.1f}분 산 판 {r["buy"]} 노드 {r["nodes"]}'); print(' ',r['cond'])
            for b in (10,20,30,40,50): print(f'  보스{b}:',r[f'b{b}'])
            print('  net',{k:r[k] for k in ('n15','n29','n31','n45','n55')})
            print('  핀볼(내부R,표시,수):',r['ball']); print('  단계표시:',r['mark'],'마지막 열:',r['last'])
        for k in ('n15','n29','n31','n45','n55'): print('중앙값',k,med([x[k] for x in res]))
        for b in (10,20,30,40,50):
            print(f'보스{b} 중앙값: 도착판 {med([x[f"b{b}"]["arr"] for x in res])} 공격력 {med([x[f"b{b}"]["atk"] for x in res])} 도전 {med([x[f"b{b}"]["ch"] for x in res])} 처치판 {med([x[f"b{b}"]["kill"] for x in res])} 분 {med([x[f"b{b}"]["min"] for x in res]):.1f}')
