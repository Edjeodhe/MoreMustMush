"""Spirit faces for the puppet rig: a bare head (hair and face shape, no eyes/brows/mouth) + one face layer per expression.

1. Bare head + neutral face, cut from the head part itself (Characters/Spirits/p_<id>_head[_skin]):
   the face skin = the biggest patch of the skin colour (the torso's neck stub) in the head; eyes, brows and mouth
   = the dark holes inside it. They are inpainted with the skin colour (bare head) and copied out as the neutral face.
   Blush and flame specks are light, so they stay on the head. The hair is the original pixels, so a face swap never
   makes it flicker.
2. Other expressions come from a generated features-only sheet (sheets-dream.mjs `spiritFaces`, sliced into
   Art/Raw_Dream/faces/f_<id>_<expr>.png). One scale per spirit (the median of width ratios against the neutral face,
   since a sheet is drawn at one scale), centred on the neutral face, tops (brows) aligned.
Every face layer is written on the head's own canvas, so in the rig it sits at (0, 0) under the head.

out: Characters/Spirits/p_<id>_head[_skin]_bare.png, p_<id>_face[_skin]_<expr>.png (expr: normal + EXPRS)
usage: python Tools/art/build_spirit_faces.py [id ...]   [--preview]  (preview → Logs/Screens/spirit_faces_<id>.png)
"""
import glob, os, sys
import cv2
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PARTS = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/Spirits")
GEN = os.path.join(ROOT, "Art/Raw_Dream/faces")
EXPRS = ["blink", "happy", "surprise", "love", "effort", "tired"]
FIX = __import__("json").load(open(os.path.join(ROOT, "Tools/art/spirit_rig_fix.json"), encoding="utf-8"))     if os.path.exists(os.path.join(ROOT, "Tools/art/spirit_rig_fix.json")) else {}   # eyes: hints, keepFace: no expressions
TOL = 40


def load(p):
    return np.asarray(Image.open(p).convert("RGBA")).astype(np.float32)


def save(a, p):
    Image.fromarray(np.uint8(np.clip(a, 0, 255))).save(p)


def skin_of(kid, skin=""):
    """The spirit's skin colour = the torso's neck stub (top rows of the torso), the one place that is surely bare skin."""
    t = load(os.path.join(PARTS, f"p_{kid}_torso{skin}.png"))
    a = t[:, :, 3] > 100
    lab, n = ndi.label(a)
    a = lab == 1 + int(np.argmax(ndi.sum(a, lab, range(1, n + 1))))
    ys, xs = np.nonzero(a)
    y0 = ys.min()
    row = np.nonzero(a[y0 + 3])[0]
    cx = (row.min() + row.max()) / 2
    w = row.max() - row.min() + 1
    return np.median(t[y0 + 2:y0 + 12, int(cx - w / 3):int(cx + w / 3) + 1, :3].reshape(-1, 3), 0)


def find_eyes(im):
    """The two eyes: dark compact blobs of about the same size side by side in the lower part of the head.
    → ((x, y, size), (x, y, size)) left to right, or None."""
    a = im[:, :, 3] > 100
    ys, xs = np.nonzero(a)
    y0, y1 = ys.min(), ys.max()
    lum = im[:, :, :3] @ np.array([0.299, 0.587, 0.114])
    best = None
    for thr in (70, 85, 100, 60, 50):
        dark = a & (lum < thr)
        lab, n = ndi.label(dark)
        blobs = []
        for i, sl in enumerate(ndi.find_objects(lab), 1):
            m = lab[sl] == i
            area = m.sum()
            h, w = m.shape
            if area < 40 or area > a.sum() * 0.03 or h > w * 3 or w > h * 6:
                continue
            cy, cx = ndi.center_of_mass(m)
            blobs.append((sl[1].start + cx, sl[0].start + cy, float(np.sqrt(area)), w, h))
        for i in range(len(blobs)):
            for j in range(i + 1, len(blobs)):
                p, q = sorted((blobs[i], blobs[j]))
                size = (p[2] + q[2]) / 2
                d = q[0] - p[0]
                if not (0.6 < p[2] / q[2] < 1.6 and abs(p[1] - q[1]) < size * 0.8 and 1.8 * size < d < 9 * size):
                    continue
                if (p[1] - y0) / (y1 - y0) < 0.3:
                    continue
                score = size - abs(p[1] - q[1]) - abs(p[2] - q[2]) * 2
                if best is None or score > best[0]:
                    best = (score, p, q)
        if best is not None:
            break
    return None if best is None else (best[1][:3], best[2][:3])


