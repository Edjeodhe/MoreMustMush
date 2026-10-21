# 한 스킬 = 한 노드 (그 자리에서 Lv.1부터 끝까지 레벨업). 효과 등급은 Lv.1 기준
exec(open('nodes.py').read().replace("for b in ('ed','md','ps'):","for b in ():"))
_by={}
for br,name,tag,lv,rar in L:
    _by[(br,name)]=dict(br=br,line=name,name=name,tag=tag,lv=('단일' if len(lv)==1 and lv[0][0]==0 else f'Lv.1~{lv[-1][0]}'),
        inplace=len(lv)>1,rar=rar[0],gem=False,eff=[(f'Lv.{a} ' if a else '')+e for a,e in lv])
nodes=[nodes[0]]+list(_by.values())+[n for n in nodes if n.get('gem')]
