# 7-2: 종별 첫 수확 판(state codex.first, 내부 판) 6회차 vs 7회차 — 희귀 종 해금 사슬이 늦어졌는가
import json,re,statistics as S
A='BalanceData/r6/test/T6-3/'; B='BalanceData/r7/test/T7-3/'
src=open('Assets/Scripts/Core/Defs.cs',encoding='utf-8').read()
sp=[]
for m in re.finditer(r'new\[\] \{ "(\w+)", "([^"]+)", "(\d)", "[^"]*", "[^"]*", "[^"]*", "[^"]*", "([^"]*)"',src):
    sp.append((m.group(1),m.group(2),int(m.group(3)),m.group(4)))
# id 규칙: 분류 + 분류 안 순번
cnt={};ids=[]
for c,n,t,o in sp:
    i=cnt.get(c,0); cnt[c]=i+1; ids.append((c+str(i),n,t,o))
res=[]
for id_,n,t,o in ids:
    if t<2: continue
    fa=[];fb=[]
    for sd in range(1,7):
        a=json.load(open(f'{A}state_k06_s{sd}.json'))['codex'].get(id_); b=json.load(open(f'{B}state_k06_s{sd}.json'))['codex'].get(id_)
        fa.append(a['first']-4 if a else None); fb.append(b['first']-4 if b else None)
    ma=S.median([x for x in fa if x is not None]) if any(x is not None for x in fa) else None
    mb=S.median([x for x in fb if x is not None]) if any(x is not None for x in fb) else None
    res.append((id_,n,t,o,ma,mb))
    print(f'{id_} {n} t{t} [{o}] 첫 수확 표시 스테이지 중앙 6회차 {ma} → 7회차 {mb}')
json.dump(res,open('BalanceData/r7/theory/r72/unlock_out.json','w'),ensure_ascii=False,indent=1)
