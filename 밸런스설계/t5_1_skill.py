import json
D='BalanceData/r5/test/T5-1/'
for k in ('10','15'):
    for sd in (1,2,3):
        log=open(D+f't51sk_k{k}_s{sd}.log',encoding='utf-8').read().splitlines()
        ph=json.load(open(D+f'phasesk_k{k}_s{sd}.json'))
        stg,atk,inc,br,play=ph['stageR'],ph['atkR'],ph['incR'],ph['bossR'],ph['playR']
        out=[]
        for b in (10,20,30):
            rb=b+4; ls=[l for l in log if l.startswith(f'  보스 R{rb} ')]; idx=[i for i,s in enumerate(stg) if s==rb]
            out.append(f'보스{b}: 도전 {len(ls)} 도착공격력 {atk[idx[0]]:.1f} 처치판 {idx[-1]+1} {play[idx[-1]]/60:.1f}분' if idx else f'보스{b} 미도달')
        n15=[inc[i] for i,x in enumerate(stg) if x==19 and br[i]==0]
        print(f'SKILL {k} 시드{sd}', ' | '.join(out), '| net15',n15)