def skin_near_eyes(im, eyes):
    """Skin colour from just around the eyes: between and under them, without dark strokes (the mouth)."""
    (lx, ly, ls), (rx, ry, rs) = eyes
    d = rx - lx
    y = (ly + ry) / 2
    box = im[int(y + d * 0.15):int(y + d * 0.55), int(lx - d * 0.1):int(rx + d * 0.1)]
    box = box[box[:, :, 3] > 200][:, :3]
    lum = box @ np.array([0.299, 0.587, 0.114])
    box = box[lum > np.percentile(lum, 40)]
    q = (box // 12).astype(int)
    key = q[:, 0] * 10000 + q[:, 1] * 100 + q[:, 2]
    keys, cnt = np.unique(key, return_counts=True)
    return np.median(box[key == keys[cnt.argmax()]], 0)


def face_mask(im, skin):
    """The face skin: the biggest patch of the skin colour in the head."""
    a = im[:, :, 3] > 100
    sk = (np.sqrt(((im[:, :, :3] - skin) ** 2).sum(2)) < TOL) & a
    lab, n = ndi.label(sk)
    return lab == (1 + np.argmax(ndi.sum(sk, lab, range(1, n + 1))))


def split(im, skin, hint=None, dark=False):
    """(bare head, neutral face layer) on the head's canvas. skin = skin_of(...) (fallback when no eye pair is found);
    hint = [[x, y], [x, y]] eye centres for heads whose eyes are not a matching dark pair (wink, closed, sunglasses)."""
    rgb = im[:, :, :3]
    if hint:
        d = abs(hint[1][0] - hint[0][0])
        eyes = tuple(sorted(((float(x), float(y), d * 0.15) for x, y in hint)))
    else:
        eyes = find_eyes(im)
    if eyes is not None:
        skin = skin_near_eyes(im, eyes)
    face = face_mask(im, skin)
    ys, xs = np.nonzero(face)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    region = np.ones_like(face)
    if eyes is not None:
        # features that touch the hair line are not enclosed by the face: close small gaps first, and keep only holes
        # around the eyes and mouth
        (lx, ly, _), (rx, ry, _) = eyes
        d, ey = rx - lx, (ly + ry) / 2
        k = max(3, int(d * 0.2)) | 1
        face_c = cv2.morphologyEx(face.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((k, k), np.uint8)) > 0
        region = np.zeros_like(face)
        region[max(0, int(ey - d * 0.65)):int(ey + d * 1.0), max(0, int(lx - d * 0.6)):int(rx + d * 0.6)] = True
        holes = ndi.binary_fill_holes(face_c) & ~face & region
    else:
        holes = ndi.binary_fill_holes(face) & ~face
    hl, hn = ndi.label(holes)
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    skin_l = float(skin @ np.array([0.299, 0.587, 0.114]))
    keep = []
    for i in range(1, hn + 1):
        m = hl == i
        if m.sum() < 25:
            continue
        cy, cx = ndi.center_of_mass(m)
        if eyes is None and (cy - y0) / (y1 - y0) < 0.2:
            continue                                   # hair specks caught at the top of the face patch
        if np.percentile(lum[m], 10) > skin_l - 70:
            continue                                   # blush and flame specks are light; features have dark strokes
        keep.append(i)
    # only the dark strokes of those holes: a blush that touches an eye shares its hole but is light, so it stays
    feat = np.isin(hl, keep) & (lum < skin_l - 45)
    if dark and eyes is not None:
        # hair as light as the skin (wind): the features are not enclosed, take the dark strokes around the eyes
        dk = region & (lum < skin_l - 70) & (im[:, :, 3] > 100)
        dl, dn = ndi.label(dk)
        rb = region & ~ndi.binary_erosion(region)                       # pieces cut by the box edge are hair
        for i in range(1, dn + 1):
            m = dl == i
            if m.sum() >= 25 and not (m & rb).any():
                feat |= m
    feat = ndi.binary_fill_holes(cv2.morphologyEx(feat.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)) > 0)
    m = cv2.dilate(feat.astype(np.uint8), np.ones((5, 5), np.uint8))
    bare = im.copy()
    bare[:, :, :3] = cv2.inpaint(rgb.astype(np.uint8), m, 7, cv2.INPAINT_TELEA)
    soft = cv2.GaussianBlur(cv2.dilate(m, np.ones((3, 3), np.uint8)).astype(np.float32), (5, 5), 0)
    face_l = im.copy()
    face_l[:, :, 3] = np.minimum(im[:, :, 3], soft * 255)
    return bare, face_l


