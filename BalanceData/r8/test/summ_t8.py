# T8 요약: rounds_s*.csv -> 표. 사용: python -X utf8 BalanceData/r8/test/summ_t8.py <폴더/>
import csv, glob, sys, statistics as S
d = sys.argv[1]
OFF = 4
seeds = {}
for f in sorted(glob.glob(d + 'rounds_s*.csv')):
    rows = list(csv.DictReader(open(f, encoding='utf-8')))
    seeds[int(rows[0]['seed'])] = rows
def F(x): return f'{x:.3g}'
print(open(d + 'cond.txt', encoding='utf-8').read().strip())
print('\n## 판별 (내부 판 1~24, 시드별): net | 구매수 | 수확기 | 군락 시작/최대 | 수확/만든 | 보유골드 | 공격력 | 분')
for st in range(1, 25):
    print(f'-- 내부 {st} (표시 {st-OFF})')
    for sd, rows in seeds.items():
        rs = [r for r in rows if int(r['stage']) == st]
        for r in rs:
            tag = ' 보스' + r['bossRes'] + f" 남은{float(r['bossLeft']):.0%}" if r['boss'] else ''
            print(f"  s{sd} 판{r['i']}: net {F(float(r['net']))} | +{r['buys']} [{r['bought'][:60]}] | 핀볼 {r['balls']} | 군락 {r['colsStart']}/{r['colsMax']} | {r['harvests']}/{r['made']} | 골드 {F(float(r['gold']))} | atk {F(float(r['atk']))} | {float(r['playMin']):.1f}분{tag}")
print('\n## 보스 (내부 판 14/24/34/44/54): 시드별 도전 수·처치 여부·처치 분·남은 체력 열')
for b in (14, 24, 34, 44, 54):
    for sd, rows in seeds.items():
        rs = [r for r in rows if int(r['stage']) == b and r['boss']]
        if not rs: print(f'  R{b} s{sd}: 도달 못함'); continue
        print(f"  R{b} s{sd}: 도전 {len(rs)} 처치 {any(r['bossRes']=='처치' for r in rs)} 끝 {float(rs[-1]['playMin']):.1f}분 남은체력 {[round(float(r['bossLeft']),2) for r in rs]}")
print('\n## 구간 요약 (일반 판, 시드 평균의 중앙): 판수 | net | 수확기 | 군락 시작/최대 | 수확')
for name, lo, hi in [('1~4', 1, 4), ('5~13', 5, 13), ('15~23', 15, 23), ('25~33', 25, 33), ('35~43', 35, 43), ('45~53', 45, 53)]:
    v = []
    for sd, rows in seeds.items():
        x = [r for r in rows if lo <= int(r['stage']) <= hi and not r['boss'] and r['bossFlag'] == '0']
        if x: v.append((len(x), S.mean(float(r['net']) for r in x), S.mean(int(r['balls']) for r in x), S.mean(int(r['colsStart']) for r in x), S.mean(int(r['colsMax']) for r in x), S.mean(int(r['harvests']) for r in x)))
    if v: print(f'  내부 {name}: ' + ' | '.join(f'{F(S.median(c))}' for c in zip(*v)))
print('\n## 끝: 시드별 마지막 판·누적 분')
for sd, rows in seeds.items():
    r = rows[-1]; print(f"  s{sd}: 판 {len(rows)} 마지막 R{r['stage']} {float(r['playMin']):.1f}분 보스결과 {r['bossRes']}")
