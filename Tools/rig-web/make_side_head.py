"""코덱스 옆머리(head_side)가 없는 ★2의 임시 옆모습 머리를 있는 그림으로 합성한다 (사용자 2026-10-19: 대각선 말고 불·물·풀처럼 옆을 보게).
  옆얼굴(눈 하나·코·앞머리) = 같은 등급 다른 정령(donor)의 코덱스 옆머리에서 갓을 뺀 얼굴
  갓 = 이 정령 정면 머리 그림(k_<모습>_head_bare)에서 얼굴·머리카락을 뺀 갓 (버섯 갓은 앞·옆 모양이 거의 같다)
  → Art/Raw_Dream/rig2/<모습>/gen_head_side_tmp.png. fit_gen_parts.py는 gen_head_side.png가 없을 때 이것을 쓴다
    (코덱스로 진짜 옆머리를 뽑으면 그쪽이 우선).

python Tools/rig-web/make_side_head.py --look rock_2 --donor fire_2 [--preview out.png]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
RAW = os.path.join(ROOT, "Art/Raw_Dream/rig2")
EVO = os.path.join(ROOT, "Assets/Resources/Art/Dream/Characters/Evo")


def arg(name, default=None):
    return next((sys.argv[i + 1] for i, a in enumerate(sys.argv[:-1]) if a == name), default)


def load(p):
    im = np.asarray(Image.open(p).convert("RGBA")).copy()
    im[im[:, :, 3] < 12] = 0
    return im


def crop(im):
    ys, xs = np.nonzero(im[:, :, 3] > 40)
    return im[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()


def blur(m, r):
    return np.asarray(Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255


def hsv(im):
    rgb = im[:, :, :3].astype(np.float32) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return mx, sat


def face_blob(H, seed_rows=(0.55, 0.95)):
    """머리 그림에서 얼굴+앞머리(밝고 채도 낮은 살·머리카락) 덩어리: 아래쪽 가운데 눈(어두운 점) 근처를 품은 밝은 덩어리.
    눈·입·볼(구멍)은 채운다."""
    mx, sat = hsv(H)
    al = H[:, :, 3] > 40
    light = al & (mx >= 0.80) & (sat <= 0.42)
    light = ndimage.binary_opening(light, iterations=1)
    lab, n = ndimage.label(light)
    h, w = al.shape
    # 눈: 아주 어두운 작은 점 (갓 무늬와 겹치지 않게 아래쪽에서)
    dark = al & (mx < 0.35)
    dark[: int(h * seed_rows[0])] = False
    dl, dn = ndimage.label(dark)
    best, score = 0, -1
    for i in range(1, n + 1):
        m = lab == i
        mm = ndimage.binary_fill_holes(m)
        s = (mm & (dl > 0)).sum() * 1000 + m.sum() * (m.nonzero()[0].mean() / h)
        if s > score:
            best, score = i, s
    m = ndimage.binary_fill_holes(ndimage.binary_closing(lab == best, iterations=3))
    return m


def donor_face(D):
    """옆머리 그림에서 얼굴(+앞머리·목 그루터기)만: 갓(겉·안쪽 주름)을 지운다."""
    m = face_blob(D)
    m = ndimage.binary_dilation(m, iterations=1) & (D[:, :, 3] > 10)
    out = D.copy()
    out[:, :, 3] = (D[:, :, 3] * blur(m, 0.8)).astype(np.uint8)
    return crop(out), m


def eyes(C):
    """정면 그림(머리 + 얼굴 층)에서 두 눈 (x, y) 둘과 눈 사이 거리. 아래쪽 반의 어두운 둥근 점 중 높이가 비슷한 큰 둘."""
    mx, _ = hsv(C)
    al = C[:, :, 3] > 40
    h = al.shape[0]
    dark = al & (mx < 0.38)
    dark[: int(h * 0.35)] = False
    lab, n = ndimage.label(dark)
    bl = []
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(ys) >= 15 and (ys.max() - ys.min()) < h * 0.15:
            bl.append((len(ys), xs.mean(), ys.mean()))
    bl.sort(reverse=True)
    best = None
    for i in range(len(bl)):
        for j in range(i + 1, len(bl)):
            a, b = bl[i], bl[j]
            if abs(a[2] - b[2]) < 0.04 * h and abs(a[1] - b[1]) > 0.08 * C.shape[1] and 0.5 < a[0] / b[0] < 2:
                cand = (a[0] + b[0], a, b)
                if best is None or cand[0] > best[0]:
                    best = cand
    _, a, b = best
    L, R = sorted([a, b], key=lambda t: t[1])
    return (L[1], L[2]), (R[1], R[2]), R[1] - L[1]


def lab_of(px):
    return np.asarray(Image.fromarray(px.reshape(-1, 1, 3).astype(np.uint8)).convert("LAB")).reshape(-1, 3).astype(np.float32)


def front_cap(F, C):
    """정면 머리 그림 F에서 갓만 (얼굴·앞머리·갓 밑 머리카락은 지움). C = F + 얼굴 층 (눈 찾기용).
    색 표본: 살(두 눈 사이·아래), 머리카락(눈 위 이마 띠), 갓(그림 맨 위 25%). 픽셀마다 가장 가까운 표본이 살·머리카락이고
    눈을 품은 덩어리 = 얼굴. 그 덩어리와 그 아래를 지운 나머지 중 가장 큰 덩어리 = 갓."""
    (lx, ly), (rx, ry), de = eyes(C)
    cx, ey = (lx + rx) / 2, (ly + ry) / 2
    al = F[:, :, 3] > 40
    h, w = al.shape
    ys, xs = np.mgrid[0:h, 0:w]
    skin = al & (((xs - cx) / (0.35 * de)) ** 2 + ((ys - (ey + 0.25 * de)) / (0.2 * de)) ** 2 <= 1)
    hair = al & (np.abs(xs - cx) < 0.5 * de) & (ys > ey - 0.75 * de) & (ys < ey - 0.42 * de)
    rows = np.nonzero(al.any(axis=1))[0]
    capm = al & (ys < rows[0] + 0.25 * (rows[-1] - rows[0]))
    L = lab_of(F[:, :, :3])
    rng = np.random.default_rng(0)
    def samp(m):
        idx = np.flatnonzero(m.ravel())
        return L[rng.choice(idx, min(300, len(idx)), replace=False)]
    S = {"skin": samp(skin), "hair": samp(hair), "cap": samp(capm)}
    def dist(P):
        d = np.full(len(L), np.inf, np.float32)
        for k in range(0, len(P), 50):
            d = np.minimum(d, np.min(((L[:, None, :] - P[None, k:k + 50]) ** 2).sum(-1), axis=1))
        return d
    dface = np.minimum(dist(S["skin"]), dist(S["hair"])).reshape(h, w)
    dcap = dist(S["cap"]).reshape(h, w)
    face = al & (dface < dcap)
    face = ndimage.binary_opening(face, iterations=1)
    lab, n = ndimage.label(face)
    seed = lab[int(ey + 0.25 * de), int(cx)]
    fm = lab == seed if seed else face
    fm = ndimage.binary_fill_holes(ndimage.binary_closing(fm, iterations=3))
    fm[int(ey):] |= al[int(ey):] & (np.abs(xs[int(ey):] - cx) < 1.2 * de)     # 눈 아래 가운데는 다 얼굴(볼·턱·목)
    fm = ndimage.binary_dilation(fm, iterations=2)
    keep = al & ~fm
    lab, n = ndimage.label(keep)
    if n:
        sz = ndimage.sum(keep, lab, range(1, n + 1))
        keep = lab == 1 + int(np.argmax(sz))
    out = F.copy()
    out[:, :, 3] = (F[:, :, 3] * blur(keep, 0.8)).astype(np.uint8)
    return out, keep, fm, (cx, ey, de)


def donor_eye(D, dm):
    """옆머리의 눈 하나 (얼굴 덩어리 안 가장 큰 어두운 점) 가운데."""
    mx, _ = hsv(D)
    dark = ndimage.binary_fill_holes(dm) & (mx < 0.38) & (D[:, :, 3] > 40)
    lab, n = ndimage.label(dark)
    sz = ndimage.sum(dark, lab, range(1, n + 1))
    ys, xs = np.nonzero(lab == 1 + int(np.argmax(sz)))
    return xs.mean(), ys.mean()


def front_comp(kid):
    F = load(os.path.join(EVO, f"k_{kid}_head_bare.png"))
    fp = os.path.join(EVO, f"k_{kid}_face_normal.png")
    C = Image.fromarray(F)
    if os.path.exists(fp):
        C = C.copy(); C.alpha_composite(Image.open(fp).convert("RGBA"))
    return F, np.asarray(C)


# 갓 안쪽 주름의 아래 테두리 (머리카락 위 끝). RIM~GILL 사이 = 정면에서 보이는 주름 → 옆머리 뒤에 깔아 머리가 갓 속으로 이어지게
GILL = {
    "rock_2": [(0, .7), (.1, .74), (.2, .74), (.25, .62), (.32, .53), (.4, .48), (.5, .46), (.6, .46), (.7, .5), (.77, .6), (.82, .72), (.9, .68), (1, .6)],
    "spark_2": [(0, .45), (.08, .48), (.18, .6), (.26, .68), (.3, .62), (.38, .55), (.5, .53), (.62, .55), (.7, .62), (.75, .68), (.82, .6), (.92, .48), (1, .45)],
}

CAP_DY = {"wind_2": 0.11}   # 갓을 이만큼(갓 높이 비율) 더 내림: 깔때기 갓 안쪽 주름이 머리 위에 얹히게

# 정면 머리 그림에서 갓 겉면의 아래 테두리 (x, y 비율 꺾은선). 이 선 위 = 갓 겉면 (눈으로 그려 맞춘 값, 2026-10-19)
RIM = {
    "rock_2": [(0, .64), (.08, .67), (.18, .6), (.3, .53), (.42, .48), (.55, .45), (.7, .44), (.85, .47), (.95, .52), (1, .56)],
    "spark_2": [(0, .42), (.1, .43), (.2, .39), (.35, .37), (.5, .41), (.62, .41), (.75, .37), (.85, .41), (.95, .46), (1, .46)],
    "star_2": [(0, .68), (.2, .63), (.33, .6), (.45, .63), (.5, .67), (.55, .63), (.67, .6), (.8, .63), (1, .68)],
    "wind_2": [(0, .38), (.08, .42), (.16, .46), (.24, .5), (.35, .47), (.5, .45), (.65, .47), (.73, .52), (.8, .58), (.9, .55), (1, .5)],   # 깔때기: 안쪽 주름까지
}


def cap_top(F, rim, drop_hair=True):
    """정면 머리 그림에서 RIM 꺾은선 위(갓 겉면)만. 선 위라도 갓 밑으로 삐져나온 머리카락(밝고 채도 낮은 덩어리, 그림 45% 아래)은 뺀다 (별)."""
    h, w = F.shape[:2]
    xs = np.arange(w) / max(1, w - 1)
    line = np.interp(xs, [p[0] for p in rim], [p[1] for p in rim]) * h
    ys = np.arange(h)[:, None]
    m = (ys < line[None, :]) & (F[:, :, 3] > 40)
    mx, sat = hsv(F)
    hairish = (mx > 0.86) & (sat < 0.2) & (ys > 0.45 * h)
    if drop_hair:
        m &= ~ndimage.binary_opening(hairish, iterations=2)
    lab, n = ndimage.label(m)
    if n:
        sz = ndimage.sum(m, lab, range(1, n + 1))
        m = np.isin(lab, 1 + np.nonzero(sz > (0.05 if drop_hair else 0.5) * sz.max())[0])   # 떨어진 테두리 조각 빼기
    out = F.copy()
    out[:, :, 3] = (F[:, :, 3] * blur(m, 0.8)).astype(np.uint8)
    ys_, xs_ = np.nonzero(out[:, :, 3] > 40)
    return crop(out), line, (int(ys_.min()), int(xs_.min()))


def donor_outer(D):
    """옆머리 그림에서 갓 겉면(채도 높은 빨강·파랑) + 떨어진 물방울 등 채도 높은 조각. 얼굴(볼)은 빼고."""
    mx, sat = hsv(D)
    al = D[:, :, 3] > 40
    hi = al & (sat > 0.42)
    lab, n = ndimage.label(ndimage.binary_closing(hi, iterations=2))
    sz = ndimage.sum(hi, lab, range(1, n + 1))
    big = ndimage.binary_fill_holes(lab == 1 + int(np.argmax(sz)))
    ys = np.arange(D.shape[0])[:, None]
    has = big.any(axis=0)
    last = np.where(has, D.shape[0] - 1 - np.argmax(big[::-1], axis=0), -1)
    big |= (ys <= last[None, :]) & has[None, :]                               # 겉면의 흰 점까지
    fm = face_blob(D)
    others = hi & ~ndimage.binary_dilation(fm, iterations=3)                  # 물방울 등
    return (big | others) & ~fm, big


def main():
    look, donor = arg("--look"), arg("--donor", "fire_2")
    D = crop(load(os.path.join(RAW, donor, "gen_head_side.png")))
    DF, DC = front_comp(donor)
    _, _, dde = eyes(DC)
    F, C = front_comp(look)
    _, _, de = eyes(C)
    def bw(im):
        xs = np.nonzero((im[:, :, 3] > 40).any(axis=0))[0]
        return xs.max() - xs.min() + 1
    # 옆머리를 이 정령 정면 그림 크기로: donor 옆→정면 배율(머리 폭) × 눈 사이 거리 비
    k = bw(DF) / bw(D) * de / dde
    D = np.asarray(Image.fromarray(D).resize((max(1, round(D.shape[1] * k)), max(1, round(D.shape[0] * k))), Image.LANCZOS)).copy()
    rm, outer = donor_outer(D)
    # donor는 얼굴(앞머리·목 포함)만 남긴다: 갓 안쪽 주름 색이 정령마다 달라 뒤로 삐져나와 보임
    fm = face_blob(D)
    # 갓 테두리 띠(얇고 밝음)가 앞머리에 붙어 같이 잡히면 뒤로 길게 삐져나온다 → 얇은 부분을 끊고 얼굴 덩어리만
    core = ndimage.binary_opening(fm, iterations=3)
    lab, n = ndimage.label(core)
    if n:
        sz = ndimage.sum(core, lab, range(1, n + 1))
        fm = fm & ndimage.binary_dilation(lab == 1 + int(np.argmax(sz)), iterations=4)
    fm = ndimage.binary_dilation(fm, iterations=1)
    base = D.copy()
    base[:, :, 3] = (D[:, :, 3] * blur(fm & ~rm, 0.8)).astype(np.uint8)
    cap, _, (cy0, cx0) = cap_top(F, RIM[look], drop_hair=look != "wind_2")
    gill = None
    if look in GILL:
        # 정면 그림에서 RIM~GILL 사이만 (cap과 같은 crop 기준이 되게 RIM 위까지 포함한 그림에서 겉면을 뺀다)
        h_, w_ = F.shape[:2]
        line = np.interp(np.arange(w_) / max(1, w_ - 1), [q[0] for q in RIM[look]], [q[1] for q in RIM[look]]) * h_
        G = F.copy()
        ys_ = np.arange(h_)[:, None]
        GILLm = (ys_ >= line[None, :] - 2)
        gl = np.interp(np.arange(w_) / max(1, w_ - 1), [q[0] for q in GILL[look]], [q[1] for q in GILL[look]]) * h_
        GILLm &= ys_ < gl[None, :]
        G[:, :, 3] = (F[:, :, 3] * blur(GILLm & (F[:, :, 3] > 40), 0.8)).astype(np.uint8)
        # cap은 crop되어 있다 → 같은 crop 원점으로 맞춘 캔버스에 넣는다
        gill = np.zeros((max(cap.shape[0], int(gl.max()) - cy0 + 2), cap.shape[1], 4), np.uint8)
        sub = G[cy0:cy0 + gill.shape[0], cx0:cx0 + cap.shape[1]]
        gill[:sub.shape[0], :sub.shape[1]] = sub          # 바람 갓 테두리는 흰색이라 머리카락으로 잘못 지워짐
    if arg("--hair-tint"):
        # 앞머리 색 바꾸기 (예: 물 정령의 파란 앞머리 → 바람 정령 은빛 라벤더): 파랑 쪽 픽셀의 채도를 낮추고 색을 섞는다
        tint = np.array([int(arg("--hair-tint")[i:i + 2], 16) for i in (0, 2, 4)], np.float32)
        rgb = base[:, :, :3].astype(np.float32)
        blue = np.clip((rgb[:, :, 2] - rgb[:, :, 0]) / 40, 0, 1)[:, :, None]   # 파란 기가 있는 곳만 (살·볼은 빨강 쪽)
        lum = rgb.mean(axis=2, keepdims=True)
        mixed = np.clip(lum * (tint / tint.mean()) * 0.6 + lum * 0.4, 0, 255)          # 밝기는 두고 색만 tint 쪽으로
        base[:, :, :3] = (rgb * (1 - blue) + mixed * blue).astype(np.uint8)
    # 자리: 갓 가운데 x = donor 갓 겉면 가운데 x, 갓 아래끝 = donor 갓 겉면 아래끝(가운데 1/3의 평균)
    oys, oxs = np.nonzero(outer)
    ocx = (oxs.min() + oxs.max()) / 2
    mid = outer[:, int(oxs.min() + (oxs.max() - oxs.min()) / 3):int(oxs.min() + 2 * (oxs.max() - oxs.min()) / 3)]
    obot = np.mean([np.nonzero(c)[0].max() for c in mid.T if c.any()])
    cal = cap[:, :, 3] > 40
    cmid = cal[:, cap.shape[1] // 3: 2 * cap.shape[1] // 3]
    cbot = np.mean([np.nonzero(c)[0].max() for c in cmid.T if c.any()])
    px, py = int(round(ocx - cap.shape[1] / 2)), int(round(obot - cbot + CAP_DY.get(look, 0) * cap.shape[0]))
    left, top = min(0, px), min(0, py)
    right, bot = max(D.shape[1], px + cap.shape[1]), max(D.shape[0], py + (gill.shape[0] if gill is not None else cap.shape[0]))
    can = Image.new("RGBA", (right - left, bot - top), (0, 0, 0, 0))
    if gill is not None:
        can.alpha_composite(Image.fromarray(gill), (px - left, py - top))      # 갓 안쪽 주름: 머리 뒤
    can.alpha_composite(Image.fromarray(base), (-left, -top))
    can.alpha_composite(Image.fromarray(cap), (px - left, py - top))
    can = Image.fromarray(crop(np.asarray(can)))
    out = os.path.join(RAW, look, "gen_head_side_tmp.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    can.save(out)
    print(look, "<-", donor, "scale", round(k, 3), "->", out)
    pv = arg("--preview")
    if pv:
        bg = Image.new("RGBA", can.size, (150, 150, 150, 255)); bg.alpha_composite(can); bg.save(pv)


if __name__ == "__main__":
    main()
