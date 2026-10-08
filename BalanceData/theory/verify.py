# -*- coding: utf-8 -*-
"""verify 에이전트의 독립 재계산 스크립트 (theory의 nodes.py/replay.py를 쓰지 않는다).

python BalanceData/theory/verify.py [check|replay|sens|all]

- check : (a) ComputeStats 형제 노드 합산, (b) TIER_LINES per vs NF, (c) 가격 조건 재측정
- replay: 시뮬 기록(판별 수입)을 그대로 넣고 일괄 강화(가장 싼 것부터)를 다시 돌려 구매 기록·단계 완료 판을 재현
- sens  : 수입 ×0.5 / ×2 민감도 (보유 레벨 → 수입 근사, 지역 해금 스테이지 고정)
"""
import json, math, os, re, sys, statistics as stt

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
P = lambda *a: os.path.join(ROOT, *a)
def rd(p): return open(p, encoding="utf-8").read()

NODES_CS = P("Assets", "Scripts", "Core", "Defs.Nodes.cs")
GAME_CS = P("Assets", "Scripts", "Core", "Game.cs")
PRICES_CS = P("Assets", "Scripts", "Core", "Defs.Prices.cs")
SIM_CS = P("Assets", "Editor", "BalanceSim.cs")
BD = P(os.environ.get("VERIFY_DATA", "BalanceData"))   # 기록 폴더. tuned T2 = BalanceData/test/before_1313

# ---------- 노드 정의 파싱 (NODES 순서 그대로: BulkBuy 동률은 앞 노드가 이긴다) ----------
def parse_nodes():
    src = rd(NODES_CS)
    body = src[src.index("static void InitSkillsAndNodes()"):]
    skills = []  # (id, name, gold, noPow, noRate)
    for m in re.finditer(r'AddSkill\("(\w+)",\s*"([^"]+)",\s*"\w+",\s*([\d.]+),([^\n]*)', body):
        rest = m.group(4)
        skills.append((m.group(1), m.group(2), float(m.group(3)), "noPow: true" in rest, "noRate: true" in rest))
    tier_m = re.search(r"SKILL_TIER = new Dictionary<string, int>\s*\{(.*?)\};", body, re.S).group(1)
    skill_tier = {k: int(v) for k, v in re.findall(r'\["(\w+)"\] = (\d)', tier_m)}
    tree_m = re.search(r"SKILL_TREE = new Dictionary<string, string>\s*\{(.*?)\};", src, re.S).group(1)
    skill_tree = dict(re.findall(r'\["(\w+)"\] = "(\w+)"', tree_m))
    nodes = []
    pat = re.compile(r'\bN\("(\w+)",\s*"(\w+)",\s*(null|"\w+"),\s*("[^"]*"|s\.n[^,]*),\s*(\d+),\s*([\w.*]+),\s*([\d.]+),(.*)')
    skill_loop = body.index("foreach (var id in SKILL_IDS)")
    def mk(id_, br, parent, max_, gold, g, rest):
        def kw(k, d):
            mm = re.search(k + r":\s*([\w.]+)", rest)
            return mm.group(1) if mm else d
        return dict(id=id_, br=br, parent=parent, max=max_, gold=gold, g=g,
                    gem=float(kw("gem", "0")), core=kw("core", "false") == "true",
                    tier=int(kw("tier", "1")), needMax=kw("needMax", "false") == "true")
    inserted = False
    for line in body.split("\n"):
        pos = body.index(line) if line.strip() else 0
        if not inserted and pos >= skill_loop and line.strip():
            for sid, sn, sg, noPow, noRate in skills:
                tr = skill_tier[sid]
                nodes.append(mk(sid, "md", skill_tree[sid], 1, sg, 1.0, f"tier: {tr}"))
                if not noRate: nodes.append(mk(sid + "_rate", "md", sid, 2, sg * 2, 3.0, f"tier: {tr}"))
                if not noPow: nodes.append(mk(sid + "_pow", "md", sid, 2, sg * 2, 3.0, f"tier: {tr}"))
            inserted = True
        m = pat.search(line)
        if not m or "static Node N(" in line: continue
        id_, br, par, _n, mx, gold, g, rest = m.groups()
        if id_ in [s[0] for s in skills]: continue
        full = full_stmt(body, id_)   # 여러 줄에 걸친 정의(gm_meteor)
        nodes.append(mk(id_, br, None if par == "null" else par.strip('"'), int(mx), float(gold), float(g), full))
    return nodes

