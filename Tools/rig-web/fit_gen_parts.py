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
from PIL import Image, ImageFilter
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_parts as MP  # noqa: E402

ROOT = MP.ROOT
RAW = os.path.join(ROOT, "Art/Raw_Dream/rig2")
PPU = MP.PPU
KID = next((sys.argv[i + 1] for i, a in enumerate(sys.argv[:-1]) if a == "--look"), "fire_3")   # 맞출 모습 (--look dew_3)
LIFT = 0.34            # 다리를 늘인 만큼 몸을 올림 (리그 단위). 무릎이 치마 밑으로 보이게
THIGH_TOP_OVER = 0.12  # 허벅지 윗끝이 엉덩이 관절보다 이만큼 위 (치마 속)
SIDE_HAIR_SCALE = 0.26          # 옆모습 머리카락 한 장(hair_side_full 원본, 잘라낸 크기)의 배율
SIDE_HAIR_OFF = (-0.09, -0.95)  # 그 그림 중심 = 옆모습 머리 중심 + 이만큼 (얼굴 뒤를 빈틈없이 감싸고 갓 밑에서 나오게 맞춘 값)
SIDE_HAIR_BONES = (0.30, 0.48, 0.66, 0.84)  # 뼈 자리: 그림 높이의 이 비율 줄, 얼굴 쪽 홈 왼쪽 머리카락 덩어리의 가운데
BACK_HAIR_SCALE = 0.27          # 뒷모습 머리카락 한 장(hair_back_full 원본)의 배율
BACK_HAIR_OFF = (0.0, -1.0)     # 그 그림 중심 = 뒷모습 머리 중심 + 이만큼 (매끈한 정수리는 갓 밑에 숨고 갓 테두리 밑에서 머리카락이 나오게)
SIDE_SHOULDER_DROP = 0.01  # 옆모습 어깨 = 정면 어깨보다 이만큼 아래
SIDE_NECK_UP = 0.25    # 옆모습: 몸통 그림 목을 위로 이만큼 늘여 턱 속까지 (머리에 가려짐)
SIDE_CHIN_OVER = 0.03  #옆모습: 턱이 몸통 그림 목 그루터기의 (온전한 폭) 윗끝보다 이만큼 아래 = 머리가 잘린 목 끝을 덮는 만큼
SIDE_HEAD_BACK = 0.25  # 옆모습 머리(갓·얼굴)를 이만큼 뒤(-x)로: 턱 밑 목이 몸통 위 끝에 얹히게 (머리카락은 머리 기준이라 같이 간다)


DRESS_DONOR = {2: ("fire_2", "leaf_2", "dew_2"), 3: ("leaf_3", "dew_3")}   # 크림 드레스는 모든 정령이 같다 → 없으면 같은 등급에서 빌림


def has_raw(name):
    return os.path.exists(os.path.join(RAW, KID, f"gen_{name}.png")) or os.path.exists(os.path.join(RAW, KID, f"gen_{name}_tmp.png"))


def raw(name):
    """원본: 모습 폴더(Art/Raw_Dream/rig2/<모습>/gen_<부위>.png). 없으면
    - 다리·팔: 불 ★3 공용(Art/Raw_Dream/rig2/gen_<부위>.png)
    - 몸통·소매(크림 드레스): 같은 등급 다른 정령 것 (DRESS_DONOR)
    - 머리·머리카락: 없음 → 오류 (예전엔 불 ★3 머리를 대신 써서 다른 정령에 불 머리가 붙었다)"""
    f = os.path.join(RAW, KID, f"gen_{name}.png")
    if not os.path.exists(f) and os.path.exists(f[:-4] + "_tmp.png"):
        f = f[:-4] + "_tmp.png"                                                 # 임시 합성(make_side_head.py): 코덱스 원본이 나오면 그쪽이 우선
    if not os.path.exists(f) and name.startswith(("leg_", "arm_")):
        f = os.path.join(RAW, f"gen_{name}.png")
    if not os.path.exists(f) and name in ("body_side", "body_back", "sleeve"):
        form = int(KID.split("_")[1])
        f = next((g for d in DRESS_DONOR.get(form, ()) for g in [os.path.join(RAW, d, f"gen_{name}.png")] if os.path.exists(g)), f)
    if not os.path.exists(f):
        raise FileNotFoundError(f"{KID}: 원본 없음 gen_{name}.png (코덱스로 먼저 생성)")
    im = np.asarray(Image.open(f).convert("RGBA")).copy()
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


def cap_only(H):
    """머리 그림에서 갓(빨간 겉 + 회갈색 안쪽 주름)만 남기고 갓 밑 머리카락(밝은 크림색)은 지운 그림.
    뒷모습에서 이 그림을 긴 머리카락 위에 한 번 더 그려, 머리카락이 갓 테두리 밑에서 나와 맨 앞에 보이게 한다.
    세로줄마다 위에서 내려가며 갓 안쪽(어둡고 채도 있는 베이지)을 만난 뒤 밝은 크림색이 3줄 이어지는 곳 = 머리카락 시작 → 그 아래를 지운다.
    자른 선은 가로로 중앙값을 써서 매끄럽게 한다 (갓 테두리 선이 들쭉날쭉하지 않게)."""
    rgb = H[:, :, :3].astype(np.float32) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    al = H[:, :, 3] > 40
    under = al & (mx < 0.86) & (sat > 0.2) & (sat < 0.45)      # 갓 안쪽 주름
    bright = al & (mx >= 0.87) & (sat <= 0.45)                  # 머리카락 (크림색)
    h, w = al.shape
    cut = np.full(w, h, np.float32)                             # 이 줄부터 아래를 지움 (h = 안 지움)
    has = np.zeros(w, bool)
    for x in range(w):
        u = np.nonzero(under[:, x])[0]
        if not len(u):                                          # 안쪽이 안 보이는 줄 (갓 가장자리) = 다 갓
            continue
        b = bright[u[0]:, x]
        run = b[:-2] & b[1:-1] & b[2:] if len(b) > 2 else np.zeros(0, bool)
        r = np.nonzero(run)[0]
        if len(r):
            cut[x] = u[0] + r[0]
            has[x] = True
    sm = cut.copy()
    for x in np.nonzero(has)[0]:                                # 머리카락이 있는 줄끼리 중앙값 (폭 15)
        lo, hi = max(0, x - 7), min(w, x + 8)
        nb = cut[lo:hi][has[lo:hi]]
        sm[x] = np.median(nb)
    ys = np.arange(h, dtype=np.float32)[:, None]
    soft = np.clip(sm[None, :] - ys + 0.5, 0, 1)                # 자른 선에서 1px 흐리게
    out = H.copy()
    out[:, :, 3] = (H[:, :, 3] * soft).astype(np.uint8)
    return out


