using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MoreMush.EditorTools
{
    // 다국어 도구. 순서: 폰트 에셋 만들기 → 씬 글자 연결 → 번역표 갱신 → (번역) → 폰트 굽기.
    // 폰트 세트는 언어마다 제목(Title)·본문(Body) 두 벌. 언어를 늘리면 FONT_SETS에 한 줄 더하고 TTF를 Assets/Fonts에 넣는다.
    public static class LocTools
    {
        const string FontDir = "Assets/Fonts", LocFontsPath = "Assets/Resources/Loc/LocFonts.asset", TablePath = "Assets/Resources/Loc/strings.txt";
        const int SamplingSize = 64, Padding = 8, AtlasSize = 2048;

        // 언어, 제목 TTF, 본문 TTF (확장자 없는 파일 이름)
        static readonly (string lang, string title, string body)[] FONT_SETS =
        {
            ("ko", "ONEMobilePOP", "ONEMobileRegular"),
            ("en", "ONEMobilePOP", "LINESeedSans_Rg"),
            ("ja", "LINESeedJP_Eb", "LINESeedJP_Rg"),
        };
        // 어느 폰트에도 없는 글자가 나오면 이 폰트로 넘긴다 (한글·가나·한자·기호를 모두 가진 폰트)
        const string UniversalFallback = "ONEMobileRegular";
        // 외곽선 프리셋 (LocFonts.PRESETS와 같은 순서)
        static readonly (Color32 col, float width)[] OUTLINES =
        {
            (new Color32(0x2a, 0x1a, 0x10, 255), 0.28f),
            (new Color32(0x3b, 0x24, 0x14, 255), 0.22f),
            (new Color32(0x5a, 0x2a, 0x00, 255), 0.3f),
        };
        // 예전 폰트 → 역할 (씬 글자 연결 때 역할을 정하는 데만 쓴다)
        static readonly Dictionary<string, LocFonts.Role> OLD_ROLE = new Dictionary<string, LocFonts.Role>
        {
            ["Jua SDF"] = LocFonts.Role.Title, ["NotoSansKR SDF"] = LocFonts.Role.Body,
        };
        const string SYMBOLS = "★☆♥♡●○▲▼▶◀→←↑↓×÷±−–—…·≥≤∞①②③④⑤⑥⑦⑧⑨⑩巨°「」『』、。・！？：（）～";

        static string FontAssetPath(string ttf) => $"{FontDir}/{ttf} SDF.asset";
        static string PresetPath(string ttf, int i) => $"{FontDir}/{ttf} {LocFonts.PRESETS[i]}.mat";
        static IEnumerable<string> AllTtfs() => FONT_SETS.SelectMany(s => new[] { s.title, s.body }).Distinct();

        // ===== 1. 폰트 에셋 =====
        [MenuItem("MoreMush/다국어/1 폰트 에셋 만들기")]
        public static string BuildFonts()
        {
            var assets = new Dictionary<string, TMP_FontAsset>();
            foreach (var ttf in AllTtfs()) assets[ttf] = MakeFontAsset(ttf);
            // 대체 폰트: 제목은 한국어 제목 폰트(번역이 빈 칸이면 한글이 굵게 보이게) → 같은 세트의 본문 → 만능 폰트 순
            foreach (var a in assets.Values) a.fallbackFontAssetTable?.Clear();
            foreach (var s in FONT_SETS)
            {
                AddFallback(assets[s.title], assets[FONT_SETS[0].title]);
                AddFallback(assets[s.title], assets[s.body]);
                AddFallback(assets[s.title], assets[UniversalFallback]);
                AddFallback(assets[s.body], assets[UniversalFallback]);
            }
            foreach (var kv in assets)
                for (int i = 0; i < LocFonts.PRESETS.Length; i++) OutlinePreset(kv.Value, PresetPath(kv.Key, i), OUTLINES[i].col, OUTLINES[i].width);

            var lf = AssetDatabase.LoadAssetAtPath<LocFonts>(LocFontsPath);
            if (lf == null) { lf = ScriptableObject.CreateInstance<LocFonts>(); AssetDatabase.CreateAsset(lf, LocFontsPath); }
            lf.sets = FONT_SETS.Select(s => new LocFonts.Set { lang = s.lang, title = Face(s.title, assets), body = Face(s.body, assets) }).ToArray();
            EditorUtility.SetDirty(lf);

            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = assets[FONT_SETS[0].title];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            string missingLangs = string.Join(",", Loc.LANGS.Where(l => FONT_SETS.All(s => s.lang != l)));
            return $"폰트 {assets.Count}개, 세트 {FONT_SETS.Length}개" + (missingLangs.Length > 0 ? $" (폰트 세트 없는 언어: {missingLangs})" : "");
        }

        static LocFonts.Face Face(string ttf, Dictionary<string, TMP_FontAsset> assets)
        {
            var f = new LocFonts.Face { font = assets[ttf], presets = new Material[LocFonts.PRESETS.Length] };
            for (int i = 0; i < f.presets.Length; i++) f.presets[i] = AssetDatabase.LoadAssetAtPath<Material>(PresetPath(ttf, i));
            return f;
        }

        static TMP_FontAsset MakeFontAsset(string ttf)
        {
            string path = FontAssetPath(ttf);
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{ttf}.ttf");
            if (font == null) throw new InvalidOperationException($"{FontDir}/{ttf}.ttf 없음");
            var fa = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(fa, path);
            // 아틀라스 텍스처·머티리얼은 폰트 에셋 안에 둔다
            fa.atlasTextures[0].name = fa.name + " Atlas";
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            return fa;
        }

        static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == fallback) return;
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (font.fallbackFontAssetTable.Contains(fallback)) return;
            font.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(font);
        }

        static void OutlinePreset(TMP_FontAsset font, string path, Color32 col, float width)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor(ShaderUtilities.ID_OutlineColor, col);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);
            EditorUtility.SetDirty(m);
        }

        // ===== 2. 씬·프리팹 글자에 LocText 붙이기 =====
        [MenuItem("MoreMush/다국어/2 씬 글자 연결")]
        public static string AttachAll()
        {
            var lf = AssetDatabase.LoadAssetAtPath<LocFonts>(LocFontsPath);
            if (lf == null) return "LocFonts 없음 — 1 폰트 에셋 만들기를 먼저";
            int n = 0;
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) n += Attach(t, lf) ? 1 : 0;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            int p = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) changed |= Attach(t, lf);
                if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); p++; }
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return $"씬 글자 {n}개, 프리팹 {p}개 연결";
        }

        // 역할은 예전 폰트(Jua=제목, Noto=본문), 프리셋은 머티리얼 이름 끝(Outline Dark 등)으로 정하고, 한국어 폰트 세트로 바꾼다
        static bool Attach(TMP_Text t, LocFonts lf)
        {
            var lt = t.GetComponent<LocText>();
            bool added = lt == null;
            if (added)
            {
                lt = t.gameObject.AddComponent<LocText>();
                string fontName = t.font != null ? t.font.name : "";
                lt.role = OLD_ROLE.TryGetValue(fontName, out var r) ? r : RoleOfNewFont(fontName, lf);
                string mat = t.fontSharedMaterial != null ? t.fontSharedMaterial.name : "";
                lt.preset = Array.FindIndex(LocFonts.PRESETS, pr => mat.EndsWith(pr));
            }
            string key = ContainsHangul(t.text) ? t.text : "";
            bool changed = added || lt.key != key;
            lt.key = key;
            var face = lf.FaceFor("ko", lt.role);
            if (face?.font != null)
            {
                var mat = lt.preset >= 0 && face.presets[lt.preset] != null ? face.presets[lt.preset] : face.font.material;
                if (t.font != face.font || t.fontSharedMaterial != mat) { t.font = face.font; t.fontSharedMaterial = mat; changed = true; }
            }
            if (changed) { EditorUtility.SetDirty(t); EditorUtility.SetDirty(lt); }
            return changed;
        }

        static LocFonts.Role RoleOfNewFont(string fontName, LocFonts lf)
        {
            foreach (var s in lf.sets) if (s.body.font != null && s.body.font.name == fontName) return LocFonts.Role.Body;
            return LocFonts.Role.Title;
        }

        static bool ContainsHangul(string s) => s != null && s.Any(c => c >= '가' && c <= '힣');

        // ===== 3. 번역표 =====
        // 소스의 한국어 리터럴 + 씬·프리팹 LocText 키를 모아 strings.txt를 다시 쓴다. 있던 번역은 그대로 두고, 안 쓰는 줄은 지운다.
        [MenuItem("MoreMush/다국어/3 번역표 갱신")]
        public static string ExportTable()
        {
            var keys = new List<string>();
            var seen = new HashSet<string>();
            void Add(string k) { if (ContainsHangul(k) && seen.Add(k)) keys.Add(k); }
            var interp = new List<string>();
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                foreach (var (lit, isInterp, line) in Literals(File.ReadAllText(path)))
                {
                    if (!ContainsHangul(lit)) continue;
                    if (isInterp) { interp.Add($"{path}:{line}"); continue; }
                    Add(lit);
                }
            foreach (var k in SceneKeys()) Add(k);

            var langs = Loc.LANGS;
            var old = ReadTable();
            var sb = new StringBuilder();
            sb.Append(string.Join("\t", langs)).Append('\n');
            int filled = 0;
            foreach (var k in keys)
            {
                old.TryGetValue(k, out var row);
                sb.Append(Loc.Escape(k));
                for (int i = 1; i < langs.Length; i++)
                {
                    string v = row != null && row.TryGetValue(langs[i], out var x) ? x : "";
                    if (v.Length > 0) filled++;
                    sb.Append('\t').Append(Loc.Escape(v));
                }
                sb.Append('\n');
            }
            Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
            File.WriteAllText(TablePath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(TablePath);
            int removed = old.Keys.Count(k => !seen.Contains(k));
            string msg = $"키 {keys.Count}개 (번역 칸 {filled}/{keys.Count * (langs.Length - 1)}), 지운 줄 {removed}";
            if (interp.Count > 0) msg += $"\n보간 문자열에 한국어 {interp.Count}곳 (Loc.F로 바꿔야 함):\n" + string.Join("\n", interp.Take(40));
            return msg;
        }

        static Dictionary<string, Dictionary<string, string>> ReadTable()
        {
            var map = new Dictionary<string, Dictionary<string, string>>();
            if (!File.Exists(TablePath)) return map;
            string[] header = null;
            foreach (var row in Loc.ParseTsv(File.ReadAllText(TablePath)))
            {
                if (header == null) { header = row; continue; }
                var d = new Dictionary<string, string>();
                for (int i = 1; i < row.Length && i < header.Length; i++) d[header[i]] = row[i];
                map[row[0]] = d;
            }
            return map;
        }

        static IEnumerable<string> SceneKeys()
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var lt in root.GetComponentsInChildren<LocText>(true)) yield return lt.key;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var lt in go.GetComponentsInChildren<LocText>(true)) yield return lt.key;
            }
        }

        // C# 소스의 문자열 리터럴 (주석 제외, Debug.Log 줄 제외). 보간 문자열은 표시만 한다
        static readonly Regex TOKEN = new Regex(@"//[^\r\n]*|/\*[\s\S]*?\*/|(\$@|@\$|\$|@)?""((?:""""|\\.|[^""\\\r\n])*)""|'(?:\\.|[^'\\])'", RegexOptions.Compiled);

        static IEnumerable<(string lit, bool interp, int line)> Literals(string src)
        {
            foreach (Match m in TOKEN.Matches(src))
            {
                if (!m.Groups[2].Success) continue;
                int lineStart = src.LastIndexOf('\n', m.Index) + 1, lineEnd = src.IndexOf('\n', m.Index);
                string line = src.Substring(lineStart, (lineEnd < 0 ? src.Length : lineEnd) - lineStart);
                if (line.Contains("Debug.Log") || line.Contains("Exception(")) continue;
                string prefix = m.Groups[1].Value;
                bool verbatim = prefix.Contains("@"), interp = prefix.Contains("$");
                string body = m.Groups[2].Value;
                string lit = verbatim ? body.Replace("\"\"", "\"") : Unescape(body);
                yield return (lit, interp, interp ? src.Take(m.Index).Count(c => c == '\n') + 1 : 0);   // 줄 번호는 보고할 때만 센다
            }
        }

        static string Unescape(string s) => Regex.Replace(s, @"\\(u[0-9a-fA-F]{4}|.)", m =>
        {
            string e = m.Groups[1].Value;
            switch (e[0])
            {
                case 'n': return "\n";
                case 't': return "\t";
                case 'r': return "\r";
                case '0': return "\0";
                case 'u': return ((char)Convert.ToInt32(e.Substring(1), 16)).ToString();
                default: return e;
            }
        });

        // ===== 4. 폰트 굽기 =====
        // 폰트마다 그 폰트를 쓰는 언어 열의 글자만 굽는다 (WebGL 메모리). 만능 대체 폰트는 한국어 + 언어 이름 + 기호.
        [MenuItem("MoreMush/다국어/4 폰트 굽기")]
        public static string BakeFonts()
        {
            var table = ReadTable();
            var langChars = new Dictionary<string, HashSet<char>>();
            foreach (var l in Loc.LANGS) langChars[l] = new HashSet<char>();
            foreach (var kv in table)
            {
                langChars["ko"].UnionWith(kv.Key);
                foreach (var l in Loc.LANGS.Skip(1))
                    if (kv.Value.TryGetValue(l, out var v)) langChars[l].UnionWith(v);
            }
            var basic = new HashSet<char>(Enumerable.Range(32, 95).Select(i => (char)i));
            basic.UnionWith(SYMBOLS);
            basic.UnionWith(string.Concat(Loc.LANG_NAME.Values));

            var log = new StringBuilder();
            var all = new HashSet<char>(basic);
            foreach (var ttf in AllTtfs())
            {
                var chars = new HashSet<char>(basic);
                foreach (var s in FONT_SETS) if (s.title == ttf || s.body == ttf) chars.UnionWith(langChars[s.lang]);
                if (ttf == UniversalFallback) chars.UnionWith(langChars["ko"]);
                chars.RemoveWhere(c => c < 32 && c != '\n');
                all.UnionWith(chars);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath(ttf));
                if (font == null) throw new InvalidOperationException("1 폰트 에셋 만들기를 먼저 실행하세요: " + ttf);
                Bake(font, new string(chars.OrderBy(c => c).ToArray()));
                log.AppendLine($"{font.name}: {font.characterTable.Count}자, 아틀라스 {font.atlasTextures.Length}장");
            }
            var fonts = AllTtfs().Select(t => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath(t))).ToArray();
            string unsupported = new string(all.Where(c => c >= 32 && !fonts.Any(f => f.HasCharacter(c))).OrderBy(c => c).ToArray());
            if (unsupported.Length > 0) log.AppendLine("어느 폰트에도 없는 글자: " + unsupported);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/font-characters.txt", new string(all.OrderBy(c => c).ToArray()), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static void Bake(TMP_FontAsset font, string chars)
        {
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.ClearFontAssetData();
            font.isMultiAtlasTexturesEnabled = true;
            font.TryAddCharacters(chars, out _);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.isMultiAtlasTexturesEnabled = false;
            // 새로 생긴 아틀라스 장도 폰트 에셋 안에 저장한다
            foreach (var texture in font.atlasTextures)
            {
                if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(texture);
            }
            EditorUtility.SetDirty(font);
        }
    }
}
