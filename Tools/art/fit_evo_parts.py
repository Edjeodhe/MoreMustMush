"""Rig the evolving mushroom spirits (Characters/Evo/e_<id>_<form>_*) and write Assets/Resources/Rig/evo_rig.json.

Forms (= awakening ★): 0 plain mushroom · 1 mushroom critter · 2 mushroom child · 3 humanoid mushroom spirit.
  ★0·★1  one BODY sprite = the cap and the stem/body in one piece (the cap is never cut off: cutting it meant carving a
         socket under each cap by hand, see Assets/인계/2026-10-15_몽환수채화_2.md). ★1's stubby arms are painted on it.
  ★2·★3  HEAD (face · hair · cap) on the TORSO's neck, two-piece ARMs (upper/lower, elbow) on the torso's shoulder nubs.
Joints are found as exact points on the parts themselves (fit_spirit_parts.py does the same for the old spirits):
  neck      torso: the stub at the top of the body · head: the stub under the chin (searched below the face skin);
            the two stubs' bottoms meet (the head's stub covers the torso's), the head turns about the torso stub's middle
  shoulder  torso: circle fitted to the round shoulder nub on each side (three-quarter view: each side on its own)
            arm  : circle fitted to the round shoulder end → ball joint (arm scaled so both circles match)
  elbow     the arm is cut in the middle into _up / _lo, both keeping a disc of pixels round the elbow
Faces (build_spirit_faces.split): the dot eyes and mouth are lifted off the body (★0·★1) or the head (★2·★3) into a face
layer on the same canvas, leaving a bare sprite (_bare); expressions come from the shared sheet `evo_faces`
(Art/Raw_Dream/faces/fe_<expr>.png), scaled to the neutral face's eye span and centred on it.
Size: each form is scaled (k) to its height in the approved evolution line (Characters/Evo/evo_<id>_<form>), so the
spirit visibly grows from form to form; positions are part pixels / 100, origin at the feet (bottom centre / neck x).

Per-part fixes in Tools/art/evo_rig_fix.json: {"<id>_<form>": {"neck": [dx, dy], "head": [dx, dy],
"shoulderL": [x, y, r], "shoulderR": [x, y, r], "armRest": deg, "armScale": mul}} (part pixels, y down).

usage: python Tools/art/fit_evo_parts.py [id ...] [--preview]   (preview: Logs/Screens/evo_fit_<id>.png + evo_joints_<id>.png)
"""
import glob, json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fit_spirit_parts as SP          # joint finders
import build_spirit_faces as FACES     # face split

ROOT = SP.ROOT
EVO = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/Evo")
OUT = os.path.join(ROOT, "Assets/Resources/Rig/evo_rig.json")
FIX = os.path.join(ROOT, "Tools/art/evo_rig_fix.json")
GEN = os.path.join(ROOT, "Art/Raw_Dream/faces")
PPU = 100.0
EXPRS = FACES.EXPRS


def path(name):
    return os.path.join(EVO, name + ".png")


def load(name):
    return np.asarray(Image.open(path(name)).convert("RGBA")).astype(np.float32)


def save(a, name):
    Image.fromarray(np.uint8(np.clip(a, 0, 255))).save(path(name))


def opaque(img):
    return SP.biggest(img[:, :, 3] > 100)


def faces_for(kid, form, part):
    """Split the face off `part` (body or head): writes <part>_bare and e_<id>_<form>_face_<expr>. → (prefix, eyes)."""
    img = load(part)
    eyes = FACES.find_eyes(img)
    skin = FACES.skin_near_eyes(img, eyes) if eyes is not None else np.array([240, 220, 200], np.float32)
    bare, neutral = FACES.split(img, skin)
    save(bare, part + "_bare")
    prefix = f"e_{kid}_{form}_face_"
    save(neutral, prefix + "normal")
    gens = {e: FACES.clean(FACES.load(p)) for e in EXPRS if os.path.exists(p := os.path.join(GEN, f"fe_{e}.png"))}
    if gens and (neutral[:, :, 3] > 100).any():
        nw = FACES.obox(neutral)[2] - FACES.obox(neutral)[0] + 1
        for e, g in gens.items():
            gw = FACES.obox(g)[2] - FACES.obox(g)[0] + 1
            save(FACES.place(g, neutral, nw / gw), prefix + e)
    return prefix, eyes


