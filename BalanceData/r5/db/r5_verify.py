import json,subprocess,sys
new=json.load(open('Assets/Resources/DB/db.json',encoding='utf-8'))
old=json.loads(subprocess.check_output(['git','show','HEAD:Assets/Resources/DB/db.json']).decode('utf-8'))
exp=json.load(open('BalanceData/r5/db/r5_set.json',encoding='utf-8'))
rows=json.load(open('BalanceData/r5/db/r5_rows.json',encoding='utf-8'))
KEY={'Boss':'Boss_Key','Upgrade':'Upg_Key','Upgrade_Cost':'Cost_ID','Ball':'Ball_Key','String':'Str_ID'}
def idx(d,s): return {str(r[KEY[s]]):r for r in d[s]}
bad=0
print("== set 대조 (시트.키.열: 이전 -> 지금, 기대) ==")
for s,rs in exp.items():
    n,o=idx(new,s),idx(old,s)
    for k,cells in rs.items():
        for c,v in cells.items():
            got=n[k][c]; ok = got==v if isinstance(v,str) else abs(got-v)<=1e-9*max(1,abs(v))
            bad+=not ok
            if s!='String': print(f"{'OK ' if ok else 'BAD'} {s}.{k}.{c}: {o[k][c]} -> {got} (기대 {v})")
            else: print(f"{'OK ' if ok else 'BAD'} String.{k}.{c}: {str(got)[:60]}")
print("== rows 대조 ==")
for s,rl in rows.items():
    n=idx(new,s) if s!='Upgrade_Cost' else {str(r['Cost_ID']):r for r in new[s]}
    for r in rl:
        k=str(r[KEY[s]]) if s!='Upgrade' else r['Upg_Key']
        if s=='Upgrade': n=idx(new,s)
        row=n.get(k)
        ok=row is not None and all((row[c]==v if isinstance(v,str) else abs(row[c]-v)<1e-9) for c,v in r.items())
        bad+=not ok; print('OK ' if ok else 'BAD',s,k)
# 의도 밖 변경
print("== 의도 밖 변경 검사 ==")
allowed=set()
for s,rs in exp.items():
    for k,cells in rs.items():
        for c in cells: allowed.add((s,k,c))
extra=0
for s in new:
    if s=='_source': print('_source:',old[s],'->',new[s]); continue
    if s not in old: print('새 시트',s); extra+=1; continue
    ok_=KEY.get(s)
    if ok_:
        o,n=idx(old,s),idx(new,s)
        added=[k for k in n if k not in o]; removed=[k for k in o if k not in n]
        print(f"{s}: 행 {len(o)} -> {len(n)} (추가 {added}, 삭제 {removed})")
        for k in o:
            if k in n:
                for c in o[k]:
                    if o[k][c]!=n[k].get(c) and (s,k,c) not in allowed: print('  의도 밖',s,k,c,o[k][c],n[k].get(c)); extra+=1
    else:
        print(s,'행',len(old[s]),len(new[s]),'동일' if old[s]==new[s] else '다름'); extra+= old[s]!=new[s]
print("BAD:",bad,"의도 밖:",extra)
