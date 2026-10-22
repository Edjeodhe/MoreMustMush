"""Fit the spirit paths in tree_layout.json to the spirit size and the tree size.

0. foot: the picture line that sits on the ground (tree_defs.measure_foot), so the faded base band goes into the ground.
A standing spirit is tree_defs.spirit_px(pic) (110 x 155 stage px x zoom^0.75); the tree is WT.width wide. Per stage:
  1. spots (node.spot) must not overlap: two spirits standing on them may not cover each other
     (|dx| < 0.9 width and |dy| < 0.8 height = overlap). A shelf-mushroom step spot is turned into a pass-through step
     when there are spots to spare; otherwise the spot farther from the trunk is pushed outward.
  2. plank platforms are at least 1.3 spirit widths long (the outer end is pushed along the plank).
  3. there are at least as many spots as spirits that can be on the farm by that stage (warned, not invented).
The game lets one spirit stand on one node (FarmView.TakenNav), so non-overlapping spots = spirits never stack.

  4. every tree mushroom is pickable: from the ground, or (stage 4+) from a path node (fit_mush)
  Stages before WT.climbFrom (4) have no path at all.
usage: python Tools/tree-layout/fit_spots.py [--dry] [pic ...]
"""
import sys
import tree_defs as T

GAP_X, GAP_Y = 0.9, 0.8          # overlap if closer than this many spirit widths / heights
PLANK_MIN = 1.3                  # plank platform length in spirit widths


def fit_mush(p, log):
    """Every tree mushroom must be pickable: from the ground (low enough) or, from stage 4, from a path node.
    Before climbing (stages 1-3) high mushrooms are lowered to ground reach. From stage 4 a mushroom out of reach is
    nudged into reach of the nearest node when that moves it <= half a spirit height, otherwise a pass-through node is added under it
    and linked to the nearest node (walk if mostly sideways, climb if mostly up) — a bark path the new drawing must have."""
    pic = p["pic"]
    aspect = p.get("aspect") or 1
    W = T.WIDTH[pic]; H = T.tree_height(pic, aspect)
    sw, sh = T.spirit_px(pic)
    foot = p.get("foot", 1)                                          # picture line that sits on the tree foot WT.y
    S = lambda u, v: ((u - 0.5) * W, T.WT_Y - (foot - v) * H)       # picture fraction -> stage px
    UVf = lambda x, y: (round(x / W + 0.5, 3), round(foot - (T.WT_Y - y) / H, 3))
    ground_y = T.WT_Y - 0.05 * H - 1.1 * sh                        # FarmTreeView.GroundFeetY (root mound) + reach
    nodes = [n for n in p.get("nodes", []) if not n.get("ground")] if T.climbs(pic) else []
    for zone, pts in p.get("mush", {}).items():
        for i, (u, v) in enumerate(pts):
            x, y = S(u, v)
            if y >= ground_y:
                continue
            if not nodes:
                pts[i] = list(UVf(x, ground_y + 2))
                log.append(f"  mush {zone} {i + 1}: lowered to ground reach (v {v} -> {pts[i][1]})")
                continue
            feet = {n["id"]: S(n["u"], n["v"]) for n in nodes}
            if any(T.reach(f, (x, y), pic) for f in feet.values()):
                continue
            nid = min(feet, key=lambda k: (feet[k][0] - x) ** 2 + (feet[k][1] - y) ** 2)
            fx, fy = feet[nid]
            nx = min(max(x, fx - 0.7 * sw), fx + 0.7 * sw)
            ny = min(max(y, fy - 1.0 * sh), fy + 0.2 * sh)
            if ((nx - x) ** 2 + (ny - y) ** 2) ** 0.5 <= 0.5 * sh:     # a small nudge keeps it where it was drawn
                pts[i] = list(UVf(nx, ny))
                log.append(f"  mush {zone} {i + 1}: pulled next to node {nid}")
            else:
                k = 1
                while any(n["id"] == f"w{k}" for n in p["nodes"]):
                    k += 1
                wu, wv = UVf(x, y + 0.6 * sh)                          # feet under the mushroom: it sits at hand height
                node = {"id": f"w{k}", "u": wu, "v": wv}
                p["nodes"].append(node); nodes.append(node)
                kind = "walk" if abs(fx - x) > abs(fy - (y + 0.6 * sh)) else "climb"
                p["edges"].append([nid, node["id"], kind])
                log.append(f"  mush {zone} {i + 1}: out of reach -> new path node w{k} ({kind} from {nid})")


