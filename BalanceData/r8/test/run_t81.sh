#!/bin/bash
# 사용: bash BalanceData/r8/test/run_t81.sh <items 쉼표> [reps]  (결과 BalanceData/r8/test/T8-1/)
ITEMS=$1; REPS=${2:-6}
cd /c/Project/MoreMustMush || exit 1
"/c/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Project/MoreMustMush" \
  -executeMethod MoreMush.EditorTools.BalanceR8b.T81 -items $ITEMS -reps $REPS -out BalanceData/r8/test/${OUT:-T8-1}/ \
  -logFile "C:/Project/MoreMustMush/BalanceData/r8/test/${OUT:-T8-1}/unity81_${ITEMS//,/_}.log"
echo exit=$?
