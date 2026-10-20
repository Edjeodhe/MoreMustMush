"""Game definitions the tree layout tools need, read from the C# sources so they never drift:
deco list (id, owner kid, hang), kid join order, zones (weight, open stage), tree slot counts, tree widths and zoom.

Coordinates in tree_layout.json are fractions of the game picture Assets/Resources/Art/Dream/Farm/Tree/tree_<pic>.png
(u right, v down). pic = stage - 1.
"""
import json, os, re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LAYOUT = os.path.join(ROOT, "Assets/Resources/Rig/tree_layout.json")
TREE = os.path.join(ROOT, "Assets/Resources/Art/Dream/Farm/Tree")
PROPS = os.path.join(ROOT, "Assets/Resources/Art/Dream/Farm/Props")
_FARM = open(os.path.join(ROOT, "Assets/Scripts/Core/Defs.Farm.cs"), encoding="utf-8").read()
_DEFS = open(os.path.join(ROOT, "Assets/Scripts/Core/Defs.cs"), encoding="utf-8").read()


def _floats(name):
    m = re.search(rf"\b{name}\s*=\s*\{{([^}}]*)\}}", _FARM)
    return [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]


WIDTH = _floats("width")            # WT.width (stage px)
ZOOM = _floats("zoom")              # WT.zoom
MUSH_SLOTS = [int(x) for x in _floats("mushSlots")]
DECO_SLOTS = [int(x) for x in _floats("decoSlots")]
BEAUTY_SLOTS = 2                    # BeautyStep "slots" (+2 mushroom slots)
STAGES = len(WIDTH)
JOIN = re.findall(r'"(\w+)"', re.search(r"CRITTER_JOIN\s*=\s*\{([^}]*)\}", _DEFS).group(1))
DECOS = [dict(id=m[0], of=m[1], hang=m[2] == "true", art=m[3]) for m in
         re.findall(r'new Deco \{ id = "(\w+)", of = "(\w+)".*?hang = (true|false).*?art = "(\w+)"', _FARM)]
ZONES = [dict(id=m[0], open=int(m[1]), weight=int(m[2])) for m in
         re.findall(r'new Zone \{ id = "(\w+)".*?open = (\d+), weight = (\d+)', _FARM)]


def decos_for(pic):
    """Decos that can be crafted by this stage: the 3 decos of every kid that has joined (kid k joins at stage k)."""
    kids = JOIN[:pic + 1]
    return [d for d in DECOS if d["of"] in kids]


def zone_slots(pic, total=None):
    """Game.ZoneSlots for stage pic+1 with `total` slots (default: the most it can have, beauty bonus included)."""
    stage = pic + 1
    total = MUSH_SLOTS[pic] + BEAUTY_SLOTS if total is None else total
    open_ = [z for z in ZONES if stage >= z["open"]]
    wsum = sum(z["weight"] for z in open_)
    o = {z["id"]: total * z["weight"] // wsum for z in open_}
    left = total - sum(o.values())
    for z in sorted(open_, key=lambda z: (-(total * z["weight"] % wsum), ZONES.index(z))):
        if left <= 0:
            break
        o[z["id"]] += 1
        left -= 1
    return {z["id"]: o.get(z["id"], 0) for z in ZONES}


def deco_px(d, pic):
    """FarmTreeView.DecoPx: long side of a deco in stage px."""
    return (95 if d["hang"] else 115) * ZOOM[pic]


def fruit_px(pic):
    """FarmTreeView.FruitPx: tree mushroom size in stage px."""
    return (34 + 20 * pic / (STAGES - 1)) * ZOOM[pic]


# mushroom spirit footprint (stage px at camera zoom 1): measured in play 2026-10-20 (zoom 2.28: 180-295 wide, 220-295
# tall with arms/cap) -> typical standing spirit 110 x 155. FarmCritter scales the spirit by zoom^0.75 (FarmView.Radius 40).
SPIRIT_W, SPIRIT_H = 110, 155


CLIMB_FROM = int(re.search(r"climbFrom\s*=\s*(\d+)", _FARM).group(1))   # spirits climb the tree from this stage (WT.climbFrom)
WT_X, WT_Y = 820, 720                                                                 # tree foot (WT.y), stage px


def climbs(pic):
    return pic + 1 >= CLIMB_FROM


def reach(feet, p, pic):
    """FarmTreeView.Reach in stage px: up to 0.8 spirit widths aside, 1.1 heights up, 0.3 down."""
    sw, sh = spirit_px(pic)
    return abs(p[0] - feet[0]) <= 0.8 * sw and feet[1] - 1.1 * sh <= p[1] <= feet[1] + 0.3 * sh


def measure_foot(pic):
    """Picture line (v) that sits on the ground: the lowest row of the base mound that is still solid (alpha > 128 over
    >= 85% of the widest row in the bottom 30%). Below it blend_tree_base.py fades the base out, so that band goes
    into the ground and the solid base touches the tree foot WT.y (2026-10-20: trees looked like floating)."""
    from PIL import Image
    im = Image.open(os.path.join(TREE, f"tree_{pic}.png")).getchannel("A")
    W, H = im.size
    px = im.load()
    wid = [sum(1 for x in range(0, W, 2) if px[x, y] > 128) for y in range(H)]
    lo = int(H * 0.7)
    i = max(range(lo, H), key=lambda y: wid[y])
    j = i
    while j + 1 < H and wid[j + 1] >= 0.85 * wid[i]:
        j += 1
    return round(j / H, 3)


def spirit_px(pic):
    """(width, height) of a standing spirit in stage px at this stage."""
    k = ZOOM[pic] ** 0.75
    return SPIRIT_W * k, SPIRIT_H * k


def tree_height(pic, aspect):
    return WIDTH[pic] / aspect


def kids_at(pic):
    """spirits that can be on the farm by this stage (kid k joins at stage k)."""
    return pic + 1


def load():
    return json.load(open(LAYOUT, encoding="utf-8"))


def dumps(data):
    """json indent=1 with short number/string arrays kept on one line (the web editor writes the same format)."""
    s = json.dumps(data, ensure_ascii=False, indent=1)
    return re.sub(r"\[\s*([^\[\]{}]*?)\s*\]", lambda m: "[" + re.sub(r"\s*\n\s*", " ", m.group(1)) + "]", s) + "\n"


def save(data):
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(dumps(data))
