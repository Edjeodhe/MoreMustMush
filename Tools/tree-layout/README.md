# tree-layout — 세계수 도면

세계수 그림보다 **도면을 먼저** 정한다. 단계마다 정령 길(발판·사다리·버섯 계단), 나무버섯 자리, 장식 자리를 그림 비율 좌표로 적고,
게임은 그 자리를 쓰며, 새 나무 그림은 도면에서 만든 가이드 그림을 따라 뽑는다.

정령이 움직이는 규칙 전체: [기획서/세계수_정령이동.md](../../기획서/세계수_정령이동.md). 정령은 4단계(`WT.climbFrom`)부터 나무에 오른다.

데이터: `Assets/Resources/Rig/tree_layout.json` (예전 `tree_nav.json`을 합침). 게임 쪽 읽기: `Assets/Scripts/View/FarmTreeView.Nav.cs`.

```json
{ "pics": [ { "pic": 7, "aspect": 0.9761,
    "nodes": [ { "id": "g0", "u": 0.4, "v": 0.9, "ground": true }, { "id": "m1", "u": 0.39, "v": 0.78, "spot": true } ],
    "edges": [ ["g0", "m1", "climb"], ["lp", "p2", "walk", "plank"] ],
    "mush":  { "root": [[0.5, 0.88], ...], "trunk": [...], "branch": [...], "crown": [...] },
    "deco":  { "leaf_lamp": [0.3, 0.45], "dew_pond": [0.8, 0.95, 1.2] } } ] }
```

- 좌표: 게임 나무 그림 `Farm/Tree/tree_<pic>.png`의 비율 (u 오른쪽, v 아래). pic = 단계 − 1. 그림 밖(u < 0, u > 1)도 된다(나무 옆 땅).
- `nodes`/`edges`: 정령 길. edge = `[a, b, 움직임, 그림?]` — 움직임 walk·climb·hop(정령 동작), 그림 plank·ladder·bridge(가이드에 그릴 것, 게임은 안 씀).
- `mush`: 구역별 나무버섯 자리. 게임은 구역마다 **앞 번호부터** 슬롯 수만큼 쓴다. 모자라면 예전 공식 자리.
- `deco`: 장식 id별 자리. 그 단계에 열리는 장식(합류한 꼬마의 장식 3종씩 → n단계 3n종) **전부** 자리를 가진다.
  걸이형은 고리(그림 위 끝), 설치형은 바닥(그림 아래 끝)이 점에 닿는다. 셋째 값 = 크기 배율(기본 1).
  장식이 어느 슬롯에 놓이든 제 자리에 그려진다. 슬롯 수·걸이/설치 규칙(게임 규칙)은 그대로.
- `aspect`: 그림 너비/높이. 가이드 그림의 틀.

## 순서

1. 처음 한 번: `python Tools/tree-layout/seed_layout.py` — 지금 그림에서 초안 (길은 눈으로 짚은 값, 버섯·장식은 그림 색으로 자동). **덮어쓴다.**
1-1. 정령 크기에 맞추기: `python Tools/tree-layout/fit_spots.py [--dry]` — 서 있는 정령(1배 화각 110×155px × 화각^0.75, 플레이에서 잰 값,
   `tree_defs.SPIRIT_W/H`)과 나무 너비로: 머무는 곳끼리 안 겹치게(|dx| < 0.9폭 · |dy| < 0.8키면 겹침 → 버섯 계단이면 지나가는 마디로, 아니면 바깥으로 밂),
   막다른 발판은 정령 폭 1.3배 이상, 머무는 곳 수 ≥ 그 단계 정령 수(경고만). 게임은 한 마디에 한 마리만 세운다(`FarmView.TakenNav`).
2. 편집: `node Tools/tree-layout/serve.mjs` → http://127.0.0.1:5179/ (Claude 미리보기 `tree-layout`). 끌어서 옮기고 Ctrl+S.
   - 마디 추가 · 선 잇기(두 마디를 차례로, 있으면 지움) · 버섯 추가(구역 고르고) · Delete로 지우기 · Ctrl+Z
   - 머무는 곳마다 정령 크기 상자(발이 점)를 그리고, 겹치면 빨강. 아래 상태줄에 정령 크기와 `머무는 곳 / 정령 수`.
   - 왼쪽 장식 목록에서 "자리 없음"을 누르면 화면 가운데에 자리를 만든다. 나무버섯은 구역별 `있음 / 필요` 칸 수를 보여 준다.
   - 저장할 때 `Temp/tree_layout.bak.<시각>.json`에 백업한다.
3. 확인 그림: `python Tools/tree-layout/make_guide.py [pic ...]` → `Logs/Screens/tree_layout_<pic>.png` (장식 그림을 게임 크기로 얹음)
4. 가이드 그림: `python Tools/tree-layout/make_guide.py --guide` → `Art/Raw_Dream/tree_guide/guide_s<단계>.png` + `.txt`(프롬프트)
   - 회색 점선 틀 = 나무가 채울 자리, 옅은 회색 = 지금 실루엣, 갈색 막대 = 발판, 사다리 = 사다리, 연보라 타원 = 선반버섯 계단,
     빨간 점선 상자 = 비워 둘 자리(걸이 장식, 점 바로 위에 가지), 주황 상자 = 평평한 빈 땅(설치 장식), 초록 점 = 나무버섯이 날 맨 껍질,
     파란 점선 상자 = 정령이 설 자리(아래 변이 발판·계단·굵은 가지 위, 상자 안은 비움).
5. 새 나무 그림: `bash Tools/tree-layout/gen_trees.sh [단계 ...]` → `Art/Raw_Dream/concept/v11/tree_v11_s<단계>.png`
   (참고 그림 = 지금 그 단계 그림 + 가이드, 이미 있으면 건너뜀)
6. 게임에 넣기: `python Tools/art/install_tree_stages.py && python Tools/art/blend_tree_base.py` (v11이 있으면 v11을 먼저 쓴다),
   출력되는 `WT.width`를 `Defs.Farm.cs`에 붙이고, 편집기에서 점을 새 그림에 맞게 다시 맞춘다.

파이썬은 `uv run --with pillow python ...`으로 돌린다(시스템 파이썬엔 Pillow 없음).
`tree_defs.py`가 C# 정의(장식·꼬마 합류 순서·구역·슬롯 수·나무 너비·화각)를 정규식으로 읽는다. `serve.mjs`의 `/api/defs`도 같은 정규식.
