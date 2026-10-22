"""★0·★1 모습이 리그2 데이터에 없는 정령(달·별)을 통짜 그림(evo_<모습>.png)에서 만들어 spirit_rig2.json에 넣는다 (사용자 2026-10-19).
python Tools/rig-web/add_mush_looks.py moon_0 moon_1 star_0 star_1
  - 통짜 그림을 같은 등급 다른 정령 평균 키로 키워 e_<모습>_body(.png)로 저장 → make_parts.build_headless로 갓·몸통·발·팔·관절을 나눈다
  - 얼굴은 통짜 그림 몸통 쪽의 눈·입 덩어리만 남겨 e_<모습>_face_normal 로 (표정 변형은 build_headless가 만든다)
  - 그다음 gen_looks.mjs(m_* 새 파츠) → fit_gen_parts.py --look <모습> 으로 깨끗한 파츠로 바꾼다 (다른 ★0·★1과 같은 과정)
"""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_parts as MP  # noqa: E402


def save_evo(arr, name):
    """Characters/Evo/<name>.png (+ .meta: make_parts.save와 같은 가져오기 설정)."""
    out_dir = MP.OUT
    MP.OUT = MP.EVO
    try:
        MP.save(arr, name)
    finally:
        MP.OUT = out_dir


def face_from_whole(W):
    """통짜 그림에서 얼굴(눈·입)만: 가운데 아래쪽 영역의 아주 어두운 점이 든 덩어리."""
    from scipy import ndimage
    al = W[:, :, 3] > 40
    ys, xs = np.nonzero(al)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    h, w = y1 - y0, x1 - x0
    region = np.zeros(al.shape, bool)
    region[int(y0 + h * 0.42):int(y0 + h * 0.85), int(x0 + w * 0.22):int(x1 - w * 0.22)] = True
    rgb = W[:, :, :3].astype(np.float32) / 255.0
    v, mn = rgb.max(axis=2), rgb.min(axis=2)
    sat = np.where(v > 0, (v - mn) / np.maximum(v, 1e-6), 0)
    dark = al & (v < 0.42) & region
    mouth_in = al & region & (sat > 0.5) & (rgb[:, :, 0] > rgb[:, :, 1] * 1.4)   # 입 안 빨강·혀 (볼터치는 채도가 낮다)
    feat = dark | mouth_in
    lab, n = ndimage.label(ndimage.binary_dilation(feat, iterations=2))
    keep = np.zeros(al.shape, bool)
    for i in range(1, n + 1):
        m = lab == i
        if (dark & m).sum() >= 12:
            keep |= m
    keep = ndimage.binary_fill_holes(keep & (feat | ndimage.binary_dilation(feat, iterations=1))) & al
    out = W.copy()
    out[:, :, 3] = (W[:, :, 3] * keep).astype(np.uint8)
    return out


def main():
    ids = [a for a in sys.argv[1:] if not a.startswith("--")]
    src = open(MP.NEW_RIG, encoding="utf-8").read()
    data = json.loads(src)
    have = {l["id"] for l in data["looks"]}
    for lid in ids:
        kind, form = lid.split("_")[0], int(lid.split("_")[1])
        same = [l for l in data["looks"] if l["form"] == form and "body" not in l]
        target_h = float(np.mean([l["capTop"][1] - l["sole"] for l in same]))
        k = float(np.mean([l["k"] for l in same]))
        W = np.asarray(Image.open(os.path.join(MP.EVO, f"evo_{lid}.png")).convert("RGBA"))
        ys, xs = np.nonzero(W[:, :, 3] > 40)
        s = target_h * MP.PPU / (ys.max() - ys.min() + 1)
        W = np.asarray(Image.fromarray(W).resize((round(W.shape[1] * s), round(W.shape[0] * s)), Image.LANCZOS)).copy()
        save_evo(W, f"e_{lid}_body")
        save_evo(face_from_whole(W), f"e_{lid}_face_normal")
        sole = -0.08
        l = {"id": lid, "kind": kind, "form": form, "height": round(target_h - 0.05, 4), "k": round(k, 4), "face": f"e_{lid}_face_",
             "body": {"x": 0.0, "y": MP.r4(sole + W.shape[0] / 2 / MP.PPU), "sprite": f"e_{lid}_body"}}
        look = MP.build_headless(l, "--preview" in sys.argv)
        data["looks"] = [x for x in data["looks"] if x["id"] != lid]
        # 같은 정령 다른 모습 옆에 (순서: dew… 처럼 정령별 ★0→★3)
        idx = next((i for i, x in enumerate(data["looks"]) if x["kind"] == kind), len(data["looks"]))
        data["looks"].insert(idx + (form if lid not in have else 0), look)
        print(lid, "added", look["height"], look["k"])
    data["looks"].sort(key=lambda x: (x["kind"], x["form"]))
    out = json.dumps(data, ensure_ascii=False, indent=1) + ("\n" if src.endswith("\n") else "")
    open(MP.NEW_RIG, "w", encoding="utf-8", newline="\n").write(out)


if __name__ == "__main__":
    main()
