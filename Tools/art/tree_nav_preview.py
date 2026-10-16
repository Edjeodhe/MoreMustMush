"""Draw Resources/Rig/tree_nav.json on the tree pictures to check it (Logs/Screens/tree_nav_<pic>.png).

tree_nav.json: per tree picture (Farm/Tree/tree_<pic>), the places spirits can stand on and how to get there, read off
the picture by eye: nodes in picture fractions (u right, v down; ground = an entry from the ground, spot = a place to
stay and play) and edges (walk along a platform/branch · climb a ladder/trunk/rope · hop up bracket-fungus steps).
FarmView turns them into stage px (FarmTreeView.NavPos) and walks the spirits along the shortest path.

usage: python Tools/art/tree_nav_preview.py [pic ...]
"""
import json, os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
NAV = os.path.join(ROOT, "Assets/Resources/Rig/tree_nav.json")
TREE = os.path.join(ROOT, "Assets/Resources/Art/Dream/Farm/Tree")
COL = {"walk": (80, 255, 120, 255), "climb": (255, 200, 40, 255), "hop": (255, 90, 200, 255)}


def main():
    data = json.load(open(NAV, encoding="utf-8"))["pics"]
    want = [int(a) for a in sys.argv[1:]]
    for p in data:
        if want and p["pic"] not in want:
            continue
        im = Image.open(os.path.join(TREE, f"tree_{p['pic']}.png")).convert("RGBA")
        k = 1400 / im.width
        im = im.resize((1400, int(im.height * k)), Image.LANCZOS)
        bg = Image.new("RGBA", im.size, (40, 50, 70, 255)); bg.alpha_composite(im)
        d = ImageDraw.Draw(bg)
        W, H = im.size
        pos = {n["id"]: (n["u"] * W, n["v"] * H) for n in p["nodes"]}
        for a, b, kind in p["edges"]:
            d.line([pos[a], pos[b]], fill=COL[kind], width=4)
        for n in p["nodes"]:
            x, y = pos[n["id"]]
            r = 9 if n.get("spot") else 6
            fill = (255, 255, 255, 255) if n.get("spot") else (60, 200, 255, 255) if n.get("ground") else (255, 255, 255, 120)
            d.ellipse([x - r, y - r, x + r, y + r], fill=fill, outline=(0, 0, 0, 255))
            d.text((x + 10, y - 16), n["id"], fill=(255, 255, 255, 255))
        out = os.path.join(ROOT, f"Logs/Screens/tree_nav_{p['pic']}.png")
        bg.save(out)
        print(out)


if __name__ == "__main__":
    main()
