# T7-4 분석: 6회차(T6-3) / 7-1(T7-3) / 7-2 적용 뒤(T7-4) 봇 T5-2 조건 비교. 프로젝트 루트에서 실행.
import json, math, re, statistics as S
A='BalanceData/r6/test/T6-3/'; B='BalanceData/r7/test/T7-3/'; C='BalanceData/r7/test/T7-4/'
OFF=4; SC=[1,5,20,100]
def ld(d,sd): return json.load(open(f'{d}phase_k06_s{sd}.json'))
def rows(d,sd):
    ph=ld(d,sd); out=[]; cum=0
    for i,r in enumerate(ph['stageR']):
        out.append(dict(i=i,R=r,c=r-OFF,boss=ph['bossR'][i],inc=ph['incR'][i],cum=cum,m=ph['madeT'][r-1][:4],k=ph['killT'][r-1][:4],
                        dur=ph['playR'][i]-(ph['playR'][i-1] if i>0 else 0),atk=ph['atkR'][i]))
        cum+=ph['buysR'][i]
    return out
D={lab:{sd:rows(d,sd) for sd in range(1,7)} for lab,d in (('6',A),('7',B),('4',C))}
gm=lambda v: math.exp(S.mean(math.log(max(x,1e-9)) for x in v))
V=lambda k: sum(k[t]*SC[t] for t in range(4))/max(1,sum(k))
IV=[('1~5',1,5),('6~13',6,13),('14~20',14,20),('21~30',21,30),('31~55',31,55)]
print('## 1. 구간 판 수입 (일반 판, 시드별 평균의 중앙) 6회차 / 7-1 / 7-2후')
for name,lo,hi in IV:
    res={}
    for lab in '674':
        mn=[];gmn=[];mk=[];du=[];n=0
        for sd in range(1,7):
            x=[y for y in D[lab][sd] if lo<=y['c']<=hi and y['boss']==0]
            mn.append(S.mean(y['inc'] for y in x)); gmn.append(gm([y['inc'] for y in x])); mk.append(S.mean(sum(y['m']) for y in x)); du.append(S.mean(y['dur'] for y in x)); n+=len(x)
        res[lab]=(S.median(mn),S.median(gmn),S.median(mk),S.median(du),n/6,mn)
    r6,r7,r4=res['6'],res['7'],res['4']
    print(f"{name} | 평균 6:{r6[0]:.4g} 7-1:{r7[0]:.4g} 7-2:{r4[0]:.4g} | 7-2/6 ×{r4[0]/r6[0]:.2f} (기하 ×{r4[1]/r6[1]:.2f}) | 7-2/7-1 ×{r4[0]/r7[0]:.2f} | 만든/판 {r6[2]:.0f}→{r4[2]:.0f} ×{r4[2]/r6[2]:.2f} | 판시간 {r6[3]:.1f}→{r4[3]:.1f} | 판수/시드 {r6[4]:.1f}/{r7[4]:.1f}/{r4[4]:.1f}")
    print('    시드별 평균 7-2/6:',[f'{a/b:.2f}' for a,b in zip(r4[5],r6[5])])
print('\n## 1b. 보스 10~50 도전 수·처치 분·표시 50 처치 분')
def boss_info(d,sd):
    log=open(f'{d}t51_k06_s{sd}.log',encoding='utf-8').read().splitlines(); ph=ld(d,sd); out={}
    for b in (10,20,30,40,50):
        rb=b+OFF; lines=[l for l in log if l.startswith(f'  보스 R{rb} ')]
        idx=[i for i,s in enumerate(ph['stageR']) if s==rb]
        if not idx: out[b]=(0,False,None); continue
        out[b]=(len(lines),any('처치' in l for l in lines),ph['playR'][idx[-1]]/60)
    return out
for lab,d in (('6',A),('7-1',B),('7-2',C)):
    bi=[boss_info(d,sd) for sd in range(1,7)]
    for b in (10,20,30,40,50):
        mins=[x[b][2] for x in bi if x[b][2]]
        print(lab,'보스',b,'도전',[x[b][0] for x in bi],'처치',[x[b][1] for x in bi],'분',[round(x[b][2],1) if x[b][2] else None for x in bi],'중앙 분',round(S.median(mins),1) if mins else None)
    print(lab,'산 판/총 판',[f"{ld(d,sd)['buyRounds']}/{ld(d,sd)['rounds']}" for sd in range(1,7)])
