# T1a 요약: phase json 판 단위 배열(incR=net·stageR·bossR·playR·atkR·gateR) + 로그의 보스 줄(관문 예상/판 시간)
# 실행: PYTHONIOENCODING=utf-8 python 밸런스설계/t1a_parse.py BalanceData/round2_t1a/phase_s1_k0.6.json BalanceData/round2_t1a/t1a_s1_k0.6.log
import json,sys,re,statistics as st
d=json.load(open(sys.argv[1],encoding='utf-8'))
logl=[re.search(r'보스 R(\d+) (\S+(?: \S+)*): (처치|실패) · 관문 예상 ([\d.]+)s / 판 (\d+)s',l) for l in open(sys.argv[2],encoding='utf-8')]
logl=[m for m in logl if m]
n=len(d['incR']); print('판 수',n,'마지막 스테이지',d['stageR'][-1],'누적 플레이(분)',round(d['playR'][-1]/60,1))
med=lambda a: st.median(a) if a else float('nan')
print('| 보스 스테이지 | 보스 | 도전 | 패배 | 첫 도전 처치 | 첫 도전→처치 관문 예상/판 시간(초) | 첫 도전 시각(분) | 처치 시각(분) | 처치 때 공격력 | 이전 9판 net 중앙값 | 처치 판 net | 처치 판 net ÷ 이전 9판 |')
print('|---|---|---|---|---|---|---|---|---|---|---|---|')
for s in sorted(set(d['stageR'])):
    idx=[i for i in range(n) if d['stageR'][i]==s]
    att=[i for i in idx if d['bossR'][i] in (1,-1)]
    if not att: continue
    clr=[i for i in att if d['bossR'][i]==1]
    ls=[m for m in logl if int(m[1])==s]
    prev=[d['incR'][i] for i in range(n) if s-9<=d['stageR'][i]<s and d['bossR'][i]==0]
    c=d['incR'][clr[0]] if clr else float('nan')
    gp=f"{ls[0][4]}/{ls[0][5]} → {ls[-1][4]}/{ls[-1][5]}" if ls else ''
    print(f"| {s} | {ls[0][2] if ls else ''} | {len(att)} | {len(att)-len(clr)} | {'예' if d['bossR'][att[0]]==1 else '아니오'} | {gp} | {d['playR'][att[0]]/60:.0f} | {(d['playR'][clr[0]]/60 if clr else float('nan')):.0f} | {d['atkR'][clr[0]] if clr else float('nan'):.0f} | {med(prev):.3g} | {c:.3g} | {c/med(prev):.2f} |")
