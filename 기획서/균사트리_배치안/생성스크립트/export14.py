import json, math, random, collections, sys
exec(open('nodes.py').read().replace("for b in ('ed','md','ps'):","for b in ():"))
# 9차: 6갈래. 각 영역 첫 노드에서 바로 좌우 두 갈래로 갈라져 반쪽 영역(60°)씩 채운다
CEN={'ed':-90,'md':30,'ps':150}
UTIL={'노장의 체력','탈피','할아버지 손은 약손'}
FIRST={'ed':('효율적 사냥','Lv.1~3'),'md':('스프링','Lv.1'),'ps':('날 갈기','Lv.1')}
SUB={  # 갈래 → 줄 목록 (-1: 중심축 왼쪽(반시계), +1: 오른쪽)
 ('ed',-1):('식용 1 · 획득률',['효율적 사냥','흥정 기술','단골 손님','재빠른 손길','섬세한 손길','신들린 손길','은둔 고수','플렉스']),
 ('ed', 1):('식용 2 · 스테이지',['넓은 시야','하늘의 시야','신의 눈','희귀한 버섯 소문','굉장한 버섯 전설','고속 범람','더 많은 버섯','일확천금의 신화']),
 ('md',-1):('약용 1 · 슬라이드 바 버프',['스프링','관통','유도','할아버지 손은 약손','분열','노인의 지혜','피버까지 피버']),
 ('md', 1):('약용 2 · 스킬·환경·저항',['포자 폭발','이독제독','연속 번개','회오리','환경 복원','탈피']),
 ('ps',-1):('독성 1 · 공격·치명',['날 갈기','치명 공격','치명 박살','회전 칼날','회전 회오리']),
 ('ps', 1):('독성 2 · 바·이동·지속',['한가득 포옹','분신술','거울 분신술','노장의 체력','초토화','바람의 길','콤보 계수']),
}
DIRS=[(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1),(0,-1),(1,-1)]
def ang(v): return math.degrees(math.atan2(v[1],v[0]))
def off(a,c): return ((a-c+180)%360)-180
RMAX=float(sys.argv[1]) if len(sys.argv)>1 else 9.2
EDGE=58.5; AXGAP=4.0
def region(br,sd,p):
    if p==(0,0) or math.hypot(*p)>RMAX: return False
    o=off(ang(p),CEN[br])
    return (AXGAP<o<=EDGE) if sd>0 else (-EDGE<=o<-AXGAP)
def weight(br,d): return 2 if abs(off(ang(d),CEN[br]))<=22.6 else 1
def crosses(p,q,edges):
    if abs(p[0]-q[0])==1 and abs(p[1]-q[1])==1:
        a=(p[0],q[1]); b=(q[0],p[1]); return (a,b) in edges or (b,a) in edges
    return False
