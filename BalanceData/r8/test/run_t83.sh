#!/bin/bash
# T8-3 전체: bash BalanceData/r8/test/run_t83.sh
cd /c/Project/MoreMustMush || exit 1
bash BalanceData/r8/test/run_t8.sh T8-3
bash BalanceData/r8/test/run_t8s.sh T8-3/skill10 1.0
OUT=T8-3 bash BalanceData/r8/test/run_t81.sh snap,eff,m 4
U="/c/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe"
"$U" -batchmode -nographics -projectPath "C:/Project/MoreMustMush" -executeMethod MoreMush.EditorTools.SinkChecks.Batch -logFile BalanceData/r8/test/T8-3/sinkchecks_unity.log
cp BalanceData/sink_checks.txt BalanceData/r8/test/T8-3/sinkchecks.txt
echo ALLDONE
