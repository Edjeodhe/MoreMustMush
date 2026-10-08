# -*- coding: utf-8 -*-
"""balance.log(판마다 한 줄)에서 지역별 첫 판. 사용: python zone_entry.py balance.log  (시드 구간은 'R1 |'이 다시 나오면 새 시드)"""
import sys
sys.stdout.reconfigure(encoding="utf-8")
seed=0; first={}
def flush():
    if first: print(f"시드 {seed}:", " · ".join(f"{z} R{r}" for z,r in first.items()))
for line in open(sys.argv[1],encoding="utf-8"):
    c=[x.strip() for x in line.split("|")]
    if len(c)<4 or not c[0].startswith("R") or not c[0][1:].isdigit(): continue
    r=int(c[0][1:])
    if r==1: flush(); seed+=1; first={}
    first.setdefault(c[2],r)
flush()
