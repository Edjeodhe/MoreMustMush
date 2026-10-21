using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MoreMush.EditorTools
{
    // 출시 빌드(WebGL itch.io용 · Windows exe). 결과는 문자열로 반환.
    // 메뉴 MoreMush/Build/* · 배치 -executeMethod MoreMush.EditorTools.ReleaseBuild.BatchAll (결과 Builds/build_report.txt)
    public static class ReleaseBuild
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Out(string rel) => Path.Combine(Root, rel);

        [MenuItem("MoreMush/Build/WebGL (itch.io)")]
        public static void MenuWebGL() => Log(WebGL());
        // 개발자 패널(` 키)·치트 키(Shift+숫자)가 들어간 테스트용. 출시 메뉴는 Build Settings의 Development Build 체크를 따르지 않는다 (늘 출시 빌드)
        [MenuItem("MoreMush/Build/WebGL (개발용)")]
        public static void MenuWebGLDev() => Log(WebGL(true));
        [MenuItem("MoreMush/Build/Windows (exe)")]
        public static void MenuWindows() => Log(Windows());
        // 개발자 패널·치트 키(Shift+숫자)가 들어간 테스트용 exe (Development Build). 결과는 Builds/Windows_Dev
        [MenuItem("MoreMush/Build/Windows (개발용)")]
        public static void MenuWindowsDev() => Log(Windows(true));
        [MenuItem("MoreMush/Build/All")]
        public static void MenuAll() => Log(All());

        static void Log(string s)
        {
            if (s.Contains("결과: 실패")) Debug.LogError(s); else Debug.Log(s);
        }

        public static void BatchAll()
        {
            int code = 0; string text;
            try { text = All(); if (text.Contains("결과: 실패")) code = 1; }
            catch (Exception e) { text = "실패: " + e; code = 1; }
            Directory.CreateDirectory(Out("Builds"));
            File.WriteAllText(Out("Builds/build_report.txt"), text);
            EditorApplication.Exit(code);
        }

        // 배치 -executeMethod MoreMush.EditorTools.ReleaseBuild.BatchWebGL : WebGL만 빌드 (결과 Builds/build_report.txt)
        public static void BatchWebGL()
        {
            int code = 0; string text;
            var origTarget = EditorUserBuildSettings.activeBuildTarget;
            var origGroup = BuildPipeline.GetBuildTargetGroup(origTarget);
            try { text = WebGL(); if (text.Contains("결과: 실패")) code = 1; }
            catch (Exception e) { text = "실패: " + e; code = 1; }
            finally { Restore(origTarget, origGroup); }
            Directory.CreateDirectory(Out("Builds"));
            File.WriteAllText(Out("Builds/build_report.txt"), text);
            EditorApplication.Exit(code);
        }

        // WebGL → Windows 순서로 빌드하고 원래 활성 타깃으로 복귀
        public static string All()
        {
            var origTarget = EditorUserBuildSettings.activeBuildTarget;
            var origGroup = BuildPipeline.GetBuildTargetGroup(origTarget);
            try { return WebGL() + "\n\n" + Windows(); }
            finally { Restore(origTarget, origGroup); }
        }

        static string[] Scenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        static void Restore(BuildTarget t, BuildTargetGroup g)
        {
            if (EditorUserBuildSettings.activeBuildTarget != t) EditorUserBuildSettings.SwitchActiveBuildTarget(g, t);
        }

        public static string WebGL(bool dev = false)
        {
            var sb = new StringBuilder("[WebGL 빌드]\n");
            var origTarget = EditorUserBuildSettings.activeBuildTarget;
            var origGroup = BuildPipeline.GetBuildTargetGroup(origTarget);
            try
            {
                // 출시용 압축 설정 (itch는 Content-Encoding을 안 붙이므로 Gzip + fallback)
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.nameFilesAsHashes = false;
                // 전용 템플릿(Assets/WebGLTemplates/MoreMush): 캔버스가 창·전체화면 크기를 채운다(기본 템플릿은 960×600 고정이라 itch 전체화면에서도 안 커졌다).
                // 기본 크기는 게임 화면 비율 16:9 — itch Embed Viewport도 960×540
                PlayerSettings.WebGL.template = "PROJECT:MoreMush";
                PlayerSettings.defaultWebScreenWidth = 960;
                PlayerSettings.defaultWebScreenHeight = 540;
                sb.AppendLine($"템플릿={PlayerSettings.WebGL.template} 기본해상도={PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight} 예외지원={PlayerSettings.WebGL.exceptionSupport} 압축={PlayerSettings.WebGL.compressionFormat} fallback={PlayerSettings.WebGL.decompressionFallback}");

                string dir = Out(dev ? "Builds/WebGL_Dev" : "Builds/WebGL");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);

                var opt = new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    locationPathName = dir,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = dev ? BuildOptions.Development : BuildOptions.None,
                };
                var rep = BuildPipeline.BuildPlayer(opt);
                bool ok = Report(sb, rep);
                if (ok)
                {
                    string zip = Out(dev ? "Builds/MoreMustMush_WebGL_Dev.zip" : "Builds/MoreMustMush_WebGL.zip");
                    Zip(dir, zip, null);
                    sb.AppendLine($"zip: {zip} ({new FileInfo(zip).Length / 1048576.0:F1} MB)");
                }
                sb.AppendLine(ok ? "결과: 성공" : "결과: 실패");
            }
            catch (Exception e) { sb.AppendLine("결과: 실패 (예외) " + e); }
            finally { Restore(origTarget, origGroup); }
            return sb.ToString();
        }

        public static string Windows(bool dev = false)
        {
            var sb = new StringBuilder(dev ? "[Windows 개발용 빌드]\n" : "[Windows 빌드]\n");
            var origTarget = EditorUserBuildSettings.activeBuildTarget;
            var origGroup = BuildPipeline.GetBuildTargetGroup(origTarget);
            try
            {
                string dir = Out(dev ? "Builds/Windows_Dev" : "Builds/Windows");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);

                var opt = new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    locationPathName = Path.Combine(dir, "MoreMustMush.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = dev ? BuildOptions.Development : BuildOptions.None,
                };
                var rep = BuildPipeline.BuildPlayer(opt);
                bool ok = Report(sb, rep);
                if (ok)
                {
                    string zip = Out(dev ? "Builds/MoreMustMush_Windows_Dev.zip" : "Builds/MoreMustMush_Windows.zip");
                    Zip(dir, zip, rel => rel.Split('/')[0].EndsWith("_BurstDebugInformation_DoNotShip"));
                    sb.AppendLine($"zip: {zip} ({new FileInfo(zip).Length / 1048576.0:F1} MB)");
                }
                sb.AppendLine(ok ? "결과: 성공" : "결과: 실패");
            }
            catch (Exception e) { sb.AppendLine("결과: 실패 (예외) " + e); }
            finally { Restore(origTarget, origGroup); }
            return sb.ToString();
        }

        static bool Report(StringBuilder sb, BuildReport rep)
        {
            var s = rep.summary;
            var errs = rep.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).ToList();
            sb.AppendLine($"BuildResult={s.result} 크기={s.totalSize / 1048576.0:F1} MB 시간={s.totalTime:hh\\:mm\\:ss} 오류={s.totalErrors}({errs.Count}) 경고={s.totalWarnings}");
            if (s.result != BuildResult.Succeeded)
                foreach (var m in errs.Take(20)) sb.AppendLine("  오류: " + m.content);
            return s.result == BuildResult.Succeeded;
        }

        // dir 내용을 zip 루트에 바로 담는다 (감싸는 폴더 없음). skip(상대경로)가 true면 제외.
        static void Zip(string dir, string zipPath, Func<string, bool> skip)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var za = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
                    if (skip != null && skip(rel)) continue;
                    za.CreateEntryFromFile(f, rel, System.IO.Compression.CompressionLevel.Optimal);
                }
            }
        }
    }
}
