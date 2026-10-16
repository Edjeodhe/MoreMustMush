"""공용 맨몸 틀 + 부위별 그림 조립 미리보기 (2026-10-16 예시용, 리그 확정 전 눈대중 배치).

python Tools/art/kit_preview.py [정령=fire] [모습=3]  → Logs/Screens/kit_<정령>_<모습>.png
층(뒤→앞): 뒷머리 · 다리(+신발) · 몸통 · 복장 · 머리(갓) · 팔(위팔·아래팔, 팔꿈치에서 나눔)
크기는 마네킹(kit_base_<모습>) 좌표 기준: 각 부위를 마네킹의 해당 길이에 맞춰 줄인다.
"""
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
KIT = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/EvoKit")
kid = sys.argv[1] if len(sys.argv) > 1 else "fire"
form = sys.argv[2] if len(sys.argv) > 2 else "3"


def load(name):
    p = os.path.join(KIT, f"kit_{name}.png")
    return Image.open(p).convert("RGBA") if os.path.exists(p) else None


def fit_h(img, h):
    return img.resize((max(1, round(img.width * h / img.height)), round(h)), Image.LANCZOS)


def paste(canvas, img, pivot, at, angle=0.0):
    """img의 pivot(픽셀) 점을 canvas의 at에 두고, 그 점을 축으로 angle도(반시계) 돌려 붙인다."""
    if angle:
        big = max(img.width, img.height) * 2
        pad = Image.new("RGBA", (big, big))
        ox, oy = big // 2 - round(pivot[0]), big // 2 - round(pivot[1])
        pad.alpha_composite(img, (ox, oy))
        img, pivot = pad.rotate(angle, resample=Image.BICUBIC, center=(big // 2, big // 2)), (big // 2, big // 2)
    canvas.alpha_composite(img, (round(at[0] - pivot[0]), round(at[1] - pivot[1])))


def width_at(img, f):
    a = np.asarray(img)[:, :, 3] > 40
    return int(a[int(f * (img.height - 1))].sum())


# 마네킹 기준 치수 (kit_base_3: 머리 0~42% · 목 45% · 치마 끝 80% · 발 100%)
base = load(f"base_{form}")
BH = base.height
CX = base.width / 2
def measure(b):
    """마네킹에서 목(머리와 몸통 사이 가장 좁은 줄)과 치마 끝(가운데가 비어 다리가 갈라지는 첫 줄)을 잰다."""
    al = np.asarray(b)[:, :, 3] > 40
    w = al.sum(axis=1)
    lo, hi = int(b.height * 0.25), int(b.height * 0.65)
    neck = lo + int(np.argmin(np.where(w[lo:hi] > 0, w[lo:hi], 1 << 30)))
    cx = int(np.nonzero(al[neck])[0].mean()) if al[neck].any() else b.width // 2
    hem = next((y for y in range(int(b.height * 0.55), b.height) if w[y] > 0 and not al[y, cx - 3:cx + 4].any()), int(b.height * 0.8))
    return neck, hem


_neck, _hem = measure(base)
NECK, HEM, FOOT = _neck + 0.024 * BH, _hem - 0.025 * BH, BH   # ★3에서 눈으로 맞춘 값(0.43·0.80)에 맞춘 보정
HEAD_W = max(width_at(base, f) for f in np.linspace(0.1, 0.35, 10))

head, hair = load(f"{kid}_{form}_head"), load(f"{kid}_{form}_hair")
torso, dress = load(f"body_{form}_torso"), load(f"outfit_gill_{form}_dress")
arm = load(f"body_{form}_arm")
leg, shoe = load(f"outfit_gill_{form}_leg") or load(f"body_{form}_leg"), None   # 다리는 신발 포함 복장 부품

# 머리: 갓 아래 얼굴 폭(머리 그림 85% 높이)을 마네킹 머리 폭에 맞춤, 목 그루터기 끝이 목에 오도록
hs = HEAD_W / width_at(head, 0.85)
head_s = head.resize((round(head.width * hs), round(head.height * hs)), Image.LANCZOS)
hair_s = hair.resize((round(hair.width * hs * 0.95), round(hair.height * hs * 0.95)), Image.LANCZOS) if hair else None
torso_s = fit_h(torso, HEM - NECK + 20)
dress_s = fit_h(dress, (HEM - NECK) * 1.08)


def collar_of(d):
    """드레스 목 프릴만 떼어 낸 층: 위에서부터 폭이 넓어졌다가(프릴) 다시 좁아지는 곳까지. 팔 위에 덮어 어깨 관절을 가린다."""
    al = np.asarray(d)[:, :, 3] > 40
    w = al.sum(axis=1)
    top = int(np.argmax(w > 0))
    peak = top + int(np.argmax(w[top:top + d.height // 3]))
    end = peak + int(np.argmin(w[peak:peak + d.height // 6]))
    arr = np.asarray(d).copy()
    fade = np.clip((end - np.arange(d.height)) / 6.0, 0, 1)    # 아래 끝을 6px 부드럽게
    arr[:, :, 3] = (arr[:, :, 3] * fade[:, None]).astype(np.uint8)
    return Image.fromarray(arr)


collar_s = collar_of(dress_s)
LEG_H = FOOT - HEM + 55          # 신발 포함
leg_s = fit_h(leg, LEG_H) if leg else None
ARM_H = (HEM - NECK) * 0.66
arm_s = fit_h(arm, ARM_H) if arm else None
SH_X = width_at(torso_s, 0.2) / 2 * 0.72        # 어깨 관절: 몸통 어깨 혹 안쪽
SH_Y = NECK + torso_s.height * 0.16
HIP_X = torso_s.width * 0.17       # 엉덩이 관절 간격 (좁으면 안짱다리처럼 모임)
LEG_OUT = 3                          # 서 있을 때도 다리를 바깥으로 살짝 벌림


def split_arm(a):
    """팔 한 장을 팔꿈치에서 둘로 (공 관절): 팔꿈치 높이의 실제 팔 굵기로 원을 그려, 위팔은 아래끝을·아래팔은 위끝을
    그 원으로 둥글게 깎는다. 두 조각이 같은 원 중심(팔꿈치)으로 돌아서 접어도 이음새가 둥글게 맞물린다.
    돌려주는 elbow는 팔 그림 좌표의 원 중심."""
    al = np.asarray(a)[:, :, 3] > 40
    m = int(a.height * 0.5)
    cols = np.where(al[m])[0]
    ex, r = (cols[0] + cols[-1]) / 2, (cols[-1] - cols[0]) / 2 * 1.02
    yy, xx = np.mgrid[0:a.height, 0:a.width]
    disk = (xx - ex) ** 2 + (yy - m) ** 2 <= r * r
    arr = np.asarray(a).copy()
    up, lo = arr.copy(), arr.copy()
    up[(yy > m) & ~disk, 3] = 0           # 위팔: 팔꿈치 아래는 원 안만
    lo[(yy < m) & ~disk, 3] = 0           # 아래팔: 팔꿈치 위는 원 안만
    return Image.fromarray(up), Image.fromarray(lo), (ex, m)


def compose(pose):
    top = round(head_s.height)            # 머리 갓이 위로 삐져나가는 만큼 여유
    W = round(max(head_s.width, base.width) * 1.5)
    c = Image.new("RGBA", (W, top + BH + 80))
    dx, dy = W / 2 - CX, top
    P = lambda x, y: (x + dx, y + dy)
    neck_at = P(CX, NECK + 12)
    def draw_arm(side, part="both"):
        a = arm_s if side < 0 else arm_s.transpose(Image.FLIP_LEFT_RIGHT)
        up, lo, (ex, ey) = split_arm(a)
        top = a.width * 0.35                  # 어깨 축 (팔 그림 위끝에서 조금 아래)
        sh = P(CX + side * SH_X, SH_Y)
        ua, la = pose["shoulder"][0 if side < 0 else 1] * side, pose["elbow"][0 if side < 0 else 1] * side
        r = math.radians(ua)
        el = (sh[0] + math.sin(r) * (ey - top), sh[1] + math.cos(r) * (ey - top))
        if part in ("both", "lo"):
            paste(c, lo, (ex, ey), el, ua + la)
        if part in ("both", "up"):
            paste(c, up, (ex, top), sh, ua)

    if hair_s:   # 뒷머리: 갓 밑 (머리 그림 35% 높이)부터 늘어뜨림
        paste(c, hair_s, (hair_s.width / 2, 0), (neck_at[0], neck_at[1] - head_s.height * 0.62))
    for side in (-1, 1):
        if leg_s:
            l = leg_s.transpose(Image.FLIP_LEFT_RIGHT) if side < 0 else leg_s   # 좌우 바꿔 끼움: 발끝이 바깥으로
            hip = P(CX + side * HIP_X, HEM - 30)
            ang = (pose["leg"] + LEG_OUT) * side
            paste(c, l, (l.width / 2, l.width * 0.3), hip, ang)
            if shoe:
                s = fit_h(shoe, l.width * 0.85)
                s = s.transpose(Image.FLIP_LEFT_RIGHT) if side < 0 else s   # 코가 바깥을 향하게
                r = math.radians(ang)
                fx, fy = hip[0] + math.sin(r) * (l.height - l.width * 0.3), hip[1] + math.cos(r) * (l.height - l.width * 0.3)
                paste(c, s, (s.width / 2, s.height * 0.75), (fx, fy), ang)
    paste(c, torso_s, (torso_s.width / 2, 0), P(CX, NECK - 8))
    paste(c, dress_s, (dress_s.width / 2, 0), P(CX, NECK - 14))
    if arm_s:
        for sd in (-1, 1):
            draw_arm(sd)         # 팔은 드레스 앞
    paste(c, collar_s, (collar_s.width / 2, 0), P(CX, NECK - 14))   # 목 프릴이 어깨 관절을 덮는다
    paste(c, head_s, (head_s.width / 2, head_s.height * 0.97), neck_at)
    bb = c.getbbox()
    return c.crop(bb)


POSES = {
    # 각도는 바깥쪽이 + (좌우 대칭으로 뒤집어 씀)
    "서기": {"shoulder": (6, 10), "elbow": (-18, -12), "leg": 0},     # 팔꿈치 살짝 안으로 굽혀 손이 치마 옆에
    "버둥": {"shoulder": (35, 50), "elbow": (-40, -30), "leg": 12},
    "손 흔들기": {"shoulder": (6, 120), "elbow": (-18, -55), "leg": 0},
}
shots = [compose(p) for p in POSES.values()]
H = max(s.height for s in shots)
shots = [fit_h(s, 700) if s.height > 700 else s for s in shots]
sheet = Image.new("RGBA", (sum(s.width for s in shots) + 40 * (len(shots) + 1), max(s.height for s in shots) + 80), (236, 230, 218, 255))
x = 40
for s in shots:
    sheet.alpha_composite(s, (x, sheet.height - 40 - s.height))
    x += s.width + 40
out = os.path.join(ROOT, "Logs/Screens", f"kit_{kid}_{form}.png")
os.makedirs(os.path.dirname(out), exist_ok=True)
sheet.save(out)
print(out)


# ── 게임 리그로 내보내기: python Tools/art/kit_preview.py fire 3 --export
# 서 있는 자세(각도 0) 좌표로 부품을 Characters/Evo/k_<정령>_<모습>_* 에 저장하고 evo_rig.json의 <정령>_<모습>을 바꿔 쓴다.
# 좌표는 fit_evo_parts.py와 같은 약속: 픽셀/100, 원점 = 발바닥 높이 · 목 x. 그림은 E배로 줄여 저장 (텍스처 크기)
# 얼굴: 머리 그림에서 눈·입을 떼어 _bare + face_normal, 공용 표정(Art/Raw_Dream/faces/fe_*)을 눈 폭에 맞춰 face_<표정>
# 복장: 기본(gill) 드레스 + 작업복(pick·climb·carry) 드레스가 있으면 body_<복장> · collar_<복장> (같은 캔버스·같은 자리)
if "--export" in sys.argv:
    import json
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import build_spirit_faces as FACES
    E = 0.5
    EVO = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/Evo")
    RIG = os.path.join(ROOT, "Assets/Resources/Rig/evo_rig.json")
    FGEN = os.path.join(ROOT, "Art/Raw_Dream/faces")
    pre = f"k_{kid}_{form}_"
    sc = lambda im: im.resize((max(1, round(im.width * E)), max(1, round(im.height * E))), Image.LANCZOS)

    def save(name, im, scale=True):
        (sc(im) if scale else im).save(os.path.join(EVO, pre + name + ".png"))
        return pre + name

    neck = (CX, NECK + 12)
    hip_y = HEM - 30
    leg_l = leg_s.transpose(Image.FLIP_LEFT_RIGHT)                 # 왼다리 = 좌우 바꿔 끼운 그림 (발끝 바깥)
    leg_top = leg_l.width * 0.3
    bottom = hip_y + leg_l.height - leg_top
    head_top = neck[1] - head_s.height * 0.97
    rig = lambda px, py: [round((px - CX) * E / 100, 4), round((bottom - py) * E / 100, 4)]

    # 몸통+드레스 한 장 (몸이 목을 축으로 같이 흔들린다). 모든 복장이 같은 캔버스·같은 자리
    dresses = {"": dress_s}
    for o in ("pick", "climb", "carry"):
        d = load(f"outfit_{o}_{form}_dress")
        if d is not None:
            dresses[o] = fit_h(d, (HEM - NECK) * 1.08)
    bw = round(max([torso_s.width] + [d.width for d in dresses.values()]))
    bh = round(max([torso_s.height + 6] + [d.height for d in dresses.values()]))
    x0, y0 = CX - bw / 2, NECK - 14
    for o, d in dresses.items():
        body = Image.new("RGBA", (bw, bh))
        body.alpha_composite(torso_s, (round(CX - torso_s.width / 2 - x0), 6))
        body.alpha_composite(d, (round(CX - d.width / 2 - x0), 0))
        col = Image.new("RGBA", (bw, bh))
        col.alpha_composite(collar_of(d), (round(CX - d.width / 2 - x0), 0))
        save("body" + (o and "_" + o), body)
        save("collar" + (o and "_" + o), col)

    # 머리 + 얼굴 층 + 표정
    head_e = np.asarray(sc(head_s)).astype(np.float32)
    eyes = FACES.find_eyes(head_e)
    skin = FACES.skin_near_eyes(head_e, eyes) if eyes is not None else np.array([240, 220, 200], np.float32)
    bare, neutral = FACES.split(head_e, skin)
    toimg = lambda a: Image.fromarray(np.uint8(np.clip(a, 0, 255)))
    save("head", head_s)
    save("head_bare", toimg(bare), False)
    save("face_normal", toimg(neutral), False)
    faces = ["normal"]
    if (neutral[:, :, 3] > 100).any():
        nw = FACES.obox(neutral)[2] - FACES.obox(neutral)[0] + 1
        for e in FACES.EXPRS:
            fp = os.path.join(FGEN, f"fe_{e}.png")
            if os.path.exists(fp):
                g = FACES.clean(FACES.load(fp))
                save("face_" + e, toimg(FACES.place(g, neutral, nw / (FACES.obox(g)[2] - FACES.obox(g)[0] + 1))), False)
                faces.append(e)

    up, lo, (ex, ey) = split_arm(arm_s)
    atop = arm_s.width * 0.35
    loc = lambda px, py: [round(px * E / 100, 4), round(-py * E / 100, 4)]
    look = {
        "id": f"{kid}_{form}", "kind": kid, "form": int(form), "face": pre + "face_",
        "body": {**dict(zip(("x", "y"), rig(x0 + bw / 2, y0 + bh / 2))), "sprite": pre + "body", "bare": ""},
        "head": {**dict(zip(("x", "y"), rig(CX, neck[1] - head_s.height * 0.97 + head_s.height / 2))),
                 "sprite": pre + "head", "bare": pre + "head_bare", "back": ""},
        "collar": pre + "collar", "collarPos": rig(x0 + bw / 2, y0 + bh / 2),
        "arm": "", "armUp": save("arm_up", up), "armLo": save("arm_lo", lo),
        "armCenter": loc(arm_s.width / 2 - ex, arm_s.height / 2 - atop), "armElbow": loc(0, ey - atop),
        "neck": rig(*neck), "shoulderL": rig(CX - SH_X, SH_Y), "shoulderR": rig(CX + SH_X, SH_Y),
        "armScale": 1.0, "armTilt": 0, "armRest": 0,
        "hair": save("hair", hair_s) if hair_s else "",
        "hairPos": rig(CX, neck[1] - head_s.height * 0.62 + hair_s.height / 2) if hair_s else [0, 0],
        "leg": save("leg", leg_l), "hipL": rig(CX - HIP_X, hip_y), "hipR": rig(CX + HIP_X, hip_y),
        "legCenter": loc(0, leg_l.height / 2 - leg_top), "legTilt": -LEG_OUT,
        "height": round((bottom - head_top) * E / 100, 4),
    }
    line = Image.open(os.path.join(EVO, f"evo_{kid}_{form}.png"))
    look["k"] = round(line.height / (look["height"] * 100), 4)     # 진화 줄 그림 키에 맞춤 (자랄수록 커짐)
    data = json.load(open(RIG, encoding="utf-8"))
    data["looks"] = [l for l in data["looks"] if l["id"] != look["id"]] + [look]
    data["looks"].sort(key=lambda l: l["id"])
    json.dump(data, open(RIG, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("exported", look["id"], "outfits", [o or "gill" for o in dresses], "faces", faces, "eyes", eyes is not None)
