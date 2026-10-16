"""Lay out the doodle grandpa's separately drawn parts (Characters/Grandpa/Parts, sheet grandpa_rig) as a joint rig.

Head, hat, beard, torso, backpack and legs are matched onto the assembled drawing `full` (masked colour SSD, see
build_critter_rig.match). Arms and the scythe are posed by rule because the drawing shows them in another pose:
each arm hangs from its shoulder (the torso's upper corner) slightly outward, the scythe stands in the front fist.
Writes Tools/art/grandpa_rig_doodle.json (px of the full drawing, y up, origin at the feet centre) that
MoreMush/Art/Build Doodle Grandpa Rig (Assets/Editor/GrandpaRigBuild.cs) turns into GrandpaRig.prefab.

Usage: python Tools/art/build_grandpa_rig_doodle.py [--debug <dir>] [--side left|right]  (side of the scythe hand on screen)
"""
import json, math, os, sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_critter_rig import load, resize, bbox, fit  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, "Assets/ArtArchive/Doodle/Characters/Grandpa/Parts")
OUT = os.path.join(ROOT, "Tools/art/grandpa_rig_doodle.json")
WORK = 360
MATCHED = ["torso", "head", "hat", "beard", "backpack", "leg_back", "leg_front", "eyes_closed", "mouth_open"]


def axis(alpha):
    """Principal axis of a mask: centre, unit direction, half length."""
    ys, xs = np.nonzero(alpha > 0.5)
    pts = np.stack([xs, ys], 1).astype(np.float64)
    c = pts.mean(0)
    u, s, vt = np.linalg.svd(pts - c, full_matrices=False)
    d = vt[0]
    proj = (pts - c) @ d
    return c, d, proj.min(), proj.max(), pts, proj


