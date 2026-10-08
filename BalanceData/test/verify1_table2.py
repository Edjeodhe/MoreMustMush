# -*- coding: utf-8 -*-
"""verify#1 (T3 새 가격표) 표. 사용: python BalanceData/test/verify1_table2.py name=경로 ...  (경로 = balance_phase_s*.json)"""
import json, sys
sys.stdout.reconfigure(encoding="utf-8")
def f(x):
    for u,s in ((1e12,"T"),(1e9,"B"),(1e6,"M"),(1e3,"K")):
        if x>=u: return f"{x/u:.3g}{s}"
    return f"{x:.0f}"
for a in sys.argv[1:]:
    name,path=a.split("=",1)
    p=json.load(open(path,encoding="utf-8")); inc=p["inc"]; fin=max(p["done"][1:])
    print(name,"첫",p["first"][1:],"완료",p["done"][1:],"분",[round(x) for x in p["doneMin"][1:]],"산 판",p["buyRounds"],"/",p["rounds"],
          "완료 전 산 판",sum(1 for i,b in enumerate(p["buys"]) if b>0 and i+1<=fin),"/",fin,
          "수입",{r:f(inc[r-1]) for r in (1,10,20,30,45,60,75,90,104)})
