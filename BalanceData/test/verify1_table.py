# -*- coding: utf-8 -*-
"""verify#1 (옛 가격표, tuned) 시드 4~6 · skill 0.75 시드 1의 단계 완료 판과 대표 판 수입. 입력: before_1414(시드 4~6), before_1417(skill 0.75 시드 1)"""
import json, sys
sys.stdout.reconfigure(encoding="utf-8")
def f(x):
    for u,s in ((1e12,"T"),(1e9,"B"),(1e6,"M"),(1e3,"K")):
        if x>=u: return f"{x/u:.3g}{s}"
    return f"{x:.0f}"
for name,path in (("시드4","before_1414/balance_phase_s4.json"),("시드5","before_1414/balance_phase_s5.json"),("시드6","before_1414/balance_phase_s6.json"),("시드1 skill0.75","before_1417/balance_phase_s1.json")):
    p=json.load(open("BalanceData/test/"+path,encoding="utf-8"))
    inc=p["inc"]
    print(name,"첫",p["first"][1:],"완료",p["done"][1:],"분",[round(x) for x in p["doneMin"][1:]],"산 판",p["buyRounds"],"/",p["rounds"],
          "수입",{r:f(inc[r-1]) for r in (1,10,19,29,40,60,75,104)})
