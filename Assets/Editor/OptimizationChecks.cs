using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // Regression checks against failure cases; the selected preset is restored.
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

            var device = new Device { type = "acorn", pts = new[] { new Vector2(1, 2), new Vector2(3, 4), new Vector2(7, 8) }, lit = new int[3] };
            var shapes = device.Shapes(); var first = shapes[0];
            device.pts[0] = new Vector2(5, 6);
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

            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/optimization-regression.txt", passed);
            Debug.Log(string.Join("\n", passed));
        }
    }
}
