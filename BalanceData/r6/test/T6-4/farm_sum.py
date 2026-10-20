# T6-4 집계: 표시 40 막힘 파밍 (lag 8 스냅샷 14판). 출처 T6-4/farm_p{720,500}_s*.log
import re,sys
D='BalanceData/r6/test/T6-4/'
for hp in ('farm_p720','farm_p500'):
    print('====',hp); firsts=[]
    for sd in range(1,7):
        L=open(D+f'{hp}_s{sd}.log',encoding='utf-8').read().splitlines()
        att=[];atks=[]
        for l in L:
            m=re.match(r'  보스 R44 .*?: (처치|실패(?: \(남은 체력 (\d+) ?%\))?)',l)
            if m:
                att.append('처치' if m.group(1)=='처치' else int(m.group(2)))
                if m.group(1)=='처치': break
            m=re.match(r'R\d+ \| \d+분 \| [^|]+\| \d+s \| (\d+) \|',l)
            if m: atks.append(m.group(1))
        k=next((i+1 for i,v in enumerate(att) if v=='처치'),None)
        print(f'시드{sd}: 도전 {len(att)}번 {"처치 "+str(k)+"번째" if k else "미처치"} | 남은체력% {att} | 공격력 {atks[0] if atks else "?"}→{atks[-1] if atks else "?"}')
