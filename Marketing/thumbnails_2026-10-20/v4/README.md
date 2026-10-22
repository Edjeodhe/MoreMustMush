# itch 커버 v4 '통 일러스트' (2026-10-20)

v3(배경 + 게임 그림 합성)에 이어, 정령·수확기·코인·버섯까지 **한 장의 수채화로 함께 그린다** — 붙인 스티커처럼 보이지 않게(`style.md`).

- 참조 `refs/`: 1 구도(v3 결과), 2 정령(★1 빨간 버섯), 3 수확기, 4 화풍(게임 배경).
- 시안: `A_num`(그림 안에 "+590K"·"!" 포함), `B_clean`(글자 없음).
- 만들기: `bash v4/gen.sh` → `python v4/compose.py A_num A --logo 0.50,0.03,0.035` / `python v4/compose.py B_clean B --logo 0.50,0.03,0.035` → `out/`.

## 상태

- 11:58 첫 생성 시도: Codex 사용 한도 초과로 2장 모두 실패("try again at 2:40 PM").
- 12:10 **ChatGPT 웹(앱 브라우저, 사용자 계정)으로 대신 생성** — 대화 https://chatgpt.com/c/6ad6db13-68f0-83e8-a27f-0940935fd507
  - 첨부는 정령+수확기 시트 한 장(436px JPEG, base64로 `input#_r_d_`에 넣음). 구도·화풍 참조(1_layout·bg_ranch_core)는 base64가 커서 첨부 대신 프롬프트에 글로 적음 — 프롬프트는 `gen.sh`의 COMMON + `style.md`를 합친 것.
  - 받기: 생성 그림 → 캔버스 → WebP dataURL → `Tools/rig-web/receive.html`(rig-web 서버 5178) → `Logs/Screens/v4_*_raw.png` → `A_num_raw.png`·`B_clean_raw.png`(PNG로 다시 저장).
  - A_num 1672×941(16:9), B_clean 1536×1024(3:2). 정령이 오른쪽에 있어 오른쪽 기준으로 자름(`--crop 0.95`).

합성:
```
python v4/compose.py A_num   A --logo 0.50,0.03,0.035 --crop 0.95
python v4/compose.py B_clean B --logo 0.50,0.03,0.035 --crop 0.95
```
결과 `out/cover_<A|B>_1260x1000.png`(업로드용), `_630x500`, `preview_*_315x250`, 비교 `out/시안2종.png`.

## 비교 (목록 크기 315×250)
- A: 16:9 원본을 많이 잘라 정령·수확기가 커서 목록 크기에서도 또렷. 그림 안 "+590K"는 붓으로 그린 글자라 v3(게임 숫자 글꼴)보다 덜 튀지만 그림과 어울림.
- B: 글자 없음, 시야가 넓어 버섯·코인이 많아 풍요 느낌은 크지만 목록 크기에선 정령이 작고 산만함.
- 둘 다 v3처럼 붙인 티(스티커 느낌)는 없음.