def _blur(m, r):
    return np.asarray(Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255


def cap_red_only(H):
    """옆모습 머리 그림에서 갓 겉(빨강 + 그 안의 흰 점)만 남긴 그림. 리그가 머리카락 위에 덮어(<머리>_cap) 머리카락이 갓 밑에서 나오게 한다."""
    from scipy import ndimage
    rgb = H[:, :, :3].astype(np.float32) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    red = (H[:, :, 3] > 40) & (sat > 0.42)
    lab, n = ndimage.label(ndimage.binary_closing(red, iterations=2))
    sz = ndimage.sum(red, lab, range(1, n + 1))
    m = ndimage.binary_fill_holes(lab == 1 + int(np.argmax(sz)))
    # 가장자리에 걸친 흰 점도 넣게: 세로줄마다 맨 위 ~ 그 줄의 마지막 빨강(갓 테두리)까지는 다 갓
    ys = np.arange(H.shape[0])[:, None]
    has = m.any(axis=0)
    last = np.where(has, H.shape[0] - 1 - np.argmax(m[::-1], axis=0), -1)
    m |= (ys <= last[None, :]) & has[None, :]
    m = ndimage.binary_dilation(m, iterations=2) & (H[:, :, 3] > 10)
    out = H.copy()
    out[:, :, 3] = (H[:, :, 3] * _blur(m, 0.8)).astype(np.uint8)
    return out

def side_head_without_hair(H, xb=((200, 228), (215, 242)), band=24, feather=12):
    """옆모습 머리 그림에서 얼굴 뒤 경계 xb(y)보다 뒤(왼쪽)의 머리카락을 지운 머리 바탕 (갓·얼굴·앞머리 + 얼굴 뒤 좁은 띠).
    새 머리카락 한 장(hair_side_full)이 그 자리를 덮는다. 얼굴 뒤 band px 띠의 옛 머리카락은 남겨 두어
    새 머리카락 가닥 사이 틈으로 배경 대신 머리카락이 보이게 하고, 띠의 뒤쪽 끝은 feather px 동안 서서히 지운다 (선이 안 생기게)."""
    base, _ = split_side_locks(H, xb, band=band)
    h, w = H.shape[:2]
    (y0, x0), (y1, x1) = xb
    ys = np.arange(h, dtype=np.float32)[:, None]
    xs = np.arange(w, dtype=np.float32)[None, :]
    edge = x0 + (x1 - x0) * np.clip((ys - y0) / max(1, y1 - y0), 0, 1)
    # split_side_locks는 경계 띠(feather)를 머리 바탕에 남긴다 → 그 띠도 머리카락이면 경계 쪽으로 서서히 지운다
    rgb = H[:, :, :3].astype(np.float32) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    hairish = _blur((H[:, :, 3] > 40) & (mx >= 0.8) & (sat <= 0.42), 1.5)
    ramp = np.clip(((edge - xs) - (band - feather)) / feather, 0, 1)     # 띠 안쪽(얼굴 쪽) 0 → 띠 뒤 끝 1
    ramp *= (edge - xs) < band + 4                                       # 띠에만 (더 뒤의 갓 안쪽 밝은 주름은 건드리지 않음)
    ramp *= 1 - cap_red_only(H)[:, :, 3].astype(np.float32) / 255       # 갓 겉(흰 점 포함)은 그대로
    out = base.copy()
    out[:, :, 3] = (base[:, :, 3] * (1 - hairish * ramp)).astype(np.uint8)
    return out


def split_side_locks(H, xb=((200, 228), (215, 242)), band=18, grow=2):
    """옆모습 머리 그림 → (머리 바탕, 뒤쪽 머리카락 층).
    뒤쪽 머리카락 = 갓을 뺀 밝은 크림색 가장 큰 덩어리 중 얼굴 뒤 경계선 xb(y)보다 뒤(왼쪽).
    - xb: (y, x) 두 점 사이를 이은 경계 (위 = 앞머리와 나누는 선, 아래 = 볼·목 뒤)
    - 경계에서 band px 동안 머리카락 층이 서서히 사라지고, 머리 바탕은 그 띠를 그대로 갖고 있다 → 층이 조금 돌아도 이음 자국이 안 보인다
    - 띠보다 안쪽은 머리 바탕에서 지운다 (그 뒤는 긴 머리카락이 메움)"""
    from scipy import ndimage
    h, w = H.shape[:2]
    rgb = H[:, :, :3].astype(np.float32) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    al = H[:, :, 3] > 40
    cap = al & ((sat > 0.42) | ((mx < 0.84) & (sat > 0.18)))
    hair = _blur(al & ~cap & (mx >= 0.8), 2.5) > 0.5
    hair &= _blur(cap, 4) < 0.35
    lab, n = ndimage.label(hair)
    sizes = ndimage.sum(hair, lab, range(1, n + 1))
    blob = lab == (1 + int(np.argmax(sizes)))
    # 머리카락 사이 어두운 그늘(색으로는 갓 안쪽과 비슷)도 머리카락에 넣는다: 틈 메우기 + 구멍 채우기
    blob = ndimage.binary_fill_holes(ndimage.binary_closing(blob, iterations=4))
    blob = ndimage.binary_dilation(blob, iterations=grow) & al            # 머리카락 테두리 선까지
    (y0, x0), (y1, x1) = xb
    ys = np.arange(h, dtype=np.float32)[:, None]
    xs = np.arange(w, dtype=np.float32)[None, :]
    edge = x0 + (x1 - x0) * np.clip((ys - y0) / max(1, y1 - y0), 0, 1)   # 줄마다 경계 x
    ramp = np.clip((edge - xs) / band, 0, 1)                             # 경계 0 → band px 뒤 1
    core = np.clip((edge - band - xs) / 4, 0, 1)                         # 띠보다 안쪽
    m = _blur(blob, 1.0)
    # 머리 바탕에서 지울 곳 = 띠 안쪽의 갓이 아닌 것 전부 (머리카락 그늘·잔조각까지). 갓 = 빨간 겉에 이어진 큰 덩어리(겉+안쪽)
    #   (겉의 흰 점은 빨강에 둘러싸인 구멍이라 채우기로 들어간다)
    capc, nc = ndimage.label(ndimage.binary_closing(cap, iterations=3))
    if nc:
        csz = ndimage.sum(cap, capc, range(1, nc + 1))
        cap_keep = _blur(ndimage.binary_fill_holes(capc == (1 + int(np.argmax(csz)))) & ~blob, 1.0)
    else:
        cap_keep = np.zeros((h, w), np.float32)
    a = H[:, :, 3].astype(np.float32)
    locks = H.copy(); locks[:, :, 3] = (a * m * ramp).astype(np.uint8)
    near = _blur(ndimage.binary_dilation(blob, iterations=10), 2.0)      # 머리카락 둘레만 (먼 갓 가장자리는 건드리지 않음)
    base = H.copy(); base[:, :, 3] = (a * (1 - core * np.maximum(m, near * (1 - cap_keep)))).astype(np.uint8)
    return base, locks


def hair_bones_of(R, center, whole=False):
    """머리카락 한 장의 뼈 사슬 자리 (리그 좌표): SIDE_HAIR_BONES 비율 줄마다,
    옆모습 = 얼굴 쪽 홈 왼쪽(뒤쪽) 머리카락 덩어리의 가운데 · whole(뒷모습) = 그림 가로 가운데 (좌우 대칭, 물결 윤곽에 흔들리지 않게)."""
    al = R[:, :, 3] > 40
    out = []
    for fr in SIDE_HAIR_BONES:
        r = int(R.shape[0] * fr)
        xs = np.nonzero(al[r])[0]
        seg = np.split(xs, np.nonzero(np.diff(xs) > 1)[0] + 1)[0]
        out.append(MP.px2rig(center, R.shape, R.shape[1] / 2 if whole else (seg[0] + seg[-1]) / 2, r))
    return out

ARM_UPPER = 0.573       # 그중 위팔 비율 (예전 0.4095 / 0.7145)
ARM_FAT = 1.45          # 팔 가로 배율 (길이는 그대로, 치비 몸에 맞게 통통하게)
SLEEVE_W = 0.36         # 소매 폭 (리그 단위)
SLEEVE_TILT = 17.7      # 불 ★3 소매 원본이 기운 각도 (입구가 수평이 되게 반시계로 돌린다). 다른 모습은 0, fit_overrides.json sleeveTilt로 바꿈


def straighten_arm(A, hand_from=0.8):
    """휘어 그려진 팔을 곧게: 줄마다 팔의 가운데가 한 세로줄에 오도록 가로로 민다 (가운데 선은 부드럽게 다듬어서).
    손(그림 높이 hand_from 아래)은 모양이 틀어지지 않게 손목 줄과 같은 만큼만 민다."""
    al = A[:, :, 3] > 40
    rows = np.nonzero(al.any(axis=1))[0]
    h, w = A.shape[:2]
    cx = np.full(h, np.nan, np.float32)
    for r in rows:
        xs = np.nonzero(al[r])[0]
        cx[r] = (xs[0] + xs[-1]) / 2
    r0, r1 = rows[0], rows[-1]
    rh = int(r0 + (r1 - r0) * hand_from)
    k = 25
    sm = cx.copy()
    for r in range(r0, rh + 1):
        sm[r] = np.nanmean(cx[max(r0, r - k):min(rh, r + k) + 1])
    sm[rh + 1:] = sm[rh]
    target = sm[r0 + (rh - r0) // 4]                                    # 위팔 윗부분 가운데에 맞춘다
    pad = int(np.nanmax(np.abs(sm[r0:r1 + 1] - target))) + 2
    out = np.zeros((h, w + 2 * pad, 4), np.uint8)
    for r in range(h):
        d = 0 if np.isnan(sm[r]) else int(round(target - sm[r]))
        out[r, pad + d:pad + d + w] = A[r]
    ys, xs = np.nonzero(out[:, :, 3] > 40)
    return out[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def arm_parts(look_views):
    """팔 한 장(arm_full, 어깨 공 관절 없이 매끈한 끝) → 위팔·아래팔·손 (같은 캔버스, 이음매는 둥글게 겹침) + 소매.
    - 어깨 축 = 그림 맨 위에서 반 폭 아래 가운데, 손목 = 아래 35% 안에서 가장 가는 줄, 팔꿈치 = 어깨~손목의 ARM_UPPER 지점
    - 위팔은 팔꿈치에서 둥글게 끝나고(반지름 = 그 줄 반 폭), 아래팔·손은 위끝이 둥글게 시작한다 → 굽혀도 잘린 면이 안 보인다
    look_views: 팔 자리를 쓸 모습 칸들 (정면 look, side, back). 칸마다 어깨(shoulderL) 기준으로 elbow·wrist·부품 자리를 새로 쓴다."""
    F = look_views[0]
    arm_len = float(np.hypot(F["wrist"][0] - F["shoulderL"][0], F["wrist"][1] - F["shoulderL"][1]))   # 이 모습의 예전 팔 길이 그대로
    A = straighten_arm(raw("arm_full")[:, ::-1].copy())   # 좌우 뒤집기: 생성 그림은 엄지가 바깥(몸 반대)쪽 → 몸 쪽으로
    al = A[:, :, 3] > 40
    rows = np.nonzero(al.any(axis=1))[0]
    def span(r):
        xs = np.nonzero(al[r])[0]
        return xs[0], xs[-1]
    top = rows[0]
    w0 = span(top + 40)
    sh_px = np.array([(w0[0] + w0[1]) / 2, top + (w0[1] - w0[0]) / 2])
    lo = int(rows[0] + (rows[-1] - rows[0]) * 0.62); hi = int(rows[0] + (rows[-1] - rows[0]) * 0.86)
    wr = min(range(lo, hi), key=lambda r: span(r)[1] - span(r)[0])
    wr_px = np.array([(span(wr)[0] + span(wr)[1]) / 2, wr])
    el_r = int(sh_px[1] + (wr_px[1] - sh_px[1]) * ARM_UPPER)
    el_px = np.array([(span(el_r)[0] + span(el_r)[1]) / 2, el_r])
    s = arm_len * PPU / np.hypot(*(wr_px - sh_px))
    k = np.array([s * ARM_FAT, s])                                      # 가로만 더 굵게 (생성 그림이 몸에 비해 가늘다)
    hh, ww = A.shape[:2]
    A = np.asarray(Image.fromarray(A).resize((max(1, round(ww * k[0])), max(1, round(hh * k[1]))), Image.LANCZOS)).copy()
    sh_px, el_px, wr_px = sh_px * k, el_px * k, wr_px * k
    al = A[:, :, 3] > 40
    h, w = A.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    def half_w(p):
        r = int(round(p[1])); c = np.nonzero(al[min(r, h - 1)])[0]
        return (c[-1] - c[0]) / 2 + 1.5
    def cut(a, keep):
        o = A.copy(); o[:, :, 3] = (A[:, :, 3] * _blur(keep, 0.7)).astype(np.uint8); return o
    re_, rw_ = half_w(el_px), half_w(wr_px)
    disk_e = (xs - el_px[0]) ** 2 + (ys - el_px[1]) ** 2 <= re_ ** 2
    disk_w = (xs - wr_px[0]) ** 2 + (ys - wr_px[1]) ** 2 <= rw_ ** 2
    upper = cut(A, (ys <= el_px[1]) | disk_e)
    fore = cut(A, ((ys >= el_px[1]) & (ys <= wr_px[1])) | disk_e | disk_w)
    hand = cut(A, (ys >= wr_px[1]) | disk_w)
    sp = {"upperArm": MP.save(upper, f"gen_{KID}_arm_upper"), "foreArm": MP.save(fore, f"gen_{KID}_arm_fore"), "hand": MP.save(hand, f"gen_{KID}_arm_hand")}
    # 소매: 입구가 수평이 되게 돌리고 폭을 맞춘다. 중심 = 어깨 축 조금 아래
    S = Image.fromarray(raw("sleeve")).rotate(override("sleeveTilt", SLEEVE_TILT if KID == "fire_3" else 0.0), resample=Image.BICUBIC, expand=True)
    S = np.asarray(S).copy(); S[S[:, :, 3] < 12] = 0
    ys2, xs2 = np.nonzero(S[:, :, 3] > 40); S = S[ys2.min():ys2.max() + 1, xs2.min():xs2.max() + 1]
    S = scale(S, SLEEVE_W * PPU / S.shape[1])
    sleeve_sp = MP.save(S, f"gen_{KID}_sleeve")
    for L in look_views:
        sh = L["shoulderL"]
        P = lambda px: [MP.r4(sh[0] + (px[0] - sh_px[0]) / PPU), MP.r4(sh[1] - (px[1] - sh_px[1]) / PPU)]
        c = P([w / 2, h / 2])
        L["elbow"], L["wrist"] = P(el_px), P(wr_px)
        for k, v in sp.items():
            L[k] = MP.part(v, *c)
        if L.get("sleeveOn"):
            L["sleeve"] = MP.part(sleeve_sp, MP.r4(sh[0] + 0.01), MP.r4(sh[1] - 0.05))
    return sp


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


OVR_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fit_overrides.json")
# 머리카락 한 장 자리 (불 ★3에서 맞춘 값을 목 기준으로): 너비(리그 단위) · 윗끝 = 목 y + top · 가운데 x = 목 x + cx
HAIR_FIT = {"side": {"w": 2.59, "top": 2.03, "cx": -0.41}, "back": {"w": 2.95, "top": 2.15, "cx": 0.025}}


def override(key, default):
    """fit_overrides.json의 {모습: {key: 값}} (눈으로 보고 고친 값). 없으면 default."""
    try:
        return json.load(open(OVR_FILE, encoding="utf-8")).get(KID, {}).get(key, default)
    except FileNotFoundError:
        return default


def write_layout(look, view, out):
    """머리카락 생성용 참고 그림: 그 방향의 머리(머리카락 없음)·몸통·치마를 리그 자리대로 겹친 흰 바탕 그림 (200px/단위)."""
    V = look if view == "front" else look[view]
    W0, X1, Y0, Y1, Sc = -2.3, 2.3, 0.9, 5.9, 200
    can = Image.new("RGBA", (int((X1 - W0) * Sc), int((Y1 - Y0) * Sc)), (255, 255, 255, 255))
    for k in ("skirt", "chest", "collar", "head"):
        if not (V.get(k) or {}).get("sprite"):
            continue
        im = Image.open(os.path.join(os.path.dirname(MP.OUT), V[k]["sprite"] + ".png")).convert("RGBA")
        im = im.resize((im.size[0] * 2, im.size[1] * 2), Image.LANCZOS)
        w, h = im.size
        can.alpha_composite(im, (int((V[k]["x"] - W0) * Sc - w / 2), int((Y1 - V[k]["y"]) * Sc - h / 2)))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    can.convert("RGB").save(out)


def fit_hair(look, view):
    """머리카락 한 장(hair_<view>_full)을 목 기준 자리에 맞추고 뼈 사슬(hairBones)을 단다. fit_overrides.json의 <view>Hair {dx, dy, s}로 고친다."""
    V = look[view]
    neck = V.get("neck", look["neck"])
    f = HAIR_FIT[view]
    o = override(view + "Hair", {})
    R = raw(f"hair_{view}_full")
    R = scale(R, f["w"] * PPU * o.get("s", 1.0) / R.shape[1])
    h = R.shape[0] / PPU
    c = [MP.r4(neck[0] + f["cx"] + o.get("dx", 0)), MP.r4(neck[1] + f["top"] - h / 2 + o.get("dy", 0))]
    V["hair"] = MP.part(MP.save(R, f"gen_{KID}_{view}_hair"), *c)
    V["hairBones"] = hair_bones_of(R, c, whole=(view == "back"))
    V["hairRoot"] = list(V["hairBones"][0])


def fit_front_hair(look):
    """정면 뒷머리 한 장(hair_front_full)을 예전 정면 머리카락(k_<모습>_hair) 자리(너비·윗끝·가운데)에 맞추고 세로 가운데 뼈 사슬을 단다."""
    if "hairOrig" not in look:
        look["hairOrig"] = dict(look["hair"])                                   # 예전 자리 기억 (다시 돌려도 같은 기준)
    O = look["hairOrig"]
    old = np.asarray(Image.open(os.path.join(MP.EVO, O["sprite"].split("/", 1)[1] + ".png")).convert("RGBA"))
    ox0, oy0, ox1, oy1 = bbox(old)
    top = O["y"] + (old.shape[0] / 2 - oy0) / PPU
    cx = O["x"] + ((ox0 + ox1) / 2 - old.shape[1] / 2) / PPU
    o = override("frontHair", {})
    R = raw("hair_front_full")
    R = scale(R, (ox1 - ox0 + 1) * o.get("s", 1.0) / R.shape[1])
    c = [MP.r4(cx + o.get("dx", 0)), MP.r4(top - R.shape[0] / 2 / PPU + o.get("dy", 0))]
    look["hair"] = MP.part(MP.save(R, f"gen_{KID}_front_hair"), *c)
    look["hairBones"] = hair_bones_of(R, c, whole=True)
    look["hairRoot"] = list(look["hairBones"][0])


def clean_face(F):
    """예전 얼굴 그림(통짜에서 잘라낸 것)에서 눈·입 덩어리만 남긴다: 아주 어두운 점(눈동자·입 선)이 15개 넘게 든 덩어리만."""
    from scipy import ndimage
    al = F[:, :, 3] > 30
    v = F[:, :, :3].max(axis=2) / 255.0
    lab, n = ndimage.label(ndimage.binary_dilation(al, iterations=1))
    keep = np.zeros(al.shape, bool)
    for i in range(1, n + 1):
        m = lab == i
        if ((v < 0.35) & m & al).sum() >= 15:
            keep |= m
    out = F.copy()
    out[:, :, 3] = (F[:, :, 3] * _blur(keep, 0.6)).astype(np.uint8)
    return out


def stage_mush(data, look):
    """★0·★1: 새로 그린 갓·몸통·발·팔(m_*)을 예전 잘라 쓴 그림(r2_*) 자리에 맞춰 같은 캔버스로 저장, 얼굴은 눈·입만 남긴다."""
    def old(name):
        return np.asarray(Image.open(os.path.join(MP.OUT, name.split("/", 1)[1] + ".png")).convert("RGBA"))
    def place(src, ref, align="center", flip=False):
        R = raw(src)
        if flip:
            R = R[:, ::-1].copy()
        x0, y0, x1, y1 = bbox(ref)
        bw, bh = x1 - x0 + 1, y1 - y0 + 1
        k = min(bw / R.shape[1], bh / R.shape[0]) if align != "width" else bw / R.shape[1]
        R = scale(R, k)
        can = np.zeros_like(ref)
        h, w = R.shape[:2]
        px = int(round((x0 + x1) / 2 - w / 2))
        py = y0 if align == "width" else (y1 - h + 1 if align == "bottom" else int(round((y0 + y1) / 2 - h / 2)))
        ys0, xs0 = max(0, py), max(0, px)
        ys1, xs1 = min(can.shape[0], py + h), min(can.shape[1], px + w)
        can[ys0:ys1, xs0:xs1] = R[ys0 - py:ys1 - py, xs0 - px:xs1 - px]
        return can
    pre = f"gen_{KID}_"
    for part, src, align, flip in (("cap", "m_cap", "width", False), ("stem", "m_stem", "bottom", False),
                                   ("footL", "m_foot", "center", False), ("footR", "m_foot", "center", True),
                                   ("nubL", "m_nub", "center", False), ("nubR", "m_nub", "center", True)):
        if part not in look or not os.path.exists(os.path.join(RAW, KID, f"gen_{src}.png")):
            continue
        ref = old(look[part].get("orig", look[part]["sprite"]))
        look[part].setdefault("orig", look[part]["sprite"])
        img = place(src, ref, align, flip)
        look[part]["sprite"] = MP.save(img, pre + part)
        if part == "cap":
            # 갓 축 = 갓이 몸에 붙는 곳 (가운데 세로줄들의 갓 맨 아래보다 조금 위). 예전엔 테두리 높이라 깔때기 갓(번개·바람)은
            # 걸을 때 갓이 기울면 몸과 붙는 곳이 옆으로 미끄러져 머리가 떨어져 보였다
            al = img[:, :, 3] > 40
            w = img.shape[1]
            mid = al[:, int(w * 0.42):int(w * 0.58)]
            rows = np.nonzero(mid.any(axis=1))[0]
            bottom = rows[-1] if len(rows) else img.shape[0] // 2
            look.setdefault("capJointOrig", list(look["capJoint"]))
            jy = MP.px2rig([look["cap"]["x"], look["cap"]["y"]], img.shape, w / 2, bottom - 0.12 * PPU)[1]
            look["capJoint"] = [look["capJointOrig"][0], MP.r4(min(jy, look["capJointOrig"][1]))]
    face = look.get("faceOrig", look["face"])
    look["faceOrig"] = face
    for e in ("normal", "blink", "happy", "surprise", "effort"):
        f = os.path.join(MP.OUT, face.split("/", 1)[1] + e + ".png")
        if os.path.exists(f):
            MP.save(clean_face(np.asarray(Image.open(f).convert("RGBA"))), pre + "face_" + e)
    look["face"] = "Rig2/" + pre + "face_"


def collar_row(B):
    """옆모습 몸통 그림: (위 끝 줄, 옷깃이 시작되는 줄, 몸통 위쪽 최대 폭). 옷깃 = 위에서 내려가며 폭이 위쪽 35% 최대 폭의 55%를 처음 넘는 줄.
    생성 그림의 목 그루터기는 몸통 폭의 절반 가까이로 굵어서 그대로 쓰면 목이 두껍다 → 이 줄 위(그림의 목)는 버리고 목을 새로 그린다."""
    w = (B[:, :, 3] > 40).sum(axis=1)
    rows = np.nonzero(w)[0]
    top = int(rows[0])
    maxw = float(w[top:top + int((rows[-1] - top) * 0.35) + 1].max())
    cr = next((y for y in range(top, rows[-1]) if w[y] > maxw * 0.55), top)
    return top, int(cr), maxw


def round_chin(H, y, look=20, fit=6):
    """턱 맨 아래(y 줄) 근처에서 테두리가 곧게 내려오는 곳(머리 그림 목 그루터기의 남은 옆선 = 각진 계단)을
    그 위 턱 곡선의 기울기로 이어 깎는다. 왼쪽(뒤통수 쪽)·오른쪽 다 본다."""
    out = H.copy()
    al = H[:, :, 3] > 40
    rows = [r for r in range(max(0, y - look), y + 1) if al[r].any()]
    if len(rows) < fit + 3:
        return out
    for side in (0, 1):
        E = np.array([np.nonzero(al[r])[0][-1 if side else 0] for r in rows], np.float32)
        sgn = -1 if side else 1                                   # 턱 곡선에서 테두리가 안쪽으로 가는 방향
        d = np.diff(E) * sgn
        # 안쪽으로 가던 테두리가 멈추는 첫 줄 (그 뒤로 2줄 이상 안 움직임)
        k = next((i for i in range(fit, len(d) - 1) if d[i] <= 0 and d[i + 1] <= 0), None)
        if k is None:
            continue
        slope = max(1.0, float(np.mean(d[k - fit:k])))
        xs = np.arange(H.shape[1], dtype=np.float32)
        for j, r in enumerate(rows[k + 1:], 1):
            edge = E[k] + sgn * slope * j
            keep = np.clip((xs - edge) * sgn + 0.5, 0, 1)
            out[r, :, 3] = (out[r, :, 3] * keep).astype(np.uint8)
    return out


def neck_full_row(B):
    """옆모습 몸통 그림: 위 끝부터 목 그루터기가 온전한 폭(그루터기 줄 폭 중앙값의 90%)이 되는 첫 줄까지의 줄 수.
    생성 그림의 목 윗끝은 비스듬히 잘려 있어, 턱이 이 줄보다 아래로 와야 잘린 끝이 안 보인다."""
    top, wide, _ = collar_row(B)
    w = (B[:, :, 3] > 40).sum(axis=1)
    med = float(np.median(w[top:max(top + 1, wide)]))
    return next((y for y in range(top, wide) if w[y] >= med * 0.9), top) - top


def eye_span(face_sprite):
    """얼굴 그림(…_face_normal)의 눈~입 어두운 점 가로 폭(px): 얼굴 크기 기준."""
    f = np.asarray(Image.open(os.path.join(os.path.dirname(MP.OUT), face_sprite + ".png")).convert("RGBA"))
    d = (f[:, :, 3] > 100) & (f[:, :, :3].max(axis=2) < 90)
    xs = np.nonzero(d.any(axis=0))[0]
    return float(xs[-1] - xs[0]) if len(xs) else 0.0


def scale_front_head(look):
    """★2·★3: 같은 등급 불 정령(fire_<★>)과 얼굴 크기를 같게 (사용자 2026-10-19: 크기·비율을 불에 맞춤).
    정면 머리·얼굴 그림을 배율만큼 키운/줄인 복사본(gen_<모습>_front_head, …_front_face_*)으로 바꾸고, 목 기준으로 자리·capTop을 옮긴다.
    예전 값은 headOrig·faceOrig·capTopOrig에 두어 다시 돌려도 같은 결과. 돌려주는 값 = 배율."""
    look.setdefault("headOrig", dict(look["head"]))
    look.setdefault("faceOrig", look["face"])
    look.setdefault("capTopOrig", list(look["capTop"]))
    ref = f"fire_{look['form']}"
    if KID == ref:
        return 1.0
    data = json.load(open(MP.NEW_RIG, encoding="utf-8"))
    R = next(l for l in data["looks"] if l["id"] == ref)
    mine = eye_span(look["faceOrig"] + "normal")
    hs = eye_span(R.get("faceOrig", R["face"]) + "normal") / mine if mine >= 5 else 1.0   # 눈을 못 찾으면 그대로
    hs = override("headScale", float(np.clip(hs, 0.6, 1.6)))                           # 엉뚱한 값으로 그림이 터지지 않게
    N = look["neck"]
    H0 = look["headOrig"]
    src = np.asarray(Image.open(os.path.join(os.path.dirname(MP.OUT), H0["sprite"] + ".png")).convert("RGBA"))
    look["head"] = MP.part(MP.save(scale(src, hs), f"gen_{KID}_front_head"),
                           MP.r4(N[0] + (H0["x"] - N[0]) * hs), MP.r4(N[1] + (H0["y"] - N[1]) * hs))
    for e in ("normal", "blink", "happy", "surprise", "effort", "love", "tired"):
        f = os.path.join(os.path.dirname(MP.OUT), look["faceOrig"] + e + ".png")
        if os.path.exists(f):
            MP.save(scale(np.asarray(Image.open(f).convert("RGBA")), hs), f"gen_{KID}_front_face_{e}")
    look["face"] = f"Rig2/gen_{KID}_front_face_"
    ct = look["capTopOrig"]
    look["capTop"] = [MP.r4(N[0] + (ct[0] - N[0]) * hs), MP.r4(N[1] + (ct[1] - N[1]) * hs)]
    look["headScale"] = round(hs, 4)
    look.setdefault("heightOrig", look["height"])
    look["height"] = MP.r4(look["heightOrig"] + look["capTop"][1] - ct[1])      # 키(갓 꼭대기~발바닥)도 머리 크기만큼
    return hs


SIDE_FACE_SHIFT = 0.12  # 임시 옆모습(정면 머리): 눈·입 층을 걷는 쪽으로 이만큼 옮겨 3/4로 보이게


def front_as_side(look):
    """옆 머리 원본(head_side)이 아직 없는 ★2의 임시 옆모습 머리: 정면 머리 그림을 그대로 쓰고(몸통·다리·팔은 옆모습),
    눈·입이 따로 된 얼굴 층이면 SIDE_FACE_SHIFT만큼 오른쪽(걷는 쪽)으로 옮긴 복사본을 쓴다. 얼굴이 머리 그림에 그려진 정령은 정면 얼굴 그대로.
    코덱스로 head_side를 뽑은 뒤 --stage body를 다시 돌리면 진짜 옆 머리로 바뀐다."""
    face = look["face"]
    base = os.path.join(os.path.dirname(MP.OUT), face)
    n = np.asarray(Image.open(base + "normal.png").convert("RGBA")) if os.path.exists(base + "normal.png") else None
    if n is None or not (n[:, :, 3] > 40).any():
        print(KID, "얼굴이 머리 그림에 그려져 있음 → 임시 옆모습은 정면 얼굴 그대로")
        return dict(look["head"]), list(look["capTop"]), face
    d = int(round(SIDE_FACE_SHIFT * PPU))
    for e in ("normal", "blink", "happy", "surprise", "effort", "love", "tired"):
        if os.path.exists(base + e + ".png"):
            f = np.asarray(Image.open(base + e + ".png").convert("RGBA"))
            o = np.zeros_like(f); o[:, d:] = f[:, :-d]
            MP.save(o, f"gen_{KID}_side_face_{e}")
    print(KID, "임시 옆모습: 정면 머리 + 얼굴", SIDE_FACE_SHIFT, "이동")
    return dict(look["head"]), list(look["capTop"]), f"Rig2/gen_{KID}_side_face_"


def stage_body(data, look):
    """다리(정면·옆·뒤)·머리·몸통(옆·뒤)·팔·소매를 맞춰 look에 쓰고, 머리카락 생성용 배치 그림을 만든다. 머리카락 칸은 있던 값을 둔다."""
    if not has_raw("head_side") and look["form"] != 2:
        sys.exit(f"{KID}: 옆머리(head_side) 원본 없음 → 코덱스로 먼저 생성")
    old = {v: {k: look[v][k] for k in ("hair", "hairRoot", "hairBones") if k in look.get(v, {})} for v in ("side", "back")}
    for k in ("side", "back"):
        look.pop(k, None)
    E = MP.EVO
    front_head = np.asarray(Image.open(os.path.join(E, f"k_{KID}_head_bare.png")).convert("RGBA"))
    front_body = np.asarray(Image.open(os.path.join(E, f"k_{KID}_body.png")).convert("RGBA"))
    fhx0, fhy0, fhx1, fhy1 = bbox(front_head)
    fbx0, fby0, fbx1, fby1 = bbox(front_body)

    legs, _ = leg_parts("leg_front", True, look, "front")
    look.update(legs)
    neck = look["neck"]
    hs = scale_front_head(look)                                                # 같은 등급 불 정령과 얼굴 크기를 맞춘 배율
    hb_y = neck[1] + (look["headOrig"]["y"] - (fhy1 - front_head.shape[0] / 2) / PPU - neck[1]) * hs   # 정면 머리 그림 목 그루터기 아래끝
    body_bot = look["chest"]["y"] - (fby1 - front_body.shape[0] / 2) / PPU
    body_top = look["chest"]["y"] + (front_body.shape[0] / 2 - fby0) / PPU

    def chin_row(H):
        """턱 맨 아래 줄: 아래(목 그루터기)에서 올라가며 폭이 목 폭의 1.3배를 처음 넘는 줄 + 3 (둥근 턱 선은 다 남긴다).
        예전엔 '갓 폭의 40%'로 잘라 턱 가운데가 수평으로 잘렸다."""
        w = (H[:, :, 3] > 40).sum(axis=1)
        rows = np.nonzero(w)[0]
        bot = rows[-1]
        stub = np.median(w[bot - 16:bot - 3])                                 # 맨 끝 몇 줄은 가늘게 흐려져 있어 뺀다
        y = bot
        while y > rows[0] and w[y] <= stub * 1.3:
            y -= 1
        return min(bot, y + 1)
    # 정면 턱 높이 (리그 y): 옆모습 턱을 여기에 맞춰 돌아설 때 머리 높이가 같고 목 길이도 같게
    front_chin_y = neck[1] + (look["headOrig"]["y"] - (chin_row(front_head) - front_head.shape[0] / 2) / PPU - neck[1]) * hs

    def head_view(name, view):
        H = raw(name)
        H = scale(H, (fhx1 - fhx0 + 1) * hs / H.shape[1])                    # 갓 폭을 정면과 같게 (얼굴 크기 배율 포함)
        sx = stub_x(H, True)
        c = center_for(H, sx, H.shape[0] - 1, [neck[0], hb_y])
        if view == "side" and KID != "fire_3":
            # 정면 턱보다 높아지지는 않게
            # 턱 = 몸통 그림에 그려진 목 그루터기의 비스듬히 잘린 윗끝보다 SIDE_CHIN_OVER 아래 (머리가 잘린 끝을 덮고, 그림의 목·옷깃은 그대로 보인다)
            Bq = raw("body_side")
            Bq = scale(Bq, (body_top - body_bot) * PPU / Bq.shape[0])
            c = center_for(H, sx, chin_row(H), [neck[0], min(front_chin_y, body_top - neck_full_row(Bq) / PPU - SIDE_CHIN_OVER)])
        cut_side = view == "side" and KID != "fire_3"
        if (view == "back" and look["form"] >= 3) or cut_side:
            # ★3 뒷모습: 뒷목 그루터기는 지운다 (긴 머리카락에 덮임). ★2는 뒤에서 목을 남긴다
            # 옆모습(불 ★3 빼고): 머리 그림의 목 그루터기를 턱 아래에서 지우고, 목은 몸통 그림에 그려진 목·옷깃을 그대로 쓴다
            #   (머리 목과 몸통 목이 두 토막으로 겹쳐 보이던 이음새 없애기, 사용자 2026-10-19)
            y = chin_row(H)
            H = H.copy(); H[y + 1:, :, 3] = 0
            H = MP.fade_rows(H, y - 2, y + 1, keep_above=True)
            if cut_side:
                H = round_chin(H, y)                                           # 턱 뒤쪽 각진 계단(남은 목 옆선) 깎기
        MP.save(cap_red_only(H), f"gen_{KID}_{view}_head_cap")               # 갓 겉만: 머리카락 위에 덮는다
        if view == "side" and KID == "fire_3":
            H = side_head_without_hair(H)                                      # 불 ★3 원본만 귀 뒤 머리카락이 그려져 있다
        _, y0, _, _ = bbox(H)
        cap_top = MP.px2rig(c, H.shape, sx, y0 + 4)
        return MP.part(MP.save(H, f"gen_{KID}_{view}_head"), *c), cap_top

    def body_view(name, view):
        B = raw(name)
        B = scale(B, (body_top - body_bot) * PPU / B.shape[0])
        sx = stub_x(B, False)
        seam = MP.waist_row(B)
        ext = 0
        if view == "side" and KID != "fire_3":
            # 옆모습 목: 그림의 목 그루터기는 윗끝이 비스듬히 잘려 뒤쪽 모서리가 턱 뒤로 삐져나온다 →
            # 온전한 폭 줄을 위로 SIDE_NECK_UP만큼 이어 턱 속까지 올린다 (뒷목 선이 턱 곡선에 자연스럽게 닿음, 걸을 때 머리가 기울어도 틈이 안 생김)
            top = int(np.nonzero((B[:, :, 3] > 40).any(axis=1))[0][0])
            nf = top + neck_full_row(B) + 1
            ext = int(round(SIDE_NECK_UP * PPU))
            B = np.concatenate([np.zeros((ext, B.shape[1], 4), B.dtype), B], axis=0)
            B[:ext + nf] = B[ext + nf]
            seam += ext
        c = center_for(B, sx, ext, [neck[0], body_top])
        chest, skirt = MP.split_body(B, seam)
        waist = MP.px2rig(c, B.shape, sx, seam)
        return MP.part(MP.save(chest, f"gen_{KID}_{view}_chest"), *c), MP.part(MP.save(skirt, f"gen_{KID}_{view}_skirt"), *c), waist, B

    # 옆모습
    side_face = ""
    if has_raw("head_side"):
        head, cap_top = head_view("head_side", "side")
    else:
        head, cap_top, side_face = front_as_side(look)                         # ★2 임시: 옆 머리 그림이 나오기 전까지
    if KID == "fire_3":
        head["x"] = MP.r4(head["x"] - SIDE_HEAD_BACK); cap_top[0] = MP.r4(cap_top[0] - SIDE_HEAD_BACK)   # 목이 몸통에 얹히게 (불 ★3 손맞춤)
    chest, skirt, waist, Bs = body_view("body_side", "side")
    hp_y = look["hipL"][1]
    sr = shoulder_row(Bs)
    if KID != "fire_3":
        # 어깨 높이 = 정면 어깨 높이 - SIDE_SHOULDER_DROP (물 ★2 기준, 사용자 2026-10-19: 팔 길이를 물 정령과 같게).
        # 그림 모양으로 찾던 높이는 몸통 그림마다 들쭉날쭉해(불 ★2 +0.35) 손이 가슴에서 끝나 팔이 짧아 보였다
        sr = int(round(Bs.shape[0] / 2 - (look["shoulderL"][1] - SIDE_SHOULDER_DROP - chest["y"]) * PPU))
    cols = np.nonzero(Bs[sr, :, 3] > 40)[0]
    sh_side = MP.px2rig([chest["x"], chest["y"]], Bs.shape, (cols[0] + cols[-1]) / 2, sr)
    side = {"head": head, "face": side_face, "collar": MP.part("", 0, 0), "chest": chest, "skirt": skirt,
            "waist": waist, "neck": neck, "capTop": cap_top,
            "hipL": [MP.r4(sh_side[0] - 0.02), hp_y], "hipR": [MP.r4(sh_side[0] + 0.05), hp_y]}
    side.update(arm_at(look, sh_side))
    side["shoulderR"] = [MP.r4(sh_side[0] + 0.05), MP.r4(sh_side[1] + 0.01)]
    side.update(leg_parts("leg_side", False, side, "side")[0])
    # 뒷모습 (뒷머리 그림이 없으면 건너뜀 → 리그는 뒤돌 때 정면 값을 쓴다)
    back = None
    if has_raw("head_back"):
        head, cap_top = head_view("head_back", "back")
        chest, skirt, waist, Bb = body_view("body_back", "back")
        back = {"head": head, "face": "", "collar": MP.part("", 0, 0), "chest": chest, "skirt": skirt, "waist": waist, "capTop": cap_top}
        br = shoulder_row(Bb)
        cols = np.nonzero(Bb[br, :, 3] > 40)[0]
        inset = (cols[-1] - cols[0]) * 0.1
        back.update(arm_at(look, MP.px2rig([chest["x"], chest["y"]], Bb.shape, cols[0] + inset, br)))
        back["shoulderR"] = MP.px2rig([chest["x"], chest["y"]], Bb.shape, cols[-1] - inset, br)
        back.update(leg_parts("leg_back", False, look, "back")[0])
        look["back"] = back
    else:
        print(KID, "뒷머리(head_back) 원본 없음 → 뒷모습 건너뜀")
    look["side"] = side
    views = [v for v in (side, back) if v is not None]
    # 팔 (정면·옆·뒤) + 소매 (옆·뒤)
    for V in views:
        V["sleeveOn"] = True
    arm_parts([look] + views)
    for V in views:
        V.pop("sleeveOn")
    for v in ("side", "back"):
        if v in look:
            look[v].update(old[v])
            if "hair" not in look[v]:
                # 이 방향 머리카락 그림이 아직 없으면 비워 둔다 (안 그러면 정면 뒷머리가 옆·뒤에서 몸 위에 그려짐)
                look[v]["hair"] = MP.part("", 0, 0)
            write_layout(look, v, os.path.join(RAW, KID, f"ref_{v}_layout.png"))
    write_layout(look, "front", os.path.join(RAW, KID, "ref_front_layout.png"))


def stage_hair(data, look):
    for v in ("side", "back"):
        if os.path.exists(os.path.join(RAW, KID, f"gen_hair_{v}_full.png")):
            fit_hair(look, v)
    if os.path.exists(os.path.join(RAW, KID, "gen_hair_front_full.png")):
        fit_front_hair(look)


def main():
    """python Tools/rig-web/fit_gen_parts.py --look dew_3 --stage body|hair|all
    body: 다리·머리·몸통·팔·소매 (gen_looks phase 1 원본) · hair: 머리카락 한 장 + 뼈 사슬 (phase 2 원본).
    ⚠ fire_3은 손으로 맞춘 값이 있어 다시 돌리지 않는다 (--force 없이는 거절)."""
    stage = next((sys.argv[i + 1] for i, a in enumerate(sys.argv[:-1]) if a == "--stage"), "all")
    if stage == "nohair":
        # ★2: 뒷머리카락을 정면·옆·뒤 모두 뗀다 (★3 긴 머리와 차별, 사용자 2026-10-19). 머리 그림에 붙은 앞머리·짧은 머리는 그대로
        rig_path = MP.NEW_RIG
        src = open(rig_path, encoding="utf-8").read()
        data = json.loads(src)
        look = next(l for l in data["looks"] if l["id"] == KID)
        if look["hair"].get("sprite"):
            look.setdefault("hairOrig", dict(look["hair"]))
        for V in (look, look.get("side"), look.get("back")):
            if V is None:
                continue
            V["hair"] = MP.part("", V.get("hair", {}).get("x", 0), V.get("hair", {}).get("y", 0))
            V.pop("hairBones", None)
        out = json.dumps(data, ensure_ascii=False, indent=1) + ("\n" if src.endswith("\n") else "")
        open(rig_path, "w", encoding="utf-8", newline="\n").write(out)
        print(KID, stage)
        return
    if stage in ("layout", "front"):
        # 정면만: layout = 정면 배치 그림(뒷머리 생성용) · front = 정면 뒷머리 맞추기. 손으로 맞춘 옆·뒤 값은 건드리지 않는다
        rig_path = MP.NEW_RIG
        src = open(rig_path, encoding="utf-8").read()
        data = json.loads(src)
        look = next(l for l in data["looks"] if l["id"] == KID)
        if stage == "layout":
            write_layout(look, "front", os.path.join(RAW, KID, "ref_front_layout.png"))
        else:
            fit_front_hair(look)
            out = json.dumps(data, ensure_ascii=False, indent=1) + ("\n" if src.endswith("\n") else "")
            open(rig_path, "w", encoding="utf-8", newline="\n").write(out)
        print(KID, stage)
        return
    if KID == "fire_3" and "--force" not in sys.argv:
        sys.exit("fire_3은 손으로 맞춘 값이 있습니다. 정말 다시 쓰려면 --force")
    rig_path = MP.NEW_RIG
    src = open(rig_path, encoding="utf-8").read()
    data = json.loads(src)
    look = next(l for l in data["looks"] if l["id"] == KID)
    if look["form"] < 2:
        stage_mush(data, look)
    elif stage in ("body", "all"):
        stage_body(data, look)
    if look["form"] >= 2 and stage in ("hair", "all"):
        stage_hair(data, look)
    out = json.dumps(data, ensure_ascii=False, indent=1) + ("\n" if src.endswith("\n") else "")
    open(rig_path, "w", encoding="utf-8", newline="\n").write(out)
    print("wrote", rig_path, KID, stage)


if __name__ == "__main__":
    main()
