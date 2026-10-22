"""World tree stages 1-8 from the 2026-10-20 watercolor redraw (Codex, Art/Raw_Dream/concept/tree_v10_s1..7 +
tree_v9_c = stage 8, user-approved) -> Art/Raw_Dream/tree_src/tree_0..7.png, then blend_tree_base.py makes the game
sprites in Assets/Resources/Art/Dream/Farm/Tree.

Each drawing is cropped to its painted area (small margin) and resized to about twice its on-screen width. Each stage's
on-stage HEIGHT follows the camera zoom (see TOP/ZOOM below) and WT.width comes from the drawing's own proportions;
paste the printed WT.width and WT.zoom arrays into Defs.Farm.cs.

Usage: python Tools/art/install_tree_stages.py && python Tools/art/blend_tree_base.py
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONCEPT = os.path.join(ROOT, "Art/Raw_Dream/concept")
SRC = os.path.join(ROOT, "Art/Raw_Dream/tree_src")
FILES = [f"tree_v10_s{i}.png" for i in range(1, 8)] + ["tree_v9_c.png"]
# 2026-10-20: the trees go in bare (concept/bare/bare_s<n>.png: lanterns, flags, birdhouse, swings, crystals, cottage,
# stall cut out) because decorations are now separate props placed in deco slots (Farm/Props, FarmTreeView.DrawDecos).
# Platforms and ladders stay: they are the spirits' climbing paths (tree_layout.json). Stages 1-2 have nothing to cut.
FILES = [f"bare/bare_s{i + 1}.png" if os.path.exists(os.path.join(CONCEPT, f"bare/bare_s{i + 1}.png")) else f
         for i, f in enumerate(FILES)]
# 2026-10-20: stages redrawn from the tree layout guide (Tools/tree-layout/gen_trees.sh) win over everything above.
# The layout points are picture fractions, so after installing re-check them in the web editor (Tools/tree-layout).
FILES = [f"v11/tree_v11_s{i + 1}.png" if os.path.exists(os.path.join(CONCEPT, f"v11/tree_v11_s{i + 1}.png")) else f
         for i, f in enumerate(FILES)]
# 2026-10-20 (user: size and camera should grow together, naturally): the camera widens ~13% a stage and the tree top
# climbs an easing curve of the screen height (from the bottom; the trunk foot WT.y=720 is 360 px above it), so each
# stage is clearly bigger without the old sudden 74% -> 95% jump at stage 8. Paste the printed zoom too.
ZOOM = [1, 1.12, 1.26, 1.42, 1.6, 1.8, 2.02, 2.28]
TOP = [0.50, 0.54, 0.58, 0.63, 0.68, 0.74, 0.81, 0.92]
HEIGHT = [t * 1080 * z - 360 for t, z in zip(TOP, ZOOM)]   # tree height in stage px
MARGIN = 0.015

widths = []
for i, f in enumerate(FILES):
    if not os.path.exists(os.path.join(CONCEPT, f)):                        # not drawn yet: keep the old drawing
        im = Image.open(os.path.join(SRC, f"tree_{i}.png"))
        widths.append(round(HEIGHT[i] * im.width / im.height))
        print(f"tree_{i}", "missing", f, "-> old drawing kept")
        continue
    im = Image.open(os.path.join(CONCEPT, f)).convert("RGBA")
    l, t, r, b = im.getchannel("A").point(lambda v: 255 if v > 12 else 0).getbbox()
    m = int(MARGIN * max(r - l, b - t))
    im = im.crop((max(0, l - m), max(0, t - m), min(im.width, r + m), b))   # no margin under the base: it sits on the ground
    w = round(HEIGHT[i] * im.width / im.height)
    widths.append(w)
    px = min(2048, max(512, round(w / ZOOM[i] * 2)))                        # ~2x its width on a 1080p screen
    if im.width > px:
        im = im.resize((px, round(im.height * px / im.width)), Image.LANCZOS)
    im.save(os.path.join(SRC, f"tree_{i}.png"))
    print(f"tree_{i}", im.size, f"height {HEIGHT[i]:.0f} -> width {w}")
print("WT.zoom = {", ", ".join(f"{z}f" for z in ZOOM), "}")
print("WT.width = {", ", ".join(str(w) for w in widths), "}")