def line_height(kid, form):
    p = path(f"evo_{kid}_{form}")
    if not os.path.exists(p):
        return None
    a = opaque(np.asarray(Image.open(p).convert("RGBA")).astype(np.float32))
    return float(SP.bbox(a)[3] - SP.bbox(a)[1] + 1)


def rig_headless(kid, form, fix):
    name = f"e_{kid}_{form}_body"
    if not os.path.exists(path(name)):
        return None, None
    img = load(name)
    a = opaque(img)
    x0, y0, x1, y1 = SP.bbox(a)
    H, W = img.shape[:2]
    cx = (x0 + x1) / 2
    face, eyes = faces_for(kid, form, name)
    rig = lambda px, py: [round((px - cx) / PPU, 4), round((y1 - py) / PPU, 4)]
    res = {"id": f"{kid}_{form}", "kind": kid, "form": form, "face": face,
           "body": dict(zip(("x", "y"), rig(W / 2, H / 2)), sprite=name, bare=name + "_bare"),
           "height": round((y1 - y0 + 1) / PPU, 4)}
    return res, {"body": (name, eyes)}


def rig_headed(kid, form, fix):
    names = {k: f"e_{kid}_{form}_{k}" for k in ("head", "torso", "arm", "hair", "leg")}
    if not all(os.path.exists(path(names[k])) for k in ("head", "torso", "arm")):
        return None, None
    has_hair, has_leg = os.path.exists(path(names["hair"])), os.path.exists(path(names["leg"]))
    f = fix.get(f"{kid}_{form}", {})
    T = load(names["torso"])
    ta = opaque(T)
    stub = SP.stub_down(ta)
    nubs = {}
    for side, key in ((-1, "shoulderL"), (1, "shoulderR")):
        nubs[side] = tuple(f[key]) if key in f else SP.shoulder_nub(ta, stub, side)
        if nubs[side] is None:      # last resort: beside the stub, a little down
            nubs[side] = (stub[0] + side * stub[2] * 1.3, stub[4] + stub[2] * 0.5, stub[2] * 0.6)
    neck = (stub[0] + f.get("neck", [0, 0])[0], stub[1] + f.get("neck", [0, 0])[1])

    Hd = load(names["head"])
    ha = opaque(Hd)
    face, eyes = faces_for(kid, form, names["head"])
    col = FACES.skin_near_eyes(Hd, eyes) if eyes is not None else SP.stub_colour(T, ta, stub)
    hb = head_stub_bottom(ha, Hd, col, stub[2], eyes)
    chin = head_chin(ha, Hd, col, stub[2], hb)
    hn = (hb[0] + f.get("head", [0, 0])[0], chin + f.get("head", [0, 0])[1])
    # short neck (user): the chin sits just above the torso's collar, so only a sliver of neck shows; the head's own stub
    # goes to the back layer (behind the torso) and the top of the torso's stub hides behind the face
    anchor = (stub[0], stub[4] - (stub[4] - stub[3]) * 0.3)

    # long hair below the chin goes BEHIND the torso: split the bare head at the chin line into front (face, cap, hair
    # above the chin) and back (hair below it). Both sit on the head's canvas, so they turn together with the head
    bare = load(names["head"] + "_bare")
    cut = hn[1] + 2 if not has_hair else 10 ** 6         # the chin line (only when the back hair isn't its own part)
    yy = np.arange(bare.shape[0])[:, None]
    # the two pieces overlap by 8 px with hard edges (feathering both made a see-through seam): the front covers the
    # back's top edge, and the back's top edge is hidden behind the torso anyway
    back, front = bare.copy(), bare.copy()
    back[:, :, 3] *= (yy >= cut - 4)
    front[:, :, 3] *= (yy < cut + 4)
    has_back = (back[:, :, 3] > 100).sum() > 400
    if has_back:
        save(front, names["head"] + "_front")
        save(back, names["head"] + "_back")

    # back hair (its own part, same sheet scale as the head): centred under the eyes, its top at the forehead.
    # It hangs from the head (turns with it) and is drawn behind the torso, the arms and the legs
    hair = None
    if has_hair:
        Hr = load(names["hair"])
        hr = opaque(Hr)
        hx0, hy0, hx1, hy1 = SP.bbox(hr)
        if eyes is not None:
            d = eyes[1][0] - eyes[0][0]
            ex, ey = (eyes[0][0] + eyes[1][0]) / 2, (eyes[0][1] + eyes[1][1]) / 2
        else:
            hx_, hy_, hx1_, hy1_ = SP.bbox(ha); d = (hx1_ - hx_) * 0.25; ex, ey = (hx_ + hx1_) / 2, hy_ + (hy1_ - hy_) * 0.6
        hf = f.get("hair", [0, 0])
        top = ey - d * 1.0 + hf[1]                         # in head px
        # hair sprite centre in head px
        hair = (ex + hf[0] - ((hx0 + hx1) / 2 - Hr.shape[1] / 2), top + (Hr.shape[0] / 2 - hy0))

    # legs: hip balls just above the skirt hem, apart by a third of the hem width; the leg's round top hides under the hem
    leg = None
    if has_leg:
        Lg = load(names["leg"])
        la = opaque(Lg)
        lball, lfoot, ltilt = SP.arm_joints(la)
        tx0, ty0, tx1, ty1 = SP.bbox(ta)
        hem = [x for x in range(tx0, tx1 + 1) if ta[max(ty0, ty1 - 6):ty1 + 1, x].any()]
        hw = (max(hem) - min(hem)) if hem else (tx1 - tx0)
        hy = ty1 - (ty1 - ty0) * 0.10 + f.get("hip", [0, 0])[1]
        hipL = (stub[0] - hw * f.get("hipApart", 0.17), hy)
        hipR = (stub[0] + hw * f.get("hipApart", 0.17), hy)
        lbot = SP.bbox(la)[3]
        leg = {"Lg": Lg, "ball": lball, "tilt": ltilt, "hipL": hipL, "hipR": hipR,
               "reach": float(lbot - lball[1])}       # ball centre → sole (leg px; legs are drawn at the torso's scale)

    Ar = load(names["arm"])
    aa = opaque(Ar)
    ball, hand, tilt = SP.arm_joints(aa)
    SP.ART = os.path.dirname(EVO)          # split_arm writes next to the arm (Characters/Evo)
    el = split_arm(names["arm"], ball, hand)
    scale = float(np.clip((nubs[-1][2] + nubs[1][2]) / 2 / ball[2], 0.6, 1.5)) * f.get("armScale", 1.0)

    tH, tW = T.shape[:2]
    bottom = SP.bbox(ta)[3]
    if leg is not None:
        bottom = max(bottom, leg["hipL"][1] + leg["reach"])     # the feet are the figure's bottom now
    rig = lambda px, py: [round((px - neck[0]) / PPU, 4), round((bottom - py) / PPU, 4)]
    hH, hW = Hd.shape[:2]
    aH, aW = Ar.shape[:2]
    loc = lambda px, py: [round((px - ball[0]) / PPU, 4), round((ball[1] - py) / PPU, 4)]
    head_top = anchor[1] - (hn[1] - SP.bbox(ha)[1])
    res = {"id": f"{kid}_{form}", "kind": kid, "form": form, "face": face,
           "body": dict(zip(("x", "y"), rig(tW / 2, tH / 2)), sprite=names["torso"], bare=""),
           "head": dict(zip(("x", "y"), rig(anchor[0] + (hW / 2 - hn[0]), anchor[1] + (hH / 2 - hn[1]))),
                        sprite=names["head"], bare=names["head"] + ("_front" if has_back else "_bare"),
                        back=names["head"] + "_back" if has_back else ""),
           "arm": names["arm"], "armUp": names["arm"] + "_up", "armLo": names["arm"] + "_lo",
           "armCenter": loc(aW / 2, aH / 2), "armElbow": loc(el[0], el[1]),
           "neck": rig(*neck), "shoulderL": rig(nubs[-1][0], nubs[-1][1]), "shoulderR": rig(nubs[1][0], nubs[1][1]),
           "armScale": round(scale, 3), "armTilt": tilt, "armRest": f.get("armRest", 14),
           "hair": "", "hairPos": [0, 0], "leg": "", "hipL": [0, 0], "hipR": [0, 0], "legCenter": [0, 0], "legTilt": 0,
           "height": round((bottom - min(head_top, SP.bbox(ta)[1])) / PPU, 4)}
    if hair is not None:
        hp = (anchor[0] + (hair[0] - hn[0]), anchor[1] + (hair[1] - hn[1]))   # head px → torso px (head anchored at hn)
        res["hair"] = names["hair"]
        res["hairPos"] = rig(*hp)
        res["head"]["back"] = ""
        head_hair_top = hp[1] - load(names["hair"]).shape[0] / 2
    if leg is not None:
        Lg, lb = leg["Lg"], leg["ball"]
        res["leg"] = names["leg"]
        res["hipL"], res["hipR"] = rig(*leg["hipL"]), rig(*leg["hipR"])
        res["legCenter"] = [round((Lg.shape[1] / 2 - lb[0]) / PPU, 4), round((lb[1] - Lg.shape[0] / 2) / PPU, 4)]
        res["legTilt"] = leg["tilt"]
        res["height"] = round((bottom - min(head_top, SP.bbox(ta)[1])) / PPU, 4)
    dbg = {"torso": (names["torso"], stub, nubs), "head": (names["head"], hn, eyes), "arm": (names["arm"], ball, el, hand)}
    return res, dbg


