using System.Globalization;
using TMPro;
using static MoreMush.Game;

namespace MoreMush
{
    // 골드·균사석 표시 (배치한 Gold_UI_BG 안의 글자).
    // 값이 바뀐 프레임에만 글자를 새로 만든다 — 매 프레임 확인은 숫자 비교 두 번뿐.
    public class CurrencyBar
    {
        const double SHORT_FROM = 10000;   // 이 값부터 1.00 M처럼 약자로
        readonly TMP_Text gold, gem;
        double shownGold = double.NaN, shownGem = double.NaN;

        public CurrencyBar(TMP_Text gold, TMP_Text gem) { this.gold = gold; this.gem = gem; }

        // 값이 바뀌었으면 true (열린 상점이 버튼 상태를 다시 그릴 때 쓴다)
        public bool Tick()
        {
            if (G == null) return false;
            double g = System.Math.Floor(G.gold), s = System.Math.Floor(G.gem);
            bool changed = false;
            if (!Same(g, shownGold)) { shownGold = g; UIUtil.SetText(gold, Num(g, " G")); changed = true; }
            if (!Same(s, shownGem)) { shownGem = s; UIUtil.SetText(gem, Num(s, "")); changed = true; }
            return changed;
        }

        // NaN끼리도 같은 값으로 본다 (NaN이면 매 프레임 글자를 새로 만들지 않게)
        static bool Same(double a, double b) => a == b || (double.IsNaN(a) && double.IsNaN(b));

        // 9,999 G · 10.0 K · 1.00 M (약자일 때는 G를 붙이지 않는다)
        static string Num(double v, string suffix)
        {
            if (v < SHORT_FROM) return v.ToString("#,0", CultureInfo.InvariantCulture) + suffix;
            string s = U.Fmt(v);   // 10.0K · 1.00M
            int i = s.Length;
            while (i > 0 && char.IsLetter(s[i - 1])) i--;
            return s.Substring(0, i) + " " + s.Substring(i);
        }
    }
}