def obox(a, thr=100):
    ys, xs = np.nonzero(a[:, :, 3] > thr)
    return xs.min(), ys.min(), xs.max(), ys.max()


def clean(gen):
    """Drop the generator's loose specks: pieces much smaller than the biggest feature."""
    m = gen[:, :, 3] > 40
    lab, n = ndi.label(cv2.dilate(m.astype(np.uint8), np.ones((3, 3), np.uint8)))
    if n == 0:
        return gen
    sizes = ndi.sum(m, lab, range(1, n + 1))
    keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s >= 0.04 * sizes.max()])
    out = gen.copy()
    out[:, :, 3] *= keep
    return out


def place(gen, neutral, scale):
    """Generated features → the neutral face's canvas: scaled, centred on it, tops aligned."""
    x0, y0, x1, y1 = obox(gen)
    g = gen[y0:y1 + 1, x0:x1 + 1]
    g = cv2.resize(g, (max(1, round(g.shape[1] * scale)), max(1, round(g.shape[0] * scale))), interpolation=cv2.INTER_AREA)
    nx0, ny0, nx1, ny1 = obox(neutral)
    out = np.zeros_like(neutral)
    ox, oy = int(round((nx0 + nx1) / 2 - g.shape[1] / 2)), int(ny0)
    H, W = out.shape[:2]
    sx0, sy0 = max(0, -ox), max(0, -oy)
    dx0, dy0 = max(0, ox), max(0, oy)
    w, h = min(g.shape[1] - sx0, W - dx0), min(g.shape[0] - sy0, H - dy0)
    out[dy0:dy0 + h, dx0:dx0 + w] = g[sy0:sy0 + h, sx0:sx0 + w]
    return out


def build(kid, want_preview):
    tiles = []
    for skin in ("", "_skin"):
        hp = os.path.join(PARTS, f"p_{kid}_head{skin}.png")
        if not os.path.exists(hp):
            continue
        head = load(hp)
        name = f"p_{kid}_head{skin}"
        bare, neutral = split(head, skin_of(kid, skin), FIX.get("eyes", {}).get(name), name in FIX.get("darkFeatures", []))
        save(bare, os.path.join(PARTS, f"p_{kid}_head{skin}_bare.png"))
        save(neutral, os.path.join(PARTS, f"p_{kid}_face{skin}_normal.png"))
        faces = {"normal": neutral}
        gens = {} if name in FIX.get("keepFace", []) else {e: clean(load(p)) for e in EXPRS if os.path.exists(p := os.path.join(GEN, f"f_{kid}_{e}.png"))}
        if gens:
            nw = obox(neutral)[2] - obox(neutral)[0] + 1
            scale = float(np.median([nw / (obox(g)[2] - obox(g)[0] + 1) for g in gens.values()]))
            for e, g in gens.items():
                faces[e] = place(g, neutral, scale)
                save(faces[e], os.path.join(PARTS, f"p_{kid}_face{skin}_{e}.png"))
        print(kid + skin, "faces", list(faces))
        if want_preview:
            for e, f in faces.items():
                t = bare.copy()
                al = f[:, :, 3:] / 255
                t[:, :, :3] = t[:, :, :3] * (1 - al) + f[:, :, :3] * al
                bg = np.zeros_like(t); bg[:, :, :3] = (40, 50, 70)
                ta = t[:, :, 3:] / 255
                tiles.append(np.uint8(t[:, :, :3] * ta + bg[:, :, :3] * (1 - ta)))
    if want_preview and tiles:
        n = len(EXPRS) + 1
        h, w = max(t.shape[0] for t in tiles), max(t.shape[1] for t in tiles)
        rows = (len(tiles) + n - 1) // n
        S = np.full((h * rows, w * n, 3), 20, np.uint8)
        for i, t in enumerate(tiles):
            S[(i // n) * h:(i // n) * h + t.shape[0], (i % n) * w:(i % n) * w + t.shape[1]] = t
        os.makedirs(os.path.join(ROOT, "Logs/Screens"), exist_ok=True)
        out = os.path.join(ROOT, f"Logs/Screens/spirit_faces_{kid}.png")
        Image.fromarray(S).save(out)
        print("preview", out)


def main():
    args = sys.argv[1:]
    want_preview = "--preview" in args
    kids = [a for a in args if not a.startswith("--")] or sorted(
        {os.path.basename(p).split("_")[1] for p in glob.glob(os.path.join(PARTS, "p_*_head.png"))})
    for kid in kids:
        build(kid, want_preview)


if __name__ == "__main__":
    main()
