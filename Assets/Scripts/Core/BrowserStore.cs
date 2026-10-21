#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace MoreMush
{
    // 웹 브라우저 localStorage (Assets/Plugins/WebGL/MMMStore.jslib). 웹 빌드가 아니면 아무것도 하지 않는다 (Get = null).
    // 웹의 persistentDataPath는 페이지 URL마다 달라 버전을 올리면(새 주소) 세이브가 안 보였다. localStorage는 같은 사이트면 그대로라
    // 세이브 사본과 기기 ID를 여기에도 둔다 (2026-10-20). itch.io는 여러 게임이 한 사이트를 같이 쓰므로 키에 게임 이름을 붙인다
    public static class BrowserStore
    {
        const string PREFIX = "MoreMustMush.";
        public const string SAVE = "save", UID = "uid", LEGACY_SCANNED = "legacyScanned";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string MMM_StoreGet(string key);
        [DllImport("__Internal")] static extern int MMM_StoreSet(string key, string value);
        [DllImport("__Internal")] static extern void MMM_StoreDel(string key);
        public static bool Available => true;
        public static string Get(string key) => MMM_StoreGet(PREFIX + key);
        public static bool Set(string key, string value) => MMM_StoreSet(PREFIX + key, value) == 1;
        public static void Delete(string key) => MMM_StoreDel(PREFIX + key);
#else
        public static bool Available => false;
        public static string Get(string key) => null;
        public static bool Set(string key, string value) => false;
        public static void Delete(string key) { }
#endif
    }
}
