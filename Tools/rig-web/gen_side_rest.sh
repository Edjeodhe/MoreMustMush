#!/bin/bash
# 옆모습·★3 나머지 생성 + 리깅 맞추기 (2026-10-19 사용자: 옆모습 없는 애들 전부 · ★3은 불 ★3 구조 그대로).
#   ★3 구조(불 ★3 = 기준, 손대지 않음): 옆·뒤 머리 + 크림 드레스 몸통 옆·뒤 + 소매 + 무릎 다리 + 공 관절 없는 팔
#     + 머리카락 한 장 옆·뒤·정면(뼈 사슬 스킨). fit_gen_parts.py가 같은 방식으로 맞춘다.
#   크림 드레스(몸통 옆·뒤·소매)는 모든 정령이 같아, 없으면 fit_gen_parts.py가 같은 등급 물·불·풀 것을 빌린다.
#   ★2 옆머리 임시 합성(gen_head_side_tmp.png)은 코덱스 원본(gen_head_side.png)이 생기면 자동으로 밀려난다.
#   코덱스 한도에 걸리면 바로 멈춘다 (다시 돌리면 이미 있는 그림은 건너뜀).
#   bash Tools/rig-web/gen_side_rest.sh
cd /c/Project/MoreMustMush || exit 1
G="node Tools/rig-web/gen_looks.mjs"
F="python Tools/rig-web/fit_gen_parts.py"
S2="moon_2 rock_2 spark_2 star_2 wind_2"
S3="dew_3 leaf_3 moon_3 rock_3 spark_3 star_3 wind_3"
RAW=Art/Raw_Dream/rig2

need() {   # need <모습> <부위>...: 원본이 다 있어야 계속
  local lk=$1; shift
  for p in "$@"; do
    [ -f "$RAW/$lk/gen_$p.png" ] || { echo "STOP: $lk gen_$p.png 생성 실패 (코덱스 한도?) — 한도 풀린 뒤 다시 돌리면 있는 것은 건너뜀"; exit 1; }
  done
}

# 1) 옆·뒤 머리 (있는 것은 건너뜀)
for lk in $S2 $S3; do
  $G --looks $lk --phase 1 --parts head_side,head_back --jobs 2
  need $lk head_side head_back
done
# 2) ★3 몸통 옆·뒤·소매: 불 ★3처럼 자기 것 (없는 것만)
for lk in $S3; do
  $G --looks $lk --phase 1 --parts body_side,body_back,sleeve --jobs 3
  need $lk body_side body_back sleeve
done
# 3) 몸통·다리·팔·소매 맞추기 (★2는 뒷머리카락 떼기)
for lk in $S2; do $F --look $lk --stage body && $F --look $lk --stage nohair || exit 1; done
for lk in $S3; do $F --look $lk --stage body || exit 1; done
# 4) ★3 머리카락 (옆·뒤·정면 한 장씩, 3)에서 만든 배치 그림을 보고 이어 그리기).
#    물 ★3 머리카락은 예전 프롬프트로 뽑은 것이라 새로 (표시 파일로 한 번만)
if [ ! -f "$RAW/dew_3/.hair_v2" ]; then
  rm -f "$RAW/dew_3/gen_hair_side_full.png" "$RAW/dew_3/gen_hair_back_full.png" "$RAW/dew_3/gen_hair_front_full.png"
fi
for lk in $S3; do
  $G --looks $lk --phase 2 --parts hair_side_full,hair_back_full,hair_front_full --jobs 3
  need $lk hair_side_full hair_back_full hair_front_full
  $F --look $lk --stage hair || exit 1
  [ "$lk" = dew_3 ] && touch "$RAW/dew_3/.hair_v2"
done
# 5) 불 ★3 정면 뒷머리 한 장 (불 ★3의 나머지는 그대로, 정면만)
$G --looks fire_3 --phase 2 --parts hair_front_full --jobs 1
need fire_3 hair_front_full
$F --look fire_3 --stage front || exit 1
# 6) 같은 등급 키
python Tools/rig-web/grade_heights.py
echo ALLDONE
