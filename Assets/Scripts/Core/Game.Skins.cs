using static MoreMush.Defs;

namespace MoreMush
{
    // Skin shop rules (prototype "스킨 상점"): bought with 균사석 (gem), worn or taken off any time.
    public static partial class Game
    {
        public static bool SkinOwn(string id) => G?.skins != null && G.skins.own.TryGetValue(id, out var b) && b;
        // 착용 중인 스킨 (없으면 null). 한 번도 고르지 않은 칸(키 없음)은 사 둔 스킨을 입은 것으로 본다 —
        // 스킨을 먼저 사고 꼬마·수확기를 나중에 얻으면(황금 수확기 보상·세계수 합류) BuySkin이 입혀 주지 못했다. 벗으면 null이 남는다
        public static Skin CharSkinOn(string critter) => SkinOn(G?.skins?.ch, critter, CHAR_SKINS);
        public static Skin HvSkinOn(string hv) => SkinOn(G?.skins?.hv, hv, HV_SKINS);
        static Skin SkinOn(System.Collections.Generic.Dictionary<string, string> slot, string of, Skin[] list)
        {
            if (slot == null) return null;
            if (slot.TryGetValue(of, out var s)) return s != null && SKIN.TryGetValue(s, out var k) ? k : null;
            foreach (var sk in list) if (sk.of == of && SkinOwn(sk.id)) return sk;
            return null;
        }
        static bool HasOwner(Skin sk) => sk.ch ? HasSpecial(sk.of) : HvStar(sk.of) > 0;

        public static bool BuySkin(string id)
        {
            var sk = SKIN[id];
            if (SkinOwn(id) || G.gem < sk.price) return false;
            G.gem -= sk.price; G.skins.own[id] = true;
            if (HasOwner(sk)) (sk.ch ? G.skins.ch : G.skins.hv)[sk.of] = id;   // 가진 꼬마·수확기면 바로 입힌다
            SaveGame();
            return true;
        }

        // 입기 / 벗기
        public static void WearSkin(string id)
        {
            var sk = SKIN[id];
            if (!SkinOwn(id)) return;
            var slot = sk.ch ? G.skins.ch : G.skins.hv;
            var cur = sk.ch ? CharSkinOn(sk.of) : HvSkinOn(sk.of);
            slot[sk.of] = cur == sk ? null : id;
            SaveGame();
        }
    }
}
