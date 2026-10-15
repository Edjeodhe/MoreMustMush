"""Fit each critter's separately drawn parts onto its design drawing and write the rig layout.

Inputs (Assets/Art/Doodle/Characters/Critters):
  <id>_ref.png                       the whole critter (design sheet slice)
  cap_<id>, body_<id>, blink_<id>, arm_<id>, foot_<id>.png   parts (critter_rig_a|b sheets)
Output: Assets/Resources/Rig/critter_rig.json read by CritterRig (rig units: the critter is 1.0 tall, feet line at
y = -0.5, x = 0 at the centre; part x/y = sprite centre, s = localScale for a 100 px-per-unit sprite).

Each part is matched by masked colour SSD over a range of scales (FFT correlation), the arm and foot also mirrored.
The arm's shoulder joint is the arm pixel nearest the body centre. A debug overlay per critter goes to --debug <dir>.

Usage: python Tools/art/build_critter_rig.py [--debug <dir>] [ids...]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy.signal import fftconvolve

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, "Assets/Art/Doodle/Characters/Critters")
OUT = os.path.join(ROOT, "Assets/Resources/Rig/critter_rig.json")
IDS = ["fire", "spark", "dew", "coin", "star", "leaf", "moon", "wind", "rock", "chest"]
PPU = 100
WORK = 256          # the reference is matched at this height (px)


def load(path):
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255
    return a


def resize(a, s):
    h, w = a.shape[:2]
    nw, nh = max(2, int(round(w * s))), max(2, int(round(h * s)))
    im = Image.fromarray((a * 255).astype(np.uint8), "RGBA").resize((nw, nh), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255


def bbox(alpha, t=0.5):
    ys, xs = np.nonzero(alpha > t)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def match(ref, part, scales, cover=None):
    """Best (score, scale, x, y) placing `part` (top-left x, y in ref px) by masked SSD of premultiplied colour.
    cover: optional ref-space weight that penalises part pixels landing on transparent ref pixels."""
    R = ref[:, :, :3] * ref[:, :, 3:4]
    RA = ref[:, :, 3]
    best = None
    for s in scales:
        p = resize(part, s)
        ph, pw = p.shape[:2]
        if ph >= ref.shape[0] or pw >= ref.shape[1]:
            continue
        m = (p[:, :, 3] > 0.5).astype(np.float32)
        n = m.sum()
        if n < 30:
            continue
        P = p[:, :, :3] * p[:, :, 3:4]
        mf = m[::-1, ::-1]
        # SSD(x, y) = sum m*R^2 - 2 sum m*R*P + sum m*P^2   (colour) + outside penalty
        ssd = np.zeros((ref.shape[0] - ph + 1, ref.shape[1] - pw + 1), np.float32)
        for c in range(3):
            r2 = fftconvolve(R[:, :, c] ** 2, mf, mode="valid")
            rp = fftconvolve(R[:, :, c], (m * P[:, :, c])[::-1, ::-1], mode="valid")
            ssd += r2 - 2 * rp + (m * P[:, :, c] ** 2).sum()
        outside = n - fftconvolve(RA, mf, mode="valid")          # part pixels over transparent reference
        score = (ssd + 1.5 * outside) / n
        iy, ix = np.unravel_index(np.argmin(score), score.shape)
        cand = (float(score[iy, ix]), s, int(ix), int(iy))
        if best is None or cand[0] < best[0]:
            best = cand
    return best


def fit(ref, part, base_scale, spread=0.35, mirror=False):
    scales = np.linspace(base_scale * (1 - spread), base_scale * (1 + spread), 15)
    out = match(ref, part, scales)
    out = (*out, False) if out else None
    if mirror:
        alt = match(ref, part[:, ::-1], scales)
        if alt and (out is None or alt[0] < out[0]):
            out = (*alt, True)
    # refine around the best scale
    if out:
        s0 = out[1]
        src = part[:, ::-1] if out[4] else part
        r = match(ref, src, np.linspace(s0 * 0.95, s0 * 1.05, 7))
        if r and r[0] <= out[0]:
            out = (*r, out[4])
    return out


ICON_PX = 300      # icon pixels per rig unit
ICON_UNITS = 1.2   # icon canvas spans -0.6..0.6 units around the rig origin (CritterIcon draws it at 1.2 * unitPx)


def compose(e, cid, capkind):
    """One-piece UI icon of the critter in its rest pose: icon_<id>.png (own cap) / icon_<id>_skin.png (skin cap)."""
    size = int(ICON_PX * ICON_UNITS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    def put(name, part, x, y, sc, mirror=False):
        path = os.path.join(DIR, f"{name}_{cid}.png")
        if not os.path.exists(path):
            return
        im = Image.open(path).convert("RGBA")
        f = sc * ICON_PX / PPU
        im = im.resize((max(1, int(im.width * f)), max(1, int(im.height * f))), Image.LANCZOS)
        if mirror:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        canvas.alpha_composite(im, (int(size / 2 + x * ICON_PX - im.width / 2), int(size / 2 - y * ICON_PX - im.height / 2)))

    f, b, a, c = e["foot"], e["body"], e["arm"], e["cap"]
    put("foot", f, f["x"], f["y"], f["s"]); put("foot", f, -f["x"], f["y"], f["s"], True)
    put("body", b, b["x"], b["y"], b["s"])
    put("arm", a, a["x"], a["y"], a["s"]); put("arm", a, -a["x"], a["y"], a["s"], True)
    put(capkind, c, c["x"], c["y"], c["s"])
    canvas.save(os.path.join(DIR, f"icon_{cid}{'_skin' if capkind == 'skin' else ''}.png"))


def main():
    args = sys.argv[1:]
    dbg = None
    if "--debug" in args:
        i = args.index("--debug"); dbg = args[i + 1]; del args[i:i + 2]
        os.makedirs(dbg, exist_ok=True)
    ids = args or IDS
    old = {}
    if os.path.exists(OUT):
        for k in json.load(open(OUT, encoding="utf-8"))["kinds"]:
            old[k["id"]] = k
    for cid in ids:
        ref_full = load(os.path.join(DIR, f"{cid}_ref.png"))
        x0, y0, x1, y1 = bbox(ref_full[:, :, 3])
        ref_full = ref_full[y0:y1, x0:x1]
        k = WORK / ref_full.shape[0]
        ref = resize(ref_full, k)
        H, W = ref.shape[:2]
        parts = {n: load(os.path.join(DIR, f"{n}_{cid}.png")) for n in ("cap", "body", "blink", "arm", "foot")}
        # parts share one scale (same sheet): estimate it from the cap's width vs the ref's top region width
        res = {}
        cap = parts["cap"]; cb = bbox(cap[:, :, 3])
        base = (W * 0.98) / (cb[2] - cb[0])
        res["cap"] = fit(ref, cap, base, spread=0.3)
        base = res["cap"][1]
        res["body"] = fit(ref, parts["body"], base, spread=0.25)
        res["blink"] = fit(ref, parts["blink"], base, spread=0.25)
        res["arm"] = fit(ref, parts["arm"], base, spread=0.3, mirror=True)
        res["foot"] = fit(ref, parts["foot"], base, spread=0.3, mirror=True)

        def centre(name):
            sc, s, x, y, flip = res[name]
            p = parts[name]
            h, w = int(round(p.shape[0] * s)), int(round(p.shape[1] * s))
            return x + w / 2, y + h / 2, s, flip, (x, y, w, h)

        def units(px, py):  # ref work px → rig units
            return (px - W / 2) / H, (H - py) / H - 0.5

        entry = {"id": cid}
        for name in ("cap", "body", "blink", "arm", "foot"):
            cx, cy, s, flip, _ = centre(name)
            ux, uy = units(cx, cy)
            entry[name] = {"x": round(ux, 4), "y": round(uy, 4), "s": round(s / H * PPU, 4)}
        # arm: keep it on the left side; the shoulder is the arm pixel nearest the body centre
        acx, acy, s, flip, (ax, ay, aw, ah) = centre("arm")
        bcx, bcy, *_ = centre("body")
        am = resize(parts["arm"][:, ::-1] if flip else parts["arm"], s)[:, :, 3] > 0.5
        ys, xs = np.nonzero(am)
        d = (xs + ax - bcx) ** 2 + (ys + ay - bcy) ** 2
        j = np.argmin(d)
        sx, sy = units(xs[j] + ax, ys[j] + ay)
        ux = entry["arm"]["x"]
        # the drawn arm is the left one if it sits left of the body centre; otherwise mirror the layout and the sprite
        drawn_left = acx < bcx
        if not drawn_left:
            entry["arm"]["x"] = -ux; sx = -sx
        entry["shoulderX"], entry["shoulderY"] = round(sx, 4), round(sy, 4)
        # flipping the art (part drawn mirrored vs the ref, or as the right arm) is done once here on the png
        if flip != (not drawn_left):
            Image.open(os.path.join(DIR, f"arm_{cid}.png")).transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(DIR, f"arm_{cid}.png"))
        fcx, *_ , fflip, _ = centre("foot")
        if entry["foot"]["x"] > 0:
            entry["foot"]["x"] = -entry["foot"]["x"]
            fflip = not fflip
        if fflip:
            Image.open(os.path.join(DIR, f"foot_{cid}.png")).transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(DIR, f"foot_{cid}.png"))
        # cap wobble pivot: a little above the cap's bottom edge
        _, _, _, _, (cx_, cy_, cw, ch) = centre("cap")
        entry["capJointY"] = round(units(0, cy_ + ch * 0.8)[1], 4)
        entry["scores"] = {n: round(res[n][0], 4) for n in res}
        old[cid] = entry
        print(cid, json.dumps(entry["scores"]))

        if dbg:
            im = Image.fromarray((ref * 255).astype(np.uint8)).convert("RGBA")
            ov = Image.new("RGBA", im.size, (0, 0, 0, 0))
            dr = ImageDraw.Draw(im)
            cols = {"cap": (255, 0, 0), "body": (0, 160, 0), "blink": (0, 0, 255), "arm": (255, 0, 255), "foot": (255, 160, 0)}
            for name, col in cols.items():
                _, _, _, _, (x, y, w, h) = centre(name)
                dr.rectangle([x, y, x + w, y + h], outline=col, width=2)
            # rebuilt critter from parts next to the reference
            canvas = Image.new("RGBA", (W * 2 + 10, H), (200, 230, 190, 255))
            canvas.alpha_composite(Image.fromarray((ref * 255).astype(np.uint8)), (0, 0))
            for name in ("foot", "body", "blink", "arm", "cap"):
                sc, s, x, y, flip = res[name]
                p = resize(parts[name][:, ::-1] if flip else parts[name], s)
                canvas.alpha_composite(Image.fromarray((p * 255).astype(np.uint8)), (W + 10 + x, y))
            canvas.alpha_composite(im, (0, 0))
            canvas.save(os.path.join(dbg, f"rig_{cid}.png"))
    for cid in ids:
        if cid in old:
            compose(old[cid], cid, "cap")
            compose(old[cid], cid, "skin")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    kinds = [old[i] for i in IDS if i in old]
    json.dump({"kinds": kinds}, open(OUT, "w", encoding="utf-8"), indent=1)
    print("wrote", OUT, len(kinds))


if __name__ == "__main__":
    main()
