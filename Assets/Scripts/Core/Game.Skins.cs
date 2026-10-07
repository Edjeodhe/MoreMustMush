using static MoreMush.Defs;

namespace MoreMush
{
    // Skin shop rules (prototype "스킨 상점"): bought with gems (균사석), worn or taken off any time.
    public static partial class Game
    {
        public static bool SkinOwn(string id) => G?.skins != null && G.skins.own.TryGetValue(id, out var b) && b;
        // 착용 중인 스킨 (없으면 null)
        public static Skin CharSkinOn(string critter) => G?.skins != null && G.skins.ch.TryGetValue(critter, out var s) && s != null && SKIN.TryGetValue(s, out var k) ? k : null;
        public static Skin HvSkinOn(string hv) => G?.skins != null && G.skins.hv.TryGetValue(hv, out var s) && s != null && SKIN.TryGetValue(s, out var k) ? k : null;
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
            slot[sk.of] = slot.TryGetValue(sk.of, out var cur) && cur == id ? null : id;
            SaveGame();
        }
    }
}