def build(br,rnd,alpha,forkb,late):
    c=CEN[br]
    first=min(DIRS,key=lambda d:abs(off(ang(d),c)))
    used={first:None}; inc={first:first}; depth={first:1}; turn={first:0}; nk=collections.Counter(); sub={first:0}
    edges=set(); blocked=set()
    gpt=max([p for p in [(round(math.cos(math.radians(c))*k),round(math.sin(math.radians(c))*k)) for k in range(1,30)] if math.hypot(*p)<=RMAX],key=lambda p:math.hypot(*p))
    need={sd:sum(1 for x in nodes if x['br']==br and x['line'] in SUB[(br,sd)][1])-(1 if FIRST[br][0] in SUB[(br,sd)][1] else 0) for sd in (-1,1)}
    have={-1:0,1:0}
    def cand_from(q,sd):
        out=[]
        i=DIRS.index(inc[q])
        for t in (-1,1,-2,2):
            d=DIRS[(i+t)%8]; sg=1 if t>0 else -1
            for m in (1,2):
                p=(q[0]+d[0]*m,q[1]+d[1]*m)
                if p in used or p in blocked or p==gpt or not region(br,sd,p): continue
                if math.hypot(*p)<=math.hypot(*q)+0.15: continue
                if m==2:
                    mid=(q[0]+d[0],q[1]+d[1])
                    if mid in used or mid in blocked or mid==gpt: continue
                elif crosses(p,q,edges): continue
                out.append((p,d,m,sg,t))
        return out
    # 첫 노드에서 좌우 두 갈래 시작
    for sd in (-1,1):
        best=None
        for d in DIRS:
            p=(first[0]+d[0],first[1]+d[1])
            if region(br,sd,p) and math.hypot(*p)>math.hypot(*first) and not crosses(p,first,edges):
                k=abs(off(ang(p),c+sd*30))
                if best is None or k<best[0]: best=(k,p,d)
        if not best: return None
        _,p,d=best; used[p]=first; inc[p]=d; depth[p]=2; turn[p]=sd; nk[first]+=1; sub[p]=sd; have[sd]+=1; edges.add((first,p))
    while any(have[sd]<need[sd] for sd in (-1,1)):
        best=None
        for q in list(used):
            sd=sub[q]
            if sd==0 or have[sd]>=need[sd]: continue
            maxk=3 if depth[q]%2==0 else 1
            if nk[q]>=maxk: continue
            for p,d,m,sg,t in cand_from(q,sd):
                near=[(p[0]+dx,p[1]+dy) for dx in (-1,0,1) for dy in (-1,0,1) if (dx,dy)!=(0,0)]
                crowd=sum(1 for r_ in near if r_ in used and r_!=q)
                s_=math.hypot(*p) + alpha*crowd + rnd.random()*1.2
                # 갈래 가운데 방향(c±30) 근처를 고르게 덮도록: 각도 쏠림 줄이기
                if abs(t)==2: s_+=1.5
                if m==2: s_+=1.0
                if turn[q]!=0 and sg==turn[q]: s_+=2.0
                if nk[q]>=1: s_-= forkb if nk[q]==1 else forkb*0.5   # 짝수 번째 노드는 2~3갈래로
                if best is None or s_<best[0]: best=(s_,p,q,d,m,sg,sd)
        if best is None: return None
        _,p,q,d,m,sg,sd=best
        used[p]=q; inc[p]=d; depth[p]=depth[q]+1; turn[p]=sg; nk[q]+=1; sub[p]=sd; have[sd]+=1
        if m==1: edges.add((q,p))
        else: blocked.add((q[0]+d[0],q[1]+d[1]))
    # 균사석: 중심축 끝 칸을 가장 가까운 노드에 잇기
    opts=[]
    for d in DIRS:
        for m in (1,2):
            q=(gpt[0]-d[0]*m,gpt[1]-d[1]*m)
            if q in used and nk[q]<2 and (m==1 and not crosses(gpt,q,edges) or m==2 and (gpt[0]-d[0],gpt[1]-d[1]) not in used and (gpt[0]-d[0],gpt[1]-d[1]) not in blocked):
                opts.append((m,-depth[q],q,d))
    if not opts: return None
    m,_,q,d=min(opts)
    used[gpt]=q; inc[gpt]=d; depth[gpt]=depth[q]+1; nk[q]+=1; sub[gpt]=sub[q]
    cr=math.radians(c); ux,uy=math.cos(cr),math.sin(cr)
    def geo(p):
        ax=p[0]*ux+p[1]*uy; lat=abs(-p[0]*uy+p[1]*ux); return 2*ax+lat
    g0=geo(first)
    rar={p:1+round(geo(p)-g0) for p in used}
    return used,rar,gpt,first,sub
