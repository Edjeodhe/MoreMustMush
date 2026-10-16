# -*- coding: utf-8 -*-
"""verify — 판 1~6 '판마다 1~2개'가 성립하는 수입 배율 창 (alpha별, 결정적). r3_verify.py의 함수를 쓴다."""
import r3_verify as v

pts, st7 = v.load_t0()
new = v.load_nodes(True, True)
for a in (0.1, 0.3, 0.5, 0.7, 1.0):
    m = v.fit(pts, a)
    ok = []
    rows = []
    for i in range(6, 51):
        mul = i * 0.05
        log = v.run("new", new, m, 7.06, True, mul, rounds=6)
        n = [x["n"] for x in log]
        good = all(1 <= k <= 2 for k in n)
        ok.append((mul, good))
        rows.append(f"{mul:.2f}:{''.join(str(min(9,k)) for k in n)}{'*' if good else ''}")
    goods = [m_ for m_, g in ok if g]
    print(f"alpha {a}: 1~2개 성립 배율 {min(goods):.2f}~{max(goods):.2f} (연속 아님 가능) | " + " ".join(rows))
