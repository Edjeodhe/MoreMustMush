"""Move the tree layout onto a picture that was drawn from its guide (2026-10-20).

The guide (make_guide.py --guide) placed the tree frame in a square canvas; the new drawing (gen_trees.sh) follows that
frame; install_tree_stages.py crops the drawing to its painted area. So every layout point can be carried over exactly:
picture fraction -> guide px -> drawing px -> fraction of the cropped drawing. The drawing only follows the guide roughly,
so check the result in the web editor afterwards (make_guide.py for overlays).

Each pic remembers the drawing it was mapped to ("src"), so running again does nothing; pass --force to redo.
usage: python Tools/tree-layout/remap_layout.py [--dry] [--force] [pic ...]   (then install_tree_stages.py, blend_tree_base.py)
"""
import os, sys
from PIL import Image
import tree_defs as T

CONCEPT = os.path.join(T.ROOT, "Art/Raw_Dream/concept")
S, M = 1254, 0.04          # make_guide.guide(): canvas and margin
MARGIN = 0.015             # install_tree_stages.py: crop margin (none under the base)


def frame(aspect):
    m = M * S
    if aspect >= 1:
        W = S - 2 * m; H = W / aspect
    else:
        H = S - 2 * m; W = H * aspect
    return (S - W) / 2, S - m - H, W, H


def crop_box(im):
    l, t, r, b = im.getchannel("A").point(lambda v: 255 if v > 12 else 0).getbbox()
    m = int(MARGIN * max(r - l, b - t))
    return max(0, l - m), max(0, t - m), min(im.width, r + m), b


def remap(p, log, force):
    pic = p["pic"]
    rel = f"v11/tree_v11_s{pic + 1}.png"
    path = os.path.join(CONCEPT, rel)
    if not os.path.exists(path):
        return
    if p.get("src") == rel and not force:
        log.append("  already mapped"); return
    im = Image.open(path).convert("RGBA")
    ox, oy, W, H = frame(p.get("aspect") or 1)
    k = im.width / S
    cl, ct, cr, cb = crop_box(im)

    def f(u, v):
        x, y = (ox + u * W) * k, (oy + v * H) * k
        return round((x - cl) / (cr - cl), 3), round((y - ct) / (cb - ct), 3)

    for n in p.get("nodes", []):
        n["u"], n["v"] = f(n["u"], n["v"])
    for pts in p.get("mush", {}).values():
        for q in pts:
            q[0], q[1] = f(q[0], q[1])
    for q in p.get("deco", {}).values():
        q[0], q[1] = f(q[0], q[1])
    old = p.get("aspect")
    p["aspect"] = round((cr - cl) / (cb - ct), 4)
    p["src"] = rel
    log.append(f"  {rel}: frame {W * k:.0f}x{H * k:.0f} at ({ox * k:.0f},{oy * k:.0f}) -> crop {cr - cl}x{cb - ct} at ({cl},{ct}), aspect {old} -> {p['aspect']}")


def main():
    want = [int(a) for a in sys.argv[1:] if not a.startswith("--")]
    data = T.load()
    for p in data["pics"]:
        if want and p["pic"] not in want:
            continue
        log = []
        remap(p, log, "--force" in sys.argv)
        print(f"stage {p['pic'] + 1}"); [print(l) for l in log]
    if "--dry" not in sys.argv:
        T.save(data)


if __name__ == "__main__":
    main()