def head_stub_bottom(a, img, col, w0, eyes):
    """Bottom of the neck stub under the chin (x, y). The stub is searched only BELOW the face skin (so the mouth row
    can't pass for it) and may be in shadow, so its colour is matched loosely; long hair may hang beside it.
    Falls back to straight below the eyes at the face's bottom."""
    face = FACES.face_mask(img, col)
    fy1 = np.nonzero(face.any(1))[0].max() if face.any() else SP.bbox(a)[3] * 0.7
    mx = (eyes[0][0] + eyes[1][0]) / 2 if eyes is not None else SP.bbox(a)[0] + (SP.bbox(a)[2] - SP.bbox(a)[0]) / 2
    if eyes is not None:
        # hair as light as the skin leaks into the face patch: the chin is never far below the eyes
        d = eyes[1][0] - eyes[0][0]
        ey = max(eyes[0][1], eyes[1][1])
        fy1 = int(np.clip(fy1, ey + d * 0.45, ey + d * 1.1))
    skin = a & (np.sqrt(((img[:, :, :3] - col) ** 2).sum(2)) < 75)
    best = None
    for y in range(int(fy1) - 2, SP.bbox(a)[3] + 1):
        hit = None
        for s0, e0 in SP.runs(skin[y]):
            c = (s0 + e0) / 2
            # a stub row: narrow, under the eyes, and (once the stub started) a straight continuation of it
            if 0.4 * w0 <= e0 - s0 + 1 <= 1.8 * w0 and abs(c - mx) < w0 * 1.2 and (best is None or abs(c - best[0]) < w0 * 0.25):
                hit = (c, y)
        if hit is None and best is not None and y - best[1] > 2:
            break                             # the stub ended (light hair below must not continue it)
        if hit is not None:
            best = hit                        # keep going down: the last stub row is its bottom
    if best is not None:
        return float(best[0]), float(best[1])
    return float(mx), float(fy1)


