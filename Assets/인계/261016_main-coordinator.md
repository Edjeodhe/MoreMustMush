# 261016 메인 조율 세션 인계 (2차, 21:35)

브랜치 KHD-Teset. **커밋 안 함**(사용자 확인 뒤에만). 보고는 한글.
1차 병렬 세션 4개(트리 연결선·외국어 글꼴·버튼 스킨·PV)는 모두 끝나 있었다. 2차는 이 세션 혼자 + 하위 에이전트.

## Unity 에디터 공용 규칙
- 플레이·씬 전환·씬 저장·메뉴 실행 전에 `Temp/unity_editor.lock` 확인 → `이름 | 하는 일 | 시각` 쓰고 잡기, 최대 5분, 끝나면 플레이 멈추고 MainMenu로, 잠금 삭제. 10분 넘은 잠금은 지워도 됨.
- 플레이 중 재컴파일 → `MainGameController.OnLang` NRE (도메인 리로드 탓, 재시작하면 사라짐).

## 2차에서 한 일
### 정산(결과) 창 복원 — 끝, 플레이 확인
- 원인: 씬 분리 커밋 `ed6b8aa`(10-15 "UI 와이어 수정중")가 옛 `Game.unity`와 `SettleScreen.cs`·`NewCard`·`UIAction`·`UIButtonFx`·`RuntimeOverlays`를 지웠다. `resource_test`(7424d37)에서 사용자가 수채화 그림·손글씨 제목(`Lettering/*/settle_done·settle_stop`)으로 꾸민 정산 창도 같이 사라졌다. Stage의 `StageController.Finished`에 구독자가 없어 결과창 없이 바로 MainGame으로 갔다.
- 복원: 7424d37에서 `SettleScreen.cs`·`NewCard.cs`·`UIAction.cs`·`UIButtonFx.cs`(+meta, 원래 GUID)와 `Assets/Prefabs/UI/NewCard.prefab`을 되살리고, 옛 씬의 `Canvas/SettleScreen`을 그대로 복사해 `Stage.unity` Canvas 맨 끝에 넣었다(꺼진 채, Stage 씬 diff는 추가만 7517줄).
- 고친 점: `UIAction`은 GameFlow가 없어 이름표만 남기고, `SettleScreen`이 act를 보고 버튼을 붙인다(totree→MainGame, worldtree→OutGame, dmgstats→데미지 통계 창; 통계 창은 옛 RuntimeOverlays 코드를 옮김). `SettleScreen.Attach`가 Stage 로드 때 `Finished`를 구독한다. 옛 타이틀 배경용 `ShiftCritters`는 지움.
- 확인: 중단(Harvest Stopped)·완료(Harvest Complete!) 둘 다 표시, 균사 트리로 버튼 → MainGame 이동. `Captures/stage_settle_check.png`, `stage_settle_dmgstats.png`.
- **부작용**: 완료 테스트로 에디터 세이브 스테이지가 48 → 49로 올랐다.

### 기타
- 수채화 바: Stage에서 판정 줄 위치에 보임 `Captures/stage_bar_check.png`. 분신 바(파란 틴트)는 못 봄.
- `OutGameController.cs` BackButton: 이전 세션이 없는 클래스 `OptionWindowMark`를 참조한 채 끊겨 컴파일 오류 → 그 검사만 빼서 고침. 세계수 돌아가기 팻말 화면 확인은 아직.
- 세계수 정령 옛 그림·나무 버섯 그림: 이미 고쳐져 있음(UIUtil.KidPortrait, Defs.Farm.cs TREE_MUSH). 화면 확인은 아직.
- 한국어 제목·트리 이름표 Jua: 하위 에이전트 진행(아래 결과 참고).

## 남은 일
1. 세계수(OutGame) 화면 확인: 돌아가기 팻말, 정령 목록 그림·비율, 나무 버섯 4종.
2. 정령 칸·수확기 카드 아이콘 preserveAspect 화면 확인.
3. PV: `Marketing/PV/out/MoreMustMush_PV_30s_preview.mp4`까지. 균사 트리·세계수 컷 녹화가 남음(`Tools/pv/build_pv.py`, 프레임은 `Temp/pvcap/<이름>/`).
4. 사용자에게 요약하고 커밋 여부 묻기.