print('\n## 2. 수확·만든 등급 비중 (일반 판, 시드 합)')
for name,lo,hi in [('6~13',6,13),('14~20',14,20),('21~25',21,25),('26~30',26,30),('21~30',21,30),('31~40',31,40),('41~55',41,55)]:
    s=f'{name}:'
    for lab in '674':
        m=[0]*4;k=[0]*4
        for sd in range(1,7):
            for x in D[lab][sd]:
                if lo<=x['c']<=hi and x['boss']==0:
                    for t in range(4): m[t]+=x['m'][t]; k[t]+=x['k'][t]
        s+=f" | {lab}: 만든 {[round(v/sum(m),3) for v in m]} 수확 {[round(v/sum(k),3) for v in k]} 레전드리 {k[3]/sum(k):.3f} 유니크+ {(k[2]+k[3])/sum(k):.3f} 처치율 {[round(k[t]/max(1,m[t]),2) for t in range(4)]}"
    print(s)
print('\n## 3. 같은 노드 상태(누적 구매 로그 보간): 비교판=7-2후, 기준=6회차 / (참고) 기준=7-1')
def aligned(base,tgt):
    for name,lo,hi in [('14~20',14,20),('21~30',21,30),('31~55',31,55)]:
        ri=[];rv=[];rk=[];dc=[];pairs=[]
        for sd in range(1,7):
            b=[y for y in D[tgt][sd] if y['boss']==0]
            for x in D[base][sd]:
                if not(lo<=x['c']<=hi) or x['boss']: continue
                for y0,y1 in zip(b,b[1:]):
                    if y0['cum']<=x['cum']<=y1['cum'] and y1['cum']>y0['cum']:
                        w=(x['cum']-y0['cum'])/(y1['cum']-y0['cum'])
                        li=(1-w)*math.log(y0['inc'])+w*math.log(y1['inc'])
                        lv=(1-w)*math.log(V(y0['k']))+w*math.log(V(y1['k']))
                        lk=(1-w)*math.log(max(1,sum(y0['k'])))+w*math.log(max(1,sum(y1['k'])))
                        ri.append(math.exp(li)/x['inc']); rv.append(math.exp(lv)/V(x['k'])); rk.append(math.exp(lk)/max(1,sum(x['k'])))
                        dc.append((1-w)*y0['c']+w*y1['c']-x['c']); break
        print(f"  {name}: 짝 {len(ri)} | 수입 기하 ×{gm(ri):.2f} 중앙 ×{S.median(ri):.2f} | 수확 수 ×{gm(rk):.2f} | V ×{gm(rv):.2f} | 짝 표시 스테이지 차 {S.mean(dc):+.2f}")
print(' 기준 6회차 대 7-2후'); aligned('6','4'); print(' 기준 6회차 대 7-1'); aligned('6','7')
print('\n## 5a. 종별 첫 수확 표시 스테이지 (티어 2~3 종, 시드 중앙) 6 / 7-1 / 7-2후')
src=open('Assets/Scripts/Core/Defs.cs',encoding='utf-8').read()
sp=[]
for m in re.finditer(r'new\[\] \{ "(\w+)", "([^"]+)", "(\d)", "[^"]*", "[^"]*", "[^"]*", "[^"]*", "([^"]*)"',src): sp.append((m.group(1),m.group(2),int(m.group(3)),m.group(4)))
cnt={};ids=[]
for c,n,t,o in sp:
    i=cnt.get(c,0); cnt[c]=i+1; ids.append((c+str(i),n,t,o))
for id_,n,t,o in ids:
    if t<2: continue
    mm=[]
    for d in (A,B,C):
        fa=[]
        for sd in range(1,7):
            a=json.load(open(f'{d}state_k06_s{sd}.json'))['codex'].get(id_); fa.append(a['first']-OFF if a else None)
        v=[x for x in fa if x is not None]; mm.append((S.median(v) if v else None, len(v)))
    print(f'  {id_} {n} t{t} [{o}] {mm[0][0]}({mm[0][1]}) / {mm[1][0]}({mm[1][1]}) / {mm[2][0]}({mm[2][1]})')
