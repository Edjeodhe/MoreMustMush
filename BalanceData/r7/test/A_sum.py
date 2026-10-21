import csv,collections,statistics as S
rows=list(csv.DictReader(open('BalanceData/r7/test/A_grid.csv',encoding='utf-8')))
d=collections.OrderedDict()
for r in rows: d.setdefault((r['map'],r['cols'],r['ii'],r['cond']),[]).append(r)
for k,v in d.items():
    print(k,'max',v[0]['max'],'놓임',S.mean(int(x['placed']) for x in v),'colS',round(S.mean(float(x['colS']) for x in v),2),'ms max',max(float(x['ms']) for x in v),'zoom',v[0]['zoom'])
print('전부 놓임',all(r['max']==r['placed'] for r in rows), len(rows)); print('ms 최대',max(float(r['ms']) for r in rows))