def assign(br,used,rar,g,first,sub,rnd,fixed=None):
    fixed=fixed or {}
    ns=[x for x in nodes if x['br']==br]
    gi=[i for i,x in enumerate(ns) if x.get('gem')][0]
    fi=[i for i,x in enumerate(ns) if (x['line'],x['lv'])==FIRST[br]][0]
    m={g:ns[gi], first:ns[fi]}; m.update(fixed); tot=0
    fixedn={id(v) for v in fixed.values()}
    for sd in (-1,1):
        lines=SUB[(br,sd)][1]
        rest=[i for i,x in enumerate(ns) if x['line'] in lines and i not in (gi,fi) and id(x) not in fixedn]
        order={}
        for i in rest: order.setdefault(ns[i]['line'],[]).append(i)
        ks=sorted([k for k in used if sub[k]==sd and k not in (g,first) and k not in fixed], key=lambda k:(rar[k], math.hypot(*k), rnd.random()))
        cr=math.radians(CEN[br]); ux,uy=math.cos(cr),math.sin(cr)
        stage=lambda k:(k[0]*ux+k[1]*uy)-(first[0]*ux+first[1]*uy)
        okU={k:stage(k)>=3-0.01 for k in ks}
        assert len(ks)==len(rest),(br,sd,len(ks),len(rest))
        best=None
        for t in range(800):
            key=sorted(rest, key=lambda i:(ns[i]['rar'], rnd.random()))
            # 칸을 레어도 순으로 돌며 노드를 채운다. 유틸은 세로 3단계 이후 칸에만
            pend=list(key); seq=[]
            for p_,k_ in enumerate(ks):
                al=[j for j,i in enumerate(pend) if okU[k_] or ns[i]['line'] not in UTIL][:3]
                j=rnd.choice(al) if al and rnd.random()<0.5 else (al[0] if al else 0)
                seq.append(pend.pop(j))
            key=seq
            slots={}
            for p_,i in enumerate(key): slots.setdefault(ns[i]['line'],[]).append(p_)
            arr=[None]*len(rest)
            for line,idx in order.items():
                for p_,i in zip(sorted(slots[line]), idx): arr[p_]=i
            mm={ks[p_]:ns[i] for p_,i in enumerate(arr)}; mm.update(m)
            bad=sum(1 for k,q in used.items() if q is not None and k in mm and q in mm and mm[q]['line']==mm[k]['line'])
            bad+=10*sum(1 for k_,o_ in mm.items() if o_['line'] in UTIL and k_ in okU and not okU[k_])
            pos_of={}
            for k_,o_ in mm.items(): pos_of.setdefault(o_['line'],[]).append((o_['lv'],k_))
            for line_,idx_ in order.items():
                ps_=[next(k_ for lv_,k_ in pos_of[line_] if lv_==ns[i]['lv']) for i in idx_]
                rs_=[math.hypot(*k_) for k_ in ps_]
                if rs_!=sorted(rs_): bad+=5      # 다음 레벨이 중심에서 더 가까우면 안 됨
            if best is None or bad<best[0]: best=(bad,mm)
        m.update(best[1])
    bad=sum(1 for k,q in used.items() if q is not None and m[q]['line']==m[k]['line'])
    cr=math.radians(CEN[br]); ux,uy=math.cos(cr),math.sin(cr)
    bad+=10*sum(1 for k,o in m.items() if o['line'] in UTIL and (k[0]*ux+k[1]*uy)-(first[0]*ux+first[1]*uy)<3-0.01)
    return bad,m
CHAIN=[('분신술','Lv.1'),('한가득 포옹','Lv.1'),('분신술','Lv.2'),('한가득 포옹','Lv.2'),('분신술','Lv.3'),('한가득 포옹','Lv.3'),('거울 분신술','')]
def find_chain(br,used,rar,g,first,sub,sd):
    ch={}
    for p,q in used.items():
        if q: ch.setdefault(q,[]).append(p)
    best=None
    def dfs(path):
        nonlocal best
        if len(path)==len(CHAIN):
            rs=[rar[p] for p in path]
            if rs!=sorted(rs) or len(set(rs))<len(rs): return
            key=abs(rs[0]-T)+(10 if rs[0]<T-1 else 0)
            if best is None or key<best[0]: best=(key,list(path))
            return
        for c in ch.get(path[-1],[]):
            if c not in (g,first) and sub[c]==sd: dfs(path+[c])
    pts=sorted([p for p in used if sub[p]==sd and p not in (g,first)],key=lambda p:rar[p])
    T=rar[pts[int(len(pts)*0.6)]]   # 분신 바는 레어도가 높음 → 바깥쪽에서 시작
    for p in pts: dfs([p])
    return (best[1],best[0]) if best else None
