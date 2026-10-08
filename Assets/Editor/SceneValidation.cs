using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MoreMush.EditorTools
{
    // The same contracts are checked from the menu and before each scene enters a player build.
    public sealed class SceneValidation : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<GameFlow>(true))) return;
            var errors = Validate(scene);
            if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
        }

        [MenuItem("MoreMush/Validate Scene")]
        public static void Run()
        {
            var errors = Validate(SceneManager.GetActiveScene());
            foreach (var error in errors) Debug.LogError("[MoreMush validation] " + error);
            Debug.Log($"[MoreMush validation] {errors.Count} error(s)");
        }

        public static void Batch()
        {
            int code = 1;
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                var errors = Validate(scene);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/scene-validation.txt", errors.Count == 0 ? "PASS: scene contracts\n" : string.Join("\n", errors));
                code = errors.Count == 0 ? 0 : 1;
            }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        static string PathOf(Component c)
        {
            string path = c.name;
            for (var t = c.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return path + " (" + c.GetType().Name + ")";
        }

        public static List<string> Validate(Scene scene)
        {
            var errors = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded) { errors.Add("No loaded scene."); return errors; }
            var roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var ours = all.Where(m => m != null && m.GetType().Namespace == "MoreMush").ToArray();
            var actions = ActionNames();

            foreach (var root in roots)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) errors.Add(PathOf(t) + ": missing script");

            foreach (var mb in ours)
            {
                CheckReferences(mb, errors);
                void Slots(string field, Array slots, int needed)
                {
                    if (slots == null || slots.Length < needed) errors.Add($"{PathOf(mb)}.{field}: needs {needed} slots, has {slots?.Length ?? 0}");
                }
                switch (mb)
                {
                    case PreRoundPanel p:
                        Slots(nameof(p.themeCards), p.themeCards, Defs.THEMES.Count); Slots(nameof(p.themeIcons), p.themeIcons, Defs.THEMES.Count);
                        Slots(nameof(p.themeNames), p.themeNames, Defs.THEMES.Count); Slots(nameof(p.themeDescs), p.themeDescs, Defs.THEMES.Count); Slots(nameof(p.themeCounts), p.themeCounts, Defs.THEMES.Count);
                        for (int i = 0; i < Math.Min(p.themeCards.Length, Defs.THEMES.Count); i++)
                            CheckAction(p.themeCards[i], "pretheme", Defs.THEMES[i].id, errors);
                        break;
                    case BuildPanel p: Slots(nameof(p.cards), p.cards, Defs.BUILDINGS.Length); break;
                    case FarmScreen p: Slots(nameof(p.chipList), p.chipList, Defs.SPECIALS.Length); break;
                    case PetCardPanel p: Slots(nameof(p.hearts), p.hearts, Defs.FARM.hearts); Slots(nameof(p.heartFills), p.heartFills, Defs.FARM.hearts); break;
                    case SkinPanel p: Slots(nameof(p.cards), p.cards, Math.Max(Defs.CHAR_SKINS.Length, Defs.HV_SKINS.Length)); break;
                    case FarmTreeView p: Slots(nameof(p.fruits), p.fruits, Defs.TREE.Slots(Defs.TREE.maxLv)); Slots(nameof(p.glows), p.glows, Defs.TREE.Slots(Defs.TREE.maxLv)); break;
                    case TreeNodeView p:
                        if (!Defs.NODE.TryGetValue(p.nodeId ?? "", out var node)) errors.Add(PathOf(p) + ": unknown node " + p.nodeId);
                        else if (node.max > 1) Slots(nameof(p.levelArcs), p.levelArcs, node.max);
                        break;
                    case HvCard p:
                        if (!Defs.HV.ContainsKey(p.id ?? "")) errors.Add(PathOf(p) + ": unknown harvester " + p.id);
                        CheckAction(p.unlock, "hvunlock", p.id, errors); CheckAction(p.toggle, "hvtoggle", p.id, errors); CheckAction(p.level, "hvlevel", p.id, errors);
                        break;
                    case UIAction p:
                        if (!actions.Contains(p.act ?? "")) errors.Add(PathOf(p) + ": unknown action '" + p.act + "'");
                        if (p.GetComponent<Button>() == null) errors.Add(PathOf(p) + ": no Button");
                        break;
                    case CodexCell p:
                        if (p.id == null || (p.id.StartsWith("sp:") ? !Defs.SPC.ContainsKey(p.id.Substring(3)) : !Defs.SP.ContainsKey(p.id))) errors.Add(PathOf(p) + ": unknown codex id " + p.id);
                        break;
                }
            }

            CheckIds(ours.OfType<TreeNodeView>().Select(n => n.nodeId), Defs.NODES.Select(n => n.id), "tree nodes", errors);
            CheckIds(ours.OfType<HvCard>().Select(n => n.id), Defs.HARVESTERS.Select(n => n.id), "harvester cards", errors);
            CheckIds(ours.OfType<CodexCell>().Select(n => n.id), Defs.SPECIES.Select(n => n.id).Concat(Defs.SPECIALS.Select(n => "sp:" + n.id)), "codex cells", errors);
            if (!ours.OfType<UIAction>().Any(a => a.act == "speed")) errors.Add("Round HUD: missing speed UIAction");
            return errors;
        }

        static void CheckIds(IEnumerable<string> actual, IEnumerable<string> expected, string name, List<string> errors)
        {
            var ids = actual.ToArray();
            foreach (var id in expected.Except(ids)) errors.Add(name + ": missing " + id);
            foreach (var group in ids.GroupBy(x => x).Where(g => g.Count() > 1)) errors.Add(name + ": duplicate " + group.Key);
        }

        static HashSet<string> ActionNames()
        {
            var names = new HashSet<string> { "none" };
            foreach (var file in new[] { "Assets/Scripts/UI/GameFlow.cs", "Assets/Scripts/UI/DebugPanel.cs" })
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "case\\s+\"([^\"]+)\"\\s*:")) names.Add(m.Groups[1].Value);
            return names;
        }

        static void CheckAction(Component button, string act, string arg, List<string> errors)
        {
            if (button == null) return; // reference check reports this
            var ua = button.GetComponent<UIAction>();
            if (ua == null || ua.act != act || ua.arg != arg) errors.Add($"{PathOf(button)}: expected action {act} / {arg}");
        }

        static void CheckReferences(MonoBehaviour mb, List<string> errors)
        {
            foreach (var field in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized || (!field.IsPublic && !field.IsDefined(typeof(SerializeField)))) continue;
                var value = field.GetValue(mb);
                if (typeof(Object).IsAssignableFrom(field.FieldType)) Check(field.Name, value as Object);
                else if (field.FieldType.IsArray && typeof(Object).IsAssignableFrom(field.FieldType.GetElementType()))
                {
                    if (!(value is IList list)) { errors.Add(PathOf(mb) + "." + field.Name + ": null array"); continue; }
                    for (int i = 0; i < list.Count; i++) Check(field.Name + "[" + i + "]", list[i] as Object);
                }

                void Check(string name, Object reference)
                {
                    if (reference == null) { errors.Add(PathOf(mb) + "." + name + ": missing reference"); return; }
                    if (!(reference is Component c) || !c.gameObject.scene.IsValid()) return;
                    if ((field.Name.EndsWith("Template", StringComparison.OrdinalIgnoreCase) || field.Name.EndsWith("Prefab", StringComparison.OrdinalIgnoreCase)) && c.gameObject.activeSelf)
                        errors.Add(PathOf(mb) + "." + name + ": scene template must be inactive");
                }
            }
        }
    }
}