def full_stmt(body, id_):
    # 다음 문장(다음 N( 호출 · foreach · 빈 줄) 전까지. 람다 안의 ");"에서 끊지 않는다
    i = body.index('N("' + id_ + '",')
    ends = [body.find(t, i + 3) for t in ("\n            N(", "\n            foreach", "\n\n")]
    return body[i:min(e for e in ends if e > 0)]

def parse_prices_cs():
    src = rd(PRICES_CS)
    return {k: [float(x) for x in v.split(",")] for k, v in re.findall(r'\["(\w+)"\] = new double\[\] \{([^}]*)\}', src)}

def sim_const(name):
    return float(re.search(name + r"\s*=\s*([\d.]+)", rd(SIM_CS)).group(1))

def tier_lines():
    src = rd(SIM_CS)
    blk = re.search(r"TIER_LINES =\s*\{(.*?)\};", src, re.S).group(1)
    out = []
    for ids, per in re.findall(r'new\[\] \{([^}]*)\}, new\[\] \{([^}]*)\}', blk):
        out.append(([x.strip().strip('"') for x in ids.split(",")], [float(x) for x in per.split(",")]))
    return out

class Model:
    def __init__(self, table=None):
        self.nodes = parse_nodes()
        self.N = {n["id"]: n for n in self.nodes}
        self.table = table if table is not None else json.load(open(os.path.join(BD, "price_table.json")))
        for n in self.nodes:
            c = self.table.get(n["id"])
            n["costs"] = c if c is not None and len(c) == n["max"] else None
    def cost(self, n, L):
        v = n["costs"][L] if n["costs"] is not None and L < len(n["costs"]) else n["gold"] * n["g"] ** L
        return math.ceil(v)
    def gold_nodes(self, core=True):
        return [n for n in self.nodes if n["gem"] <= 0 and (core or not n["core"])]

# ---------- (a)(b) 코드 점검 ----------
def check_code(out):
    nf = dict(re.findall(r"public static (?:double|int) (\w+)\(int L\) => (?:\(\w+\))?\s*([\d.]+) \* L;", rd(NODES_CS)))
    game = rd(GAME_CS)
    out.append("## (a) ComputeStats 형제 노드 합산")
    for stat, ids in [("atkFlat", ["ps_atk", "ps_atk2", "ps_atk2p", "ps_atk3", "ps_atk3p", "ps_atk4"]),
                      ("scoreFlat", ["ed_score", "ed_score1p", "ed_score2", "ed_score2p", "ed_score3", "ed_score3p"]),
                      ("harvFlat", ["ed_bonus", "ed_bonus1p", "ed_bonus2", "ed_bonus2p", "ed_bonus3", "ed_bonus3p"])]:
        line = next(l for l in game.split("\n") if re.search(rf"\b{stat}\b\s*=", l))
        miss = [i for i in ids if f'NF.{i}(Lv("{i}"))' not in line]
        out.append(f"- {stat}: {'빠진 노드 없음' if not miss else '빠짐 ' + ','.join(miss)} (노드 {len(ids)}개)")
    # NF 중 어디서도 쓰지 않는 함수
    allsrc = "".join(rd(os.path.join(d, f)) for d, _, fs in os.walk(P("Assets", "Scripts")) for f in fs if f.endswith(".cs"))
    nfblk = rd(NODES_CS); nfblk = nfblk[nfblk.index("public static class NF"):nfblk.index("public class Node")]
    allnf = re.findall(r"public static (?:double|int|Meteor) (\w+)\(int \w\)", nfblk)
    unused = [f for f in allnf if f"NF.{f}(" not in allsrc]
    out.append(f"- NF 함수 {len(allnf)}개 중 어디서도 안 쓰는 것: {unused or '없음'}")
    out.append("## (b) TIER_LINES per vs NF 계수")
    for ids, per in tier_lines():
        bad = [(i, p, nf.get(i)) for i, p in zip(ids, per) if float(nf.get(i, -1)) != p]
        out.append(f"- {ids[0]} 줄: " + ("일치" if not bad else "불일치 " + str(bad)))