def main():
    args = sys.argv[1:]
    dbg = args[args.index("--debug") + 1] if "--debug" in args else None
    side = args[args.index("--side") + 1] if "--side" in args else "left"
    full = load(os.path.join(DIR, "full.png"))
    x0, y0, x1, y1 = bbox(full[:, :, 3])
    full = full[y0:y1, x0:x1]
    k = WORK / full.shape[0]
    ref = resize(full, k)
    H, W = ref.shape[:2]
    parts = {n: load(os.path.join(DIR, f"{n}.png")) for n in MATCHED + ["arm_back", "arm_front", "scythe"] if os.path.exists(os.path.join(DIR, f"{n}.png"))}

    # common part scale from the torso (all parts come from one sheet)
    tb = bbox(parts["torso"][:, :, 3])
    base = W * 0.55 / (tb[2] - tb[0])
    res = {}
    res["torso"] = fit(ref, parts["torso"], base, spread=0.45)
    base = res["torso"][1]
    for n in MATCHED[1:]:
        if n in parts:
            res[n] = fit(ref, parts[n], base, spread=0.25, mirror=n.startswith("leg"))
    s_all = float(np.median([r[1] for r in res.values()]))

    def to_ref(px, py):   # work px (y down) → full-drawing px, y up, origin feet centre
        return (px - W / 2) / k, (H - py) / k

    out = {"height": H / k, "parts": {}}
    for n, (sc, s, x, y, flip) in res.items():
        p = parts[n]
        w, h = p.shape[1] * s, p.shape[0] * s
        cx, cy = to_ref(x + w / 2, y + h / 2)
        out["parts"][n] = {"x": round(cx, 1), "y": round(cy, 1), "s": round(s / k, 4), "flip": bool(flip), "rot": 0.0,
                           "score": round(sc, 4), "top": round(to_ref(0, y)[1], 1), "bottom": round(to_ref(0, y + h)[1], 1),
                           "left": round(to_ref(x, 0)[0], 1), "right": round(to_ref(x + w, 0)[0], 1)}

    P = out["parts"]
    t = P["torso"]
    # joints
    j = {}
    j["neck"] = (P["head"]["x"], P["head"]["bottom"] + (P["head"]["top"] - P["head"]["bottom"]) * 0.18)
    j["beard"] = (P["beard"]["x"], P["beard"]["top"] - (P["beard"]["top"] - P["beard"]["bottom"]) * 0.1)
    j["hat"] = (P["hat"]["x"], P["hat"]["bottom"] + (P["hat"]["top"] - P["hat"]["bottom"]) * 0.2)
    for leg in ("leg_back", "leg_front"):
        L = P[leg]; j[leg] = (L["x"], L["top"] - (L["top"] - L["bottom"]) * 0.1)
    tw = t["right"] - t["left"]
    sh_y = t["top"] - (t["top"] - t["bottom"]) * 0.16
    front_x = t["left"] + tw * 0.12 if side == "left" else t["right"] - tw * 0.12
    back_x = t["right"] - tw * 0.12 if side == "left" else t["left"] + tw * 0.12
    j["arm_front"] = (front_x, sh_y)
    j["arm_back"] = (back_x, sh_y)

    # arms: hang from the shoulder, the shoulder end of the sprite = the end nearer the sprite's top
    for arm, outward in (("arm_front", -1 if side == "left" else 1), ("arm_back", 1 if side == "left" else -1)):
        a = parts[arm]
        c, d, lo, hi, pts, proj = axis(a[:, :, 3])
        e1, e2 = c + d * lo, c + d * hi
        shoulder, hand = (e1, e2) if e1[1] < e2[1] else (e2, e1)        # image y down: smaller y = higher
        v = hand - shoulder
        ang_now = math.degrees(math.atan2(-v[1], v[0]))                  # y up
        want = -90 + outward * 12                                         # hanging down, 12° outward
        rot = want - ang_now
        # sprite centre relative to the shoulder after rotation (in sprite px * s_all → ref px)
        cpx = np.array([a.shape[1] / 2, a.shape[0] / 2])
        off = cpx - shoulder
        off = np.array([off[0], -off[1]]) * s_all / k
        r = math.radians(rot)
        off = np.array([off[0] * math.cos(r) - off[1] * math.sin(r), off[0] * math.sin(r) + off[1] * math.cos(r)])
        jx, jy = j[arm]
        hand_ref = np.array([v[0], -v[1]]) * s_all / k
        hand_ref = np.array([hand_ref[0] * math.cos(r) - hand_ref[1] * math.sin(r), hand_ref[0] * math.sin(r) + hand_ref[1] * math.cos(r)])
        P[arm] = {"x": round(jx + off[0], 1), "y": round(jy + off[1], 1), "s": round(s_all / k, 4), "flip": False, "rot": round(rot, 1),
                  "hand": [round(jx + hand_ref[0], 1), round(jy + hand_ref[1], 1)]}
    # scythe: handle through the front fist, blade on top, tilted 10° outward
    sc_ = parts["scythe"]
    c, d, lo, hi, pts, proj = axis(sc_[:, :, 3])
    e1, e2 = c + d * lo, c + d * hi
    # blade end = the end whose neighbourhood is wider
    def width_at(e):
        near = pts[np.linalg.norm(pts - e, axis=1) < (hi - lo) * 0.2]
        return np.ptp((near - c) @ np.array([-d[1], d[0]])) if len(near) else 0
    blade, butt = (e1, e2) if width_at(e1) > width_at(e2) else (e2, e1)
    grip = butt + (blade - butt) * 0.32
    v = blade - butt
    ang_now = math.degrees(math.atan2(-v[1], v[0]))
    outward = -1 if side == "left" else 1
    rot = (90 - outward * 10) - ang_now
    cpx = np.array([sc_.shape[1] / 2, sc_.shape[0] / 2])
    off = np.array([(cpx - grip)[0], -(cpx - grip)[1]]) * s_all / k
    r = math.radians(rot)
    off = np.array([off[0] * math.cos(r) - off[1] * math.sin(r), off[0] * math.sin(r) + off[1] * math.cos(r)])
    fx, fy = P["arm_front"]["hand"]
    j["scythe"] = (fx, fy)
    P["scythe"] = {"x": round(fx + off[0], 1), "y": round(fy + off[1], 1), "s": round(s_all / k, 4), "flip": False, "rot": round(rot, 1)}
    out["joints"] = {n: [round(v[0], 1), round(v[1], 1)] for n, v in j.items()}
    # JsonUtility-friendly lists for the Unity builder
    flat = {"height": out["height"],
            "parts": [{"name": n, "x": q["x"], "y": q["y"], "s": q["s"], "flip": q["flip"], "rot": q["rot"]} for n, q in P.items()],
            "joints": [{"name": n, "x": v[0], "y": v[1]} for n, v in out["joints"].items()]}
    json.dump(flat, open(OUT, "w", encoding="utf-8"), indent=1)
    print(json.dumps({n: P[n].get("score") for n in P}))

    if dbg:
        os.makedirs(dbg, exist_ok=True)
        scale = 0.5
        Wc, Hc = int(out["height"] * 1.6 * scale), int(out["height"] * 1.25 * scale)
        canvas = Image.new("RGBA", (Wc * 2, Hc), (200, 230, 190, 255))
        fim = Image.fromarray((full * 255).astype(np.uint8)).resize((int(full.shape[1] * scale), int(full.shape[0] * scale)))
        canvas.alpha_composite(fim, (int(Wc / 2 - fim.width / 2), Hc - fim.height - 10))
        order = ["backpack", "arm_back", "leg_back", "leg_front", "torso", "head", "beard", "mouth_open", "hat", "scythe", "arm_front"]
        for n in order:
            if n not in P or n not in parts:
                continue
            q = P[n]
            im = Image.fromarray((parts[n] * 255).astype(np.uint8))
            if q["flip"]:
                im = im.transpose(Image.FLIP_LEFT_RIGHT)
            f = q["s"] * scale
            im = im.resize((max(1, int(im.width * f)), max(1, int(im.height * f))), Image.LANCZOS).rotate(q["rot"], expand=True, resample=Image.BICUBIC)
            cx, cy = Wc + Wc / 2 + q["x"] * scale, Hc - 10 - q["y"] * scale
            canvas.alpha_composite(im, (int(cx - im.width / 2), int(cy - im.height / 2)))
        canvas.save(os.path.join(dbg, "grandpa_rig.png"))


if __name__ == "__main__":
    main()
