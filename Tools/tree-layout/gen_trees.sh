#!/usr/bin/env bash
# 도면(tree_layout.json) 가이드로 세계수 그림을 새로 뽑는다 (Codex 이미지, 한 장 1~2분, ChatGPT 이미지 한도를 쓴다).
#   bash Tools/tree-layout/gen_trees.sh [단계 ...]      (기본 1~8, 이미 있는 그림은 건너뜀 — 다시 뽑으려면 파일을 지운다)
# 참고 그림 1 = 지금 그 단계 그림(그림풍·모양), 2 = 가이드 그림(make_guide.py --guide). 프롬프트 = 가이드 옆 .txt
# 결과: Art/Raw_Dream/concept/v11/tree_v11_s<단계>.png → install_tree_stages.py가 이 그림을 먼저 쓴다.
# 그 다음: python Tools/art/install_tree_stages.py && python Tools/art/blend_tree_base.py → 웹 편집기로 점을 새 그림에 맞춤
set -u
cd "$(dirname "$0")/../.."
STAGES=${*:-1 2 3 4 5 6 7 8}
OUT=Art/Raw_Dream/concept/v11
mkdir -p "$OUT"
uv run --with pillow python Tools/tree-layout/make_guide.py --guide >/dev/null || exit 1
ref() {   # 지금 단계 그림: 다시 뽑는 v11(prev/로 옮겨 둔 것) → 장식을 뗀 맨 나무 → 예전 그림
  local s=$1 c=Art/Raw_Dream/concept
  for f in "$c/v11/prev/tree_v11_s$s.png" "$c/bare/bare_s$s.png" "$c/tree_v10_s$s.png"; do [ -f "$f" ] && { echo "$f"; return; }; done
  echo "$c/tree_v9_c.png"
}
# 덧붙일 요청 (예: EXTRA="Make the grass lusher." bash gen_trees.sh 6 7 8). 다시 뽑기: 지금 v11을 v11/prev/로 옮기고 돌린 뒤
# remap_layout.py --force <pic> 로 도면을 새 그림에 옮긴다 (가이드는 이 스크립트가 지금 도면·그림으로 다시 만든다)
EXTRA=${EXTRA:-}
for s in $STAGES; do
  out="$OUT/tree_v11_s$s.png"
  if [ -f "$out" ]; then echo "[s$s] 있음, 건너뜀"; continue; fi
  g=Art/Raw_Dream/tree_guide/guide_s$s
  echo "[s$s] 생성: ref $(ref $s) + $g.png"
  node Tools/codex-image.mjs --transparent --ref "$(ref $s)" --ref "$g.png" --out "$out" --prompt "$(cat "$g.txt") $EXTRA" 2>&1 | tail -2
  [ -f "$out" ] || { echo "[s$s] 실패 (한도면 나중에 다시)"; }
done
