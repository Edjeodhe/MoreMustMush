# T8-3b 보스 24 cleaner. python -X utf8 BalanceData/r8/test/t83b_sum.py > BalanceData/r8/test/T8-3b/t83b_sum_out.txt
import csv,statistics as S
B='BalanceData/r8/test/'
rows=list(csv.DictReader(open(B+'T8-3b/c24b.csv',encoding='utf-8')))
print('state | cleaner Hp_Mul(환산) | SKILL | n | maxHp | 총DPS | DPS/maxHp | 처치율 | 처치시간 중앙 | 진 판 남은 체력 중앙(실패 판)')
for st in ('h24','h24+atk2','bot lag0','bot lag4','bot lag8'):
    for hs,lab in (('0.487805','0.20'),('1','0.41')):
        for sk in ('0.6','1'):
            x=[r for r in rows if r['state']==st and r['skill']==sk and abs(float(r['hpScale'])-float(hs))<1e-3]
            if not x: continue
            mh=S.mean(float(r['maxHp']) for r in x); d=S.mean(float(r['grossDps']) for r in x)
            ok=[r for r in x if r['cleared']=='1']; lo=[float(r['leftPct']) for r in x if r['cleared']=='0']
            ft=S.median([float(r['fightT']) for r in ok]) if ok else float('nan')
            print(f"{st} | {lab} | {sk} | {len(x)} | {mh:.4g} | {d:.4g} | {d/mh:.3f}/s | {len(ok)}/{len(x)} | {ft:.1f}s | {S.median(lo) if lo else '-'}")
