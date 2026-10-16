# 3회차 T3-0 집계: BalanceData/round3_t0/phase_s*.json, states_s*.jsonl (+ 2회차 round2_sweep/m15_s*.json)
import json,sys,statistics as st
sys.stdout.reconfigure(encoding='utf-8')
R='BalanceData/'
def load(p): return json.load(open(p))
def stage_rows(ph,maxst=15):
    rows={}
    for i,s in enumerate(ph['stageR']):
        if s>maxst: break
        rows.setdefault(s,[]).append(i)
    return rows
def fmt(x): return f"{x:.3g}"
for tag,path in (('3회차 T3-0','round3_t0/phase_s%d.json'),('2회차 m15','round2_sweep/m15_s%d.json')):
    print('##',tag)
    print('시드|스테이지|판 수|판 수입 net(중앙/합)|스테이지 끝 누적분|보스결과|그 스테이지 산 레벨 합')
    for sd in (1,2,3):
        ph=load(R+path%sd); rows=stage_rows(ph)
        for s,ix in rows.items():
            inc=[ph['incR'][i] for i in ix]
            print(f"{sd}|{s}|{len(ix)}|{fmt(st.median(inc))} / {fmt(sum(inc))}|{ph['playR'][ix[-1]]/60:.1f}|{[ph['bossR'][i] for i in ix]}|{sum(ph['buysR'][i] for i in ix)}")
        i7=ph['stageR'].index(7)
        print(f"  시드{sd} 스테이지 7 도착: {i7+1}번째 판, 도착 전 누적 {ph['playR'][i7-1]/60:.1f}분 ({ph['playR'][i7-1]:.0f}초)")
        ix15=max(i for i,s in enumerate(ph['stageR']) if s<=15)
        print(f"  시드{sd} 스테이지 15 끝: {ix15+1}판째 {ph['playR'][ix15]/60:.1f}분")
if True:
    print('## 3회차 산 노드 순서 (판 번호: 새로 오른 노드 +레벨)')
    for sd in (1,2,3):
        ph=load(R+'round3_t0/phase_s%d.json'%sd)
        prev={}
        print('시드',sd)
        for n,line in enumerate(open(R+'round3_t0/states_s%d.jsonl'%sd,encoding='utf-8').read().splitlines()):
            if not line.strip(): continue
            cur=json.loads(line)['nodes']
            d=[f"{k}{'+%d'%(v-prev.get(k,0)) if v-prev.get(k,0)>1 else ''}" for k,v in cur.items() if v>prev.get(k,0)]
            if n<len(ph['stageR']): print(f"  R판{n+1}(스테이지{ph['stageR'][n]}): {', '.join(d)}")
            prev=cur