def fit(p, log):
    pic = p["pic"]
    foot = T.measure_foot(pic)                                # 0. the picture's ground line (FarmTreeView sinks the faded band)
    if p.get("foot") != foot:
        log.append(f"  foot {p.get('foot', 1)} -> {foot}")
        p["foot"] = foot
    if not T.climbs(pic) and (p.get("nodes") or p.get("edges")):
        p["nodes"], p["edges"] = [], []
        log.append(f"  no climbing before stage {T.CLIMB_FROM}: path removed")
    fit_mush(p, log)
    nodes = {n["id"]: n for n in p.get("nodes", [])}
    if not nodes:
        return
    W = T.WIDTH[pic]; H = T.tree_height(pic, p.get("aspect") or 1)
    sw, sh = T.spirit_px(pic)
    wu, hv = sw / W, sh / H                                   # spirit size in picture fractions
    edges = p.get("edges", [])
    deg = {i: 0 for i in nodes}
    for e in edges:
        if e[0] in deg and e[1] in deg:
            deg[e[0]] += 1; deg[e[1]] += 1
    hop = {i for e in edges if e[2] == "hop" for i in e[:2]}

    # 2. plank platforms long enough: push the dead end (one link) along the plank; planks between two junctions stay
    for e in edges:
        if len(e) < 4 or e[3] != "plank" or e[0] not in nodes or e[1] not in nodes:
            continue
        if deg[e[0]] != 1 and deg[e[1]] != 1:
            continue
        a, b = nodes[e[0]], nodes[e[1]]
        if deg[e[0]] == 1 and deg[e[1]] != 1:
            a, b = b, a                                       # b = dead end
        dx, dy = (b["u"] - a["u"]) * W, (b["v"] - a["v"]) * H
        L = (dx * dx + dy * dy) ** 0.5 or 1
        need = PLANK_MIN * sw
        if L < need - 2:                                      # 2 px slack for the 3-decimal rounding
            b["u"] = round(a["u"] + dx / L * need / W, 3); b["v"] = round(a["v"] + dy / L * need / H, 3)
            log.append(f"  plank {e[0]}-{e[1]}: {L:.0f}px -> {need:.0f}px ({b['id']} moved)")

    # 1. spots apart
    need_spots = T.kids_at(pic)
    for _ in range(50):
        spots = [n for n in p["nodes"] if n.get("spot")]
        clash = None
        for i in range(len(spots)):
            for j in range(i + 1, len(spots)):
                a, b = spots[i], spots[j]
                if abs(a["u"] - b["u"]) < GAP_X * wu and abs(a["v"] - b["v"]) < GAP_Y * hv:
                    clash = (a, b); break
            if clash:
                break
        if not clash:
            break
        a, b = clash
        far = a if abs(a["u"] - 0.5) >= abs(b["u"] - 0.5) else b
        near = b if far is a else a
        if len(spots) > need_spots and (far["id"] in hop or near["id"] in hop):
            step = far if far["id"] in hop else near
            del step["spot"]
            log.append(f"  spot {step['id']} overlaps {(near if step is far else far)['id']}: now a pass-through step")
            continue
        side = 1 if far["u"] > near["u"] or (far["u"] == near["u"] and far["u"] >= 0.5) else -1
        old = far["u"]
        far["u"] = round(near["u"] + side * (GAP_X * wu + 0.002), 3)   # a hair past the limit (rounding)
        log.append(f"  spot {far['id']} overlaps {near['id']}: u {old} -> {far['u']}")

    # 3. enough spots
    n = sum(1 for x in p["nodes"] if x.get("spot"))
    if n < need_spots:
        log.append(f"  ! only {n} spots for {need_spots} spirits: add spots in the editor")


def main():
    dry = "--dry" in sys.argv
    want = [int(a) for a in sys.argv[1:] if not a.startswith("--")]
    data = T.load()
    for p in data["pics"]:
        if want and p["pic"] not in want:
            continue
        log = []
        fit(p, log)
        sw, sh = T.spirit_px(p["pic"])
        if not T.climbs(p["pic"]):
            print(f"stage {p['pic'] + 1}: no climbing"); [print(l) for l in log]; continue
        print(f"stage {p['pic'] + 1}: spirit {sw:.0f}x{sh:.0f}px = {sw / T.WIDTH[p['pic']]:.2f} of the tree width, "
              f"{sum(1 for x in p.get('nodes', []) if x.get('spot'))} spots / {T.kids_at(p['pic'])} spirits")
        for line in log:
            print(line)
    if not dry:
        T.save(data)


if __name__ == "__main__":
    main()
