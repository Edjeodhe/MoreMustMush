using UnityEngine;

namespace MoreMush
{
    // 화면 연출 설정 (옵션 창, 기기에 남긴다)
    public static class FxPrefs
    {
        const string SimplePref = "mmm_simple_fx";
        static bool simpleV, loaded;
        // 연출 간소화: 날아가는 버섯 아이콘·대미지 숫자·스킬 효과(구름·충격파·고리·회오리·번개·스킬 이름)를 그리지 않는다 (끊김 줄이기, 2026-10-20).
        // 화면(RoundView)에서만 빼고 라운드 판정(RoundSim)은 그대로다 (구름·회오리·충격파는 안 보여도 대미지·감속을 준다)
        public static bool Simple
        {
            get { if (!loaded) { loaded = true; simpleV = PlayerPrefs.GetInt(SimplePref, 0) == 1; } return simpleV; }
            set { loaded = true; if (simpleV == value) return; simpleV = value; PlayerPrefs.SetInt(SimplePref, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
