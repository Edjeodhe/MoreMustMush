"""Slice a generated sprite sheet (rows x cols grid on a transparent background) into trimmed PNGs.

Each opaque blob is assigned to the grid cell that holds most of its area, so items that spill a little
over a cell border stay whole, and detached bits (spots, sparkles) stay with their item.

usage: python slice_sheet.py <sheet.png> <rows> <cols> <out_dir> <name1,name2,...>
prints one JSON object: {"items": {name: {"w":..,"h":..,"blobs":..}}, "warnings": [...]}
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

ALPHA_MIN = 24      # alpha below this counts as background
CORE_ALPHA = 200    # alpha at or above this is solid body (used to tell items apart)
ROW_GAP = 6         # empty pixel rows that separate two rows of items
MIN_BLOB = 40       # blobs smaller than this many pixels are noise
PAD = 6


def opaque_mask(img):
    a = np.asarray(img)[:, :, 3]
    if (a < 250).mean() > 0.05:
        return a >= ALPHA_MIN
    # The generator sometimes returns an opaque background: treat colors close to the corners as background.
    rgb = np.asarray(img)[:, :, :3].astype(np.int16)
    corners = np.concatenate([rgb[:8, :8].reshape(-1, 3), rgb[:8, -8:].reshape(-1, 3),
                              rgb[-8:, :8].reshape(-1, 3), rgb[-8:, -8:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    dist = np.abs(rgb - bg).sum(axis=2)
    return dist > 60


def main():
    sheet, rows, cols, out_dir, names = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4], sys.argv[5].split(",")
    img = Image.open(sheet).convert("RGBA")
    h, w = img.height, img.width
    mask = opaque_mask(img)
    if np.asarray(img)[:, :, 3].min() == 255:
        arr = np.asarray(img).copy()
        arr[:, :, 3] = np.where(mask, 255, 0)
        img = Image.fromarray(arr)

    # Label only solid pixels: soft glows and halos are semi-transparent and would bridge neighbouring items.
    alpha = np.asarray(img)[:, :, 3]
    core = mask & (alpha >= CORE_ALPHA)

    # The generator sometimes swaps the grid (e.g. 5x4 instead of 4x5). Find the real row bands from empty
    # horizontal gaps; when they disagree with the requested rows, re-derive rows/cols from the item count.
    filled = np.nonzero(core.any(axis=1))[0]
    bands = []
    if len(filled):
        start = prev = filled[0]
        for y in filled[1:]:
            if y - prev > ROW_GAP:
                bands.append((start, prev)); start = y
            prev = y
        bands.append((start, prev))
        bands = [b for b in bands if b[1] - b[0] > h * 0.03]   # drop stray sparkles
    row_edges = None
    if len(bands) >= rows and len(bands) > 1:
        if len(bands) != rows:
            rows = len(bands)
            cols = -(-len(names) // rows)
        row_edges = [(bands[i][1] + bands[i + 1][0]) / 2 for i in range(len(bands) - 1)]
    cw, ch = w / cols, h / rows
    used = min(len(names), rows * cols)

    def row_of(yy):
        if row_edges is None:
            return np.minimum((yy / ch).astype(int), rows - 1)
        return np.searchsorted(np.array(row_edges), yy)

    def label_with(erode):
        # Eroding first breaks thin bridges where neighbouring items touch; a small dilation then rejoins
        # an item's own detached bits (spots, sparkles). Every opaque pixel goes to the nearest label.
        src = ndimage.binary_erosion(core, iterations=erode) if erode else core
        labels, n = ndimage.label(ndimage.binary_dilation(src, iterations=2))
        labels = np.where(src, labels, 0)
        if n == 0:
            return labels, {}
        _, (iy, ix) = ndimage.distance_transform_edt(labels == 0, return_indices=True)
        labels = np.where(mask, labels[iy, ix], 0)
        yy, xx = np.nonzero(labels)
        lab = labels[yy, xx]
        cell_idx = row_of(yy) * cols + np.minimum((xx / cw).astype(int), cols - 1)
        sizes = np.bincount(lab, minlength=n + 1)
        cell_of = {}
        for blob in np.nonzero(sizes >= MIN_BLOB)[0]:
            if blob == 0:
                continue
            cell_of[int(blob)] = (int(np.bincount(cell_idx[lab == blob]).argmax()), int(sizes[blob]))
        return labels, cell_of

    # Use the smallest erosion that gives every used cell its own item.
    best = None
    for erode in (0, 2, 4, 6, 8, 11, 14, 18):
        labels, cell_of = label_with(erode)
        empty = used - len({c for c, _ in cell_of.values() if c < used})
        if best is None or empty < best[0]:
            best = (empty, labels, cell_of)
        if empty == 0:
            break
    _, labels, cell_of = best

    os.makedirs(out_dir, exist_ok=True)
    items, warnings = {}, []
    arr = np.asarray(img)
    for i, name in enumerate(names):
        if i >= rows * cols:
            warnings.append(f"{name}: no cell (grid is {rows}x{cols})")
            continue
        blobs = [b for b, (c, _) in cell_of.items() if c == i]
        if not blobs:
            warnings.append(f"{name}: empty cell {i}")
            continue
        keep = np.isin(labels, blobs)
        ys, xs = np.nonzero(keep)
        y0, y1, x0, x1 = max(ys.min() - PAD, 0), min(ys.max() + PAD + 1, h), max(xs.min() - PAD, 0), min(xs.max() + PAD + 1, w)
        crop = arr[y0:y1, x0:x1].copy()
        crop[:, :, 3] = np.where(keep[y0:y1, x0:x1], crop[:, :, 3], 0)
        # Pad transparently instead of stretching. Block compression requires multiples of four.
        # Keep the crop origin and all existing pixels unchanged for rig metadata consumers.
        ph, pw = (-crop.shape[0]) % 4, (-crop.shape[1]) % 4
        if ph or pw:
            crop = np.pad(crop, ((0, ph), (0, pw), (0, 0)), mode="constant")
        Image.fromarray(crop).save(os.path.join(out_dir, f"{name}.png"))
        items[name] = {"w": int(crop.shape[1]), "h": int(crop.shape[0]), "blobs": len(blobs), "x": int(x0), "y": int(y0)}
    print(json.dumps({"items": items, "warnings": warnings}, ensure_ascii=False))


if __name__ == "__main__":
    main()
