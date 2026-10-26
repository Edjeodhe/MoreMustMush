#!/bin/bash
# 사용: bash BalanceData/r8/test/run_t8s.sh <출력폴더> <skill>   (T80을 SKILL 지정해 돌림)
OUT=$1; SK=$2
cd /c/Project/MoreMustMush || exit 1
mkdir -p BalanceData/r8/test/$OUT
"/c/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Project/MoreMustMush" \
  -executeMethod MoreMush.EditorTools.BalanceR8.T80 -seeds 1,2,3,4,5,6 -skill $SK -out BalanceData/r8/test/$OUT/ \
  -logFile "C:/Project/MoreMustMush/BalanceData/r8/test/$OUT/unity.log"
python -X utf8 BalanceData/r8/test/summ_t8.py BalanceData/r8/test/$OUT/ > BalanceData/r8/test/$OUT/summ_out.txt
