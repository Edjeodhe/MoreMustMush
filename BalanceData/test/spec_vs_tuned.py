# -*- coding: utf-8 -*-
"""spec 기록(before_1239)과 tuned 기록(before_1313)의 같은 판 수입 비율 · 단계 판을 나란히 뽑는다.
사용: python BalanceData/test/spec_vs_tuned.py   (폴더 이름은 아래 상수)"""
import json, sys
sys.stdout.reconfigure(encoding="utf-8")
A, B = "BalanceData/test/before_1239", "BalanceData/test/before_1313"
for sd in (1, 2, 3):
    a = json.load(open(f"{A}/balance_phase_s{sd}.json")); b = json.load(open(f"{B}/balance_phase_s{sd}.json"))
    print(f"시드 {sd}: spec 완료 {a['done'][1:]} / tuned 완료 {b['done'][1:]}")
    print("  같은 판 수입 tuned/spec:", [(r, round(b['inc'][r-1] / a['inc'][r-1], 1)) for r in (1, 5, 10, 15, 19, 29, 45, 60, 75)])