# ---------- (c) 가격 조건 ----------
def check_prices(m, out):
    cs = parse_prices_cs()
    js = m.table
    diff = [k for k in set(cs) | set(js) if cs.get(k) != js.get(k)]
    out.append("## (c) 가격 조건 재측정")
    out.append(f"- Defs.Prices.cs ↔ price_table.json: {'완전 일치' if not diff else '다름 ' + str(diff)} ({len(cs)}개 노드)")
    gn = m.gold_nodes(core=False)
    nocost = [n["id"] for n in gn if n["costs"] is None]
    out.append(f"- 가격표 없는 골드 노드: {nocost or '없음'} · 골드 레벨 {sum(n['max'] for n in gn)}개(코어 제외)")
    ps = sorted((m.cost(n, l), n["id"], l) for n in gn for l in range(n["max"]))
    steps = sorted(((ps[k][0] / max(1, ps[k - 1][0]), ps[k - 1], ps[k]) for k in range(1, len(ps))), reverse=True)
    out.append(f"- 최대 가격 뜀(오름차순 정렬, PriceCheck와 같은 정의): ×{steps[0][0]:.2f}  {steps[0][1][1]}[{steps[0][1][2]}] {steps[0][1][0]:,} → {steps[0][2][1]}[{steps[0][2][2]}] {steps[0][2][0]:,}")
    out.append("  상위 5: " + " · ".join(f"×{s:.2f}({a[1]}→{b[1]})" for s, a, b in steps[:5]))
    out.append(f"  기준: PRICE_STEP_MAX {sim_const('PRICE_STEP_MAX')} 넘는 뜀 {sum(1 for s in steps if s[0] > sim_const('PRICE_STEP_MAX'))}개 · STEP_FIX {sim_const('STEP_FIX')} 넘는 뜀 {sum(1 for s in steps if s[0] > sim_const('STEP_FIX'))}개 · ×2 넘는 뜀 {sum(1 for s in steps if s[0] > 2)}개")
    ns = []
    for n in gn:
        for l in range(1, n["max"]): ns.append((m.cost(n, l) / max(1, m.cost(n, l - 1)), n["id"], l))
    lines = tier_lines()
    cross = sim_const("CROSS")
    cr = []
    for ids, per in lines:
        for t in range(1, len(ids)):
            a, b = m.N[ids[t - 1]], m.N[ids[t]]
            if per[t] == per[t - 1]:
                ns.append((m.cost(b, 0) / max(1, m.cost(a, a["max"] - 1)), f"{ids[t-1]}→{ids[t]}", 0)); continue
            r = (m.cost(b, 0) / per[t]) / (m.cost(a, a["max"] - 1) / per[t - 1])
            cr.append((ids[t - 1], ids[t], r))
    ns.sort(reverse=True)
    out.append(f"- 노드 안(형제 이음 포함) 최대: ×{ns[0][0]:.2f} {ns[0][1]} · ×4 넘는 것 {sum(1 for x in ns if x[0] > 4 + 1e-9)}개 · 상위 3: " + ", ".join(f"×{x[0]:.2f} {x[1]}" for x in ns[:3]))
    ok = sum(1 for *_, r in cr if r <= cross * 1.05)
    ok_strict = sum(1 for *_, r in cr if r <= cross)
    out.append(f"- 효율 넘김(다음 단계 첫 레벨 +1당 골드 ÷ 앞 단계 마지막 레벨 +1당 골드 ≤ {cross}×1.05): {ok}/{len(cr)} · 허용오차 없이 ≤{cross}: {ok_strict}/{len(cr)}")
    for a, b, r in cr: out.append(f"  {a}→{b}: 비율 {r:.3f}")
    return ps

