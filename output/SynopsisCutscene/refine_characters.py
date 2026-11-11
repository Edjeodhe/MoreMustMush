from pathlib import Path
from zipfile import ZipFile, ZIP_STORED
import sys

sys.path.insert(0, str(Path.home() / "AppData/Local/Temp/mmm-cutout-deps"))
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageChops

import export_layers as e


SPECS = [
    (1, "03_landing_king", e.P1_KING, e.FULL, None),
    (1, "04_fleeing_people", e.P1_PEOPLE, e.FULL, None),
    (2, "panel_01/02_grandfather_harvesting", e.P2_HARVEST, e.P2_TL, None),
    (2, "panel_01/03_fleeing_people", e.P2_RUNNERS, e.P2_TL, None),
    (2, "panel_02/02_grandfather_turning", e.P2_TURN, e.P2_BL, None),
    (2, "panel_03/02_giant_mushrooms", e.P2_GIANTS, e.P2_R, e.union(e.P2_PLANTS, e.P2_GRANDPA)),
    (2, "panel_03/03_grandfather", e.P2_GRANDPA, e.P2_R, None),
    (3, "panel_01/02_grandfather_turning", e.P3_TURN, e.P3_TL, None),
    (3, "panel_02/02_grandfather_bowing", e.P3_BOW, e.P3_BL, None),
    (3, "panel_03/03_grandfather_with_mushroom", e.P3_JOY, e.P3_R, None),
    (4, "02_fleeing_mushrooms", e.P4_MUSHROOMS, e.FULL, None),
    (4, "03_grandfather_with_scythe", e.P4_GRANDPA, e.FULL, e.P4_BALLOON),
]


def silhouette(image, guide, bounds, *, inner=12, outer=18):
    # Use a wide uncertain band around the guide and infer the edge from the artwork.
    shape = np.array(guide.resize(image.size, Image.Resampling.BILINEAR)) >= 128
    inside = np.array(bounds.resize(image.size, Image.Resampling.NEAREST)) > 0
    shape &= inside
    labels = np.zeros(shape.shape, np.uint8)
    kernel = lambda radius: cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * radius + 1, 2 * radius + 1))
    initial = shape.astype(np.uint8)
    labels[cv2.dilate(initial, kernel(outer)).astype(bool) & inside] = cv2.GC_PR_BGD
    labels[shape] = cv2.GC_PR_FGD
    core = cv2.erode(initial, kernel(inner)).astype(bool)
    labels[core] = cv2.GC_FGD
    ys, xs = np.nonzero(labels)
    y0, y1 = max(0, ys.min() - 20), min(shape.shape[0], ys.max() + 21)
    x0, x1 = max(0, xs.min() - 20), min(shape.shape[1], xs.max() + 21)
    region = np.s_[y0:y1, x0:x1]
    local = np.ascontiguousarray(labels[region])
    model = lambda: np.zeros((1, 65), np.float64)
    pixels = np.ascontiguousarray(np.array(image)[region])
    cv2.grabCut(pixels, local, None, model(), model(), 6, cv2.GC_INIT_WITH_MASK)
    count, components, stats, _ = cv2.connectedComponentsWithStats(local % 2, 8)
    ids = np.unique(components[core[region]])
    keep = np.zeros(count, bool)
    keep[ids] = True
    # Keep small isolated details when they are substantially inside the hand guide.
    for i in range(1, count):
        area = stats[i, cv2.CC_STAT_AREA]
        if area >= 12 and np.count_nonzero(shape[region] & (components == i)) >= area * .6:
            keep[i] = True
    keep[0] = False
    alpha = np.zeros(shape.shape, np.uint8)
    alpha[region] = cv2.GaussianBlur(keep[components].astype(np.uint8) * 255, (3, 3), .6)
    return ImageChops.multiply(
        Image.fromarray(alpha).resize(e.SIZE, Image.Resampling.LANCZOS), bounds
    )


def main():
    cv2.setNumThreads(2)
    cv2.setRNGSeed(7)
    originals = []
    for attachment in e.ATTACHMENTS:
        with Image.open(Path.home() / "AppData/Local/Temp" / f"codex-clipboard-{attachment}.png") as image:
            originals.append(image.convert("RGB"))
    replacements = {}
    preview = Image.new("RGB", (1260, 960), "#c9cbd3")
    for i, (page, name, guide, bounds, occlusion) in enumerate(SPECS):
        original = originals[page - 1]
        alpha = silhouette(original, guide, bounds)
        rgb = np.array(original, dtype=np.float32)
        red, green, blue = rgb.transpose(2, 0, 1)
        if "fleeing_people" in name and page == 2:
            # The near silhouettes are dark navy; the small distant runner is lighter.
            distant = (np.indices(red.shape)[1] > 473) & (np.indices(red.shape)[1] < 593)
            lightness = (red + 2 * green + blue) / 4
            threshold = np.where(distant, 108, 81)
            retention = np.clip((threshold + 7 - lightness) / 12, 0, 1)
            retention = cv2.morphologyEx(retention.astype(np.float32), cv2.MORPH_CLOSE,
                                         cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9)))
        elif "fleeing_people" in name:
            retention = np.clip((13 - (green - blue)) / 6, 0, 1)
        elif page in (2, 3) and "with_mushroom" not in name:
            # Clear cool forest spill while keeping the warm neutrals of the outfit.
            retention = np.clip((13 - (blue - red)) / 7, 0, 1)
            if "bowing" in name:
                yy, xx = np.indices(red.shape)
                face_shadow = (xx > 143) & (xx < 510) & (yy > 595) & (yy < 774)
                retention[face_shadow] = 1
        else:
            retention = None
        if retention is not None:
            color_mask = Image.fromarray(np.uint8(retention * 255))
            color_mask = color_mask.resize(e.SIZE, Image.Resampling.LANCZOS)
            alpha = ImageChops.multiply(alpha, color_mask)
        if occlusion is not None:
            alpha = ImageChops.subtract(alpha, occlusion)
        key = f"page_{page:02}/{name}.png"
        result = original.resize(e.SIZE, Image.Resampling.LANCZOS).convert("RGBA")
        result.putalpha(alpha)
        target = e.ROOT / "RefinedCharacters" / key
        target.parent.mkdir(parents=True, exist_ok=True)
        result.save(target, dpi=(72, 72))
        replacements[key] = target
        crop = result.crop(alpha.getbbox())
        crop.thumbnail((390, 210))
        x, y = i % 3 * 420, i // 3 * 240
        ImageDraw.Draw(preview).text((x+10, y+8), key, fill="#232631")
        preview.paste(crop, (x+(420-crop.width)//2, y+25), crop)
        print(key, flush=True)
    preview.save(e.ROOT / "characters_refined_preview.jpg", quality=93)
    archive = e.ROOT / "MoreMustMush_Synopsis_Layers_v2.zip"
    with ZipFile(e.ROOT / "MoreMustMush_Synopsis_Layers.zip") as old, ZipFile(archive, "w", ZIP_STORED) as new:
        assert replacements.keys() <= set(old.namelist())
        for key in old.namelist():
            if key in replacements:
                new.write(replacements[key], key)
            else:
                new.writestr(key, old.read(key))
    with ZipFile(archive) as new:
        assert len(new.namelist()) == 25 and new.testzip() is None


if __name__ == "__main__":
    main()
