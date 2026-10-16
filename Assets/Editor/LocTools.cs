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
        const string FontDir = "Assets/Fonts", LocFontsPath = "Assets/Resources/Loc/LocFonts.asset", NewStringsPath = "BalanceData/db/new_strings.json";
        // 48/6 (2026-10-16, 64/8에서 내림): 한·일 1000자가 2048 한 장에 들어간다 (WebGL 용량). 여백 비율 12.5%는 그대로라 외곽선 굵기도 같다
        const int SamplingSize = 48, Padding = 6, AtlasSize = 2048;

        // 언어, 제목 TTF, 본문 TTF (확장자 없는 파일 이름)
        static readonly (string lang, string title, string body)[] FONT_SETS =
        {
            // 2026-10-15 5차 몽환 수채화(그림책) 세트 (모두 OFL): 부드러운 세리프 제목 + 둥근 본문
            ("ko", "GowunBatang-Bold", "GowunDodum-Regular"),
            ("en", "Lora-Bold", "Nunito-SemiBold"),
            ("ja", "KiwiMaru-Medium", "ZenMaruGothic-Medium"),
            ("ru", "Lora-Bold", "Nunito-SemiBold"),     // 키릴 문자 있음
            ("uk", "Lora-Bold", "Nunito-SemiBold"),     // 키릴 + ІЇЄҐ 모두 있음
        };
        // 어느 폰트에도 없는 글자가 나오면 이 폰트로 넘긴다 (한글·가나·한자·기호를 모두 가진 폰트)
        const string UniversalFallback = "ONEMobileRegular";
        // 외곽선 프리셋 (LocFonts.PRESETS와 같은 순서)
        // 몽환 수채화(2026-10-15): 스티커처럼 두껍고 검은 테두리 대신 얇고 부드러운 먹색 테두리 (글자가 배경에서 떠 보일 만큼만)
        static readonly (Color32 col, float width)[] OUTLINES =
        {
            (new Color32(0x3a, 0x2c, 0x24, 230), 0.18f),
            (new Color32(0x4a, 0x35, 0x26, 220), 0.15f),
            (new Color32(0x5e, 0x3a, 0x22, 230), 0.2f),
        };
        const float OUTLINE_SOFT = 0.12f;   // 번진 물감처럼 테두리 끝을 흐리게
        // 예전 폰트 → 역할 (씬 글자 연결 때 역할을 정하는 데만 쓴다)
        static readonly Dictionary<string, LocFonts.Role> OLD_ROLE = new Dictionary<string, LocFonts.Role>
        {
            ["Jua SDF"] = LocFonts.Role.Title, ["NotoSansKR SDF"] = LocFonts.Role.Body,
        };
        const string SYMBOLS = "★☆♥♡●○▲▼▶◀→←↑↓×÷±−–—…·≥≤∞①②③④⑤⑥⑦⑧⑨⑩巨°「」『』、。・！？：（）～";

        static string FontAssetPath(string ttf) => $"{FontDir}/{ttf} SDF.asset";
        static string PresetPath(string ttf, int i) => $"{FontDir}/{ttf} {LocFonts.PRESETS[i]}.mat";
        static IEnumerable<string> AllTtfs() => FONT_SETS.SelectMany(s => new[] { s.title, s.body }).Append(UniversalFallback).Distinct();

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
            m.SetFloat(ShaderUtilities.ID_OutlineSoftness, OUTLINE_SOFT);
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
            string key = ContainsHangul(t.text) ? t.text : (!string.IsNullOrEmpty(lt.authored) && lt.authored == t.text ? lt.key : "");
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

        // ===== 2-1. 씬·프리팹 글자를 영어로 =====
        // 에디터에서 보이는 글자를 DB의 영어(Str_EN)로 적는다. key(한국어 원문)는 그대로 둬서 실행 중 언어 전환은 그대로 된다.
        [MenuItem("MoreMush/다국어/2-1 씬 글자 영어로")]
        public static string WriteEnglishAll()
        {
            Db.Reload();
            var en = Db.LangTable("en");
            int n = 0;
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects()) n += WriteEnglish(root, en);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            int p = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int c = WriteEnglish(root, en);
                if (c > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); p++; n += c; }
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return $"글자 {n}개를 영어로 바꿈 (프리팹 {p}개)";
        }

        static int WriteEnglish(GameObject root, Dictionary<string, string> en)
        {
            int n = 0;
            foreach (var lt in root.GetComponentsInChildren<LocText>(true))
            {
                if (string.IsNullOrEmpty(lt.key) || !en.TryGetValue(lt.key, out var english) || string.IsNullOrEmpty(english)) continue;
                var t = lt.GetComponent<TMP_Text>();
                if (t.text != lt.key && t.text != lt.authored) continue;   // 코드가 채우는 글자는 건드리지 않는다
                if (t.text == english && lt.authored == english) continue;
                t.text = english; lt.authored = english;
                EditorUtility.SetDirty(t); EditorUtility.SetDirty(lt);
                n++;
            }
            return n;
        }

        static LocFonts.Role RoleOfNewFont(string fontName, LocFonts lf)
        {
            foreach (var s in lf.sets) if (s.body.font != null && s.body.font.name == fontName) return LocFonts.Role.Body;
            return LocFonts.Role.Title;
        }

        static bool ContainsHangul(string s) => s != null && s.Any(c => c >= '가' && c <= '힣');

        // ===== 3. 번역표 =====
        // 번역표 = 엑셀 DB의 String 시트. 소스의 한국어 리터럴 + 씬·프리팹 LocText 키 + DB 데이터 이름을 모아,
        // String 시트에 없는 문구를 맨 아래에 추가한다(BalanceData/db/db_write.py, Excel COM). 안 쓰는 줄은 지우지 않고 알려만 준다(번역 보존).
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
            Db.Reload();
            var db = Db.Data;
            foreach (var u in db.Upgrade) Add(u.Upg_Name);
            foreach (var r in db.Skill) Add(r.Skill_Name);
            foreach (var r in db.Ball) { Add(r.Ball_Name); Add(r.Ball_Type); Add(r.Ball_Look); }
            foreach (var r in db.Weader) Add(r.Wea_Name);
            foreach (var r in db.Boss) Add(r.Boss_Name);
            foreach (var r in db.String) if (r.Str_Name != "ui") Add(r.Str_KR);   // 데이터 설명 문구 (upg:·wea:·boss: …)

            var have = new HashSet<string>(db.String.Select(r => r.Str_KR));
            var missing = keys.Where(k => !have.Contains(k)).ToList();
            int unused = db.String.Count(r => r.Str_Name == "ui" && !seen.Contains(r.Str_KR));
            int filled = db.String.Sum(r => new[] { r.Str_EN, r.Str_JA, r.Str_RU, r.Str_UK }.Count(t => !string.IsNullOrEmpty(t)));
            string msg = $"문구 {keys.Count}개 · DB String {db.String.Count}줄 (번역 칸 {filled}/{db.String.Count * (Loc.LANGS.Length - 1)}) · 안 쓰는 UI 줄 {unused}";
            string xlsx = DbImport.LatestDb();
            if (missing.Count > 0 && xlsx == null) msg += $"\n새 문구 {missing.Count}개가 있지만 Assets/DB에 엑셀 DB가 없어요";
            else if (missing.Count > 0)
            {
                File.WriteAllText(NewStringsPath, Newtonsoft.Json.JsonConvert.SerializeObject(missing, Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));
                var (ok, output) = RunPython("BalanceData/db/db_write.py", "strings", xlsx, NewStringsPath);
                msg += ok ? $"\n새 문구 {missing.Count}개를 {Path.GetFileName(xlsx)} String 시트에 추가: {output}\n" + DbImport.Import()
                          : $"\n새 문구 {missing.Count}개를 DB에 넣지 못했어요 (엑셀이 열려 있으면 닫고 다시): {output}\n목록: {NewStringsPath}";
            }
            if (interp.Count > 0) msg += $"\n보간 문자열에 한국어 {interp.Count}곳 (Loc.F로 바꿔야 함):\n" + string.Join("\n", interp.Take(40));
            return msg;
        }

        // python 스크립트 실행 (엑셀 COM 쓰기). 끝날 때까지 기다린다
        static (bool ok, string output) RunPython(string script, params string[] args)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python", string.Join(" ", new[] { script }.Concat(args).Select(a => $"\"{a}\"")))
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            const int TimeoutMs = 180000;
            try
            {
                EditorUtility.DisplayProgressBar("엑셀 DB", "엑셀(COM)에 쓰는 중… (최대 3분)", 0.5f);
                using var p = System.Diagnostics.Process.Start(psi);
                // 출력은 비동기로 받고 종료를 먼저 기다린다 (엑셀이 멈춰도 타임아웃이 걸리게)
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(TimeoutMs))
                {
                    try { p.Kill(); } catch (InvalidOperationException) { }
                    return (false, "3분 안에 끝나지 않았어요. 작업 관리자에서 남은 EXCEL.EXE를 닫은 뒤 다시 하세요");
                }
                p.WaitForExit();   // 출력 스트림까지 다 닫히게
                return (p.ExitCode == 0, (outTask.Result + errTask.Result).Trim());
            }
            catch (Exception e) { return (false, "python을 실행하지 못했어요 (Windows + Excel + pywin32 필요): " + e.Message); }
            finally { EditorUtility.ClearProgressBar(); }
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
        // 폰트마다 그 폰트를 쓰는 언어(DB String 시트의 Str_KR·Str_EN·Str_JA 열) 글자만 굽는다 (WebGL 메모리). 만능 대체 폰트는 한국어 + 언어 이름 + 기호.
        [MenuItem("MoreMush/다국어/4 폰트 굽기")]
        public static string BakeFonts()
        {
            Db.Reload();
            var langChars = new Dictionary<string, HashSet<char>>();
            foreach (var l in Loc.LANGS) langChars[l] = new HashSet<char>();
            foreach (var r in Db.Data.String)
            {
                langChars["ko"].UnionWith(r.Str_KR ?? "");
                langChars["en"].UnionWith(r.Str_EN ?? "");
                langChars["ja"].UnionWith(r.Str_JA ?? "");
                langChars["ru"].UnionWith(r.Str_RU ?? "");
                langChars["uk"].UnionWith(r.Str_UK ?? "");
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
