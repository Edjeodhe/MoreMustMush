#!/bin/bash
# 남은 리그2 파츠 전부 생성·맞추기 (메인 폴더). 코덱스 한도 풀린 뒤 돈다. 사용자 지시(2026-10-19 밤):
#   ★2 = 뒷머리카락 없음 (몸통·옆/뒤 머리만) · ★3 = 불 ★3처럼 전부 새로 (불 ★3은 그대로) · 달·별 ★0·★1 추가 · ★0·★1 버섯 파츠 새로
cd /c/Project/MoreMustMush
# (코덱스 한도가 걸려 있으면 풀린 뒤에 돌린다)
set -x
G="node Tools/rig-web/gen_looks.mjs"
F="python Tools/rig-web/fit_gen_parts.py"
S3="dew_3,leaf_3,moon_3,rock_3,spark_3,star_3,wind_3"
S2="moon_2,rock_2,spark_2,star_2,wind_2"
M="dew_0,dew_1,fire_0,leaf_0,leaf_1,moon_0,moon_1,rock_0,rock_1,spark_0,spark_1,star_0,star_1,wind_0,wind_1"
# 0) ★0·★1 버섯 (fire_1은 끝남)
$G --looks $M --jobs 4
for lk in ${M//,/ }; do $F --look $lk; done
# 1) ★3 전부 새로 (머리·몸통·소매)
$G --looks $S3 --phase 1 --force --jobs 4
# 2) ★2 나머지: 크림 드레스·소매 새로, 머리는 없는 것만
$G --looks $S2 --phase 1 --parts body_side,body_back,sleeve --force --jobs 4
$G --looks $S2 --phase 1 --parts head_side,head_back --jobs 4
# 3) 몸통 맞추기 (불 얼굴 크기에 맞춤 + 배치 그림)
for lk in ${S3//,/ } ${S2//,/ }; do $F --look $lk --stage body; done
for lk in ${S2//,/ }; do $F --look $lk --stage nohair; done
# 4) ★3 머리카락 3장 (옆·뒤·정면, 머리 그림을 이어 그리기)
$G --looks $S3 --phase 2 --force --jobs 4
for lk in ${S3//,/ }; do $F --look $lk --stage hair; done
# 6) 키
python Tools/rig-web/grade_heights.py
echo ALLDONE
