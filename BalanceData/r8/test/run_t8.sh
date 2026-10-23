#!/bin/bash
# T8 기준선/재측정 실행. 사용: bash BalanceData/r8/test/run_t8.sh <출력폴더 예: T8-0> [시드 목록]
# 에디터가 프로젝트를 열고 있으면 안 된다(잠금). 결과: BalanceData/r8/test/<출력폴더>/ (rounds_s*.csv, t80_s*.log, phase_s*.json, cond.txt)
OUT=${1:-T8-0}; SEEDS=${2:-1,2,3,4,5,6}
cd /c/Project/MoreMustMush || exit 1
mkdir -p BalanceData/r8/test/$OUT
"/c/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Project/MoreMustMush" \
  -executeMethod MoreMush.EditorTools.BalanceR8.T80 -seeds $SEEDS -out BalanceData/r8/test/$OUT/ \
  -logFile "C:/Project/MoreMustMush/BalanceData/r8/test/$OUT/unity.log"
echo "exit=$?"
python -X utf8 BalanceData/r8/test/summ_t8.py BalanceData/r8/test/$OUT/ > BalanceData/r8/test/$OUT/summ_out.txt
