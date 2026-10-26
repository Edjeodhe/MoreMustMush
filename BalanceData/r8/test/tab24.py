# T8-0 표: 내부 판 1~24 시드 중앙값. python -X utf8 BalanceData/r8/test/tab24.py BalanceData/r8/test/T8-0/
import csv,glob,sys,statistics as S
d=sys.argv[1]; seeds={}
for f in sorted(glob.glob(d+'rounds_s*.csv')):
    r=list(csv.DictReader(open(f,encoding='utf-8'))); seeds[int(r[0]['seed'])]=r
print('내부판(표시) | net 중앙 [최소~최대] | 구매수 중앙 | 수확기 | 군락 시작 | 군락 최대 | 수확 | 누적분 중앙 | 도전행수')
for st in range(1,25):
    x=[r for rs in seeds.values() for r in rs if int(r['stage'])==st]
    f=lambda k:[float(r[k]) for r in x]
    n=f('net')
    print(f"{st}({st-4}) | {S.median(n):.4g} [{min(n):.3g}~{max(n):.3g}] | {S.median(f('buys')):.1f} | {S.median(f('balls')):.0f} | {S.median(f('colsStart')):.0f} | {S.median(f('colsMax')):.0f} | {S.median(f('harvests')):.0f} | {S.median(f('playMin')):.1f} | {len(x)}")
