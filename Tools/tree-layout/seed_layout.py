"""First draft of Assets/Resources/Rig/tree_layout.json (2026-10-20), read off the current tree pictures.

- nodes/edges (spirit paths): placed by eye on the 10-20 bare trees (NAV below). Edge = [a, b, kind, art?]:
  kind walk/climb/hop drives the spirit animation; the optional art tells the image guide what to paint there
  (plank = wooden platform, ladder, bridge = rope bridge; none = along a branch/trunk, hop = shelf-mushroom steps).
- mush (tree mushroom spots per zone) and deco (one spot per deco unlocked by that stage) are picked automatically from
  the picture colours: bark for mushrooms, branch/canopy undersides for hanging decos, the ground for standing decos.
  Spots are ordered so the first n are spread out (the game uses the first n of each zone).

Overwrites tree_layout.json: only for a fresh start. After that, edit in the web editor (node Tools/tree-layout/serve.mjs).
usage: python Tools/tree-layout/seed_layout.py [--keep-nav]
"""
import colorsys, math, os, sys
from PIL import Image
import tree_defs as T

# by eye on Farm/Tree/tree_<pic>.png (u right, v down). g = ground entry, s = spot. Spirits climb from stage 4 (pic 3, WT.climbFrom)
NAV = {
    3: ("g0 .50 .95 g|l0 .46 .70|p1 .46 .61|p2 .36 .61 s|g1 .68 .92 g|s1 .61 .75 s|s2 .64 .67 s|s3 .63 .565 s|"
        "t1 .55 .40|b1 .80 .47 s|b2 .32 .36 s|t2 .52 .22 s",
        "g0 l0 climb|l0 p1 climb ladder|p1 p2 walk plank|g1 s1 climb|s1 s2 hop|s2 s3 hop|s3 t1 climb|p1 t1 climb|"
        "t1 b1 walk|t1 b2 walk|t1 t2 climb"),
    4: ("g0 .40 .90 g|l0 .43 .68|p1 .43 .565|p2 .33 .565 s|g1 .70 .86 g|s1 .68 .69 s|s2 .64 .645 s|s3 .58 .51 s|"
        "t1 .52 .40|b1 .22 .34 s|b2 .78 .38 s|t2 .55 .20 s",
        "g0 l0 climb|l0 p1 climb ladder|p1 p2 walk plank|p1 t1 climb|g1 s1 climb|s1 s2 hop|s2 s3 hop|s3 t1 climb|"
        "t1 b1 walk|t1 b2 walk|t1 t2 climb"),
    5: ("g0 .40 .88 g|s1 .46 .76 s|s2 .42 .71 s|l0 .36 .64|p1 .36 .565|p2 .27 .565 s|t1 .46 .55|g1 .62 .88 g|"
        "f2 .555 .61 s|f3 .575 .575 s|l1 .62 .46|p3 .62 .38|p4 .69 .38 s|t2 .50 .28|b1 .20 .36 s|b2 .82 .45 s",
        "g0 s1 climb|s1 s2 hop|s2 l0 climb|l0 p1 climb ladder|p1 p2 walk plank|p1 t1 walk plank|t1 f3 walk|"
        "g1 f2 climb|f2 f3 hop|f3 l1 climb|l1 p3 climb ladder|p3 p4 walk plank|p3 t2 climb|t2 b1 walk|p4 b2 walk"),
    6: ("g0 .30 .86 g|s1 .31 .69 s|s2 .36 .63 s|t1 .40 .50|l0 .34 .45|p1 .34 .365|p2 .29 .36 s|br .52 .40 s|"
        "p3 .72 .33|p4 .80 .32 s|l1 .83 .42|b2 .88 .48 s|g1 .80 .80 g|s3 .76 .56 s|s4 .71 .52 s|s5 .71 .47 s|t2 .55 .18 s",
        "g0 s1 climb|s1 s2 hop|s2 t1 climb|t1 l0 walk|l0 p1 climb ladder|p1 p2 walk plank|p1 br walk bridge|"
        "br p3 walk bridge|p3 p4 walk plank|p4 l1 climb ladder|l1 b2 walk|g1 s3 climb|s3 s4 hop|s4 s5 hop|s5 p3 climb|"
        "br t2 climb"),
    7: ("g0 .40 .90 g|m1 .39 .78 s|m2 .44 .72|m3 .56 .67|m4 .57 .635|m5 .60 .60 s|m6 .53 .56|m7 .48 .515 s|"
        "m8 .53 .43 s|lp .43 .555|p2 .33 .555 s|rl .62 .53|rp .62 .43|rp2 .70 .43 s|t .52 .30 s|b .82 .62 s",
        "g0 m1 climb|m1 m2 hop|m2 m3 hop|m3 m4 hop|m4 m5 hop|m5 m6 hop|m6 m7 hop|m7 m8 hop|m7 lp walk|"
        "lp p2 walk plank|m5 rl climb|rl rp climb ladder|rp rp2 walk plank|m8 t climb|m5 b walk"),
}