# ---------- 리플레이 ----------
def bulk_buy(m, lv, gold):
    bought = []
    for _ in range(3000):
        best, bc = None, float("inf")
        for n in m.nodes:
            if n["br"] not in ("ed", "md", "ps") or n["gem"] > 0: continue
            L = lv.get(n["id"], 0)
            if L >= n["max"]: continue
            if L == 0:
                p = n["parent"]
                if p is not None and lv.get(p, 0) < (m.N[p]["max"] if n["needMax"] else 1): continue
            c = m.cost(n, L)
            if c < bc: bc, best = c, n
        if best is None or gold - bc < 0: break
        gold -= bc; lv[best["id"]] = lv.get(best["id"], 0) + 1; bought.append((best["id"], lv[best["id"]], bc))
    return gold, bought

def tier_done(m, lv):
    res = {}
    for t in (1, 2, 3):
        ns = [n for n in m.gold_nodes() if n["tier"] == t]
        res[t] = (sum(min(lv.get(n["id"], 0), n["max"]) for n in ns), sum(n["max"] for n in ns))
    return res

def replay(m, inc, stop=None):
    lv, gold, first, done, buys, seq, lvl_before = {}, 0.0, {}, {}, [], [], []
    for r, g in enumerate(inc, 1):
        lvl_before.append(sum(lv.values()))
        gold += g
        gold, b = bulk_buy(m, lv, gold)
        buys.append(len(b)); seq += [(r,) + x for x in b]
        td = tier_done(m, lv)
        for t, (h, mx) in td.items():
            if t not in first and h > 0: first[t] = r
            if t not in done and h == mx: done[t] = r
    return dict(first=first, done=done, buys=buys, seq=seq, gold=gold, lv=lv, lvl_before=lvl_before)

def load_phase(s):
    return json.load(open(os.path.join(BD, f"balance_phase_s{s}.json")))

def do_replay(m, out):
    out.append("## 리플레이: 기록된 판별 수입 → 일괄 강화 재실행")
    res = {}
    for s in (1, 2, 3):
        ph = load_phase(s)
        r = replay(m, ph["inc"])
        res[s] = r
        mism = [i + 1 for i, (a, b) in enumerate(zip(r["buys"], ph["buys"])) if a != b]
        st = json.load(open(os.path.join(BD, f"balance_state_s{s}.json")))
        lv_diff = {k: (r["lv"].get(k, 0), v) for k, v in st["nodes"].items() if m.N.get(k) and m.N[k]["gem"] <= 0 and r["lv"].get(k, 0) != v}
        out.append(f"- 시드 {s}: 판별 구매 수 불일치 {len(mism)}판{(' ' + str(mism[:8])) if mism else ''} · 최종 레벨 불일치 {lv_diff or '없음'} · "
                   f"최종 골드 {r['gold']:.4g} (기록 {st['gold']:.4g})")
        out.append(f"  단계 첫 구매/완료: 재현 {[(r['first'].get(t), r['done'].get(t)) for t in (1,2,3)]} · 기록 {[(ph['first'][t], ph['done'][t]) for t in (1,2,3)]}")
        gaps, last = [], 0
        for i, b in enumerate(ph["buys"], 1):
            if b > 0: gaps.append(i - last); last = i
        noBuyRun, cur = 0, 0
        for b in ph["buys"][: ph["done"][3]]:
            cur = cur + 1 if b == 0 else 0; noBuyRun = max(noBuyRun, cur)
        out.append(f"  구매 사이 판 수(완료까지): 중앙값 {stt.median(gaps)} · 최대 {max(gaps)} · 연속 무구매 최대 {noBuyRun}판 · 한 판 최다 구매 {max(ph['buys'])}레벨(R{ph['buys'].index(max(ph['buys']))+1})")
        # 실제 구매 순서에서 바로 앞 구매 대비 가격 뜀
        seq = r["seq"]
        jumps = sorted(((seq[k][3] / max(1, seq[k - 1][3]), seq[k - 1], seq[k]) for k in range(1, len(seq))), reverse=True)[:3]
        out.append("  실제 구매 순서의 가격 뜀 상위: " + " · ".join(f"×{j:.2f} R{a[0]} {a[1]}→R{b[0]} {b[1]}" for j, a, b in jumps))
        # 판 수입 뜀
        inc = ph["inc"]
        ups = sorted(((inc[i] / max(1, inc[i - 1]), i + 1) for i in range(1, len(inc))), reverse=True)[:4]
        dns = sorted(((inc[i] / max(1, inc[i - 1]), i + 1) for i in range(1, len(inc))))[:3]
        out.append("  판 수입 급등: " + ", ".join(f"R{r_} ×{x:.1f}" for x, r_ in ups) + " · 급락: " + ", ".join(f"R{r_} ×{x:.3f}" for x, r_ in dns))
        late = [x for x in seq if x[0] >= 70]
        out.append("  R70 이후 구매: " + ", ".join(f"R{x[0]} {x[1]}{x[2]}" for x in late[-14:]))
    return res

