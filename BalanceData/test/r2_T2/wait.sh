#!/bin/bash
cd /c/Project/MoreMustMush
while true; do
  out=$(timeout 100 bash Tools/uexec.sh < BalanceData/test/r2_T2/idle.cs 2>&1)
  case "$out" in *'"success":true'*) echo "$out"; break;; esac
  sleep 15
done