# spots the colour picker gets wrong, by eye: {pic: {deco: [u, v]}}
# stage 1 (sprout): hang from the two leaf undersides and the stem fork instead of the mossy mound
DECO_FIX = {0: {"leaf_lamp": [0.24, 0.40], "leaf_crown": [0.50, 0.50], "leaf_house": [0.76, 0.36]}}


def parse_nav(pic):
    if pic not in NAV:
        return [], []
    ns, es = NAV[pic]
    nodes = []
    for tok in ns.split("|"):
        p = tok.split()
        n = {"id": p[0], "u": float(p[1]), "v": float(p[2])}
        if "g" in p[3:]:
            n["ground"] = True
        if "s" in p[3:]:
            n["spot"] = True
        nodes.append(n)
    return nodes, [tok.split() for tok in es.split("|")]


def masks(im):
    """bark (brown) and opaque masks, sampled on a coarse grid."""
    W, H = im.size
    step = max(2, W // 160)
    px = im.load()
    pts = []
    for y in range(0, H, step):
        for x in range(0, W, step):
            r, g, b, a = px[x, y]
            if a < 200:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            wood = 0.04 <= h <= 0.12 and s > 0.30 and 0.30 < v < 0.90
            pts.append((x / W, y / H, wood))
    return pts, step / W, step / H


def opaque(im, u, v):
    W, H = im.size
    x, y = int(u * W), int(v * H)
    return 0 <= x < W and 0 <= y < H and im.getpixel((x, y))[3] >= 200


def wood_at(im, u, v):
    W, H = im.size
    x, y = int(u * W), int(v * H)
    if not (0 <= x < W and 0 <= y < H):
        return False
    r, g, b, a = im.getpixel((x, y))
    h, s, val = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    return a >= 200 and 0.04 <= h <= 0.12 and s > 0.30 and 0.30 < val < 0.90


def spread(cands, n, gap, start, aspect):
    """Farthest-point order from the candidate nearest `start`, keeping at least `gap` apart (gap shrinks if short)."""
    if not cands or n <= 0:
        return []
    d2 = lambda a, b: (a[0] - b[0]) ** 2 + ((a[1] - b[1]) / aspect) ** 2
    while True:
        out = [min(cands, key=lambda c: d2(c, start))]
        dist = [d2(c, out[0]) for c in cands]
        while len(out) < n:
            i = max(range(len(cands)), key=lambda i: dist[i])
            if dist[i] < gap * gap:
                break
            out.append(cands[i])
            dist = [min(dist[j], d2(cands[j], cands[i])) for j in range(len(cands))]
        if len(out) >= n or gap < 0.01:
            return [[round(c[0], 3), round(c[1], 3)] for c in out[:n]]
        gap *= 0.85


def seed_pic(pic):
    im = Image.open(os.path.join(T.TREE, f"tree_{pic}.png")).convert("RGBA")
    W, H = im.size
    aspect = W / H                                   # v distances scaled to u units (u * W == v * H in px)
    pts, du, dv = masks(im)
    width = T.WIDTH[pic]
    nodes, edges = parse_nav(pic) if T.climbs(pic) else ([], [])

    # ---- tree mushrooms: bark, by zone height (v from the bottom); crown = canopy top edge
    fr = T.fruit_px(pic) / width * 1.1
    zones = T.zone_slots(pic)
    band = {"root": (0.80, 0.97), "trunk": (0.50, 0.82), "branch": (0.22, 0.60), "crown": (0.02, 0.30)}
    mush = {}
    for z, n in zones.items():
        if n <= 0:
            continue
        lo, hi = band[z]
        if z == "crown":
            c = [(u, v) for u, v, w in pts if lo <= v <= hi and not opaque(im, u, v - 0.04)]
        elif z == "root":   # on the root bark around the trunk foot (standing decos take the ground further out)
            c = [(u, v) for u, v, w in pts if w and lo <= v <= hi and abs(u - 0.5) < 0.30]
            if len(c) < n * 4:
                c = [(u, v) for u, v, w in pts if lo <= v <= hi and abs(u - 0.5) < 0.30 and opaque(im, u, v + 0.03)]
        else:
            c = [(u, v) for u, v, w in pts if w and lo <= v <= hi and (z != "trunk" or abs(u - 0.5) < 0.22)]
        if len(c) < n:                                # small trees: any opaque point in the band
            c = [(u, v) for u, v, w in pts if lo <= v <= hi] or [(0.5, (lo + hi) / 2)]
        mush[z] = spread(c, n, fr, (0.5, (lo + hi) / 2), aspect)

    # ---- decos: hanging ones under branches/canopy, standing ones on the ground beside the trunk
    deco = {}
    ds = T.decos_for(pic)
    hang = [d for d in ds if d["hang"]]
    stand = [d for d in ds if not d["hang"]]
    if hang:
        hs = max(T.deco_px(d, pic) for d in hang) / width
        # undersides: bark with no bark right below (a branch to hang from), or the lower edge of the canopy
        bark = [(u, v) for u, v, w in pts if w and 0.18 <= v <= 0.66 and abs(u - 0.5) > 0.12 and not wood_at(im, u, v + dv * 1.5)]
        edge = [(u, v) for u, v, w in pts if 0.18 <= v <= 0.66 and abs(u - 0.5) > 0.12 and not opaque(im, u, v + dv * 1.5)]
        cand = bark if len(bark) >= len(hang) * 3 else edge + bark
        for d, p in zip(hang, spread(cand, len(hang), hs * 0.9, (0.3, 0.45), aspect)):
            deco[d["id"]] = p
    if stand:
        # ground line: lowest opaque v around the trunk foot; standing decos alternate left/right outward from the door
        foot = max((v for u, v, w in pts if abs(u - 0.5) < 0.3), default=0.95)
        x_l, x_r = 0.32, 0.68                         # outside the root mushrooms (|u - 0.5| < 0.30)
        for i, d in enumerate(stand):
            s = T.deco_px(d, pic) / width
            if i % 2 == 0:
                u = x_l - s * 0.55; x_l = u - s * 0.55
            else:
                u = x_r + s * 0.55; x_r = u + s * 0.55
            deco[d["id"]] = [round(u, 3), round(min(foot, 0.97) - 0.01 * (i % 3), 3)]
    deco.update(DECO_FIX.get(pic, {}))
    return {"pic": pic, "aspect": round(aspect, 4), "nodes": nodes, "edges": edges, "mush": mush, "deco": deco}


def main():
    keep = "--keep-nav" in sys.argv
    old = {p["pic"]: p for p in T.load()["pics"]} if keep and os.path.exists(T.LAYOUT) else {}
    pics = []
    for pic in range(T.STAGES):
        p = seed_pic(pic)
        if pic in old:
            p["nodes"], p["edges"] = old[pic].get("nodes", []), old[pic].get("edges", [])
        pics.append(p)
        print(pic, {z: len(v) for z, v in p["mush"].items()}, len(p["deco"]), "decos", len(p["nodes"]), "nodes")
    T.save({"note": "세계수 도면: 그림 비율 좌표 (u 오른쪽, v 아래). FarmTreeView.Nav.cs · Tools/tree-layout/README.md", "pics": pics})


if __name__ == "__main__":
    main()