# ---------- 민감도 ----------
ZONE_FROM = [1, 20, 45, 75, 105]
def sens(m, out, res):
    out.append("## 민감도: 수입 ×0.5 / ×2 (보유 레벨→수입 근사, 지역 배율·스테이지 성장 분리)")
    out.append("  근사: inc(r) = s × g(보유 레벨) × 10^z × 1.01^(r-1), z = 그 판에 기록된 수입으로 역산하지 않고 '열린 지역 중 공격력 조건을 만족하는 최고 지역'으로 둔다")
    hp = 3.0
    # 공격력 배율 = 1 + 코어 0.1 + 별 능력치. 처음엔 별을 0으로 뒀다가 수입 ×0.9 "절벽"이라는 틀린 결론을 냈다(manager#8).
    # 로그 표시 공격력 ÷ 고정값: R97 5,799/2,775 = 2.09 · 7,889/3,775 = 2.09 · R98 3,798/1,775 = 2.14 → theory A4 보간을 쓴다
    def atk_mul(r):
        pts = [(1, 1.1), (34, 1.1), (43, 1.33), (50, 1.55), (55, 1.83), (97, 2.1), (120, 2.15)]
        for (a, va), (b, vb) in zip(pts, pts[1:]):
            if r <= b: return va + (vb - va) * (r - a) / (b - a)
        return pts[-1][1]
    def atk_of(lv, r):
        f = 1 + 1*lv.get("ps_atk",0) + 10*lv.get("ps_atk2",0) + 10*lv.get("ps_atk2p",0) + 100*lv.get("ps_atk3",0) + 100*lv.get("ps_atk3p",0) + 1000*lv.get("ps_atk4",0)
        return f * atk_mul(r)
    def zone(lv, r):
        z = 0
        for i, fr in enumerate(ZONE_FROM):
            if r >= fr and atk_of(lv, r) >= 0.5 * hp * 10 ** i * 1.01 ** (r - 1): z = i
        return z
    for s in (1, 2, 3):
        ph = load_phase(s)
        # 기록을 다시 돌려 각 판의 보유 레벨·추정 지역 → g = inc / (10^z × 1.01^(r-1))
        lv, gold, pts = {}, 0.0, []
        zs = []
        for r, g in enumerate(ph["inc"], 1):
            z = zone(lv, r); zs.append(z)
            pts.append((sum(lv.values()), g / (10 ** z * 1.01 ** (r - 1))))
            gold += g; gold, _ = bulk_buy(m, lv, gold)
        # 레벨별 g: 로그 공간 등위 회귀(PAV, 단조 증가 최소제곱) — 누적 최대는 잡음 위쪽에 붙어 수입을 과대평가한다
        by = {}
        # 골드는 산술로 쌓이므로 산술 평균(로그 평균은 판 편차 ×10 때문에 수입을 1.5~2배 낮게 잡았다)
        for L, g in pts: by.setdefault(L, []).append(max(g, 1e-9))
        xs = sorted(by)
        blocks = [[sum(by[x]), len(by[x]), [x]] for x in xs]
        i = 0
        while i < len(blocks) - 1:
            if blocks[i][0] / blocks[i][1] > blocks[i + 1][0] / blocks[i + 1][1]:
                a, b = blocks[i], blocks.pop(i + 1); a[0] += b[0]; a[1] += b[1]; a[2] += b[2]; i = max(0, i - 1)
            else: i += 1
        val = {x: math.log(bl[0] / bl[1]) for bl in blocks for x in bl[2]}
        ys = [val[x] for x in xs]
        def G(L):
            if L <= xs[0]: return math.exp(ys[0])
            if L >= xs[-1]: return math.exp(ys[-1])
            j = next(i for i in range(len(xs)) if xs[i] >= L)
            t = (L - xs[j - 1]) / (xs[j] - xs[j - 1]); return math.exp(ys[j - 1] + t * (ys[j] - ys[j - 1]))
        zchg = [(i + 1, zs[i]) for i in range(1, len(zs)) if zs[i] != zs[i - 1]]
        # 외생 근사: 기록된 판별 수입 궤적에 ×k (구매가 수입을 키우는 되먹임 없음 → 민감도 하한)
        ex = []
        for k in (0.5, 2.0):
            r_ = replay(m, [k * x for x in ph["inc"]] + [k * ph["inc"][-1]] * 40)
            ex.append(f"×{k}: 트리 R{r_['done'].get(3, '>144')}")
        line = []
        for k in (0.5, 1.0, 2.0):
            lv, gold, done = {}, 0.0, {}
            for r in range(1, 140):
                gold += k * G(sum(lv.values())) * 10 ** zone(lv, r) * 1.01 ** (r - 1)
                gold, _ = bulk_buy(m, lv, gold)
                td = tier_done(m, lv)
                for t, (h, mx) in td.items():
                    if t not in done and h == mx: done[t] = r
                if len(done) == 3: break
            line.append(f"×{k}: 초반 R{done.get(1,'-')} 중반 R{done.get(2,'-')} 후반(트리) R{done.get(3,'>139')}")
        out.append(f"- 시드 {s}: 추정 지역(판, 지역번호) {zchg}")
        out.append(f"  외생(궤적×k): {' | '.join(ex)}")
        out.append(f"  되먹임(레벨→수입): {' | '.join(line)}  (×1.0이 기록 R{ph['done'][3]}과 맞아야 믿을 수 있음)")

