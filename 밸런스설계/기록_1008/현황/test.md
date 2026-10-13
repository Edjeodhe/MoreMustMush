# test 현황
끝: T2·T3·T4·verify#1·#2 완료, T5(판매 전부+해금 즉시 이동) 15:28 마감으로 중단(반복 1: T3 가격표 완료 R56, 가격 미수렴) · 게임 가격 T3 유지(md5 확인) · 열린 요청 research#1 다음 세션 (15:29)

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
- [x] manager#10 → T4 끝(-ramp 10: 완료 R85~87 · 바다 첫 판 R75 · 5판 창 비 최대 ×192~372), T3 가격 복원 확인(md5 334af562…), `-ramp 10 -skill 0.75`는 manager#13로 취소 · `밸런스설계/테스트.md` T4, 가격표 `BalanceData/test/prices_T4_S1/` (15:12)
- [x] research#1 → 이번엔 못 함(15:30 마감): 관리자가 넣은 phase json `madeT`/`killT`(티어별 생긴 수·수확 수)·`kill`(판별 처치율)·`zone`은 다음 실행(T5 이후)부터 생긴다. T5 기록에서 `kill`(해금 직후 5판 처치율)은 `테스트.md` T5, 티어별 표(판 × 티어 등장/처치 + 공격력 + 일반 체력)는 다음 세션 몫 (15:16)
- [x] manager#15 → 부분: 반복 1 완료 R56(T3 가격이 새 수입에서 너무 쌈), 반복 2 중단. 해금 직후 처치율(비싼 가격표): 밤 R20~24 중앙 0.00, 들판 R45~49 0.50~0.69, 바다 0.90~0.97 · `테스트.md` T5, `BalanceData/test/T5_partial/` (15:29)
