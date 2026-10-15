"""Cut the soft-watercolor grandpa (Assets/Art/Soft/Characters/Grandpa/grandpa_ref.png) into rig parts.

Same part names and rig.json layout as build_grandpa_rig.py, so GrandpaRig and its prefab work unchanged:
  body.png         whole character minus the scythe; where the handle crossed the sleeve the hole is filled
                   with the nearest colours
  scythe.png       handle + blade, drawn BEHIND the body and hinged at the wrist (the fist stays on the body);
                   the handle hidden behind the hand, beard and hat is painted in so no gap shows while it sways
  head_cover.png   empty (the soft rig needs no cover: the whole scythe sits behind the body)
  eyes_closed.png  blink patch over both eyes
  rig.json         each part's top-left in reference pixels + joint positions

Usage: python Tools/art/build_grandpa_rig_soft.py [--set soft|doodle] [--debug <dir>]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# Cut points per art set (reference pixels of that set's grandpa_ref.png). --set soft (default) | doodle
SETS = {
    "soft": dict(
        root="Assets/Art/Soft", FEET=(505, 1200), WRIST=(235, 690),
        HANDLE=[(25, 985), (235, 690), (575, 215), (705, 20)], HANDLE_HALF=34, LOWER_STOP=0.86, UPPER_START=0.06,
        BLADE_POLY=[(640, 0), (1084, 0), (1084, 560), (1000, 560), (760, 170), (700, 175), (640, 120)],
        CHARM_POLY=[(120, 840), (210, 840), (210, 1010), (120, 1010)],
        EYES=[(458, 518, 22, 30), (606, 497, 22, 30)]),
    "doodle": dict(
        root="Assets/Art/Doodle", FEET=(520, 1150), WRIST=(200, 700),
        HANDLE=[(30, 850), (200, 700), (640, 190), (770, 20)], HANDLE_HALF=34, LOWER_STOP=0.62, UPPER_START=0.10,
        BLADE_POLY=[(690, 0), (1104, 0), (1104, 560), (1030, 560), (820, 130), (760, 145), (690, 95)],
        CHARM_POLY=[(0, 0), (1, 0), (1, 1)],
        EYES=[(380, 465, 20, 28), (507, 477, 20, 28)]),
}
_set = sys.argv[sys.argv.index("--set") + 1] if "--set" in sys.argv else "soft"
_c = SETS[_set]
SRC = os.path.join(ROOT, _c["root"], "Characters/Grandpa/grandpa_ref.png")
OUT = os.path.join(ROOT, _c["root"], "Characters/Grandpa/Rig")
FEET, WRIST, HANDLE, HANDLE_HALF = _c["FEET"], _c["WRIST"], _c["HANDLE"], _c["HANDLE_HALF"]
LOWER_STOP, UPPER_START = _c["LOWER_STOP"], (2, _c["UPPER_START"])
BLADE_POLY, CHARM_POLY, EYES = _c["BLADE_POLY"], _c["CHARM_POLY"], _c["EYES"]


def seg_band(shape, a, b, half):
    m = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(m).line([a, b], fill=255, width=half * 2)
    for p in (a, b):
        ImageDraw.Draw(m).ellipse([p[0] - half, p[1] - half, p[0] + half, p[1] + half], fill=255)
    return np.asarray(m) > 0


def poly(shape, pts):
    m = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return np.asarray(m) > 0


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def nearest_fill(rgba, hole, source):
    _, (iy, ix) = ndimage.distance_transform_edt(~source, return_indices=True)
    out = rgba.copy()
    out[hole] = rgba[iy[hole], ix[hole]]
    return out


def save_part(arr, name, meta):
    img = Image.fromarray(arr.astype(np.uint8), "RGBA")
    box = img.getbbox() or (0, 0, 4, 4)
    x0, y0, x1, y1 = box
    # block compression wants multiples of four
    x1 = x0 + -(-(x1 - x0) // 4) * 4; y1 = y0 + -(-(y1 - y0) // 4) * 4
    img.crop((x0, y0, x1, y1)).save(os.path.join(OUT, name + ".png"))
    meta[name] = {"x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0}


def main():
    debug = sys.argv[sys.argv.index("--debug") + 1] if "--debug" in sys.argv else None
    os.makedirs(OUT, exist_ok=True)
    im = Image.open(SRC).convert("RGBA")
    rgba = np.asarray(im).copy()
    shape = rgba.shape[:2]
    opaque = rgba[:, :, 3] > 24

    # --- scythe mask: lower handle (butt .. fist), upper handle (above the hat), blade, leaf charm
    lower = seg_band(shape, HANDLE[0], lerp(HANDLE[0], HANDLE[1], LOWER_STOP), HANDLE_HALF)
    upper = seg_band(shape, lerp(HANDLE[2], HANDLE[3], UPPER_START[1]), HANDLE[3], HANDLE_HALF)
    sc_mask = opaque & (lower | upper | poly(shape, BLADE_POLY) | poly(shape, CHARM_POLY))

    # --- scythe part: real pixels over a painted-in handle along the whole centre line (hidden by the body)
    handle_col = rgba[ndimage.binary_erosion(lower, iterations=HANDLE_HALF - 12) & opaque][:, :3]
    col = tuple(int(c) for c in np.median(handle_col, axis=0)) if len(handle_col) else (120, 72, 40)
    fill = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(fill)
    d.line(HANDLE, fill=(70, 40, 25, 255), width=(HANDLE_HALF - 6) * 2, joint="curve")
    d.line(HANDLE, fill=col + (255,), width=(HANDLE_HALF - 14) * 2, joint="curve")
    sc = np.asarray(fill).copy()
    sc[sc_mask] = rgba[sc_mask]

    # --- body: everything else; the hole where the handle crossed the sleeve is closed and recoloured
    body_src = opaque & ~sc_mask
    closed = ndimage.binary_closing(body_src, structure=np.ones((3, 3)), iterations=HANDLE_HALF)
    hole = closed & ~body_src & lower
    body = rgba.copy()
    body[~body_src] = 0
    body = nearest_fill(body, hole, body_src)
    body[hole, 3] = 255

    # --- closed eyes: face-coloured ovals with a soft brown lid curve
    face = Image.new("RGBA", im.size, (0, 0, 0, 0))
    fd = ImageDraw.Draw(face)
    for cx, cy, rx, ry in EYES:
        skin = tuple(int(c) for c in np.median(rgba[cy - 6:cy + 6, cx - rx - 14:cx - rx - 6, :3].reshape(-1, 3), axis=0))
        fd.ellipse([cx - rx - 4, cy - ry - 4, cx + rx + 4, cy + ry + 4], fill=skin + (255,))
        fd.arc([cx - rx - 2, cy - 10, cx + rx + 2, cy + 14], 200, 340, fill=(80, 45, 30, 255), width=7)
    closed_eyes = np.asarray(face)

    meta = {"ref": {"w": im.width, "h": im.height}, "feet": list(FEET), "wrist": list(WRIST)}
    save_part(body, "body", meta)
    save_part(sc, "scythe", meta)
    save_part(np.zeros((4, 4, 4), np.uint8), "head_cover", meta)
    meta["head_cover"].update({"x": WRIST[0], "y": WRIST[1]})
    save_part(closed_eyes, "eyes_closed", meta)
    with open(os.path.join(OUT, "rig.json"), "w") as f:
        json.dump(meta, f, indent=1)

    if debug:
        os.makedirs(debug, exist_ok=True)
        bg = Image.new("RGBA", im.size, (170, 200, 170, 255))
        bg.alpha_composite(Image.fromarray(sc, "RGBA"))
        bg.alpha_composite(Image.fromarray(body.astype(np.uint8), "RGBA"))
        bg.save(os.path.join(debug, "rig_rest.png"))
        rot = Image.new("RGBA", im.size, (170, 200, 170, 255))
        for ang in (-4,):
            r = Image.fromarray(sc, "RGBA").rotate(ang, center=WRIST, resample=Image.BICUBIC)
            rot.alpha_composite(r)
        rot.alpha_composite(Image.fromarray(body.astype(np.uint8), "RGBA"))
        rot.alpha_composite(Image.fromarray(closed_eyes, "RGBA"))
        rot.save(os.path.join(debug, "rig_swayed_blink.png"))
        ov = np.zeros_like(rgba); ov[sc_mask] = (255, 0, 0, 140); ov[hole] = (0, 0, 255, 200)
        m = im.copy(); m.alpha_composite(Image.fromarray(ov, "RGBA")); m.save(os.path.join(debug, "rig_masks.png"))
    print(json.dumps(meta))


if __name__ == "__main__":
    main()
