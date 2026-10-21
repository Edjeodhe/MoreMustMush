# 7-2: 구간별 수입 비를 (수확 수) × (등급 섞임 값) × (나머지: 노드·배율) 로 나눈다
import json,math,re,statistics as S,sys
sys.path.insert(0,'BalanceData/r7/theory/r72')
from align import rows,A,B
SC=[1,5,20,100,100,100,100,100]   # 등급 점수(TIERS.score), 4 이상은 쓰지 않음
IV=[('1~5',1,5),('6~13',6,13),('14~20',14,20),('21~30',21,30),('31~55',31,55)]
def gm(v): return math.exp(sum(math.log(max(x,1e-9)) for x in v)/len(v))
def maxtier(d,sd):
    log=open(f'{d}t51_k06_s{sd}.log',encoding='utf-8').read().splitlines()
    out={}
    for l in log:
        m=re.match(r'R(\d+) \|',l)
        if m:
            t=re.search(r'최고등급 (\d)',l); c=re.search(r'도감 (\d+)',l)
            out[int(m.group(1))]=(int(t.group(1)),int(c.group(1)))
    return out
res={}
print('구간 | 수입 기하비 | 수확 수 비 | 수확당 등급점수 비 | 나머지(점수 1당 골드) 비 | 판 최고등급 3 비율 6/7 | 레전드리 수확 비중 6/7 | 유니크 이상 비중 6/7')
for name,lo,hi in IV:
    acc={k:[] for k in ('inc','kill','v','rest','t3a','t3b','sh3a','sh3b','sh2a','sh2b')}
    for sd in range(1,7):
        a=[x for x in rows(A,sd) if lo<=x['disp']<=hi and x['boss']==0]
        b=[x for x in rows(B,sd) if lo<=x['disp']<=hi and x['boss']==0]
        ma=maxtier(A,sd); mb=maxtier(B,sd)
        for X,lab in ((a,'a'),(b,'b')):
            pass
        def agg(X,mt):
            inc=gm([x['inc'] for x in X]); kill=gm([max(1,x['kill']) for x in X])
            vs=[sum(k*SC[t] for t,k in enumerate(x['killT']))/max(1,x['kill']) for x in X]
            rest=gm([x['inc']/max(1,sum(k*SC[t] for t,k in enumerate(x['killT']))) for x in X])
            t3=sum(1 for x in X if mt.get(x['R'],(0,0))[0]>=3)/len(X)
            tot=sum(x['kill'] for x in X)
            sh3=sum(x['killT'][3] for x in X)/max(1,tot); sh2=sum(x['killT'][2]+x['killT'][3] for x in X)/max(1,tot)
            return inc,kill,gm(vs),rest,t3,sh3,sh2
        ra=agg(a,ma); rb=agg(b,mb)
        acc['inc'].append(rb[0]/ra[0]); acc['kill'].append(rb[1]/ra[1]); acc['v'].append(rb[2]/ra[2]); acc['rest'].append(rb[3]/ra[3])
        acc['t3a'].append(ra[4]); acc['t3b'].append(rb[4]); acc['sh3a'].append(ra[5]); acc['sh3b'].append(rb[5]); acc['sh2a'].append(ra[6]); acc['sh2b'].append(rb[6])
    m=lambda k: S.median(acc[k])
    res[name]={k:m(k) for k in acc}
    print(f"{name} | ×{m('inc'):.2f} | ×{m('kill'):.2f} | ×{m('v'):.2f} | ×{m('rest'):.2f} | {m('t3a'):.2f}/{m('t3b'):.2f} | {m('sh3a'):.2f}/{m('sh3b'):.2f} | {m('sh2a'):.2f}/{m('sh2b'):.2f}")
json.dump(res,open('BalanceData/r7/theory/r72/decomp_out.json','w'),ensure_ascii=False,indent=1)
# 최고등급 3이 처음 나온 판, 도감 수
print('시드 | 최고등급3 첫 판(내부R) 6/7 | 표시 21 판 시작 도감 6/7 | 표시 30 도감 6/7')
for sd in range(1,7):
    ma=maxtier(A,sd); mb=maxtier(B,sd)
    fa=min([r for r,v in ma.items() if v[0]>=3],default=None); fb=min([r for r,v in mb.items() if v[0]>=3],default=None)
    print(sd,'|',fa,'/',fb,'|',ma.get(25,(0,0))[1],'/',mb.get(25,(0,0))[1],'|',ma.get(34,(0,0))[1],'/',mb.get(34,(0,0))[1])
