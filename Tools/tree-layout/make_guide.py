"""Draw the tree layout (Assets/Resources/Rig/tree_layout.json).

  --check (default): on the current game picture, with the real deco sprites at game size
                     -> Logs/Screens/tree_layout_<pic>.png
  --guide          : image-generation guide for the stage -> Art/Raw_Dream/tree_guide/guide_s<stage>.png (+ .txt prompt)
                     square canvas like the Codex output; the gray frame is where the tree must fill (the picture's
                     crop box), the faint silhouette is the current tree, and colour marks say what to paint where:
                       brown bars   = wooden plank platforms (walk edges with art plank) · rope bridge (art bridge)
                       ladder marks = wooden ladders (art ladder)
                       lilac ovals  = shelf-mushroom steps (hop edges)
                       red boxes    = keep EMPTY: a hanging decoration is attached here (branch/canopy edge above the dot)
                       orange boxes = keep a flat clear patch of ground/platform: a standing decoration goes here
                       green dots   = bare bark where tree mushrooms grow (no painted props there)
                       blue boxes   = standing room for a spirit on each spot (tree_defs.spirit_px, feet on the node)
usage: python Tools/tree-layout/make_guide.py [--guide] [pic ...]      (pic = stage - 1)
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont
import tree_defs as T

OUT_CHECK = os.path.join(T.ROOT, "Logs/Screens")
OUT_GUIDE = os.path.join(T.ROOT, "Art/Raw_Dream/tree_guide")
EDGE = {"walk": (80, 230, 120), "climb": (255, 200, 40), "hop": (255, 90, 200)}
ZCOL = {"root": (150, 110, 60), "trunk": (60, 200, 90), "branch": (40, 170, 255), "crown": (200, 120, 255)}
DECO = {d["id"]: d for d in T.DECOS}

# what each stage should look like (2026-10-20 approved drawings; the new drawing keeps the same character)
STAGE_LOOK = {
    1: "a tiny sprout: two big round leaves on a short stem in a mossy mound with two small mushrooms",
    2: "a young sapling with a slender curved trunk and two leafy crowns",
    3: "a young tree with a sturdy trunk, three leafy crowns and two shelf mushrooms on the trunk",
    4: "a mushroom tree: thick trunk with an arched wooden door at the foot, shelf-mushroom steps, one small plank platform",
    5: "a mossy old tree: low and very wide, heavy hanging moss curtains, door at the foot, platform with ladder",
    6: "a rune sacred tree: tall twisted double trunk with glowing rune carvings, two plank platforms with ladders",
    7: "an ancient tree: huge blue-violet dome canopy, a waterfall pouring from the trunk into a pool, rope bridge between two platforms",
    8: "the arcana world tree: the biggest and richest, blue-violet-yellow canopy, a spiral of lilac shelf-mushroom steps up the trunk, plank platforms with ladders, a small waterfall at the foot, arched door",
}


def font(size):
    for f in ("C:/Windows/Fonts/malgun.ttf", "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def deco_box(d, at, pic, W, H, art=None):
    """(x0, y0, x1, y1) of the deco in picture px: hanging = top at the spot, standing = bottom at the spot."""
    long = T.deco_px(d, pic) / T.WIDTH[pic] * W * (at[2] if len(at) > 2 else 1)
    if art is not None:
        k = long / max(art.size)
        w, h = art.width * k, art.height * k
    else:
        w, h = long * 0.8, long
    x, y = at[0] * W, at[1] * H
    return (x - w / 2, y, x + w / 2, y + h) if d["hang"] else (x - w / 2, y - h, x + w / 2, y)


def dashed_rect(d, box, col, width=3, dash=14):
    x0, y0, x1, y1 = box
    for (a, b) in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
        L = max(abs(b[0] - a[0]), abs(b[1] - a[1]))
        n = max(1, int(L // dash))
        for i in range(0, n, 2):
            t0, t1 = i / n, min(1, (i + 1) / n)
            d.line([(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0), (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], fill=col, width=width)


def ladder(d, a, b, w, col):
    import math
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L * w / 2, dx / L * w / 2
    d.line([(a[0] + nx, a[1] + ny), (b[0] + nx, b[1] + ny)], fill=col, width=max(3, int(w / 6)))
    d.line([(a[0] - nx, a[1] - ny), (b[0] - nx, b[1] - ny)], fill=col, width=max(3, int(w / 6)))
    for i in range(1, int(L / (w * 0.6))):
        t = i * w * 0.6 / L
        c = (a[0] + dx * t, a[1] + dy * t)
        d.line([(c[0] + nx, c[1] + ny), (c[0] - nx, c[1] - ny)], fill=col, width=max(2, int(w / 8)))


def check(p):
    pic = p["pic"]
    tree = Image.open(os.path.join(T.TREE, f"tree_{pic}.png")).convert("RGBA")
    k = 1100 / tree.width
    tree = tree.resize((1100, int(tree.height * k)), Image.LANCZOS)
    W, H = tree.size
    pad = int(W * 0.45)                                   # standing decos may sit beside the picture
    bg = Image.new("RGBA", (W + pad * 2, H + 40), (40, 50, 70, 255))
    bg.alpha_composite(tree, (pad, 0))
    d = ImageDraw.Draw(bg)
    gy = H * p.get("foot", 1)                             # ground line = the picture's foot (sits on WT.y)
    d.line([(0, gy), (bg.width, gy)], fill=(120, 140, 110, 255), width=2)
    P = lambda u, v: (pad + u * W, v * H)
    f = font(16)
    for zone, pts in p.get("mush", {}).items():
        r = T.fruit_px(pic) / T.WIDTH[pic] * W / 2
        for i, (u, v) in enumerate(pts):
            x, y = P(u, v)
            d.ellipse([x - r, y - r * 0.7, x + r, y + r * 0.7], outline=ZCOL[zone] + (255,), width=3)
            d.text((x - 5, y - 9), str(i + 1), fill=ZCOL[zone] + (255,), font=f)
    for did, at in p.get("deco", {}).items():
        dd = DECO.get(did)
        if not dd:
            continue
        art = Image.open(os.path.join(T.PROPS, dd["art"] + ".png")).convert("RGBA")
        box = deco_box(dd, at, pic, W, H, art)
        x0, y0, x1, y1 = [int(round(c)) for c in box]
        a = art.resize((max(1, x1 - x0), max(1, y1 - y0)), Image.LANCZOS)
        bg.alpha_composite(a, (pad + x0, y0))
        x, y = P(at[0], at[1])
        d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=(255, 80, 80, 255) if dd["hang"] else (255, 170, 40, 255))
        d.text((pad + x0, (y1 if dd["hang"] else y0 - 18) + (2 if dd["hang"] else 0)), did, fill=(255, 255, 255, 255), font=f)
    pos = {n["id"]: P(n["u"], n["v"]) for n in p.get("nodes", [])}
    sw, sh = [s / T.WIDTH[pic] * W for s in T.spirit_px(pic)]   # a standing spirit on each spot (feet on the dot)
    for n in p.get("nodes", []):
        if n.get("spot"):
            x, y = pos[n["id"]]
            dashed_rect(d, (x - sw / 2, y - sh, x + sw / 2, y), (90, 200, 255, 255), 2, 8)
    for e in p.get("edges", []):
        if e[0] in pos and e[1] in pos:
            d.line([pos[e[0]], pos[e[1]]], fill=EDGE.get(e[2], (255, 255, 255)) + (255,), width=5)
    for n in p.get("nodes", []):
        x, y = pos[n["id"]]
        r = 9 if n.get("spot") else 6
        fill = (255, 255, 255, 255) if n.get("spot") else (60, 200, 255, 255) if n.get("ground") else (200, 200, 200, 200)
        d.ellipse([x - r, y - r, x + r, y + r], fill=fill, outline=(0, 0, 0, 255))
        d.text((x + 10, y - 18), n["id"], fill=(255, 255, 255, 255), font=f)
    d.text((10, 10), f"stage {pic + 1}  tree_{pic}  mush {sum(len(v) for v in p.get('mush', {}).values())}  deco {len(p.get('deco', {}))}", fill=(255, 255, 255, 255), font=font(22))
    os.makedirs(OUT_CHECK, exist_ok=True)
    out = os.path.join(OUT_CHECK, f"tree_layout_{pic}.png")
    bg.convert("RGB").save(out)
    return out


def guide(p, S=1254):
    """Square guide: the tree frame (picture aspect) sits bottom-centre with a margin, like the Codex output."""
    pic = p["pic"]
    aspect = p.get("aspect") or 1
    m = 0.04 * S
    if aspect >= 1:
        W = S - 2 * m; H = W / aspect
    else:
        H = S - 2 * m; W = H * aspect
    ox, oy = (S - W) / 2, S - m - H
    bg = Image.new("RGBA", (S, S), (255, 255, 255, 255))
    tree = Image.open(os.path.join(T.TREE, f"tree_{pic}.png")).convert("RGBA").resize((int(W), int(H)), Image.LANCZOS)
    a = tree.getchannel("A").point(lambda v: 60 if v > 40 else 0)
    sil = Image.new("RGBA", tree.size, (150, 160, 170, 255)); sil.putalpha(a)
    bg.alpha_composite(sil, (int(ox), int(oy)))
    d = ImageDraw.Draw(bg)
    dashed_rect(d, (ox, oy, ox + W, oy + H), (120, 120, 120, 255), 2, 10)
    P = lambda u, v: (ox + u * W, oy + v * H)
    unit = W / T.WIDTH[pic]                                # guide px per stage px
    sp = unit * T.ZOOM[pic]                                # spirit-scaled stage px (spirits grow with the camera zoom)
    nodes = {n["id"]: n for n in p.get("nodes", [])}
    for e in p.get("edges", []):
        if e[0] not in nodes or e[1] not in nodes:
            continue
        a_, b_ = P(nodes[e[0]]["u"], nodes[e[0]]["v"]), P(nodes[e[1]]["u"], nodes[e[1]]["v"])
        art = e[3] if len(e) > 3 else None
        if art in ("plank", "bridge"):
            d.line([a_, b_], fill=(140, 85, 40, 255), width=int(max(6, 8 * sp)))
            if art == "bridge":
                d.line([(a_[0], a_[1] - 18 * sp), (b_[0], b_[1] - 18 * sp)], fill=(140, 85, 40, 255), width=3)
        elif art == "ladder":
            ladder(d, a_, b_, max(12, 18 * sp), (160, 100, 50, 255))
        elif e[2] in ("walk", "climb"):                    # bark path: a branch top to walk on / trunk to climb
            d.line([a_, b_], fill=(185, 140, 95, 255), width=int(max(3, 4 * sp)))
    for n in p.get("nodes", []):
        x, y = P(n["u"], n["v"])
        hop = any(n["id"] in e[:2] and e[2] == "hop" for e in p.get("edges", []))
        if hop:
            rx, ry = 30 * sp, 9 * sp
            d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=(200, 160, 235, 255), outline=(150, 100, 200, 255), width=2)
    sw, sh = T.spirit_px(pic)                              # standing room for a spirit on each spot (feet on the dot)
    for n in p.get("nodes", []):
        if n.get("spot"):
            x, y = P(n["u"], n["v"])
            dashed_rect(d, (x - sw * unit / 2, y - sh * unit, x + sw * unit / 2, y), (40, 160, 230, 255), 3, 10)
    for zone, pts in p.get("mush", {}).items():
        r = max(4, T.fruit_px(pic) * unit * 0.25)
        for u, v in pts:
            x, y = P(u, v)
            d.ellipse([x - r, y - r, x + r, y + r], fill=(60, 190, 90, 255))
    for did, at in p.get("deco", {}).items():
        dd = DECO.get(did)
        if not dd:
            continue
        x0, y0, x1, y1 = deco_box(dd, at, pic, W, H)
        col = (230, 50, 50, 255) if dd["hang"] else (240, 150, 30, 255)
        dashed_rect(d, (ox + x0, oy + y0, ox + x1, oy + y1), col, 3)
        x, y = P(at[0], at[1])
        d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=col)
    os.makedirs(OUT_GUIDE, exist_ok=True)
    out = os.path.join(OUT_GUIDE, f"guide_s{pic + 1}.png")
    bg.convert("RGB").save(out)
    with open(out[:-4] + ".txt", "w", encoding="utf-8") as f:
        f.write(prompt(p))
    return out


def prompt(p):
    pic = p["pic"]
    edges = p.get("edges", [])
    n_plank = sum(1 for e in edges if len(e) > 3 and e[3] == "plank")
    n_ladder = sum(1 for e in edges if len(e) > 3 and e[3] == "ladder")
    n_bridge = sum(1 for e in edges if len(e) > 3 and e[3] == "bridge")
    hops = sorted({i for e in edges if e[2] == "hop" for i in e[:2]})
    hang = [d for d in T.decos_for(pic) if d["hang"]]
    lines = [
        f"World tree, growth stage {pic + 1} of 8: {STAGE_LOOK[pic + 1]}.",
        "Paint it in the same dreamy watercolor storybook style as the FIRST attached image (the current drawing of this stage): "
        "soft puffy foliage, warm bark, no outlines, isolated on a transparent background, nothing else in the picture.",
        "The SECOND attached image is a layout guide drawn over the same frame. Follow its POSITIONS exactly, but never paint the guide marks themselves:",
        "- The tree must fill the dashed gray frame (same width, height and ground line); the faint gray shape is the current silhouette — keep it similar.",
    ]
    if n_plank:
        lines.append(f"- Brown bars = wooden plank platforms with small railings ({n_plank} segments), exactly there.")
    if n_bridge:
        lines.append("- A double brown line = a rope bridge with wooden planks.")
    if n_ladder:
        lines.append(f"- Ladder marks = wooden ladders ({n_ladder}), exactly there.")
    if hops:
        lines.append(f"- Lilac ovals = lilac shelf mushrooms growing on the bark as steps ({len(hops)}), exactly there.")
    if any(len(e) < 4 and e[2] in ("walk", "climb") for e in edges):
        lines.append("- Thin light-brown lines = a bark path: a branch top a small character walks along, or trunk bark it climbs. "
                     "There must be a branch or trunk under each line.")
    if any(n.get("spot") for n in p.get("nodes", [])):
        lines.append("- Blue dashed boxes = standing room for a small character: the bottom edge sits on a platform floor, a mushroom step "
                     "or a thick branch top, and the box itself stays open (no branch, trunk or leaves crossing it).")
    lines.append("- Green dots = plain bark/moss surface (tree mushrooms are added by the game there; don't paint props on them).")
    if hang:
        lines.append(f"- Red dashed boxes ({len(hang)}) = keep these spaces EMPTY and see-through: a branch or the canopy edge should pass just "
                     "above each red dot (something can hang from it), but nothing is painted inside the box.")
    if any(not d["hang"] for d in T.decos_for(pic)):
        lines.append("- Orange dashed boxes = flat, clear mossy ground (or platform floor) at the orange dot, nothing standing there.")
    lines += [
        "NO decorations at all: no lanterns, flags, bunting, birdhouses, crystals, swings, chimes, signs, houses, stalls, "
        "potions, furniture, fences, text. Small mushrooms on the ground, flowers, vines and moss are fine.",
        "Same size and framing as the guide; one image.",
    ]
    return "\n".join(lines) + "\n"


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    want = [int(a) for a in args]
    for p in T.load()["pics"]:
        if want and p["pic"] not in want:
            continue
        print(guide(p) if "--guide" in sys.argv else check(p))


if __name__ == "__main__":
    main()
