import json,re,sys,statistics as S
D='BalanceData/r5/test/T5-0/'
out=[]
for sd in range(1,7):
    log=open(D+f't50_s{sd}.log',encoding='utf-8').read().splitlines()
    ph=json.load(open(D+f'phase_s{sd}.json')); st=json.load(open(D+f'state_s{sd}.json'))
    inc,stg,play,atk,br=ph['incR'],ph['stageR'],ph['playR'],ph['atkR'],ph['bossR']
    out.append(f'--- 시드 {sd}: {len(inc)}판, 누적 {play[-1]/60:.1f}분, 마지막 스테이지 {stg[-1]}, 산 판 {ph["buyRounds"]}/{ph["rounds"]}')
    print(out[-1])
    print('조건:',log[0])
    # 보스: 도전 = 로그의 보스 줄
    for b in (10,20,30,40,50):
        lines=[l for l in log if l.startswith(f'  보스 R{b} ')]
        idx=[i for i,s in enumerate(stg) if s==b]
        first=idx[0]+1 if idx else None; last=idx[-1]+1 if idx else None
        kill=[l for l in lines if '처치' in l]
        print(f' 보스{b}: 도착 판 {first} 공격력 {atk[idx[0]] if idx else None} 도전 {len(lines)} 처치 판 {last if kill else None} 누적분 {play[last-1]/60:.1f}' if idx else f' 보스{b}: 미도달')
    for s in (15,30,45,54,55):
        v=[inc[i] for i,x in enumerate(stg) if x==s and br[i]==0]
        print(f' 스테이지{s} 일반 판 net: {v}')
    # 스테이지 15·30·45 대신 구간 평균
    pb=[ (i+1) for i in range(1,len(log)) ]
    nodes=st['nodes']; print(' 산 노드 수(레벨>0):',sum(1 for v in nodes.values() if v>0),'/',len(nodes),'레벨합',sum(nodes.values()),'ps_ball Lv',nodes.get('ps_ball'),'ps_ball2',nodes.get('ps_ball2'))
    # 핀볼 수 변화 판
    rows=[l.split(' | ') for l in log if re.match(r'R\d+ \|',l)]
    prev=None;ch=[]
    for k,r in enumerate(rows):
        n=int(r[4])
        if n!=prev: ch.append((k+1,r[0],n)); prev=n
    print(' 핀볼 수 변화(판,스테이지,수):',ch)
