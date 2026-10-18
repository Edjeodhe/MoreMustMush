"""코덱스로 만든 리그2 파츠(Art/Raw_Dream/rig2/gen_*.png)를 게임 크기·자리에 맞춰 Characters/Rig2/gen_fire_3_* 로 저장하고
spirit_rig2.json의 fire_3에 정면 다리(무릎) · 옆모습(side) · 뒷모습(back) 데이터를 넣는다 (불 정령 ★3 시범).

python Tools/rig-web/fit_gen_parts.py [--preview]
  - 크기: 정면 그림(k_fire_3_*)의 같은 부위 크기에 맞춘다 (머리 = 갓 폭, 몸통 = 높이, 머리카락 = 높이, 다리 = 엉덩이~발바닥 길이)
  - 다리는 한 장을 무릎·발목에서 나눈다 (허벅지/정강이 공 관절, 신발 = 갈색). 같은 캔버스라 부품 중심이 같다
  - 관절 자리는 눈대중 초기값: 웹 작업장(Tools/rig-web)에서 끌어서 맞추고 저장한다
  - 다시 돌리면 fire_3의 side/back과 정면 다리 칸을 새로 쓴다 (웹에서 고친 값이 지워짐)
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_parts as MP  # noqa: E402

ROOT = MP.ROOT
RAW = os.path.join(ROOT, "Art/Raw_Dream/rig2")
PPU = MP.PPU
KID = "fire_3"
LIFT = 0.34            # 다리를 늘인 만큼 몸을 올림 (리그 단위). 무릎이 치마 밑으로 보이게
THIGH_TOP_OVER = 0.12  # 허벅지 윗끝이 엉덩이 관절보다 이만큼 위 (치마 속)


def raw(name):
    im = np.asarray(Image.open(os.path.join(RAW, f"gen_{name}.png")).convert("RGBA")).copy()
    im[im[:, :, 3] < 12] = 0
    ys, xs = np.nonzero(im[:, :, 3] > 40)
    return im[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def scale(im, s):
    h, w = im.shape[:2]
    return np.asarray(Image.fromarray(im).resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)).copy()


def bbox(im):
    ys, xs = np.nonzero(im[:, :, 3] > 40)
    return xs.min(), ys.min(), xs.max(), ys.max()


def stub_x(im, bottom=True, frac=0.03):
    """목 그루터기 가운데 x: 맨 아래(또는 위) 몇 % 줄의 불투명 픽셀 가운데."""
    al = im[:, :, 3] > 40
    rows = np.nonzero(al.any(axis=1))[0]
    n = max(2, int(len(rows) * frac))
    sel = rows[-n:] if bottom else rows[:n]
    xs = np.nonzero(al[sel].any(axis=0))[0]
    return (xs.min() + xs.max()) / 2


def center_for(im, px, py, at):
    """그림의 픽셀 (px, py)가 리그 좌표 at에 오도록 할 때 그림 중심의 리그 좌표."""
    h, w = im.shape[:2]
    return [MP.r4(at[0] + (w / 2 - px) / PPU), MP.r4(at[1] - (h / 2 - py) / PPU)]


def leg_parts(name, flip, look, view):
    """다리 한 장 → 허벅지·정강이·신발 (같은 캔버스). 길이 = 엉덩이(+LIFT)~발바닥 + 허벅지 윗끝 여유."""
    L = raw(name)
    if flip:
        L = L[:, ::-1].copy()
    hip_y = look["hipL"][1] + LIFT
    target = (hip_y + THIGH_TOP_OVER - 0.0) * PPU          # 윗끝~발바닥 px
    L = scale(L, target / L.shape[0])
    h, w = L.shape[:2]
    sm = MP.shoe_mask(L)
    rows = np.nonzero(sm.any(axis=1))[0]
    shoe_top = int(rows[0]) if len(rows) else int(h * 0.8)
    top = 0
    hip_row = top + THIGH_TOP_OVER * PPU
    ankle_row = shoe_top + 3
    knee_row = int(round(hip_row + (ankle_row - hip_row) * 0.47))
    legp = MP.fade_rows(L, shoe_top + 6, shoe_top + 10, keep_above=True)
    shoe = L.copy()
    keep = np.zeros(L.shape[:2], bool)
    keep[shoe_top - 1:] = sm[shoe_top - 1:]
    from scipy import ndimage
    keep = ndimage.binary_dilation(keep, iterations=2) & (np.arange(h)[:, None] >= shoe_top - 1)
    shoe[~keep, 3] = 0
    thigh, shin, (kx, ky) = MP.ball_split(legp, knee_row, 1.04)
    al = L[:, :, 3] > 40
    def cx(r):
        c = np.nonzero(al[min(max(int(r), 0), h - 1)])[0]
        return (c[0] + c[-1]) / 2 if len(c) else w / 2
    hx = cx(hip_row)
    # 캔버스 중심: 엉덩이 관절(hx, hip_row)이 hipL에 오게
    hip = [look["hipL"][0], look["hipL"][1] + LIFT]            # 다리는 땅 좌표 (몸이 LIFT만큼 올라간 엉덩이에 붙음)
    c = center_for(L, hx, hip_row, hip)
    pre = f"gen_{KID}_{view}_"
    out = {
        "thigh": MP.part(MP.save(thigh, pre + "thigh"), *c),
        "shin": MP.part(MP.save(shin, pre + "shin"), *c),
        "shoe": MP.part(MP.save(shoe, pre + "shoe"), *c),
        "leg": MP.part("", *c),
        "knee": MP.px2rig(c, L.shape, kx, ky),
        "ankle": MP.px2rig(c, L.shape, cx(ankle_row), ankle_row),
        "sole": MP.r4(c[1] - h / 2 / PPU),
        "legScale": 1.0, "lift": LIFT,
    }
    return out, [L, thigh, shin, shoe]


def arm_at(look, sh):
    """정면 왼팔 관절(어깨·팔꿈치·손목)과 팔 그림을 어깨 sh로 옮긴 값 (팔 그림의 관절 원이 새 어깨에 오게)."""
    d0, d1 = sh[0] - look["shoulderL"][0], sh[1] - look["shoulderL"][1]
    mv = lambda a: [MP.r4(a[0] + d0), MP.r4(a[1] + d1)]
    out = {"shoulderL": [MP.r4(sh[0]), MP.r4(sh[1])], "elbow": mv(look["elbow"]), "wrist": mv(look["wrist"])}
    for k in ("upperArm", "foreArm", "hand"):
        out[k] = dict(look[k], x=MP.r4(look[k]["x"] + d0), y=MP.r4(look[k]["y"] + d1))
    return out


def shoulder_row(B):
    """몸통 그림에서 어깨 높이: 목 그루터기(위 끝)부터 폭이 처음 크게 넓어지는(어깨·옷깃) 줄 아래쪽."""
    al = B[:, :, 3] > 40
    w = al.sum(axis=1)
    rows = np.nonzero(w)[0]
    top = rows[0]
    stub = np.median(w[top:top + 4])
    wide = next((y for y in range(top, top + B.shape[0] // 2) if w[y] > stub * 2.2), top + 10)
    return wide + int(B.shape[0] * 0.08)


def main():
    preview = "--preview" in sys.argv
    rig_path = MP.NEW_RIG
    data = json.load(open(rig_path, encoding="utf-8"))
    look = next(l for l in data["looks"] if l["id"] == KID)
    for k in ("side", "back"):
        look.pop(k, None)
    E = MP.EVO
    front_head = np.asarray(Image.open(os.path.join(E, f"k_{KID}_head_bare.png")).convert("RGBA"))
    front_body = np.asarray(Image.open(os.path.join(E, f"k_{KID}_body.png")).convert("RGBA"))
    front_hair = np.asarray(Image.open(os.path.join(E, f"k_{KID}_hair.png")).convert("RGBA"))
    fhx0, fhy0, fhx1, fhy1 = bbox(front_head)
    fbx0, fby0, fbx1, fby1 = bbox(front_body)
    frx0, fry0, frx1, fry1 = bbox(front_hair)
    shots = []

    # ── 정면 다리 (무릎) ──
    legs, imgs = leg_parts("leg_front", True, look, "front")
    look.update(legs)
    shots += imgs

    neck = look["neck"]
    # 정면 머리 그림에서 목 그루터기 아래끝의 리그 y (옆·뒤 머리를 같은 높이에 붙인다)
    hb_y = look["head"]["y"] - (fhy1 - front_head.shape[0] / 2) / PPU
    # 정면 몸통 아래끝(치마 단)·위끝(목) 리그 y
    body_bot = look["chest"]["y"] - (fby1 - front_body.shape[0] / 2) / PPU
    body_top = look["chest"]["y"] + (front_body.shape[0] / 2 - fby0) / PPU

    def head_view(name, view):
        H = raw(name)
        H = scale(H, (fhx1 - fhx0 + 1) / H.shape[1])                 # 갓 폭을 정면과 같게
        sx = stub_x(H, True)
        c = center_for(H, sx, H.shape[0] - 1, [neck[0], hb_y])
        if view in ("back", "side"):
            # 옆·뒷모습: 머리 그림의 목 그루터기를 지운다. 머리 그림이 뒷머리 위에 그려지므로 그대로 두면 목이 뒷머리 위로 튀어나온다.
            # 목은 몸통 그림 위 끝 그루터기가 대신한다 (뒷머리 아래 → 뒷목은 머리카락에 덮이고, 옆모습 앞쪽 목만 보임)
            al = H[:, :, 3] > 40
            w = al.sum(axis=1)
            mx = w.max()
            y = H.shape[0] - 1
            while y > 0 and w[y] < mx * 0.4:
                y -= 1
            H = H.copy()
            H[y + 1:, :, 3] = 0
            H = MP.fade_rows(H, y - 4, y + 1, keep_above=True)
        _, y0, _, _ = bbox(H)
        cap_top = MP.px2rig(c, H.shape, sx, y0 + 4)
        return MP.part(MP.save(H, f"gen_{KID}_{view}_head"), *c), cap_top, H

    def body_view(name, view):
        B = raw(name)
        B = scale(B, (body_top - body_bot) * PPU / B.shape[0])
        sx = stub_x(B, False)
        c = center_for(B, sx, 0, [neck[0], body_top])
        seam = MP.waist_row(B)
        chest, skirt = MP.split_body(B, seam)
        waist = MP.px2rig(c, B.shape, sx, seam)
        return (MP.part(MP.save(chest, f"gen_{KID}_{view}_chest"), *c), MP.part(MP.save(skirt, f"gen_{KID}_{view}_skirt"), *c), waist, B)

    # ── 옆모습 (오른쪽을 봄) ──
    head, cap_top, Hs = head_view("head_side", "side")
    chest, skirt, waist, Bs = body_view("body_side", "side")
    R = raw("hair_side")
    R = scale(R, (fry1 - fry0 + 1) / R.shape[0])
    hair_top = look["hair"]["y"] + (front_hair.shape[0] / 2 - fry0) / PPU
    # 머리카락 오른쪽 끝을 뒤통수 근처(목보다 조금 앞)에 둔다
    hair_c = center_for(R, R.shape[1] - 1, 0, [neck[0] + 0.45, hair_top])
    hair = MP.part(MP.save(R, f"gen_{KID}_side_hair"), *hair_c)
    hair_root = [MP.r4(neck[0] + 0.05), MP.r4(hair_top - 0.35)]
    hp_y = look["hipL"][1]
    # 옆모습 어깨: 몸통 옆선 위쪽, 앞뒤 가운데 (그 줄의 몸통 왼끝·오른끝 가운데)
    sr = shoulder_row(Bs)
    cols = np.nonzero(Bs[sr, :, 3] > 40)[0]
    sh_side = MP.px2rig([chest["x"], chest["y"]], Bs.shape, (cols[0] + cols[-1]) / 2, sr)
    side = {"head": head, "face": "", "collar": MP.part("", 0, 0), "chest": chest, "skirt": skirt, "hair": hair,
            "waist": waist, "neck": neck, "hairRoot": hair_root, "capTop": cap_top,
            "hipL": [MP.r4(sh_side[0] - 0.02), hp_y], "hipR": [MP.r4(sh_side[0] + 0.05), hp_y]}
    side.update(arm_at(look, sh_side))
    side["shoulderR"] = [MP.r4(sh_side[0] + 0.05), MP.r4(sh_side[1] + 0.01)]   # 먼 쪽 어깨 (몸 뒤에서 살짝 비킴)
    sl, imgs = leg_parts("leg_side", False, side, "side")
    side.update(sl)
    shots += [Hs, Bs, R] + imgs

    # ── 뒷모습 ──
    head, cap_top, Hb = head_view("head_back", "back")
    chest, skirt, waist, Bb = body_view("body_back", "back")
    back = {"head": head, "face": "", "collar": MP.part("", 0, 0), "chest": chest, "skirt": skirt,
            "waist": waist, "capTop": cap_top}
    # 뒷모습 어깨: 등 위 좌우 끝 (어깨 줄에서 몸통 왼끝·오른끝 조금 안쪽)
    br = shoulder_row(Bb)
    cols = np.nonzero(Bb[br, :, 3] > 40)[0]
    inset = (cols[-1] - cols[0]) * 0.1
    shL = MP.px2rig([chest["x"], chest["y"]], Bb.shape, cols[0] + inset, br)
    shR = MP.px2rig([chest["x"], chest["y"]], Bb.shape, cols[-1] - inset, br)
    back.update(arm_at(look, shL))
    back["shoulderR"] = shR
    bl, imgs = leg_parts("leg_back", False, look, "back")
    back.update(bl)
    shots += [Hb, Bb] + imgs

    look["side"] = side
    look["back"] = back
    json.dump(data, open(rig_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("wrote", rig_path, "side/back for", KID)
    if preview:
        MP.sheet(shots, [], "rig3_gen_parts.png")


if __name__ == "__main__":
    main()
