using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MoreMush.EditorTools
{
    // 빌드마다 지금 git 커밋(짧은 해시, 커밋 안 된 변경이 있으면 끝에 "d")을 Resources/BuildInfo.txt에 넣고, 빌드가 끝나면 지운다.
    // 런타임 BuildTag가 읽어 분석 로그 version에 붙인다. 파일이 남지 않으니 git에 올라가지 않는다
    public class BuildTagWriter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string PATH = "Assets/Resources/BuildInfo.txt";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string hash = Git("rev-parse --short=7 HEAD");
            if (string.IsNullOrEmpty(hash)) hash = "nogit";
            else if (!string.IsNullOrEmpty(Git("status --porcelain --untracked-files=no"))) hash += "d";
            File.WriteAllText(PATH, hash);
            AssetDatabase.ImportAsset(PATH);
        }

        public void OnPostprocessBuild(BuildReport report) => Remove();

        // 빌드가 실패해 후처리가 안 불려도 다음 에디터 시작 때 지운다
        [InitializeOnLoadMethod]
        static void Remove()
        {
            if (File.Exists(PATH) || File.Exists(PATH + ".meta")) AssetDatabase.DeleteAsset(PATH);
        }

        static string Git(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);
                    return p.ExitCode == 0 ? o : null;
                }
            }
            catch { return null; }
        }
    }
}
