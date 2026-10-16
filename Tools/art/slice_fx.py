"""Cut an FX sheet (rows x cols grid) into tintable effect textures.

Unlike slice_sheet.py this keeps soft alpha (glows, washes) and cuts by grid cell, then trims each item and fits
it into its target canvas so the shape fills it edge to edge (FX sprites are one world unit wide: PPU = width).
White-ish shapes are converted to white + alpha so code tints them with any colour.

usage: python slice_fx.py <sheet.png> <rows> <cols> <out_dir> <name[~][@WxH],...>
  ~ keeps the soft glow around the item (a bloom); without it faint halo pixels (alpha < HALO) are cut off
"""
import json, os, sys
import numpy as np
from PIL import Image

ALPHA_MIN = 6
HALO = 60


def main():
    sheet, rows, cols, out_dir, names = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4], sys.argv[5].split(",")
    img = Image.open(sheet).convert("RGBA")
    w, h = img.size
    cw, ch = w / cols, h / rows
    os.makedirs(out_dir, exist_ok=True)
    groups = None
    if os.environ.get("FX_GROUP"):
        # reuse slice_rig's grouping (blobs per row, closest gaps merged) to get one mask per item
        import importlib.util, tempfile
        spec = importlib.util.spec_from_file_location("slice_rig", os.path.join(os.path.dirname(os.path.abspath(__file__)), "slice_rig.py"))
        sr = importlib.util.module_from_spec(spec); spec.loader.exec_module(sr)
        groups = sr.item_masks(np.asarray(img), rows, cols, len(names))
    items, warnings = {}, []
    for i, spec in enumerate(names):
        name, _, size = spec.partition("@")
        soft = name.endswith("~")
        name = name.rstrip("~")
        tw, th = (int(v) for v in size.split("x")) if size else (256, 256)
        r, c = divmod(i, cols)
        if groups is not None:
            # unevenly spaced sheet: this item's own pixels (slice_rig grouping), not its grid cell
            if i >= len(groups):
                warnings.append(f"{name}: missing"); continue
            a = np.asarray(img).astype(np.float32).copy()
            a[:, :, 3] = np.where(groups[i], a[:, :, 3], 0)
        else:
            cell = img.crop((round(c * cw), round(r * ch), round((c + 1) * cw), round((r + 1) * ch)))
            a = np.asarray(cell).astype(np.float32)
        alpha = a[:, :, 3]
        if alpha.min() >= 250:   # opaque background came back: key out the corner colour
            corner = np.median(np.concatenate([a[:6, :6, :3].reshape(-1, 3), a[-6:, -6:, :3].reshape(-1, 3)]), axis=0)
            dist = np.abs(a[:, :, :3] - corner).sum(axis=2)
            alpha = np.clip((dist - 20) * 4, 0, 255)
        if not soft:
            alpha = np.where(alpha < HALO, 0, alpha)
        ys, xs = np.nonzero(alpha > ALPHA_MIN)
        if not len(ys):
            warnings.append(f"{name}: empty cell {i}"); continue
        crop = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()
        crop[:, :, 3] = alpha[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        # tintable: colour is dropped, the watercolour pigment texture stays as light grey levels on white
        lum = crop[:, :, :3].mean(axis=2)
        body = lum[crop[:, :, 3] > 128] if (crop[:, :, 3] > 128).any() else lum.ravel()
        lo, hi = np.percentile(body, 2), np.percentile(body, 98)
        level = 170 + np.clip((lum - lo) / max(1.0, hi - lo), 0, 1) * 85
        out = np.dstack([level] * 3 + [crop[:, :, 3]]).astype(np.uint8)
        im = Image.fromarray(out, "RGBA").resize((tw, th), Image.LANCZOS)
        dest = os.path.join(out_dir, name + ".png")
        im.save(dest)
        items[name] = {"w": tw, "h": th}
    print(json.dumps({"items": items, "warnings": warnings}))


if __name__ == "__main__":
    main()
