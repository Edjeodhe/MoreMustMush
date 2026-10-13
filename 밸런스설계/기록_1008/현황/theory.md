# theory 현황
지금: 끝: manager#11 — 이론.md 3판(지역별 보정·표본 밖 확인·S1 재평가·research 0.6자릿수 확인). T3·T4 기록 대기(14:24)

## 공유할 결과
- `BalanceData/theory/replay.py` · 시뮬 기록의 판별 수입으로 BulkBuy를 다시 돌리면 시드 1~3 모두 판별 구매 수·끝 상태·끝 골드가 시뮬과 **완전히 같다**(불일치 0판). 판별 노드 레벨 복원 가능 · 13:50
- `BalanceData/theory/model.py` · 판 수입 근사(log10 RMSE 0.40 = 중앙 오차 ×1.8)로 진행 재계산: 트리 완료 몬테카를로 중앙값 R98(p10 R85 ~ p90 R110), 시뮬 R97~R98 · 가격 모양 ×1.77 · ×4.00 · 7/7 재현 · 13:55
- `밸런스설계/이론.md` · **초안(spec 기준)**: 근사식·오차, 원칙별 문제점(P10 −6%, P7 4/7, P6 ×85~170 = 지역 진입 구조), 수치안 B(17개 노드, 뜀 ≤×1.5 · CROSS 0.66 → 중앙 R104, P6은 그대로, 견고성 ⚠), tuned 외삽(현재 가격 R74, R104엔 전체 ×1.93) · 14:45
- `BalanceData/theory/theory_prices.json` · 제안 B 가격표 전체(spec 기준, 적용 보류 권장) · 14:45

- `BalanceData/theory/theory_fit_tuned.json` · tuned(T2) 재적합: RMSE 0.27, 재현 완료 R76(시뮬 R75), 완료가 바다 해금 R75에 묶임 · 15:40
- `BalanceData/theory/theory_structure_tuned.json` · 구조 실험: 가격만 ×2.08(T3 근사)이면 바다 진입이 완료 판 R103으로 밀림, 지역 배율 10판 나눠 올리기(S1)면 P6 ×241→×84·바다 R75 진입 · 15:40
- `밸런스설계/이론.md` 2판(tuned) · 15:40

- `밸런스설계/이론.md` 3판 · 지역 상수로 들판 과소 보정(표본 밖 편향 +0.15→+0.03), 가격만 ×2.24면 R93·바다 R89, S1이면 바다 R75·P6 지역 몫만 ×10.5→×2.6(구매 몫 ×43 그대로), research 0.6자릿수 = 스테이지 1.01^판 항 · 14:24
- `BalanceData/theory/model.py --compare <폴더> [--ramp 10]` · T3·T4 대조 명령 · 14:24

## 요청
- [x] #1 (답: manager 14:05 — 에디터 프리셋 = spec 확정, 빌드 기본은 tuned, 기준은 사용자 결정 → tuned. 이론.md에 spec 표시·tuned 외삽 적음) → test: 배치 시뮬이 어떤 밸런스 프리셋(`Defs.TUNE`, PlayerPrefs `mushroomPinball_preset`)으로 도는지 확인 · 왜: 로그의 지역 이동이 일반 버섯 체력 3(`spec`)일 때만 맞음 · 받고 싶은 형태: 프리셋 이름 한 줄 (13:55) [참고]

## 받은 요청 처리
- [x] manager#11 → verify 2단계(2) 요청 5개 반영, research 확인(0.6자릿수는 ZoneMul의 1.01^(판-1) 0.41자릿수 + 양끝 판 운), A4는 표본 밖 판별 기록으로 판 함수 재적합(별 함수보다 오차 작음). T3·T4는 아직 없음 → 나오면 `--compare`로 대조 · `밸런스설계/이론.md` 3판 (14:24)
- [x] manager#6(이어서) → T2로 tuned 재적합, verify 2단계 수정 요청 7개 반영, 구조 수치안 S1~S4 · `밸런스설계/이론.md` 2판 (15:40)
- [x] verify 2단계 수정 요청 1~7 → 반영 · `밸런스설계/이론.md` 6절 (15:40)
- [x] manager#2 → 재현 완료(완료 중앙 R98 vs 시뮬 R97~98, 진행 단계·지역 진입 ±15% 안), 문제점·수치안 초안 · `밸런스설계/이론.md`, `BalanceData/theory/` (14:45)
- [x] manager#6 → spec 표시 유지, P6·P7·P10 기준으로 판정, Code_Key 표(이론.md 4-3). tuned 재적합은 test T2 기록이 나오면 함(아직 `BalanceData/test/`에 tuned 기록 없음) · `밸런스설계/이론.md` 4-4·8절 (14:45)
