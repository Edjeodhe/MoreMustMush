using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.EditorTools
{
    // Regression checks against failure cases; scene fields and the selected preset are restored.
    public static class OptimizationChecks
    {
        [MenuItem("MoreMush/Validate Optimization Regressions")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            var passed = new List<string>();
            void Check(bool ok, string name)
            {
                if (!ok) throw new InvalidOperationException(name);
                passed.Add("PASS: " + name);
            }

            Check(ShopPanel.ClampQty(16777217, 20000000) == 16777217, "quantities above float integer precision");
            Check(ShopPanel.ClampQty(20000000, 16777217) == 16777217, "quantity clamped without float rounding");
            Check(ShopPanel.ClampQty(double.NaN, 100) == 0 && ShopPanel.ClampQty(-1, 100) == 0,
                "invalid and negative quantities");
            var device = new Device { type = "mole", a = new Vector2(1, 2), b = new Vector2(3, 4) };
            var shapes = device.Shapes(); var first = shapes[0];
            device.a = new Vector2(5, 6);
            Check(ReferenceEquals(shapes, device.Shapes()) && ReferenceEquals(first, device.Shapes()[0]) && first.x == 5,
                "cached collision shapes follow geometry changes");
            device.type = "stump"; device.x = 12; device.r = 24;
            Check(device.Shapes().Count == 1 && device.Shapes()[0].r == 24, "cached shapes follow device type changes");

            string preset = Defs.TUNE.id;
            string key = (string)typeof(Defs).GetField("PresetKey", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
            bool had = PlayerPrefs.HasKey(key);
            string saved = PlayerPrefs.GetString(key, "");
            try
            {
                Defs.ApplyPreset(preset == "tuned" ? "spec" : "tuned", false);
                Check(had == PlayerPrefs.HasKey(key) && saved == PlayerPrefs.GetString(key, ""),
                    "editor preset selection leaves PlayerPrefs unchanged");
            }
            finally { Defs.ApplyPreset(preset, false); }

            var scene = SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var before = SceneValidation.Validate(scene);
            Check(before.Count == 0, "scene contracts: " + string.Join("; ", before));
            var speed = all.OfType<UIAction>().Single(a => a.act == "speed");
            try
            {
                speed.act = "__unknown_regression_action";
                Check(SceneValidation.Validate(scene).Any(e => e.Contains("unknown action")), "validator detects unknown actions");
            }
            finally { speed.act = "speed"; }
            var pre = all.OfType<PreRoundPanel>().Single();
            var cards = pre.themeCards;
            try
            {
                pre.themeCards = null;
                Check(SceneValidation.Validate(scene).Any(e => e.Contains("themeCards") && e.Contains("needs")),
                    "validator reports missing slots without throwing");
            }
            finally { pre.themeCards = cards; }
            var farm = all.OfType<FarmScreen>().Single();
            var title = farm.title;
            try
            {
                farm.title = null;
                AutoWire.Fill(farm);
                Check(farm.title == null, "AutoWire refuses ambiguous title matches");
            }
            finally { farm.title = title; }
            Check(SceneValidation.Validate(scene).Count == 0, "scene restored after negative checks");
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/optimization-regression.txt", passed);
            Debug.Log(string.Join("\n", passed));
        }
    }
}
