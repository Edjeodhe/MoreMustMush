# itch.io 페이지 올리기

에셋은 `python Tools/itch/build_itch.py`로 다시 만든다 (녹화 원본: `Temp/itchcap/`, 미리보기: `preview.html`).

## 1. Edit game → 기본 정보

| 칸 | 넣을 것 |
|---|---|
| Cover image | `cover.png` (630×500) |
| Screenshots | `shot1_harvest.jpg`, `shot2_fever.jpg`, `shot3_tree.jpg`, `shot4_worldtree.jpg` (오른쪽 열에 뜬다) |
| Short description | `Bounce your harvester through a mushroom-swallowed forest and grow until it all goes pop.` |

## 2. Embed options (게임이 바로 실행되게)

- Kind of project: HTML, 업로드한 WebGL zip에 **This file will be played in the browser** 체크
- Embed options: **Embed in page**
- Viewport dimensions: **1280 × 720** (이 폭이어야 설명 칸이 873px이 되어 GIF 카드가 2×2로 놓인다)
- **Automatically start on page load** 체크 → Run game 버튼 없이 바로 로딩 시작
- Fullscreen button 체크, Mobile friendly 체크(가로)
- 참고: 브라우저 정책상 소리는 첫 클릭 뒤에 난다

## 3. Description

1. 설명 편집기를 HTML(코드) 보기로 바꾸고 `description.html` 내용을 붙인다 (맨 위 주석 줄은 빼도 된다)
2. `[[파일명]]` 자리를 편집기 이미지 버튼으로 같은 파일을 올려 바꾼다
   - `intro.png` 하나, 그다음 줄에 `step1_launch.gif` `step2_upgrade.gif`, 그다음 줄에 `step3_fever.gif` `step4_worldtree.gif` (한 줄에 두 장 → 나란히)
   - `intro.png`는 2배 크기라 올린 뒤 폭이 873px로 줄어 보이는 게 정상

## 4. Edit theme

| 항목 | 값 |
|---|---|
| Background image | `background.jpg`, 반복 없음(No repeat), 위치 가운데 위(Top center) |
| Banner image | `banner.png` |
| BG color | `#5D9E3A` |
| BG2 (본문) | `#FFFFFF`, 불투명도 92% |
| Text | `#263A2C` |
| Link | `#E0457B` |
| Button | `#EC4C8A` |
| Border | `#D6E9C4` |
| Header font | 둥근 굵은 글꼴(예: Nunito가 있으면 Nunito), 제목 색 `#2E8B4E` |

## 언어

처음 접속(저장값 없음)하면 영어로 시작한다 (`Loc.DefaultLang = "en"`, 기기 언어를 보지 않음). 옵션에서 바꾼 언어는 그 브라우저에 저장되어 다음 접속에도 유지된다. 테스트할 때 한국어로 뜨면 그 브라우저에 예전 선택이 남은 것이므로 시크릿 창으로 확인한다.