# ---------- tuned 독립 재계산 (2단계 수치) ----------
# 근사: 판 수입 = g(보유 레벨 수) × Z(지역, 판). g는 기록에서 산술 평균 + 단조 회귀(theory의 특징 회귀와 다른 구조).
# 가격을 전체 ×s로만 바꾸면 사는 순서가 그대로라 "보유 레벨 수 → 구성"이 같아 이 근사가 성립한다.
def log_atk_mul(m, data=None):
    """balance.log 표시 공격력 ÷ 리플레이 고정값 (판, 배율). 시드별 마지막 실행, 고정값 ≥ 10"""
    path = os.path.join(data or BD, "balance.log")
    lines = open(path, encoding="utf-8").read().splitlines()
    starts = [i for i, l in enumerate(lines) if l.startswith("R1 |")][-3:]
    pts = {}
    for sd, st in zip((1, 2, 3), starts):
        nxt = [i for i in [i for i, l in enumerate(lines) if l.startswith("R1 |")] if i > st]
        end = nxt[0] if nxt else len(lines)
        rep = replay(m, load_phase(sd)["inc"])
        # 판 r 시작 시점 레벨 = r-1판까지의 구매
        lv, cum = {}, []
        seq = rep["seq"]; j = 0
        for r in range(1, 106):
            while j < len(seq) and seq[j][0] < r:
                lv[seq[j][1]] = seq[j][2]; j += 1
            cum.append(dict(lv))
        for l in lines[st:end]:
            p = [x.strip() for x in l.split("|")]
            if len(p) < 6 or not p[0].startswith("R"): continue
            try: a = float(p[5].replace(",", ""))
            except ValueError: continue
            r = int(p[0][1:]); L = cum[r - 1]
            f = 1 + L.get("ps_atk", 0) + 10 * (L.get("ps_atk2", 0) + L.get("ps_atk2p", 0)) + 100 * (L.get("ps_atk3", 0) + L.get("ps_atk3p", 0)) + 1000 * L.get("ps_atk4", 0)
            if f >= 10: pts.setdefault(r, []).append((a / f, p[2]))
    return pts

