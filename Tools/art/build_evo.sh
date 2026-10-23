#!/usr/bin/env bash
# 진화 정령 그림 전체 다시 뽑기 (코덱스 이미지 한도 필요: 시트 12장)
# 부품 8장 → 표정 참고 그림 → 공용 표정 1장 → 리그(관절·얼굴) → Unity에서 MoreMush 메뉴 Rebuild Sprite DB
set -e
cd "$(dirname "$0")/../.."
# 진화 줄(evo_pilot·evo_line_a·b)은 사용자 확정 그림 그대로 둔다 (2026-10-16) — 다시 뽑지 않음
ART_CONCURRENCY=3 node Tools/art/build-art.mjs --set dream --force evo_parts_fire evo_parts_dew evo_parts_leaf evo_parts_wind evo_parts_rock evo_parts_spark evo_parts_moon evo_parts_star
python Tools/art/fit_evo_parts.py                     # 얼굴을 떼어 내야 표정 참고 그림을 만든다
python - <<'PY'
from PIL import Image
P='Assets/Resources/Characters/Evo/'
cells=[Image.open(P+n+'.png').convert('RGBA') for n in ['e_fire_2_head','e_fire_2_face_normal','e_dew_1_body','e_dew_1_face_normal']]
cw=max(i.width for i in cells)+40; ch=max(i.height for i in cells)+40
S=Image.new('RGBA',(cw*2,ch*2),(0,0,0,0))
for n,i in enumerate(cells): S.alpha_composite(i,((n%2)*cw+(cw-i.width)//2,(n//2)*ch+(ch-i.height)//2))
S.save('Art/Raw_Dream/faces_ref_evo.png')
PY
node Tools/art/build-art.mjs --set dream --force evo_faces
python Tools/art/fit_evo_parts.py --preview
