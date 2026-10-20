# itch 커버 v2 (2026-10-20)

사용자 피드백: 기존 C 커버는 AI 티가 나고, 캐릭터가 게임 그림체와 다르다 → 수채 일러스트 새로, 캐릭터는 오른쪽.

- 배경: Codex 생성 `bg_morning_raw.png`, `bg_dusk_raw.png` (스타일 `style.md`: 빛 효과·반짝이·광택·보케 금지, 캐릭터 없이).
- 캐릭터: 생성하지 않고 **게임에서 실제 쓰는 정령 그림** `Assets/Resources/Art/Dream/Characters/Evo/evo_fire_1`(★1 빨간 버섯), `evo_fire_3`(★3 진화형)을 그대로 합성.
  (Critters/*_ref, Kids/* 는 게임에서 안 쓰는 옛 그림이라 제외.)
- 로고: `itch_dream/out/banner.png`, 왼쪽 위.

합성:
```
python compose.py bg_morning morning_f1 evo_fire_1:0.73:0.94:0.56 --logo 0.56,0.03,0.04
python compose.py bg_dusk    dusk_f1    evo_fire_1:0.73:0.94:0.56 --logo 0.56,0.03,0.04
python compose.py bg_morning morning_f3 evo_fire_1:0.57:0.95:0.30 evo_fire_3:0.765:0.93:0.62 --logo 0.56,0.03,0.04
python compose.py bg_dusk    dusk_f3    evo_fire_1:0.57:0.95:0.30 evo_fire_3:0.765:0.93:0.62 --logo 0.56,0.03,0.04
```
결과 `out/cover_<이름>_1260x1000.png`(업로드용), `_630x500`, `preview_<이름>_315x250`, 비교 `out/시안4종.png`.
