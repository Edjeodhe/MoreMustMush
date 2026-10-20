# 기준선 근사: round3_t3/main 로그(3회차 R3 가격·관문 모드, 10-16)에서 스테이지별 일반 판 수입 중앙값
import re, json, statistics, sys, os
ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'round3_t3', 'main')
UNIT = {'K':1e3,'M':1e6,'B':1e9,'T':1e12,'Qa':1e15,'Qi':1e18}
def num(s):
    s=s.replace(',','').strip()
    m=re.match(r'^([\d.]+)([A-Za-z]*)$',s)
    return float(m.group(1))*UNIT.get(m.group(2),1)
def parse(path):
    rows=[]; boss_pending=None
    for line in open(path,encoding='utf-8'):
        if line.strip().startswith('보스 R'):
            boss_pending = '처치' in line and 'clear' or 'fail'; continue
        m=re.match(r'^R(\d+) \| (\d+)분 \| ([^|]+)\| (\d+)s \| (\d+) \| ([^|]+)\| ([^|]+)\|',line)
        if not m: continue
        rows.append(dict(stage=int(m.group(1)),min=int(m.group(2)),zone=m.group(3).strip(),sec=int(m.group(4)),balls=int(m.group(5)),atk=num(m.group(6)),inc=num(m.group(7)),boss=boss_pending))
        boss_pending=None
    return rows
out={}
for s in (1,2,3):
    rows=parse(os.path.join(ROOT,f'log_s{s}.log'))
    by={}
    for i,r in enumerate(rows):
        r['round']=i+1
        if r['boss'] is None: by.setdefault(r['stage'],[]).append(r)
    out[s]={st:dict(med=statistics.median([x['inc'] for x in v]),n=len(v),round=v[0]['round'],min=v[0]['min'],balls=v[0]['balls'],atk=v[0]['atk'],sec=v[0]['sec']) for st,v in sorted(by.items())}
    out[s]['_boss']=[(r['stage'],r['boss'],r['round'],r['min']) for r in rows if r['boss']]
json.dump(out,open(os.path.join(os.path.dirname(__file__),'base_r3t3.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=1)
stages=[1,5,9,10,11,14,15,16,19,21,25,29,30,31,35,39,40,41,45,49,50,51,55,59,60,61,65,70,75,80,85,90,95,100,105,110]
print('stage | '+' | '.join(f's{s} med(n) R min balls atk' for s in (1,2,3)))
for st in stages:
    cells=[]
    for s in (1,2,3):
        v=out[s].get(st)
        cells.append('-' if not v else f"{v['med']:.3g}({v['n']}) R{v['round']} {v['min']}m b{v['balls']} a{v['atk']:.3g}")
    print(st,'|',' | '.join(cells))
for s in (1,2,3):
    from collections import Counter
    b=out[s]['_boss']; c=Counter((x[0],x[1]) for x in b)
    print('seed',s,'boss tries:',{st:(c[(st,'fail')]+c[(st,'clear')]) for st in sorted(set(x[0] for x in b))}, 'clear min', {x[0]:x[3] for x in b if x[1]=='clear'})