class TModel:
    def __init__(self, m, hp, atk_pts, zone_from=(1, 20, 45, 75, 105)):
        self.m, self.hp, self.pts, self.zf = m, hp, atk_pts, list(zone_from)
        self.ramp = 0
    def amul(self, r):
        pts = self.pts
        for (a, va), (b, vb) in zip(pts, pts[1:]):
            if r <= b: return va + (vb - va) * (r - a) / (b - a)
        return pts[-1][1]
    def Z(self, z, r):
        e = z
        if self.ramp and z > 0: e = z - 1 + min(1.0, (r - self.zf[z] + 1) / self.ramp)
        return 10 ** e * 1.01 ** (r - 1)
    def zone(self, lv, r):
        f = 1 + lv.get("ps_atk", 0) + 10 * (lv.get("ps_atk2", 0) + lv.get("ps_atk2p", 0)) + 100 * (lv.get("ps_atk3", 0) + lv.get("ps_atk3p", 0)) + 1000 * lv.get("ps_atk4", 0)
        a = f * self.amul(r); z = 0
        for i, fr in enumerate(self.zf):
            if r >= fr and a >= 0.5 * self.hp * self.Z(i, r): z = i
        return z
    def fit(self, seeds=(1, 2, 3)):
        self.G = {}
        for s in seeds:
            ph = load_phase(s); lv, gold, pts = {}, 0.0, []
            for r, g in enumerate(ph["inc"], 1):
                pts.append((sum(lv.values()), g / self.Z(self.zone(lv, r), r)))
                gold += g; gold, _ = bulk_buy(self.m, lv, gold)
            by = {}
            for L, g in pts: by.setdefault(L, []).append(g)
            xs = sorted(by); bl = [[sum(by[x]), len(by[x]), [x]] for x in xs]; i = 0
            while i < len(bl) - 1:
                if bl[i][0] / bl[i][1] > bl[i + 1][0] / bl[i + 1][1]:
                    a, b = bl[i], bl.pop(i + 1); a[0] += b[0]; a[1] += b[1]; a[2] += b[2]; i = max(0, i - 1)
                else: i += 1
            val = {x: math.log(b_[0] / b_[1]) for b_ in bl for x in b_[2]}
            self.G[s] = (xs, [val[x] for x in xs])
    def g(self, s, L):
        xs, ys = self.G[s]
        if L <= xs[0]: return math.exp(ys[0])
        if L >= xs[-1]: return math.exp(ys[-1])
        j = next(i for i in range(len(xs)) if xs[i] >= L)
        t = (L - xs[j - 1]) / (xs[j] - xs[j - 1]); return math.exp(ys[j - 1] + t * (ys[j] - ys[j - 1]))
    def run(self, s, price_mul=1.0, inc_mul=1.0, rounds=160):
        lv, gold, done, zs, incs, buys = {}, 0.0, {}, [], [], []
        for r in range(1, rounds + 1):
            z = self.zone(lv, r); zs.append(z)
            inc = inc_mul * self.g(s, sum(lv.values())) * self.Z(z, r) / price_mul   # 가격 ×s ≡ 수입 ÷ s
            incs.append(inc); gold += inc
            gold, b = bulk_buy(self.m, lv, gold); buys.append(len(b))
            for t, (h, mx) in tier_done(self.m, lv).items():
                if t not in done and h == mx: done[t] = r
            if len(done) == 3: break
        fin = done.get(3) if len(done) == 3 else None
        lim = fin or len(incs)
        entry = {k: next((i + 1 for i, z in enumerate(zs) if z >= k), None) for k in (1, 2, 3)}
        med = [stt.median(incs[i:i + 5]) for i in range(0, lim - 4, 5)]
        p6 = max((med[j] / med[j - 1] for j in range(1, len(med))), default=0)
        gap = cur = 0
        for b in buys[:lim]:
            cur = cur + 1 if b == 0 else 0; gap = max(gap, cur)
        firsts = {}
        return dict(fin=fin, done=done, entry=entry, p6=p6, burst=max(buys[:lim]), gap=gap,
                    sea_rounds=(fin - entry[3] + 1) if fin and entry[3] and entry[3] <= fin else 0)

