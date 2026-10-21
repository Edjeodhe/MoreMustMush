# T6-1 집계 (사람 수준 상태, 시드 1~6 중앙값). 출처 BalanceData/r6/test/T6-1/out.csv
import csv,statistics as S,collections
rows=list(csv.DictReader(open('BalanceData/r6/test/T6-1/out.csv')))
g=collections.defaultdict(list)
for r in rows: g[(int(r['stage']),r['durLv'],r['skills'],r['skill'],r['hpset'])].append(r)
print('표시 | ps_dur | 유도·폭발 | SKILL | 보스 | 판시간 | 공격력 | 체력(전→후) | 순 DPS 전 / 후 | 총 DPS 후 | 남은 체력% 전 / 후 (중앙) | 처치 전 / 후')
for st in (10,20,30,40,50):
  for d in '01':
    for sk in '01':
      for kk in ('0.6','1'):
        b=g[(st,d,sk,kk,'before')]; a=g[(st,d,sk,kk,'after')]
        f=lambda v,c:S.median([float(x[c]) for x in v])
        print(st,d,sk,kk,a[0]['boss'],'%.0fs'%f(a,'dur'),'%.1f'%f(a,'atk'),'%.3g→%.3g'%(f(b,'maxHp'),f(a,'maxHp')),'%.2f / %.2f'%(f(b,'netDmgPerS'),f(a,'netDmgPerS')),'%.2f'%f(a,'grossDmgPerS'),'%.0f / %.0f'%(f(b,'hpLeftPct'),f(a,'hpLeftPct')),'%d/6 / %d/6'%(sum(int(x['cleared']) for x in b),sum(int(x['cleared']) for x in a)),sep=' | ')
