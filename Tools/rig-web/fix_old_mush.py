"""★0·★1 예전 잘라 쓴 파츠(r2_<모습>_cap/stem) 고치기 — 걸을 때 갓·몸 사이 흰 선·틈 (사용자 2026-10-19).
python Tools/rig-web/fix_old_mush.py dew_0 dew_1 ...
  - 몸통: 위에 같이 오려진 갓 주름 판(네모)을 지우고 몸 색 부분만 남긴 뒤, 윗부분을 둥근 머리로 이어 그린다 (갓 밑에 들어가는 둥근 몸통)
  - 갓: 몸통을 오려 낸 자리(아래 가운데 빈 네모)를 둘레 주름 색으로 메운다
  - 갓 축(capJoint)을 갓이 몸에 붙는 높이로 (깔때기 갓은 테두리보다 한참 아래)
  결과는 gen_<모습>_cap/stem (새 m_* 파츠가 나오면 fit_gen_parts.py가 같은 이름으로 다시 쓴다). 원본 이름은 orig에.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_parts as MP  # noqa: E402

CHAR = os.path.dirname(MP.OUT)


def load(sprite):
    return np.asarray(Image.open(os.path.join(CHAR, sprite + ".png")).convert("RGBA")).astype(np.float32)


def blur(m, r):
    return np.asarray(Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255


def inpaint(img, hole, known, iters=80):
    out = img.copy()
    known = known.copy()
    for _ in range(iters):
        if not (hole & ~known).any():
            break
        w = ndimage.uniform_filter(known.astype(np.float32), 5)
        acc = np.stack([ndimage.uniform_filter(out[:, :, c] * known, 5) for c in range(4)], 2)
        fill = hole & ~known & (w > 0.05)
        out[fill] = acc[fill] / w[fill][:, None]
        known = known | fill
    return out


def fix_stem(S, face=None):
    """몸 색(아래쪽 가운데 색)에 가까운 덩어리만 남기고, 위를 둥글게."""
    al = S[:, :, 3] > 40
    ys, xs = np.nonzero(al)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    h = y1 - y0
    ref = np.median(S[int(y0 + h * 0.55):int(y0 + h * 0.8), int(x0 + (x1 - x0) * 0.3):int(x1 - (x1 - x0) * 0.3), :3].reshape(-1, 3), axis=0)
    dist = np.sqrt(((S[:, :, :3] - ref) ** 2).sum(axis=2))
    body = al & (dist < 55)
    body = ndimage.binary_opening(body, iterations=2)
    lab, n = ndimage.label(body)
    if n:
        sz = ndimage.sum(body, lab, range(1, n + 1))
        body = lab == 1 + int(np.argmax(sz))
    body = ndimage.binary_fill_holes(ndimage.binary_closing(body, iterations=4))
    # 몸 둘레 선(조금 어두운 테두리)까지 원래 그림 그대로: 몸 덩어리를 4px 넓힌 안쪽의 원래 알파 (위에 붙어 있던 갓 판은 빠진다)
    keep = al & ndimage.binary_dilation(body, iterations=4)
    keep = ndimage.binary_opening(ndimage.binary_closing(keep, iterations=3), iterations=3)   # 위 끝 들쭉날쭉 정리
    rows = np.nonzero(keep.any(axis=1))[0]
    probe = rows[0]
    out = S.copy()
    # 통짜 그림에서 만든 몸통(달·별)은 얼굴이 그려져 있다 → 얼굴 층 자리를 몸 색으로 메운다 (표정이 바뀌어도 밑 얼굴이 안 비치게)
    if face is not None:
        fm = ndimage.binary_dilation(face[:, :, 3] > 20, iterations=3) & keep
        if fm.any():
            out = inpaint(out, fm, keep & ~fm)
    # 정수리 양 모서리를 둥글게 (위가 평평하게 잘려 있으면 갓이 기울 때 모서리가 각져 보인다)
    c = np.nonzero(keep[min(rows[-1], probe + 4)])[0]
    mid = np.nonzero(keep[min(rows[-1], probe + int((rows[-1] - probe) * 0.35))])[0]
    cx, hw = (mid[0] + mid[-1]) / 2, (mid[-1] - mid[0]) / 2
    r = max(6, int(hw * 0.55))
    Y, X = np.mgrid[0:S.shape[0], 0:S.shape[1]].astype(np.float32)
    t = np.clip((probe + r - Y) / r, 0, 1)                                 # 정수리 1 → r px 아래 0
    allow = hw * np.sqrt(np.clip(1 - t ** 2, 0, 1)) + hw * 0.15 * (1 - t)
    keep &= ~((np.abs(X - cx) > np.maximum(allow, hw * 0.35)) & (Y < probe + r))
    out[:, :, 3] = S[:, :, 3] * blur(keep, 1.0)
    return out, probe, (cx, hw)


def extend_funnel(C, body_top, body_cx, body_hw, cap_off):
    """깔때기 갓(번개 등): 갓 아래끝이 몸통 위보다 위에서 끊겨 틈이 보인다 → 갓 대(주름 줄기)를 몸통 속까지 아래로 늘린다.
    갓은 몸통 뒤에 그려지므로 늘린 부분은 몸통에 가리고, 몸 옆으로는 줄기가 몸으로 좁아지며 이어진다.
    cap_off = 갓 그림 픽셀 → 몸통 그림 픽셀 세로 차이 (두 그림은 같은 캔버스라 0)."""
    al = C[:, :, 3] > 40
    rows = np.nonzero(al.any(axis=1))[0]
    bot = rows[-1]
    wmax = al.sum(axis=1).max()
    xs = np.nonzero(al[bot - 2])[0]
    if len(xs) == 0 or (xs[-1] - xs[0]) > wmax * 0.6:
        return C                                                            # 깔때기가 아님 (아래가 넓은 갓)
    end = body_top + cap_off + max(20, int(body_hw * 0.6))
    if end <= bot:
        return C
    out = C.copy()
    src_row = out[bot - 3].copy()
    x0, x1 = xs[0], xs[-1]
    for y in range(bot - 2, min(end, C.shape[0])):
        t = (y - (bot - 2)) / max(1, end - (bot - 2))
        a0 = x0 + (max(x0, body_cx - body_hw * 0.8) - x0) * t
        a1 = x1 + (min(x1, body_cx + body_hw * 0.8) - x1) * t
        row = np.zeros_like(src_row)
        for x in range(int(a0), int(a1) + 1):
            sx = int(round(x0 + (x - a0) / max(1, a1 - a0) * (x1 - x0)))
            row[x] = src_row[min(max(sx, x0), x1)]
        out[y] = row
    return out


def fix_cap(C, S_orig):
    """몸통을 오려 낸 자리: 예전 몸통 그림의 갓 판 영역 중 갓에 비어 있는 곳을 둘레 색으로 메운다."""
    cal = C[:, :, 3] > 40
    sal = S_orig[:, :, 3] > 40
    # 몸통 그림 위쪽(갓 판) = 몸통 윗부분 중 갓 테두리 아래 영역
    ys = np.nonzero(cal.any(axis=1))[0]
    cap_bottom = ys[-1]
    # 메울 곳 = 예전 몸통 그림이 덮던 자리 전부 (갓에 비어 있던 곳 + 예전에 줄무늬로 메워 두었던 네모). 둘레 갓 주름 색으로 다시 칠한다
    hole = sal.copy()
    hole[cap_bottom + 1:] = False
    hole = ndimage.binary_dilation(hole, iterations=2) & (ndimage.binary_fill_holes(cal | sal) | cal)
    hole[cap_bottom + 1:] = False
    if not hole.any():
        return C
    out = inpaint(C, hole, cal & ~hole, iters=200)
    out = np.where(hole[:, :, None], np.stack([ndimage.gaussian_filter(out[:, :, c], 1.2) for c in range(4)], 2), out)
    a = np.maximum(C[:, :, 3], 255 * blur(hole | cal, 0.6) * (hole | cal))
    out[:, :, 3] = np.where(hole | cal, np.maximum(a, out[:, :, 3]), C[:, :, 3])
    return out


def main():
    ids = [a for a in sys.argv[1:] if not a.startswith("--")]
    src = open(MP.NEW_RIG, encoding="utf-8").read()
    data = json.loads(src)
    for lid in ids:
        l = next(x for x in data["looks"] if x["id"] == lid)
        cap_o = l["cap"].get("orig", l["cap"]["sprite"])
        stem_o = l["stem"].get("orig", l["stem"]["sprite"])
        C, S = load(cap_o), load(stem_o)
        fpath = os.path.join(CHAR, l["face"] + "normal.png") if l.get("face", "").startswith("Rig2/r2_") else None
        F = np.asarray(Image.open(fpath).convert("RGBA")).astype(np.float32) if fpath and os.path.exists(fpath) else None
        S2, probe, (bcx, bhw) = fix_stem(S, F if lid.split("_")[0] in ("moon", "star") else None)
        C2 = extend_funnel(fix_cap(C, S), probe, bcx, bhw, 0)
        pre = f"gen_{lid}_"
        l["cap"]["orig"], l["stem"]["orig"] = cap_o, stem_o
        l["cap"]["sprite"] = MP.save(np.clip(C2, 0, 255), pre + "cap")
        l["stem"]["sprite"] = MP.save(np.clip(S2, 0, 255), pre + "stem")
        # 갓 축: 몸통의 둥근 정수리 조금 아래 (갓이 몸에 붙는 높이)
        l.setdefault("capJointOrig", list(l["capJoint"]))
        jy = MP.px2rig([l["stem"]["x"], l["stem"]["y"]], S.shape, S.shape[1] / 2, probe + 10)[1]
        l["capJoint"] = [l["capJointOrig"][0], MP.r4(min(jy, l["capJointOrig"][1]))]
        print(lid, "fixed, capJoint", l["capJointOrig"], "->", l["capJoint"])
    out = json.dumps(data, ensure_ascii=False, indent=1) + ("\n" if src.endswith("\n") else "")
    open(MP.NEW_RIG, "w", encoding="utf-8", newline="\n").write(out)


if __name__ == "__main__":
    main()