def tuned_check(out):
    m = Model()
    out.append(f"## tuned 독립 재계산 (기록 {os.path.relpath(BD, ROOT)})")
    do_replay(m, out)
    pts = log_atk_mul(m)
    out.append("- 로그 공격력 배율(판: 시드별 값/지역): " + "; ".join(f"R{r} " + ",".join(f"{v:.2f}/{z}" for v, z in vs) for r, vs in sorted(pts.items())))
    med = [(1, 1.1)] + [(r, stt.median(v for v, _ in vs)) for r, vs in sorted(pts.items())]
    mono = []
    for r, v in med: mono.append((r, max(v, mono[-1][1]) if mono else v))
    mono.append((mono[-1][0] + 30, mono[-1][1]))
    out.append("- 배율 보간점(시드 중앙값, 단조): " + ", ".join(f"R{r} {v:.2f}" for r, v in mono))
    T = TModel(m, hp=float(os.environ.get("VERIFY_HP", "1")), atk_pts=mono); T.fit()
    rows = []
    def line(name, **kw):
        res = [T.run(s, **kw) for s in (1, 2, 3)]
        f = lambda k: " / ".join(str(r[k]) for r in res)
        out.append(f"- {name}: 완료 {f('fin')} · 진입(밤·들판·바다) " + " / ".join(f"{r['entry'][1]}·{r['entry'][2]}·{r['entry'][3]}" for r in res)
                   + f" · 바다 판 {f('sea_rounds')} · P6 최대 " + " / ".join(f"×{r['p6']:.0f}" for r in res)
                   + f" · 한 판 최다 {f('burst')} · 무구매 최대 {f('gap')} · 단계 완료 " + " / ".join(str([r['done'].get(t) for t in (1, 2, 3)]) for r in res))
        return res
    line("T2 그대로(재현)")
    for k in (0.8, 1.25): line(f"T2 수입 ×{k}", inc_mul=k)
    line("O1 가격 ×2.08", price_mul=2.08)
    for k in (0.8, 1.25): line(f"O1 + 수입 ×{k}", price_mul=2.08, inc_mul=k)
    T.ramp = 10
    line("O3(S1, 10판 나눠) 가격 ×2.51", price_mul=2.51)
    line("O3 가격 ×2.08", price_mul=2.08)
    for k in (0.8, 1.25): line(f"O3 ×2.51 + 수입 ×{k}", price_mul=2.51, inc_mul=k)
    T.ramp = 0

def main():
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what == "tuned":
        out = []; tuned_check(out); print("\n".join(out)); return
    out = []
    m = Model()
    if what in ("check", "all"):
        check_code(out); check_prices(m, out)
    res = None
    if what in ("replay", "sens", "all"):
        res = do_replay(m, out)
    if what in ("sens", "all"):
        sens(m, out, res)
    print("\n".join(out))

if __name__ == "__main__":
    main()
