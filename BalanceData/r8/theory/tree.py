# -*- coding: utf-8 -*-
"""DB(db.json) 노드·가격과 봇 구매 규칙(Game.BulkBuy: 가장 싼 것부터, gm 제외, ParentOk)을 파이썬으로 옮긴 것.
가격 = ceil(Cost_Lv x COST_MUL 0.9), 싱크 = ceil(c0 x k^L x 0.9) (Game.NodeCost)."""
import json, math, os, copy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
DB = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'db_r8_base.json')   # 8회차 기준 DB(Ver.06 = git HEAD 49345ed). 게임 db.json은 Ver.07 적용 뒤라 기준으로 못 씀
COST_MUL = 0.9


def load(db_path=DB, overrides=None, drop=()):
    """overrides: {key: {'costs': [...], 'efx': [...], 'pre': key, 'max': n}} — 수치안 적용용. drop: 빼는 노드 키"""
    d = json.load(open(db_path, encoding='utf-8'))
    cost = {c['Cost_ID']: c for c in d['Upgrade_Cost']}
    key = {u['Upg_ID']: u['Upg_Key'] for u in d['Upgrade']}
    nodes = {}
    for u in d['Upgrade']:
        k = u['Upg_Key']
        if k in drop:
            continue
        c = cost[u['Cost_ID']]
        n = dict(id=k, br=u['Branch'], pre=key.get(u['Upg_Pre']), req=key.get(u['Upg_Req']), tier=u['Tier'],
                 max=u['Efx_MaxLv'], efx=[u['Efx_Lv%d' % i] for i in range(5)],
                 costs=[c['Cost_Lv%d' % i] for i in range(1, 5)][:max(u['Efx_MaxLv'], 2)],
                 gem=c['Gem_Lv1'] > 0, inf=u['Efx_MaxLv'] == 0, name=u['Upg_Name'], uid=u['Upg_ID'], cid=u['Cost_ID'])
        if overrides and k in overrides:
            n.update(copy.deepcopy(overrides[k]))
        nodes[k] = n
    for n in nodes.values():   # 뺀 노드의 자식은 그 부모로 붙인다
        while n['pre'] is not None and n['pre'] not in nodes:
            n['pre'] = None
    consts = {c['Const_Key']: c['Value'] for c in d['Const']}
    boss = {b['Boss_Key']: b['Hp_Mul'] for b in d['Boss']}
    return nodes, consts, boss


def price(n, L):
    if n['inf']:
        c0, k = n['costs'][0], n['costs'][1] / n['costs'][0]
        return math.ceil(c0 * k ** L * COST_MUL)
    return math.ceil(n['costs'][L] * COST_MUL) if L < n['max'] else math.inf


def at_max(n, L):
    return (not n['inf']) and L >= n['max']


def parent_ok(nodes, lv, n):
    if n['pre'] is not None and lv.get(n['pre'], 0) < 1:
        return False
    if n['req'] is not None and lv.get(n['req'], 0) < nodes[n['req']]['max']:
        return False
    return True


def cheapest(nodes, lv):
    best, bc = None, math.inf
    for n in nodes.values():
        if n['gem']:
            continue
        L = lv.get(n['id'], 0)
        if at_max(n, L) or not parent_ok(nodes, lv, n):
            continue
        c = price(n, L)
        if c < bc:
            best, bc = n, c
    return best, bc


def bulk_buy(nodes, lv, gold, inf_cap=10000):
    """가장 싼 것부터 다 산다. 반환: (남은 골드, 산 목록[(키, 새 레벨, 가격)])"""
    bought = []
    for _ in range(inf_cap):
        n, c = cheapest(nodes, lv)
        if n is None or gold < c:
            break
        gold -= c
        lv[n['id']] = lv.get(n['id'], 0) + 1
        bought.append((n['id'], lv[n['id']], c))
    return gold, bought


def efx(nodes, lv, k):
    n = nodes.get(k)
    if n is None:
        return 0.0
    L = lv.get(k, 0)
    return n['efx'][min(L, n['max'])] if not n['inf'] else L