def score(br,used,sub):
    c=CEN[br]; s=0
    for sd in (-1,1):
        bins=set()
        for p in used:
            if sub.get(p)==sd and math.hypot(*p)>=RMAX-2.5:
                o=off(ang(p),c); bins.add(int(abs(o)//8))
        s-=len(bins)
    straight=sum(1 for p,q in used.items() if q and used.get(q) and (p[0]-q[0],p[1]-q[1])==(q[0]-used[q][0],q[1]-used[q][1]))
    # 경계(10·2·6시) 가까이 닿았는지
    edge=min(EDGE-abs(off(ang(p),c)) for p in used if p!=(0,0))
    return (s, straight, round(edge))
if __name__=='__main__':
    PAR={'ed':(0.6,2.5,0.8),'md':(1.3,2.5,1.6),'ps':(1.0,2.5,1.2)}
    J=[dict(nodes[0],id='root',x=0,y=0,parent=None,depth=0,sub='')]
    tot=0
    for br in CEN:
        cand=[]
        for s in range(600):
            res=build(br,random.Random(s),*PAR[br])
            if res: cand.append((score(br,res[0],res[4]),s,res))
        cand.sort(key=lambda z:z[0])
        print(br,'ok',len(cand),'best',cand[0][0] if cand else None,flush=True)
        if not cand: sys.exit(1)
        bestc=None
        for ci in range(min(600,len(cand))):
            used_,rar_,g_,first_,sub_=cand[ci][2]
            fixed={}
            if br=='ps':
                fc=find_chain(br,used_,rar_,g_,first_,sub_,1)
                if not fc: continue
                chain,ck=fc
                ns_=[x for x in nodes if x['br']=='ps']
                for p_,(ln,lv) in zip(chain,CHAIN): fixed[p_]=next(x for x in ns_ if x['line']==ln and x['lv']==lv)
            else: ck=0
            bad_,m_=assign(br,used_,rar_,g_,first_,sub_,random.Random(1),fixed)
            if bestc is None or (bad_,ck)<(bestc[0],bestc[4]): bestc=(bad_,m_,cand[ci][2],ci,ck)
            if bad_==0 and ck<=1: break
        bad,m,(used,rar,g,first,sub),ci,ck=bestc
        print(br,'chain key',ck)
        print(br,'layout rank',ci,'same-line',bad,flush=True); tot+=bad
        for p,q in used.items():
            sd=sub[p]
            J.append(dict(m[p],id=f'{br}:{p[0]},{p[1]}',x=p[0],y=p[1],parent=(f'{br}:{q[0]},{q[1]}' if q else 'root'),depth=rar[p],sub=SUB[(br,sd)][0] if sd else '시작 갈래'))
    kids={}
    for o in J:
        if o['parent']: kids.setdefault(o['parent'],[]).append(o['id'])
    subn={}
    def S(i): subn[i]=1+sum(S(c) for c in kids.get(i,[])); return subn[i]
    S('root')
    for o in J: o['sub_n']=subn[o['id']]
    for o in J: o['subsize']=subn[o['id']]
    lines={}
    for o in J: lines.setdefault((o['br'],o['line']),[]).append(o)
    viol=0
    for k,v in lines.items():
        ds=[next(x['depth'] for x in v if x['lv']==o['lv']) for o in nodes if (o['br'],o['line'])==k]
        if ds!=sorted(ds): viol+=1
    print('same-line',tot,'order viol',viol,'max rarity',max(o['depth'] for o in J),'nodes',len(J))
    occ={(o['x'],o['y']) for o in J}
    cells=[[x,y] for x in range(-12,13) for y in range(-12,13) if (x,y) not in occ and math.hypot(x,y)<=RMAX+0.3]
    for o in J: o['sub_']=o.pop('sub')
    json.dump(dict(nodes=J,R=RMAX,R0=1.0,D=(RMAX-1)/5,RINGS=6,cells=cells,SUBS={f'{k[0]}{k[1]}':v[0] for k,v in SUB.items()}),open('layout14.json','w'),ensure_ascii=False)
