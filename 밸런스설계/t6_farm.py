# T6-2 집계: 표시 20 막힘 파밍 (lag 8 스냅샷에서 14판). 출처 BalanceData/r6/test/T6-2/{before,after}_s*.log
import re,statistics as S
D='BalanceData/r6/test/T6-2/'
def parse(p):
    L=open(p,encoding='utf-8').read().splitlines(); atks=[];att=[];cur=None
    for l in L:
        m=re.match(r'R24 \| (\d+)분 \| \S+ \| (\d+)s \| (\d+) \| ([\d.,KMGBT]+) \|',l)
        if m:
            atks.append(m.group(4)); continue
        m=re.match(r'  보스 R24 .*?: (처치|실패(?: \(남은 체력 (\d+) ?%\))?)',l)
        if m: att.append((len(atks), 0 if m.group(1)=='처치' else int(m.group(2))))
    return atks,att
for hp in ('before','after'):
    print('====',hp)
    for sd in range(1,7):
        a,att=parse(D+f'{hp}_s{sd}.log')
        print(f'시드{sd}: 판{len(a)} 공격력 처음→끝 {a[0]}→{a[-1]} | 도전(판 번호, 남은 체력%) {att}')
