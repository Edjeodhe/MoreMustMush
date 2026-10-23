"""Slice a decoration sprite sheet (8 columns x 3 rows, transparent background) into Farm/Props/<art>.png.

2026-10-20: the decorations were redrawn in the world tree's watercolor style on one sheet (ChatGPT image generation,
Art/Raw_Dream/concept/deco_sheet_v1.png), in the same order as the old props (alphabetical art keys, see ORDER).
Every painted blob is found by its alpha, small specks are dropped, and each blob goes to the grid cell its centre falls
in, so items drawn in several pieces (garland stars, chime drops) stay together. Each cell is cropped to its blobs,
faint alpha noise is cleared, and the result is scaled to at most MAX px. Old props are kept in Art/Raw_Dream/props_old.

Usage: python Tools/art/slice_deco_sheet.py <sheet.png> [--preview out.png]
"""
import os, shutil, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DST = os.path.join(ROOT, "Assets/Resources/Farm/Props")
OLD = os.path.join(ROOT, "Art/Raw_Dream/props_old")
ORDER = ["dew_chime", "dew_pond", "dew_spring", "fire_brazier", "fire_candle", "fire_lantern", "leaf_crown", "leaf_house",
         "leaf_lamp", "moon_crescent", "moon_garland", "moon_owl", "rock_geode", "rock_moss", "rock_table", "spark_bell",
         "spark_lamp", "spark_orb", "star_crown", "star_orb", "star_rune", "wind_catcher", "wind_mill", "wind_swing"]
COLS, ROWS = 8, 3
ALPHA = 16             # alpha below this counts as empty (generator noise)
SPECK = 60             # blobs smaller than this many px are dropped
MARGIN = 6             # px kept around each item
MAX = 320              # longest side of a saved prop


def main():
    sheet = Image.open(sys.argv[1]).convert("RGBA")
    a = np.asarray(sheet).copy()
    a[a[:, :, 3] < ALPHA, 3] = 0
    mask = a[:, :, 3] > 0
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=2))
    lab = lab * mask
    H, W = mask.shape
    cells = {}
    for i, sl in enumerate(ndimage.find_objects(lab), 1):
        if sl is None:
            continue
        blob = lab[sl] == i
        if blob.sum() < SPECK:
            continue
        cy, cx = ndimage.center_of_mass(blob)
        cy += sl[0].start; cx += sl[1].start
        cell = min(ROWS - 1, int(cy / H * ROWS)) * COLS + min(COLS - 1, int(cx / W * COLS))
        cells.setdefault(cell, []).append(i)
    os.makedirs(OLD, exist_ok=True)
    out = []
    for c, name in enumerate(ORDER):
        if c not in cells:
            print(name, "EMPTY CELL"); out.append(None); continue
        m = np.isin(lab, cells[c])
        ys, xs = np.where(m)
        y0, y1, x0, x1 = max(0, ys.min() - MARGIN), min(H, ys.max() + 1 + MARGIN), max(0, xs.min() - MARGIN), min(W, xs.max() + 1 + MARGIN)
        px = a[y0:y1, x0:x1].copy()
        px[~m[y0:y1, x0:x1], 3] = 0                       # only this cell's blobs (a neighbour's edge is cut off)
        im = Image.fromarray(px, "RGBA")
        if max(im.size) > MAX:
            k = MAX / max(im.size)
            im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        dst = os.path.join(DST, name + ".png")
        if os.path.exists(dst) and not os.path.exists(os.path.join(OLD, name + ".png")):
            shutil.copy2(dst, os.path.join(OLD, name + ".png"))
        im.save(dst)
        out.append(im)
        print(name, im.size, len(cells[c]), "blobs")
    if "--preview" in sys.argv:
        c = 200
        pv = Image.new("RGBA", (c * COLS, c * ROWS), (207, 216, 192, 255))
        for k, im in enumerate(out):
            if im is None:
                continue
            t = im.copy(); t.thumbnail((c - 12, c - 12))
            pv.alpha_composite(t, ((k % COLS) * c + (c - t.width) // 2, (k // COLS) * c + (c - t.height) // 2))
        pv.save(sys.argv[sys.argv.index("--preview") + 1])


main()
