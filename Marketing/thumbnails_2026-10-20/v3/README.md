# itch 커버 v3 '수확 한 방' (2026-10-20)

v2(풍경 + 정령 스티커)는 "눌러 보고 싶지 않다"는 피드백 → 게임이 뭘 하는지 보이는 장면으로.

- 배경: Codex 생성 `bg_day_raw.png` / `bg_gold_raw.png` — 베인 그루터기 궤적 + 날아오르는 버섯. 캐릭터·도구·코인·숫자는 그리지 않게 함 (`style.md`).
- 나머지는 전부 **게임 실제 그림**을 합성 (`compose.py`):
  - 정령: `Rig2/gen_fire_1_*` 부품을 MushRig 그리기 순서(갓→신발→몸→얼굴→팔)로 조립, 얼굴 `face_surprise`, 팔 벌림, 살짝 뜀 + "!".
  - 수확기 `Harvesters/sam.png`(+잔상), 코인 `Harvesters/coin.png`, 숫자 `Assets/Fonts/DmgDigitsAtlas.png`(게임 데미지 숫자 그림 글자).
  - 로고 `itch_dream/out/banner.png` 왼쪽 위.

```
python v3/compose.py bg_day  day  hx=0.45 hy=0.50 hs=0.18 nx=0.75 ny=0.27 sx=0.83 sy=0.92 lw=0.50
python v3/compose.py bg_gold gold hx=0.49 hy=0.52 hs=0.18 nx=0.75 ny=0.27 sx=0.83 sy=0.92 lw=0.50
```
결과 `out/cover_<day|gold>_1260x1000.png`(업로드용), `_630x500`, `preview_*_315x250`, 비교 `out/시안2종.png`.
