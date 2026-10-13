using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MoreMush
{
    // 다국어. 번역표 = 엑셀 DB의 String 시트 (Str_KR 한국어 원문 · Str_EN · Str_JA, 빈 칸 = 번역 전). Db.cs가 db.json으로 읽는다.
    // 키는 한국어 원문 그대로다. 코드는 Loc.T("원문"), 자리 표시가 있으면 Loc.F("버섯 {0}개", n).
    // 새 문구는 Unity 메뉴 MoreMush/다국어/3 번역표 갱신이 String 시트 맨 아래에 추가한다.
    // 언어를 늘리려면 LANGS에 코드를 더하고 String 시트에 Str_<코드> 열(Db.cs도), LocFonts에 폰트 세트를 더한다.
    public static class Loc
    {
        public static readonly string[] LANGS = { "ko", "en", "ja" };
        public static readonly Dictionary<string, string> LANG_NAME = new Dictionary<string, string>
        {
            ["ko"] = "한국어", ["en"] = "English", ["ja"] = "日本語",
        };
        const string PrefKey = "mmm_lang";

        public static string Lang { get; private set; } = "ko";
        public static bool IsKo => Lang == "ko";
        public static event Action Changed;

        static Dictionary<string, string> table;       // 지금 언어: 원문 → 번역 (ko면 비어 있다)
        static readonly HashSet<string> missing = new HashSet<string>();

        static bool loaded;
        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            string saved = PlayerPrefs.GetString(PrefKey, "");
            Lang = Array.IndexOf(LANGS, saved) >= 0 ? saved : SystemLang();
            table = LoadTable(Lang);
        }

        // 처음 실행: 기기 언어가 지원 언어면 그것, 아니면 영어 (한국어 기기는 한국어)
        static string SystemLang() => Application.systemLanguage switch
        {
            SystemLanguage.Korean => "ko",
            SystemLanguage.Japanese => "ja",
            _ => "en",
        };

        public static void SetLang(string lang)
        {
            EnsureLoaded();
            if (Array.IndexOf(LANGS, lang) < 0 || lang == Lang) return;
            Lang = lang;
            table = LoadTable(lang);
            missing.Clear();
            PlayerPrefs.SetString(PrefKey, lang); PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string T(string ko)
        {
            if (string.IsNullOrEmpty(ko)) return ko;
            EnsureLoaded();
            if (table.Count == 0) return ko;
            if (table.TryGetValue(ko, out var s)) return string.IsNullOrEmpty(s) ? ko : s;   // 빈 칸 = 아직 번역 전 (한국어로 보인다)
            if (Application.isEditor && missing.Add(ko)) Debug.LogWarning($"[Loc] DB String 시트에 없는 문구 (MoreMush/다국어/3 번역표 갱신): {ko}");
            return ko;
        }

        public static string F(string ko, params object[] args)
        {
            string fmt = T(ko);
            try { return string.Format(CultureInfo.InvariantCulture, fmt, args); }
            catch (FormatException e)
            {
                Debug.LogError($"[Loc] 형식 오류 ({Lang}): {fmt}\n{e.Message}");
                return string.Format(CultureInfo.InvariantCulture, ko, args);   // 원문 형식은 코드와 함께 검사되므로 이건 맞다
            }
        }

        static Dictionary<string, string> LoadTable(string lang)
        {
            if (lang == "ko") return new Dictionary<string, string>();
            try { return Db.LangTable(lang); }
            catch (Exception e)
            {
                Debug.LogError("[Loc] DB 번역표를 읽지 못해 한국어로 보여요: " + e.Message);
                return new Dictionary<string, string>();
            }
        }
    }
}
