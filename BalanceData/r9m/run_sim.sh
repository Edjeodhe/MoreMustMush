#!/bin/sh
# r9m 측정: Unity 배치로 BalanceR9m 실행. 사용: sh BalanceData/r9m/run_sim.sh <출력폴더(프로젝트 기준)> <seeds> <skills> [lagbosses]
# Unity가 이 프로젝트를 열고 있으면(Temp/UnityLockfile) 멈춘다.
P=C:/Project/MoreMustMush
OUT=$1; SEEDS=$2; SKILLS=$3; LAG=${4:-10,20,30,40,50,60,70,80,90,100}
if [ -f "$P/Temp/UnityLockfile" ] && tasklist 2>/dev/null | grep -qi "Unity.exe"; then echo "Unity가 이 프로젝트를 열고 있음 — 중단"; exit 2; fi
mkdir -p "$P/$OUT"
"C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe" -batchmode -nographics -projectPath "$P" \
  -executeMethod MoreMush.EditorTools.BalanceR9m.Batch -logFile "$P/$OUT/unity.log" \
  -seeds "$SEEDS" -skills "$SKILLS" -lagbosses "$LAG" -gems 1 -out "$OUT/"
echo "exit $?"
