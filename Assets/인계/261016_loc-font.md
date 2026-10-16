# 261016 외국어 제목 폰트 → 둥글고 굵은 글꼴 (loc-font 세션)

## 목표
사용자 지적 "jua가 아님 외국어 폰트들이": 외국어(en/ja/ru/uk)로 바꾸면 제목·버튼·트리 이름표가 세리프(Lora·Kiwi Maru)나 가는 본문 폰트로 보였다. 한국어 화면(ONEMobilePOP·Jua 느낌)처럼 둥글고 굵게 맞춘다.

## 끝난 것
- 제목(Title) 역할 폰트: en Lora-Bold → **Jua-Regular**, ru·uk Lora-Bold → **Nunito-Black**, ja KiwiMaru-Medium → **MPLUSRounded1c-ExtraBold**. ko는 그대로(GowunBatang-Bold).
- 배치 UI의 ONEMobilePOP 글자(MainGame 140개 등): ja·ru·uk에서 본문 폰트 대신 **그 언어 제목 폰트**로 바뀐다. 외곽선 프리셋(Outline Dark/Wood/Brown)은 머티리얼 이름 끝으로 이어받는다(전에는 외곽선이 빠졌다). ko·en은 그대로 ONEMobilePOP.
- 세계수 탭 창(FarmTabWindow.LangFont)도 같은 규칙으로 바꿨다.
- 메뉴 1(폰트 에셋)·4(굽기) 실행. 다섯 언어 DB 문구를 대체 폰트까지 포함해 검사했다. 화면 글자 231개 중 빠진 글자 0. 스크린샷은 `Captures/font/`(compare_langs.png, *_main.png, *_menu.png).

## 남은 것 (순서대로)
1. **사용자 결정 필요**: 한국어 트리 이름표와 제목 역할은 지금 고운바탕(세리프)이다. Jua가 아니다. 한국어도 Jua로 하려면 `Assets/Editor/LocTools.cs` FONT_SETS의 ko 제목을 `"Jua-Regular"`로 바꾸고 메뉴 1·4를 실행한다. 이때 Jua-Regular에 한글이 다시 구워지고, 대체 폰트 첫 칸(FONT_SETS[0].title)도 Jua가 된다.
2. 세계수 탭 창(OutGame)을 ja·ru·uk로 찍어 보지 못했다.
3. WebGL 빌드에서 직접 확인하지 못했다(에디터 정적 아틀라스 검사만 했다).
4. (범위 밖) MainMenu 버튼과 "48 Stage", "Stage Start", "MushTree", "MushDex", "Harvesters"는 번역 없이 영어로 나온다.

## 바꾼 파일과 이유
- `Assets/Editor/LocTools.cs`: FONT_SETS의 en/ja/ru/uk 제목 폰트.
- `Assets/Scripts/UI/LocFonts.cs`: `koEnTitleFonts`(다른 언어에서 제목 폰트로 바꿀 한·영 전용 폰트 = ONEMobilePOP), `KoEnRole()`, `PresetOfName()`.
- `Assets/Scripts/UI/TextLangFont.cs`: koEn 글자의 역할과 프리셋을 위 함수로 정한다.
- `Assets/Scripts/UI/FarmTabWindow.cs`: `LangFont`만 고쳤다(본문 대신 KoEnRole). **이 파일의 다른 변경(버튼 글자색 등)은 ui-skin 세션 것이다.**
- `Assets/Resources/Loc/LocFonts.asset`: 세트 다시 만듦 + koEnTitleFonts.
- `Assets/Fonts/Jua-Regular SDF.asset`: 영어 제목 전용으로 다시 구움(101자). 예전 한글은 아무도 안 써서 빠졌다.
- `Assets/Fonts/MPLUSRounded1c-ExtraBold SDF.asset`: GUID를 유지한 채 48/6으로 다시 만들고 일본어 958자를 구움(아틀라스 1장, WebGL 용량).
- `Assets/Fonts/Nunito-Black SDF.asset`: 대체 폰트 첫 칸만 바뀜(Jua → GowunBatang).
- 위 3개 폰트의 `* Outline Dark/Wood/Brown.mat`: 새 아틀라스를 가리키게 하고, 지금의 몽환 외곽선 값으로 맞춤.

## 주의
- GowunBatang/GowunDodum/ONEMobileRegular/ZenMaru SDF는 메뉴 4가 같이 다시 구워서 **git checkout으로 되돌렸다**. 메뉴 4를 다시 돌리면 이 4개도 또 바뀐다(커밋할 때 넣을지 판단할 것). 한국어 폰트에 '폰'이 없다고 나오는 건 LocFonts.cs 툴팁이 번역표에 딸려 들어간 것이다. 화면에는 안 나온다.
- Lora-Bold·KiwiMaru-Medium 에셋은 이제 안 쓰지만 지우지 않았다.
- Unity 에디터 공용 규칙: 플레이·언어 전환·씬 전환·씬 저장 전에 `Temp/unity_editor.lock`을 잡는다(최대 5분, 10분 넘은 잠금은 지워도 된다).
- 플레이 중에 재컴파일되면 `MainGameController.OnLang`에서 NullReferenceException이 난다(도메인 리로드 탓). 플레이를 다시 시작하면 정상이다.
- 커밋하지 않았다.

## 확인 방법
1. 잠금을 잡고 MainGame을 플레이한다. 실행 코드로 `MoreMush.Loc.SetLang("en"|"ja"|"ru"|"uk"|"ko")`를 부르고 화면을 찍는다.
2. 빠진 글자 검사: 활성 TMP마다 `t.font.HasCharacter(c, true, false)`를 언어별로 확인한다(전부 빈 결과여야 한다).
3. 폰트 확인: en 트리 이름표 = Jua, ru·uk = Nunito Black(ІЇЄ 포함), ja = M PLUS Rounded ExtraBold, 하단 버튼(의뢰 등)도 같은 폰트에 외곽선이 있어야 한다.
