# -*- coding: utf-8 -*-
"""시뮬 기록(balance_phase_s*.json)의 판별 수입으로 Game.BulkBuy(가장 싼 것부터 전부)를 다시 돌려
판마다 노드 레벨을 복원한다. 판별 구매 수와 끝 상태(balance_state_s*.json)가 시뮬과 같은지 확인한다."""
import json, math, os
from nodes import NODES, NODE, GOLD_NODES, ROOT, load_prices
import cfg

DATA = cfg.DATA


def data_prices():
    """그 기록을 만든 가격표"""
    return load_prices(os.path.join(DATA, "price_table.json"))


def parent_ok(n, lv):
    p = n["parent"]
    if p is None:
        return True
    return lv.get(p, 0) >= (NODE[p]["max"] if n["needMax"] else 1)


def cost(n, L, prices):
    c = prices.get(n["id"])
    v = c[L] if c is not None and L < len(c) else n["gold"] * n["g"] ** L
    return math.ceil(v)


def bulk_buy(lv, gold, prices):
    """Game.BulkBuy 그대로: 열린 노드 중 가장 싼 레벨을 살 수 있는 동안 산다. 동점은 NODES 순서 앞쪽."""
    k = 0
    while True:
        best, bc = None, float("inf")
        for n in GOLD_NODES:
            L = lv.get(n["id"], 0)
            if L >= n["max"] or not parent_ok(n, lv):
                continue
            c = cost(n, L, prices)
            if c < bc:
                bc, best = c, n
        if best is None or gold - bc < 0:
            return gold, k
        gold -= bc
        lv[best["id"]] = lv.get(best["id"], 0) + 1
        k += 1


def load_phase(seed, data=None):
    return json.load(open(os.path.join(data or DATA, f"balance_phase_s{seed}.json"), encoding="utf-8"))


def replay(seed, prices=None, data=None):
    prices = prices or (load_prices(os.path.join(data, "price_table.json")) if data else data_prices())
    ph = load_phase(seed, data)
    lv, gold, rows = {}, 0.0, []
    for i, inc in enumerate(ph["inc"]):
        before = dict(lv)
        gold += inc
        gold, k = bulk_buy(lv, gold, prices)
        rows.append(dict(stage=i + 1, inc=inc, levels_before=before, levels_after=dict(lv), bought=k, sim_bought=ph["buys"][i], gold=gold))
    return rows


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    print("기록:", DATA, "· 프리셋:", cfg.PRESET)
    for sd in (1, 2, 3):
        rows = replay(sd)
        sp = os.path.join(DATA, f"balance_state_s{sd}.json")
        st = json.load(open(sp, encoding="utf-8")) if os.path.exists(sp) else {"nodes": rows[-1]["levels_after"], "gold": float("nan")}
        diff_rounds = [r["stage"] for r in rows if r["bought"] != r["sim_bought"]]
        fin = rows[-1]["levels_after"]
        mism = {k: (fin.get(k, 0), v) for k, v in st["nodes"].items() if fin.get(k, 0) != v}
        print(f"s{sd}: 판별 구매 수 불일치 {len(diff_rounds)}판 {diff_rounds[:10]} · 끝 상태 불일치 {mism} · 끝 골드 {rows[-1]['gold']:.4g} (시뮬 {st['gold']:.4g})")
