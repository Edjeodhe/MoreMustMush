"""Slice a cut-out parts sheet (one row per character, fixed part order) where the generator spaced items unevenly.

slice_sheet.py assigns blobs to an even grid, which fails when a wide part (a cap) pushes its neighbours across
the cell lines and rows nearly touch. Here, per row (row heights from a size-weighted k-means of the blobs):
  1. solid blobs whose centre is nearest that row are its pieces (more erosion if touching parts stay joined),
  2. while there are more pieces than `cols`, the two pieces with the smallest edge-to-edge gap are merged
     (a dew drop or wind curl rejoins its cap; a foot 30 px from the arm stays apart),
  3. the pieces are taken left to right in name order; soft halo pixels go to the nearest piece.

usage: python slice_rig.py <sheet.png> <rows> <cols> <out_dir> <name1,name2,...>
prints {"items": {...}, "warnings": [...]} like slice_sheet.py
"""
import json, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ALPHA_MIN = 24
CORE_ALPHA = 200
MIN_PIECE = 150     # solid blobs smaller than this are specks
PAD = 6
HALO_REACH = 24   # soft pixels further than this from any piece are generator noise


EDGE = 2            # soft edge kept outside the solid body (px)


def clean(crop, sel):
    """Tidy one cut-out: keep only this item, cut the generator's glow halo to a EDGE px soft edge, and give every
    semi-transparent pixel the colour of the nearest solid pixel (the sheet's coloured glow bled into the edges)."""
    a = np.where(sel, crop[:, :, 3], 0)
    solid = a >= 230
    if solid.any():
        dist, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
        a = np.where(dist <= EDGE, a, 0)
        soft = (a > 0) & ~solid
        crop[:, :, :3] = np.where(soft[..., None], crop[iy, ix, :3], crop[:, :, :3])
    crop[:, :, 3] = a
    crop[:, :, :3] = np.where((a == 0)[..., None], 0, crop[:, :, :3])
    return crop


def item_masks(arr, rows, cols, n_names, warnings=None):
    """One boolean mask per item, in reading order (also used by slice_fx.py for unevenly spaced FX sheets)."""
    names = range(n_names)
    warnings = [] if warnings is None else warnings
    h, w = arr.shape[:2]
    alpha = arr[:, :, 3]
    mask = alpha >= ALPHA_MIN
    core = mask & (alpha >= CORE_ALPHA)
    band = h / rows

    # row centres: size-weighted 1-D k-means of the solid blobs' heights, started from even bands, so the big
    # caps decide where a row sits even when the generator drifted the rows
    lab0, n0 = ndimage.label(core)
    s0 = np.bincount(lab0.ravel(), minlength=n0 + 1)
    c0 = ndimage.center_of_mass(core, lab0, range(1, n0 + 1))
    ys = np.array([c0[g - 1][0] for g in range(1, n0 + 1) if s0[g] >= MIN_PIECE])
    ws = np.array([s0[g] for g in range(1, n0 + 1) if s0[g] >= MIN_PIECE], float)
    centres = (np.arange(rows) + 0.5) * band
    for _ in range(20):
        k = np.abs(ys[:, None] - centres[None, :]).argmin(1)
        centres = np.array([np.average(ys[k == r], weights=ws[k == r]) if (k == r).any() else centres[r] for r in range(rows)])
    row_of = lambda y: int(np.abs(centres - y).argmin())

    cache = {}
    def pieces(erode, r):
        if erode not in cache:
            src = ndimage.binary_erosion(core, iterations=erode) if erode else core
            lab, n = ndimage.label(src)
            sizes = np.bincount(lab.ravel(), minlength=n + 1)
            cen = ndimage.center_of_mass(src, lab, range(1, n + 1))
            cache[erode] = (lab, sizes, cen)
        lab, sizes, cen = cache[erode]
        ps = [g for g in range(1, len(cen) + 1) if sizes[g] >= MIN_PIECE and row_of(cen[g - 1][0]) == r]
        return lab, ps

    plan = []
    want = lambda r: max(0, min(cols, len(names) - r * cols))   # items in this row (the last row may be short)
    # one erosion for the whole sheet (per-row levels could hand the same pixels to two rows): the least that
    # separates at least `cols` pieces in every row
    for erode in (0, 2, 4, 6, 8, 11, 14):
        if all(len(pieces(erode, r)[1]) >= want(r) for r in range(rows)):
            break
    for r in range(rows):
        lab, ps = pieces(erode, r)
        groups = [[p] for p in ps]
        n_r = want(r)
        if len(groups) > n_r:
            # edge-to-edge gaps between pieces, from each piece's distance map
            dist = {}
            for p in ps:
                d = ndimage.distance_transform_edt(lab != p)
                for q in ps:
                    if q != p:
                        dist[(p, q)] = float(d[lab == q].min())
            def gap(a, b):
                return min(dist[(p, q)] for p in a for q in b)
            while len(groups) > n_r:
                i, j = min(((i, j) for i in range(len(groups)) for j in range(i + 1, len(groups))), key=lambda ij: gap(groups[ij[0]], groups[ij[1]]))
                groups[i] += groups.pop(j)
        if len(groups) != n_r:
            warnings.append(f"row {r + 1}: {len(groups)} items, expected {n_r}")
        cx = lambda g: np.nonzero(np.isin(lab, g))[1].mean()
        groups.sort(key=cx)
        plan.append([np.isin(lab, g) for g in groups[:n_r]])

    # every opaque pixel (halo, eroded rim) goes to the nearest piece of any row
    owner = np.zeros((h, w), np.int32)
    k = 0
    for row in plan:
        for m in row:
            k += 1; owner[m] = k
    dist, (iy, ix) = ndimage.distance_transform_edt(owner == 0, return_indices=True)
    near = np.where(mask & (dist <= HALO_REACH), owner[iy, ix], 0)   # far-away specks belong to nobody
    out = []
    k = 0
    for row in plan:
        for _ in row:
            k += 1
            out.append(near == k)
    return out


def main():
    sheet, rows, cols, out_dir, names = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4], sys.argv[5].split(",")
    arr = np.asarray(Image.open(sheet).convert("RGBA"))
    h, w = arr.shape[:2]
    os.makedirs(out_dir, exist_ok=True)
    items, warnings = {}, []
    masks = item_masks(arr, rows, cols, len(names), warnings)
    mask = arr[:, :, 3] >= ALPHA_MIN
    for i, sel in enumerate(masks):
        if i >= len(names):
            break
        if not sel.any():
            warnings.append(f"{names[i]}: no pixels left")
            continue
        yy, xx = np.nonzero(sel)
        y0, y1, x0, x1 = max(yy.min() - PAD, 0), min(yy.max() + PAD + 1, h), max(xx.min() - PAD, 0), min(xx.max() + PAD + 1, w)
        crop = clean(arr[y0:y1, x0:x1].copy(), sel[y0:y1, x0:x1])
        ph, pw = (-crop.shape[0]) % 4, (-crop.shape[1]) % 4
        if ph or pw:
            crop = np.pad(crop, ((0, ph), (0, pw), (0, 0)), mode="constant")
        dest = os.path.join(out_dir, f"{names[i]}.png")
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        Image.fromarray(crop).save(dest)
        items[names[i]] = {"w": int(crop.shape[1]), "h": int(crop.shape[0]), "blobs": 1, "x": int(x0), "y": int(y0)}
    for nm in names:
        if nm not in items:
            warnings.append(f"{nm}: missing")
    print(json.dumps({"items": items, "warnings": warnings}, ensure_ascii=False))


if __name__ == "__main__":
    main()
