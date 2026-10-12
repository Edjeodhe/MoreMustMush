using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MoreMush
{
    // 다국어. 번역표 Assets/Resources/Loc/strings.txt: 첫 줄은 언어 코드(ko en ja ...), 그 아래 줄마다 한국어 원문과 번역.
    // 키는 한국어 원문 그대로다. 코드는 Loc.T("원문"), 자리 표시가 있으면 Loc.F("버섯 {0}개", n).
    // 언어를 늘리려면 LANGS에 코드를 더하고 strings.txt에 같은 이름의 열을, LocFonts에 폰트 세트를 더한다.
    public static class Loc
    {
        public static readonly string[] LANGS = { "ko", "en", "ja" };
        public static readonly Dictionary<string, string> LANG_NAME = new Dictionary<string, string>
        {
            ["ko"] = "한국어", ["en"] = "English", ["ja"] = "日本語",
        };
        const string PrefKey = "mmm_lang", TablePath = "Loc/strings";

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
            if (Application.isEditor && missing.Add(ko)) Debug.LogWarning($"[Loc] 번역표에 없는 키 (MoreMush/다국어/3 번역표 갱신): {ko}");
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
            var map = new Dictionary<string, string>();
            if (lang == "ko") return map;
            var asset = Resources.Load<TextAsset>(TablePath);
            if (asset == null) { Debug.LogError("[Loc] Resources/" + TablePath + ".txt 없음"); return map; }
            int col = -1;
            foreach (var row in ParseTsv(asset.text))
            {
                if (col < 0) { col = Array.IndexOf(row, lang); if (col < 0) { Debug.LogError("[Loc] 번역표에 " + lang + " 열 없음"); return map; } continue; }
                if (row.Length > col && row[0].Length > 0) map[row[0]] = row[col];
            }
            return map;
        }

        // TSV 한 줄 = 한 항목. 칸 안의 줄바꿈·탭·역슬래시는 \n \t \\ 로 적는다
        public static IEnumerable<string[]> ParseTsv(string text)
        {
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                var cells = line.Split('\t');
                for (int i = 0; i < cells.Length; i++) cells[i] = Unescape(cells[i]);
                yield return cells;
            }
        }

        public static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n");

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
