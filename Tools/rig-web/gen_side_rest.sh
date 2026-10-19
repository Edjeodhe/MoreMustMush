#!/bin/bash
# 옆모습 없는 정령(달·바위·번개·별·바람 ★2·★3) 옆·뒤 파츠 생성 + 리깅 맞추기 (2026-10-19 사용자: 옆모습 없는 애들 전부).
#   크림 드레스(몸통 옆·뒤·소매)는 모든 정령이 같아 fit_gen_parts.py가 같은 등급 물·불·풀 것을 빌린다 → 머리만 새로 뽑는다.
#   코덱스 한도에 걸리면 바로 멈춘다 (예전 gen_rest.sh는 실패한 채 맞추기를 계속 돌아 불 머리가 다른 정령에 붙고 메모리가 터졌다).
#   bash Tools/rig-web/gen_side_rest.sh
cd /c/Project/MoreMustMush || exit 1
G="node Tools/rig-web/gen_looks.mjs"
F="python Tools/rig-web/fit_gen_parts.py"
S2="moon_2 rock_2 spark_2 star_2 wind_2"
S3="moon_3 rock_3 spark_3 star_3 wind_3"
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
# 2) 몸통·다리·팔·소매 맞추기 (★2는 뒷머리카락 떼기)
for lk in $S2; do $F --look $lk --stage body && $F --look $lk --stage nohair || exit 1; done
for lk in $S3; do $F --look $lk --stage body || exit 1; done
# 3) ★3 머리카락 (옆·뒤·정면 한 장씩, 2)에서 만든 배치 그림을 보고 이어 그리기)
for lk in $S3; do
  $G --looks $lk --phase 2 --parts hair_side_full,hair_back_full,hair_front_full --jobs 3
  need $lk hair_side_full hair_back_full hair_front_full
  $F --look $lk --stage hair || exit 1
done
# 4) 같은 등급 키
python Tools/rig-web/grade_heights.py
echo ALLDONE
