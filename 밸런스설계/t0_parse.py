# T0 기준선 요약: BalanceData/round2_t0/t0_s{seed}_k{skill}.log -> 표 (스테이지 10 보스)
# 실행: PYTHONIOENCODING=utf-8 python 밸런스설계/t0_parse.py > BalanceData/round2_t0/summary.txt
import re,glob,os,sys
def num(s):
    s=s.replace(',','');m=1
    if s.endswith('K'): m=1e3;s=s[:-1]
    elif s.endswith('M'): m=1e6;s=s[:-1]
    return float(s)*m
print('| 시드 | 봇 오차 | 총 판 | 스테이지 1~9 | 스테이지 10 판 | 보스 도전 | 패배 | 파밍 판 | 보스 남은 체력(도전 순) | 첫 도전 시각(분) | 처치 시각(분) | 처치 때 공격력·핀볼 | 직전 일반 판 골드 | 처치 판 골드(보상 제외) |')
print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
for p in sorted(glob.glob(sys.argv[1]+'/t0_s*_k*.log' if len(sys.argv)>1 else 'BalanceData/round2_t0/t0_s*_k*.log')):
    seed,sk=re.search(r't0_s(\d+)_k([\d]+\.[\d]+)\.log',p).groups()
    rows=[];pend=None
    for l in open(p,encoding='utf-8').read().splitlines():
        m=re.match(r'\s+보스 R(\d+) (\S+): (처치|실패)(?: \(남은 체력 (\d+) ?%\))?',l)
        if m: pend=(m[3],m[4]); continue
        m=re.match(r'R(\d+) \| (\d+)분 \| (\S+) \| (\d+)s \| (\d+) \| ([\d.,KMB]+) \| ([\d.,KMB]+)',l)
        if m: rows.append(dict(st=int(m[1]),min=int(m[2]),balls=m[5],atk=m[6],gold=m[7],boss=pend)); pend=None
    boss=[r for r in rows if r['boss']]; fails=[r for r in boss if r['boss'][0]=='실패']
    r10=[r for r in rows if r['st']==10]
    left=' '.join(r['boss'][1]+'%' if r['boss'][0]=='실패' else '처치' for r in boss)
    normal=[r for r in r10 if not r['boss']]
    print(f"| {seed} | {sk} | {len(rows)} | 9 | {len(r10)} | {len(boss)} | {len(fails)} | {len(normal)} | {left} | {rows[8]['min']} (R9 끝) | {rows[-1]['min']} | {rows[-1]['atk']}·{rows[-1]['balls']} | {normal[-1]['gold'] if normal else '-'} | {rows[-1]['gold']} |")
