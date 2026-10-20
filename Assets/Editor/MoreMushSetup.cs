using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace MoreMush.EditorTools
{
    // One-time project setup for generated art and fonts. Menu: MoreMush/...
    public static class MoreMushSetup
    {
        // Art sets, later ones win: Doodle (outlined cel set) > Soft (watercolor) > Generated for the same key.
        // 옛 세트(Generated·Soft·Doodle)는 Resources 밖 ArtArchive에 둔다: Dream에 없는 키만 SpriteDB가 GUID로 물어 빌드에 들어가고, 덮인 그림은 빌드에서 빠진다.
        public static readonly string[] ArtRoots = { "Assets/ArtArchive/Generated", "Assets/ArtArchive/Soft", "Assets/ArtArchive/Doodle", "Assets/Resources/Art/Dream" };

        // key ("Icons/gold") → asset path of the sprite the game uses for it
        public static SortedDictionary<string, string> ResolvedArt()
        {
            var map = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var root in ArtRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    string key = p.Substring(root.Length + 1);
                    map[key.Substring(0, key.Length - Path.GetExtension(key).Length)] = p;
                }
            }
            return map;
        }
        const string DbPath = "Assets/Resources/SpriteDB.asset";

        [MenuItem("MoreMush/Setup All")]
        public static void SetupAll()
        {
            ConfigureSlicing();
            RebuildSpriteDB();
            BuildFontAssets();
            BuildIconSpriteAsset();
            BuildDigitSpriteAsset();
            BuildMaterials();
            Debug.Log("[MoreMush] setup done");
        }

        // ===== 9-slice borders for UI kit pieces and stretchable field patches =====
        [MenuItem("MoreMush/Configure 9-Slice Borders")]
        public static void ConfigureSlicing()
        {
            var rules = new Dictionary<string, System.Func<int, int, Vector4>>
            {
                // Vector4 = (left, bottom, right, top) in pixels
                ["UI/panel_wood"] = (w, h) => Uniform(w, h, 0.28f),
                ["UI/panel_paper"] = (w, h) => Uniform(w, h, 0.2f),
                // 도감 전용 (2026-10-16 ui_dex)
                ["UI/Dex/dex_window"] = (w, h) => Uniform(w, h, 0.16f),
                ["UI/Dex/dex_card"] = (w, h) => Uniform(w, h, 0.24f),
                ["UI/Dex/dex_card_empty"] = (w, h) => Uniform(w, h, 0.24f),
                ["UI/Dex/dex_card_sel"] = (w, h) => Uniform(w, h, 0.24f),
                ["UI/Dex/dex_page"] = (w, h) => Uniform(w, h, 0.14f),
                ["UI/Dex/dex_tab"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/Dex/dex_tab_on"] = (w, h) => Uniform(w, h, 0.4f),
                // 이름표: 장식은 양 끝에만 → 가로로만 늘인다
                ["UI/Dex/tag_name"] = (w, h) => Ends(w, h),
                ["UI/Dex/tag_wood"] = (w, h) => Ends(w, h),
                ["UI/Dex/tag_ribbon"] = (w, h) => Ends(w, h),
                // 세계수 창 3종 (2026-10-20 ui_window, 시안 concept/ui_mock_*)
                ["UI/Window/win_frame"] = (w, h) => Uniform(w, h, 0.22f),
                ["UI/Window/title_plaque"] = (w, h) => Ends(w, h),
                ["UI/Window/header_plaque"] = (w, h) => Ends(w, h),
                ["UI/Window/card_panel"] = (w, h) => Uniform(w, h, 0.14f),
                ["UI/Window/row_panel"] = (w, h) => Uniform(w, h, 0.2f),
                ["UI/Window/pic_frame"] = (w, h) => Uniform(w, h, 0.14f),
                ["UI/Window/mat_slot"] = (w, h) => Uniform(w, h, 0.22f),
                ["UI/Window/btn_leaf"] = (w, h) => new Vector4(h * 0.9f, h * 0.4f, h * 0.9f, h * 0.4f),
                ["UI/Window/btn_cream"] = (w, h) => new Vector4(h / 2, h * 0.4f, h / 2, h * 0.4f),
                ["UI/Window/name_pill"] = (w, h) => new Vector4(h / 2, h * 0.4f, h / 2, h * 0.4f),
                ["UI/Window/done_badge"] = (w, h) => new Vector4(h * 1.1f, h * 0.4f, h / 2, h * 0.4f),
                ["UI/btn_wood"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_go"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_danger"] = (w, h) => Uniform(w, h, 0.42f),
                // doodle 4th-pass kit (beige pastel base + accent buttons/panels)
                ["UI/btn_orange"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_blue"] = (w, h) => Uniform(w, h, 0.42f),
                ["UI/btn_round"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/panel_accent"] = (w, h) => Uniform(w, h, 0.22f),
                ["UI/panel_mint"] = (w, h) => Uniform(w, h, 0.22f),
                ["UI/window_title"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/bar_top"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/rail_item"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/rail_active"] = (w, h) => Uniform(w, h, 0.4f),
                ["UI/chip"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/tooltip"] = (w, h) => Uniform(w, h, 0.25f),
                ["UI/progress_back"] = (w, h) => Uniform(w, h, 0.45f),
                ["UI/progress_fill"] = (w, h) => Uniform(w, h, 0.45f),
                // watercolor log (867×156, PPU 400 = RoundView.BAR_VIS_H 39px): left end with the leaves, right cut end; the middle stretches
                ["UI/bar_log"] = (w, h) => new Vector4(Mathf.Round(w * 0.134f), 0, Mathf.Round(w * 0.067f), 0),
                ["Props/moss"] = (w, h) => Uniform(w, h, 0.3f),
                // dream gauge pills: round ends (half the height) stay round, the middle stretches
                ["UI/bar_fill"] = (w, h) => new Vector4(h / 2, 0, h / 2, 0),
                ["UI/bar_back"] = (w, h) => new Vector4(h / 2, 0, h / 2, 0),
                ["UI/hp_fill"] = (w, h) => new Vector4(h / 2, 0, h / 2, 0),
                ["UI/hp_back"] = (w, h) => new Vector4(h / 2, 0, h / 2, 0),
            };
            var art = ResolvedArt();
            foreach (var kv in rules)
            {
                var imp = art.TryGetValue(kv.Key, out var path) ? AssetImporter.GetAtPath(path) as TextureImporter : null;
                if (imp == null) { Debug.LogWarning("missing " + kv.Key); continue; }
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                imp.spriteBorder = kv.Value(w, h);
                // sliced SpriteRenderers (world-space frames, bubbles) need a full-rect mesh to 9-slice correctly
                var st = new TextureImporterSettings(); imp.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(st);
                imp.SaveAndReimport();
            }
        }

        static Vector4 Ends(int w, int h) { float e = Mathf.Round(Mathf.Min(w * 0.3f, h * 1.1f)), v = Mathf.Round(h * 0.3f); return new Vector4(e, v, e, v); }
        static Vector4 Uniform(int w, int h, float k) { float b = Mathf.Round(Mathf.Min(w, h) * k); return new Vector4(b, b, b, b); }

        // ===== Sprite DB =====
        [MenuItem("MoreMush/Rebuild Sprite DB")]
        public static void RebuildSpriteDB()
        {
            Directory.CreateDirectory("Assets/Resources");
            var db = AssetDatabase.LoadAssetAtPath<SpriteDB>(DbPath);
            if (db == null) { db = ScriptableObject.CreateInstance<SpriteDB>(); AssetDatabase.CreateAsset(db, DbPath); }
            db.entries.Clear();
            foreach (var kv in ResolvedArt())
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(kv.Value);
                if (sp == null || kv.Value.EndsWith("/grandpa_ref.png")) continue;
                db.entries.Add(new SpriteDB.Entry { key = kv.Key, sprite = sp });
            }
            db.entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoreMush] SpriteDB: {db.entries.Count} sprites");
        }

        // ===== Fonts: 언어별 제목·본문 폰트 세트 (LocTools) =====
        [MenuItem("MoreMush/Build Font Assets")]
        public static void BuildFontAssets()
        {
            LocTools.BuildFonts();
            LocTools.BakeFonts();
        }

        // ===== Inline icons for TMP text: <sprite name="gold"> =====
        [MenuItem("MoreMush/Build Icon Sprite Asset")]
        public static void BuildIconSpriteAsset()
        {
            const int cell = 128;
            var iconDirs = new[] { "Icons/", "Farm/Icons/", "Farm/Field/", "Farm/Tree/" };
            var paths = ResolvedArt().Where(kv => iconDirs.Any(d => kv.Key.StartsWith(d))).Select(kv => kv.Value).ToList();
            int cols = 8, rows = Mathf.CeilToInt(paths.Count / (float)cols);
            var atlas = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            var names = new List<string>();
            for (int i = 0; i < paths.Count; i++)
            {
                var src = LoadReadable(paths[i]);
                var scaled = Fit(src, cell - 8);
                int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
                atlas.SetPixels(cx + (cell - scaled.width) / 2, cy + (cell - scaled.height) / 2, scaled.width, scaled.height, scaled.GetPixels());
                names.Add(Path.GetFileNameWithoutExtension(paths[i]));
            }
            atlas.Apply();
            string atlasPath = "Assets/Fonts/IconAtlas.png";
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(atlasPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.maxTextureSize = 4096;   // 아틀라스가 2048보다 크면 줄어들어 아이콘 좌표(원래 픽셀)가 어긋난다 (오른쪽 열 골드가 빈칸으로 나오던 것)
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            string assetPath = "Assets/Resources/Sprite Assets/Icons.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (sa == null) { sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>(); AssetDatabase.CreateAsset(sa, assetPath); }
            // a fresh asset has no version, which makes TMP run its legacy upgrade and wipe the tables
            typeof(TMP_SpriteAsset).GetField("m_Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sa, "1.1.0");
            sa.spriteSheet = tex;
            sa.spriteGlyphTable.Clear(); sa.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
                var glyph = new TMP_SpriteGlyph { index = (uint)i, metrics = new GlyphMetrics(cell, cell, 0, cell * 0.8f, cell), glyphRect = new GlyphRect(cx, cy, cell, cell), scale = 1, atlasIndex = 0 };
                sa.spriteGlyphTable.Add(glyph);
                sa.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i] });
            }
            var shader = Shader.Find("TextMeshPro/Sprite");
            if (sa.material == null) { sa.material = new Material(shader) { name = "Icons Material" }; AssetDatabase.AddObjectToAsset(sa.material, sa); }
            sa.material.SetTexture(ShaderUtilities.ID_MainTex, tex);
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = sa;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoreMush] icon sprite asset: {names.Count} icons");
        }

        // ===== Damage number glyphs (sheet "digits" → UI/Digits/*): <sprite="DmgDigits" name="d7" tint=1> =====
        // The slicer crops every glyph tight, so the baseline is put back here: digits and letters stand on it at one
        // common height (median digit height), + and x sit centred on the digit middle, . and , sit low.
        [MenuItem("MoreMush/Build Damage Digit Sprite Asset")]
        public static void BuildDigitSpriteAsset()
        {
            const float H = 128;   // digit height in the atlas
            var src = ResolvedArt().Where(kv => kv.Key.StartsWith("UI/Digits/")).ToDictionary(kv => Path.GetFileName(kv.Key), kv => kv.Value);
            if (src.Count == 0) { Debug.LogWarning("[MoreMush] no UI/Digits art"); return; }
            var texs = src.ToDictionary(kv => kv.Key, kv => Bleach(LoadReadable(kv.Value)));
            var digitH = texs.Where(kv => kv.Key.Length == 2 && kv.Key[0] == 'd').Select(kv => kv.Value.height).OrderBy(h => h).ToList();
            float k = H / (digitH.Count > 0 ? digitH[digitH.Count / 2] : texs.Values.Max(t => t.height));
            var names = texs.Keys.OrderBy(n => n, System.StringComparer.Ordinal).ToList();
            var scaled = names.ToDictionary(n => n, n => Fit(texs[n], Mathf.RoundToInt(Mathf.Max(texs[n].width, texs[n].height) * k)));
            // everything stands on the baseline at its painted height (lowercase keeps its smaller x-height),
            // except + (centred on the digit middle), the comma and p (hang below it)
            PackSpriteAsset("DmgDigits", names, scaled, H, (n, h) => n switch
            {
                "plus" => H * 0.5f + h * 0.5f,
                "comma" => h * 0.55f,
                "lp" => h * 0.72f,
                _ => h,
            });
        }

        // Combo counter skin (sheet "combo_skin" → UI/Combo/*): <sprite="ComboDigits" name="n7">, not tinted.
        // n* = everyday gold set, c* = critical set; each set is scaled so its median digit is H tall (the crit
        // digits carry sparkles and a wider rim, so they are measured on their own), the COMBO words go with their
        // set's digits, critical (the word over critical damage numbers) goes with the crit set at its painted size.
        // super_combo_* are shown as Images, not glyphs.
        [MenuItem("MoreMush/Build Combo Digit Sprite Asset")]
        public static void BuildComboSpriteAsset()
        {
            const float H = 128;
            var src = ResolvedArt().Where(kv => kv.Key.StartsWith("UI/Combo/")).ToDictionary(kv => Path.GetFileName(kv.Key), kv => kv.Value)
                .Where(kv => (kv.Key.Length == 2 && (kv.Key[0] == 'n' || kv.Key[0] == 'c')) || kv.Key == "combo_n" || kv.Key == "combo_c" || kv.Key == "critical")
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            if (src.Count == 0) { Debug.LogWarning("[MoreMush] no UI/Combo art"); return; }
            var texs = src.ToDictionary(kv => kv.Key, kv => LoadReadable(kv.Value));
            float K(char set)
            {
                var hs = texs.Where(kv => kv.Key.Length == 2 && kv.Key[0] == set).Select(kv => kv.Value.height).OrderBy(h => h).ToList();
                return H / (hs.Count > 0 ? hs[hs.Count / 2] : H);
            }
            float kn = K('n'), kc = K('c');
            var names = texs.Keys.OrderBy(n => n, System.StringComparer.Ordinal).ToList();
            // the painted COMBO word is about half a digit tall; a little bigger reads better next to the number
            float Word(string n) => n.StartsWith("combo") ? 1.3f : 1;
            var scaled = names.ToDictionary(n => n, n => Fit(texs[n], Mathf.RoundToInt(Mathf.Max(texs[n].width, texs[n].height) * (n[0] == 'n' || n == "combo_n" ? kn : kc) * Word(n))));
            PackSpriteAsset("ComboDigits", names, scaled, H, (n, h) => h);
        }

        // Packs glyph textures into Assets/Fonts/<name>Atlas.png and the TMP sprite asset Resources/Sprite Assets/<name>.
        // top(name, height) = how far above the baseline the glyph's top edge sits.
        static void PackSpriteAsset(string name, List<string> names, Dictionary<string, Texture2D> scaled, float H, System.Func<string, float, float> top)
        {
            const int pad = 4;
            int cell = scaled.Values.Max(t => Mathf.Max(t.width, t.height)) + pad * 2;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(names.Count)), rows = Mathf.CeilToInt(names.Count / (float)cols);
            var atlas = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            var rects = new List<GlyphRect>();
            for (int i = 0; i < names.Count; i++)
            {
                var t = scaled[names[i]];
                int x = (i % cols) * cell + pad, y = (rows - 1 - i / cols) * cell + pad;
                atlas.SetPixels(x, y, t.width, t.height, t.GetPixels());
                rects.Add(new GlyphRect(x, y, t.width, t.height));
            }
            atlas.Apply();
            string atlasPath = $"Assets/Fonts/{name}Atlas.png";
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(atlasPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
            imp.maxTextureSize = 4096;   // 아틀라스가 2048보다 크면 줄어들어 아이콘 좌표(원래 픽셀)가 어긋난다 (오른쪽 열 골드가 빈칸으로 나오던 것)
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            string assetPath = $"Assets/Resources/Sprite Assets/{name}.asset";
            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (sa == null) { sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>(); AssetDatabase.CreateAsset(sa, assetPath); }
            typeof(TMP_SpriteAsset).GetField("m_Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sa, "1.1.0");
            sa.spriteSheet = tex;
            // without a face (pointSize 0) TMP scales each sprite to the font ascent on its own, which blew the dot up to
            // digit size; with one, every glyph shares one scale. Digits come out at 1 em, bigger than font digits (they carry their own rim).
            var face = new UnityEngine.TextCore.FaceInfo { pointSize = Mathf.RoundToInt(H), scale = 1, ascentLine = H, capLine = H, meanLine = H * 0.5f, baseline = 0, descentLine = -H * 0.2f, lineHeight = H * 1.2f };
            typeof(TMP_SpriteAsset).GetField("m_FaceInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sa, face);
            sa.spriteGlyphTable.Clear(); sa.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                var r = rects[i];
                float w = r.width, h = r.height;
                // italic glyphs tuck into each other a little; the thick rims overlap instead of leaving gaps
                var glyph = new TMP_SpriteGlyph { index = (uint)i, metrics = new GlyphMetrics(w, h, 0, top(names[i], h), w * 0.86f), glyphRect = r, scale = 1, atlasIndex = 0 };
                sa.spriteGlyphTable.Add(glyph);
                sa.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i] });
            }
            var shader = Shader.Find("TextMeshPro/Sprite");
            if (sa.material == null) { sa.material = new Material(shader) { name = name + " Material" }; AssetDatabase.AddObjectToAsset(sa.material, sa); }
            sa.material.SetTexture(ShaderUtilities.ID_MainTex, tex);
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MoreMush] {name} sprite asset: {names.Count} glyphs ({string.Join(" ", names)})");
        }

        // the painted fill can come out peach; tint=1 multiplies it, so blue/violet tiers turned muddy. Light pixels go to
        // a barely warm paper grey of the same lightness (grain kept, matches the field), the brown rim stays as painted.
        static Texture2D Bleach(Texture2D t)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 16) { px[i] = default; continue; }   // near-invisible coloured speckle the generator leaves in the background
                float l = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.35f, 0.6f, l));
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(1, l * 1.12f) * 255), 0, 255);
                px[i] = new Color32((byte)Mathf.Lerp(c.r, g, k), (byte)Mathf.Lerp(c.g, g * 0.985f, k), (byte)Mathf.Lerp(c.b, g * 0.95f, k), c.a);
            }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static Texture2D LoadReadable(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        static Texture2D Fit(Texture2D src, int max)
        {
            float k = Mathf.Min(1f, (float)max / Mathf.Max(src.width, src.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(src.width * k)), h = Mathf.Max(1, Mathf.RoundToInt(src.height * k));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
            o.ReadPixels(new Rect(0, 0, w, h), 0, 0); o.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return o;
        }

        // ===== Materials referenced by scene objects (keeps shaders in builds) =====
        [MenuItem("MoreMush/Build Materials")]
        public static void BuildMaterials()
        {
            Directory.CreateDirectory("Assets/Materials");
            Mat("Assets/Materials/Sprite.mat", "MoreMush/Sprite", null);
            Mat("Assets/Materials/SpriteAdditive.mat", "MoreMush/Sprite", m => m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One));
            Mat("Assets/Materials/WeatherMask.mat", "MoreMush/WeatherMask", null);
            Mat("Assets/Materials/UIFx.mat", "MoreMush/UISprite", null);
            // TMP 외곽선 프리셋은 폰트마다 LocTools.BuildFonts가 만든다
            AssetDatabase.SaveAssets();
        }

        static void Mat(string path, string shader, System.Action<Material> setup)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
        }
    }
}
