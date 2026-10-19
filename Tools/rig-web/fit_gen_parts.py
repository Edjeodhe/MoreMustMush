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

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_parts as MP  # noqa: E402

ROOT = MP.ROOT
RAW = os.path.join(ROOT, "Art/Raw_Dream/rig2")
PPU = MP.PPU
KID = "fire_3"
LIFT = 0.34            # 다리를 늘인 만큼 몸을 올림 (리그 단위). 무릎이 치마 밑으로 보이게
THIGH_TOP_OVER = 0.12  # 허벅지 윗끝이 엉덩이 관절보다 이만큼 위 (치마 속)
SIDE_HAIR_SCALE = 0.26          # 옆모습 머리카락 한 장(hair_side_full 원본, 잘라낸 크기)의 배율
SIDE_HAIR_OFF = (-0.09, -0.95)  # 그 그림 중심 = 옆모습 머리 중심 + 이만큼 (얼굴 뒤를 빈틈없이 감싸고 갓 밑에서 나오게 맞춘 값)
SIDE_HAIR_BONES = (0.30, 0.48, 0.66, 0.84)  # 뼈 자리: 그림 높이의 이 비율 줄, 얼굴 쪽 홈 왼쪽 머리카락 덩어리의 가운데
BACK_HAIR_SCALE = 0.27          # 뒷모습 머리카락 한 장(hair_back_full 원본)의 배율
BACK_HAIR_OFF = (0.0, -1.0)     # 그 그림 중심 = 뒷모습 머리 중심 + 이만큼 (매끈한 정수리는 갓 밑에 숨고 갓 테두리 밑에서 머리카락이 나오게)
SIDE_HEAD_BACK = 0.25  # 옆모습 머리(갓·얼굴)를 이만큼 뒤(-x)로: 턱 밑 목이 몸통 위 끝에 얹히게 (머리카락은 머리 기준이라 같이 간다)


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

ARM_LEN = 0.7145        # 어깨 ~ 손목 (리그 단위): 예전 팔과 같게 (IK·동작 상수 그대로)
ARM_UPPER = 0.573       # 그중 위팔 비율 (예전 0.4095 / 0.7145)
ARM_FAT = 1.45          # 팔 가로 배율 (길이는 그대로, 치비 몸에 맞게 통통하게)
SLEEVE_W = 0.36         # 소매 폭 (리그 단위)
SLEEVE_TILT = 17.7      # 소매 원본이 기운 각도 (입구가 수평이 되게 반시계로 돌린다)


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
    s = ARM_LEN * PPU / np.hypot(*(wr_px - sh_px))
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
    S = Image.fromarray(raw("sleeve")).rotate(SLEEVE_TILT, resample=Image.BICUBIC, expand=True)
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
        if view == "back":
            # 뒷모습: 머리 그림의 목 그루터기를 지운다 (뒷목은 머리카락에 덮인다).
            # 옆모습은 지우지 않는다: 턱 밑 목이 없으면 머리와 몸통 사이가 떠 보인다 (옆 머리카락은 목 뒤에 있어 목이 그 위에 그려져도 맞다)
            al = H[:, :, 3] > 40
            w = al.sum(axis=1)
            mx = w.max()
            y = H.shape[0] - 1
            while y > 0 and w[y] < mx * 0.4:
                y -= 1
            H = H.copy()
            H[y + 1:, :, 3] = 0
            H = MP.fade_rows(H, y - 4, y + 1, keep_above=True)
        if view == "side":
            # 머리카락은 따로 한 장(hair_side_full)으로 뼈 사슬에 매단다 → 머리 그림에선 얼굴 뒤 머리카락을 지우고,
            # 갓 겉(빨강)만 오린 그림(<머리>_cap)을 머리카락 위에 덮어 머리카락이 갓 밑에서 나오게 한다
            MP.save(cap_red_only(H), f"gen_{KID}_side_head_cap")
            H = side_head_without_hair(H)
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
    # 목이 몸통에 얹히게 머리를 뒤로
    head["x"] = MP.r4(head["x"] - SIDE_HEAD_BACK)
    cap_top[0] = MP.r4(cap_top[0] - SIDE_HEAD_BACK)
    # 머리카락 한 장 (머리에 붙은 머리 + 긴 머리, 처음부터 한 머리로 생성): 크기·자리는 머리 기준, 뼈 사슬(hairBones)로 휜다
    R = scale(raw("hair_side_full"), SIDE_HAIR_SCALE)
    hair_c = [MP.r4(head["x"] + SIDE_HAIR_OFF[0]), MP.r4(head["y"] + SIDE_HAIR_OFF[1])]
    hair = MP.part(MP.save(R, f"gen_{KID}_side_hair"), *hair_c)
    hair_bones = hair_bones_of(R, hair_c)
    hair_root = list(hair_bones[0])
    hp_y = look["hipL"][1]
    # 옆모습 어깨: 몸통 옆선 위쪽, 앞뒤 가운데 (그 줄의 몸통 왼끝·오른끝 가운데)
    sr = shoulder_row(Bs)
    cols = np.nonzero(Bs[sr, :, 3] > 40)[0]
    sh_side = MP.px2rig([chest["x"], chest["y"]], Bs.shape, (cols[0] + cols[-1]) / 2, sr)
    side = {"head": head, "face": "", "collar": MP.part("", 0, 0), "chest": chest, "skirt": skirt, "hair": hair,
            "waist": waist, "neck": neck, "hairRoot": hair_root, "hairBones": hair_bones, "capTop": cap_top,
            "hipL": [MP.r4(sh_side[0] - 0.02), hp_y], "hipR": [MP.r4(sh_side[0] + 0.05), hp_y]}
    side.update(arm_at(look, sh_side))
    side["shoulderR"] = [MP.r4(sh_side[0] + 0.05), MP.r4(sh_side[1] + 0.01)]   # 먼 쪽 어깨 (몸 뒤에서 살짝 비킴)
    sl, imgs = leg_parts("leg_side", False, side, "side")
    side.update(sl)
    shots += [Hs, Bs, R] + imgs

    # ── 뒷모습 ──
    head, cap_top, Hb = head_view("head_back", "back")
    MP.save(cap_red_only(Hb), f"gen_{KID}_back_head_cap")   # 리그가 <머리>_cap(갓 겉만)을 머리카락 위에 덮는다. 갓 안쪽은 덮지 않아 머리카락이 흔들려도 잘린 선이 안 생김
    chest, skirt, waist, Bb = body_view("body_back", "back")
    # 머리카락 한 장 (갓 밑 뒤통수부터 허리까지 한 머리로 생성) + 뼈 사슬: 앞모습 머리카락 그림을 빌려 쓰지 않는다
    RB = scale(raw("hair_back_full"), BACK_HAIR_SCALE)
    hb_c = [MP.r4(head["x"] + BACK_HAIR_OFF[0]), MP.r4(head["y"] + BACK_HAIR_OFF[1])]
    hb_bones = hair_bones_of(RB, hb_c, whole=True)
    back = {"head": head, "face": "", "collar": MP.part("", 0, 0), "chest": chest, "skirt": skirt,
            "hair": MP.part(MP.save(RB, f"gen_{KID}_back_hair"), *hb_c), "hairRoot": list(hb_bones[0]), "hairBones": hb_bones,
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
