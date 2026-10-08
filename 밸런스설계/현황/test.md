# test 현황
지금: T4(S1 `-ramp 10` 가격 재맞춤) 실행 중(14:48 시작) → 이어 `-ramp 10 -skill 0.75` Measure, 끝나면 가격표 T3로 복원 · T2·T3·verify#1(옛·새 가격표)·verify#2 끝 (14:50)

## 공유할 결과
- `밸런스설계/테스트.md` T2(tuned 같은 가격표: 완료 R75) · T3(Run 3회 수렴: 완료 R90·R91·R91, P1 간격 최대 3, 해금 직후 5판 R20 ×4~10) · T3-b(시드 4~6: R91·R91·R91) · T3-c(판매 가치 ÷ 판 수입: R10 0.3 → R40 4.5~7 → R70·R100 8~12) · 새 가격표 사본 `BalanceData/test/prices_T3/` · 14:50
- `밸런스설계/테스트.md` T1 · 기준선(현재 가격표, 시드 1~3): 트리 완료 R97~R98(102~103분), 최대 뜀 ×1.77, 노드 안 ×4.00, 효율 넘김 7/7, 구매 간격 중앙 1·최대 5판, 중반 B구간 판당 ~5레벨 압축. 12:39 Run 기록과 바이트 동일 · 12:58
- `BalanceData/test/phase_stats.py` · 구매 간격·단계 구간·판 수입 튐 계산 스크립트(출력 `BalanceData/test/phase_stats_1257.txt`) · 12:58
- 배치 시뮬은 **spec 프리셋**(일반 버섯 체력 3, 군락 3+3L)으로 돈다 — 에디터 PlayerPrefs `mushroomPinball_preset` = `spec`. 새 빌드 플레이어 기본은 `tuned` · 13:01 (근거: 레지스트리 `HKCU\Software\Unity\UnityEditor\DefaultCompany\MoreMustMush`의 `mushroomPinball_preset_h3492484119` = `spec`, 빌드 쪽 `HKCU\Software\DefaultCompany\MoreMustMush` 키 없음 → `LoadPreset()` 기본값 `tuned`, `Assets/Scripts/Core/Defs.cs:50~60,302`)

## 요청
- [x] #1 → manager: `BalanceSim.PhaseLog`에 판별 버섯 가치 기록 추가 — `public List<double> val`을 두고 `Set(stage, income, bought)`에 `r.value`를 넘겨 `val[stage-1]`에 저장(`PlayRound`가 `res.value = s.value`를 이미 채움, `BalanceSim.cs:78`; `Progression`의 `ph.Set(r.stage, r.scoreGold + r.bonus, bought)` 호출부 `BalanceSim.cs:~156`) · 왜: verify#2(판매 가치 ÷ (점수골드+보너스) 판별 비율)가 지금 phase json(inc·buys만)으로는 안 구해짐 · 받고 싶은 형태: json에 `val` 배열(스테이지 순서) 추가 후 알려 주면 Measure 1회(약 10분)로 R10·R40·R70·R100 비율 표 (14:20) [참고]

## 받은 요청 처리
- [x] manager#4 → 기준선 측정 끝: R97~R98 완료, ×1.77 · ×4.00 · 7/7, 구매 간격 중앙 1 · 최대 5판(시드 합 257개 중 1판 239), MCP 끊김으로 실제 확인 건너뜀 · `밸런스설계/테스트.md` T1 (12:58)
- [x] theory#1 → 프리셋 = `spec`(에디터 PlayerPrefs, 배치·에디터 플레이 공통). 새 빌드 플레이어 기본은 `tuned`. 관리자 답(spec 기준 계속)에 따라 spec/tuned 판 수입 비교 측정은 돌리지 않음. 다시 필요하면: 같은 구성 고정 측정은 `Assets/Editor`에 배치 진입점 추가가 필요(관리자 적용 몫), R1~R104 전체 tuned 비교는 에디터 PlayerPrefs를 잠시 `tuned`로 바꿔 Measure 1회(약 5분, 끝나면 `spec`으로 복원) · `밸런스설계/테스트.md` T1 "프리셋" (13:01)
- [x] manager#5 → T2·T3 끝: 위 "공유할 결과" · `밸런스설계/테스트.md` T2·T3 (14:50)
- [x] verify#1 → 표본 밖 시드 4~6(옛·새 가격표 모두) R75(옛) / R91(새), skill 0.75(옛 가격표, 시드 1) 완료 R75 · `테스트.md` T2-b·T3-b (14:50)
- [x] verify#2 → 판매 가치 비 R10 ~0.3 · R40 4.5~7 · R70·R100 8~12 · `테스트.md` T3-c (14:50)
