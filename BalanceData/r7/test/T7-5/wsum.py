# 자연 등급 비중으로 가중한 거대 처치율. 비중(verify73_out.txt §4 "바로 읽기"/theory) 표시 21·23. 입력: gx_sum 출력 값(등급별 처치율)을 CSV에서 직접 읽는다
import csv
D='BalanceData/r7/test/T7-5/'
W={21:{'verify':[.453,.348,.148,.052],'theory':[.421,.343,.163,.072]},23:{'verify':[.236,.271,.232,.260],'theory':[.231,.254,.242,.272]}}
def rate(tag):
    L=open(D+tag+'_rounds.csv',encoding='utf-8').read().splitlines()
    H=L[0].split(',')+['gTier','gHp','gSpawnT','gKillT','gSp']
    rows=list(csv.DictReader(L[1:],fieldnames=H)); out=[]
    for t in range(4):
        x=[r for r in rows if int(r['gTier'])==t]; out.append(sum(float(r['gKillT'])>=0 for r in x)/len(x))
    return out
for tag,st in (('F21new',21),('F21old',21),('F21zd',21),('F23new',23),('F23old',23)):
    p=rate(tag)
    print(tag,'등급별',[f'{v:.3f}' for v in p],' 가중 전체:',{k:round(sum(a*b for a,b in zip(p,w)),3) for k,w in W[st].items()})