def head_chin(a, img, col, w0, bottom):
    """Chin line (y) = the top of the neck stub: walk up from the stub's bottom while the skin run stays stub-narrow."""
    cx, y = bottom
    skin = a & (np.sqrt(((img[:, :, :3] - col) ** 2).sum(2)) < 75)
    w = None
    top = int(y)
    while top > 0:
        rs = [r for r in SP.runs(skin[top - 1]) if r[0] <= cx <= r[1]]
        if not rs:
            break
        rw = rs[0][1] - rs[0][0] + 1
        w = w or rw
        if rw > max(w, w0 * 0.6) * 1.45:
            break
        top -= 1
        if y - top > w0 * 1.5:
            break
    return float(top)


def split_arm(name, ball, hand):
    """SP.split_arm but in Characters/Evo."""
    old = SP.part_path
    SP.part_path = path
    try:
        return SP.split_arm(name, ball, hand)
    finally:
        SP.part_path = old


def fit_size(res, kid, form):
    """k = the form's height in the approved evolution line / its assembled height (both in px)."""
    lh = line_height(kid, form)
    res["k"] = round(lh / (res["height"] * PPU), 4) if lh else 1.0


# ---------- previews ----------
def preview(kid, data, dbgs):
    os.makedirs(os.path.join(ROOT, "Logs/Screens"), exist_ok=True)
    S = 460
    canvas = Image.new("RGBA", (S * 4, S * 3), (40, 50, 70, 255))
    for form in range(4):
        d = data.get(f"{kid}_{form}")
        lp = path(f"evo_{kid}_{form}")
        if os.path.exists(lp):        # top row: the approved line
            im = Image.open(lp).convert("RGBA")
            k0 = (S - 20) / 404
            im = im.resize((int(im.width * k0), int(im.height * k0)), Image.LANCZOS)
            canvas.alpha_composite(im, (form * S + (S - im.width) // 2, S - im.height - 10))
        if d is None:
            continue
        k = (S - 20) / 404 * d["k"]   # same px scale as the line row
        for row, (raise_, bend, face) in enumerate([(0, 14, "normal"), (70, 80, "happy")]):
            ox, oy = form * S + S / 2, S * (row + 2) - 10

            def draw(sprite, x, y, deg=0.0, s=1.0, flip=False):
                if not sprite or not os.path.exists(path(sprite)):
                    return
                im = Image.open(path(sprite)).convert("RGBA")
                im = im.resize((max(1, int(im.width * k * s)), max(1, int(im.height * k * s))), Image.LANCZOS)
                if deg:
                    im = im.rotate(deg, resample=Image.BICUBIC, expand=True)
                if flip:
                    im = im.transpose(Image.FLIP_LEFT_RIGHT)
                canvas.alpha_composite(im, (int(ox + x * PPU * k - im.width / 2), int(oy - y * PPU * k - im.height / 2)))

            def arm(flip):
                sc, C, E = d["armScale"], d["armCenter"], d["armElbow"]
                sh = (-d["shoulderR"][0], d["shoulderR"][1]) if flip else d["shoulderL"]
                a1 = d["armTilt"] - d["armRest"] - raise_
                a2 = a1 + bend
                c1 = SP.rot((C[0] * sc, C[1] * sc), a1)
                e = SP.rot((E[0] * sc, E[1] * sc), a1)
                c2 = SP.rot(((C[0] - E[0]) * sc, (C[1] - E[1]) * sc), a2)
                fl = -1 if flip else 1
                draw(d["armUp"], fl * (sh[0] + c1[0]), sh[1] + c1[1], fl * a1, sc, flip)
                draw(d["armLo"], fl * (sh[0] + e[0] + c2[0]), sh[1] + e[1] + c2[1], fl * a2, sc, flip)

            fname = d["face"] + face if os.path.exists(path(d["face"] + face)) else d["face"] + "normal"
            if "head" in d:
                draw(d.get("hair"), d["hairPos"][0], d["hairPos"][1])
                draw(d["head"].get("back"), d["head"]["x"], d["head"]["y"])
                if d.get("leg"):
                    for side, hip in ((-1, d["hipL"]), (1, d["hipR"])):
                        kick = (raise_ / 70) * 18 * side          # arms-up pose: legs kick out a little
                        a = d["legTilt"] - kick
                        c = SP.rot((d["legCenter"][0], d["legCenter"][1]), a)
                        x = hip[0] + (c[0] if side < 0 else -c[0])
                        draw(d["leg"], x, hip[1] + c[1], a if side < 0 else -a, 1.0, side > 0)
                draw(d["body"]["sprite"], d["body"]["x"], d["body"]["y"])
                arm(True)      # front-on art: both arms in front of the torso, on its shoulder nubs
                arm(False)
                draw(d["head"]["bare"], d["head"]["x"], d["head"]["y"])
                draw(fname, d["head"]["x"], d["head"]["y"])
            else:
                draw(d["body"]["bare"], d["body"]["x"], d["body"]["y"])
                draw(fname, d["body"]["x"], d["body"]["y"])
    out = os.path.join(ROOT, f"Logs/Screens/evo_fit_{kid}.png")
    canvas.save(out)
    print("preview", out)
    # joints on the parts
    cells = []
    for form in (2, 3):
        g = dbgs.get(f"{kid}_{form}")
        if not g:
            continue
        for key in ("torso", "head", "arm"):
            name = g[key][0]
            im = Image.open(path(name)).convert("RGBA")
            bg = Image.new("RGBA", im.size, (40, 50, 70, 255)); bg.alpha_composite(im)
            dr = ImageDraw.Draw(bg)
            if key == "torso":
                (cx, cy, w, top, bot), nubs = g[key][1], g[key][2]
                dr.rectangle([cx - w / 2, top, cx + w / 2, bot], outline=(0, 255, 0, 255), width=2)
                for nx, ny, nr in nubs.values():
                    dr.ellipse([nx - nr, ny - nr, nx + nr, ny + nr], outline=(255, 0, 255, 255), width=2)
            elif key == "head":
                hx, hy = g[key][1]
                dr.ellipse([hx - 6, hy - 6, hx + 6, hy + 6], fill=(0, 255, 0, 255))
                for ex, ey, es in (g[key][2] or ()):
                    dr.ellipse([ex - es, ey - es, ex + es, ey + es], outline=(255, 220, 0, 255), width=2)
            else:
                (bx, by, br), (ex, ey, er), (hx, hy) = g[key][1], g[key][2], g[key][3]
                dr.ellipse([bx - br, by - br, bx + br, by + br], outline=(255, 0, 255, 255), width=2)
                dr.ellipse([ex - er, ey - er, ex + er, ey + er], outline=(0, 200, 255, 255), width=2)
            cells.append(bg)
    if cells:
        W = sum(c.width for c in cells[:3]) + 30
        Hs = [max(c.height for c in cells[i:i + 3]) for i in range(0, len(cells), 3)]
        sheet = Image.new("RGBA", (W, sum(Hs) + 10), (20, 20, 20, 255)); y = 0
        for r, i in enumerate(range(0, len(cells), 3)):
            x = 0
            for c in cells[i:i + 3]:
                sheet.paste(c, (x, y)); x += c.width + 10
            y += Hs[r] + 10
        out = os.path.join(ROOT, f"Logs/Screens/evo_joints_{kid}.png")
        sheet.save(out)
        print("joints", out)


def main():
    args = sys.argv[1:]
    want_preview = "--preview" in args
    kids = [a for a in args if not a.startswith("--")] or sorted(
        {os.path.basename(p).split("_")[1] for p in glob.glob(os.path.join(EVO, "e_*_0_body.png"))})
    fix = json.load(open(FIX, encoding="utf-8")) if os.path.exists(FIX) else {}
    data = {}
    if os.path.exists(OUT):
        data = {l["id"]: l for l in json.load(open(OUT, encoding="utf-8"))["looks"]}
    for kid in kids:
        dbgs = {}
        for form in range(4):
            res, dbg = (rig_headless if form < 2 else rig_headed)(kid, form, fix)
            if res is None:
                continue
            fit_size(res, kid, form)
            data[res["id"]], dbgs[res["id"]] = res, dbg
            print(res["id"], "k", res["k"], "h", res["height"], {k: res[k] for k in ("neck", "shoulderL", "shoulderR", "armScale") if k in res})
        if want_preview:
            preview(kid, data, dbgs)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump({"looks": [data[k] for k in sorted(data)]}, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
