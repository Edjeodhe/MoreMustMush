# -*- coding: utf-8 -*-
"""게임 정의를 코드에서 직접 읽는다 (Assets/ 는 읽기만).
- 노드: Assets/Scripts/Core/Defs.Nodes.cs 의 N(...) · AddSkill(...) · SKILL_TREE · SKILL_TIER
- 가격표: BalanceData/price_table.json (Defs.Prices.cs 와 같음, 2026-10-08 12:33 수렴본)
노드 순서는 게임의 NODES 순서와 같다(BulkBuy 동점 처리에 쓰임)."""
import json, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Scripts", "Core", "Defs.Nodes.cs")
PRICE = os.path.join(ROOT, "BalanceData", "price_table.json")


def _num(s):
    return float(s.strip().rstrip("f"))


def load_nodes():
    src = open(SRC, encoding="utf-8").read()
    skills = []  # (id, gold, noPow, noRate)
    for m in re.finditer(r'AddSkill\("(\w+)", "[^"]*", "\w+", ([\d.]+),[^\n]*', src):
        line = m.group(0)
        skills.append((m.group(1), _num(m.group(2)), "noPow: true" in line, "noRate: true" in line))
    tree = dict(re.findall(r'\["(\w+)"\] = "(\w+)"', src.split("SKILL_TREE")[1].split("};")[0]))
    stier = {k: int(v) for k, v in re.findall(r'\["(\w+)"\] = (\d)', src.split("SKILL_TIER")[1].split("};")[0])}
    nodes = []
    body = src.split("static void InitSkillsAndNodes()")[1]
    # N("id", "br", parent, "name", max, gold, g, eff..., opts)
    pat = re.compile(r'N\("(\w+)", "(\w+)", (null|"\w+"), "([^"]*)", (\d+), ([\d.]+), ([\d.]+),(.*)')
    # 문장 단위로 읽는다(verify 수정 요청 7): "N(" 로 시작해 괄호가 닫힐 때까지 여러 줄을 이어 붙인다
    stmts, buf, depth = [], None, 0
    for line in body.splitlines():
        s = line.strip()
        if buf is None:
            if s.startswith("foreach (var id in SKILL_IDS)"):
                stmts.append("<SKILLS>")
                continue
            if not s.startswith("N("):
                continue
            buf, depth = "", 0
        buf += " " + s
        depth += s.count("(") - s.count(")")
        if depth <= 0:
            stmts.append(buf.strip())
            buf = None
    skill_loop_done = False
    for s in stmts:
        if s == "<SKILLS>" and not skill_loop_done:
            for sid, gold, nopow, norate in skills:
                tr = stier[sid]
                nodes.append(dict(id=sid, br="md", parent=tree[sid], n=sid, max=1, gold=gold, g=1, tier=tr, needMax=False, gem=0))
                if not norate:
                    nodes.append(dict(id=sid + "_rate", br="md", parent=sid, n=sid + " 발동률", max=2, gold=gold * 2, g=3, tier=tr, needMax=False, gem=0))
                if not nopow:
                    nodes.append(dict(id=sid + "_pow", br="md", parent=sid, n=sid + " 위력", max=2, gold=gold * 2, g=3, tier=tr, needMax=False, gem=0))
            skill_loop_done = True
            continue
        m = pat.search(s)
        if not m:
            continue
        rest = m.group(8)
        tier = re.search(r"tier: (\d)", rest)
        gem = re.search(r"gem: ([\d.]+)", rest)
        nodes.append(dict(id=m.group(1), br=m.group(2), parent=None if m.group(3) == "null" else m.group(3).strip('"'),
                          n=m.group(4), max=int(m.group(5)), gold=_num(m.group(6)), g=_num(m.group(7)),
                          tier=int(tier.group(1)) if tier else 1, needMax="needMax: true" in rest,
                          gem=_num(gem.group(1)) if gem else 0, core="core: true" in rest))
    for n in nodes:
        n.setdefault("core", False)
    return nodes


def load_prices(path=PRICE):
    return json.load(open(path, encoding="utf-8"))


NODES = load_nodes()
NODE = {n["id"]: n for n in NODES}
GOLD_NODES = [n for n in NODES if n["gem"] <= 0]

if __name__ == "__main__":
    pt = load_prices()
    print(len(NODES), "nodes,", len(GOLD_NODES), "gold,", sum(n["max"] for n in GOLD_NODES), "gold levels")
    miss = [n["id"] for n in GOLD_NODES if n["id"] not in pt or len(pt[n["id"]]) != n["max"]]
    print("price table mismatch:", miss)
