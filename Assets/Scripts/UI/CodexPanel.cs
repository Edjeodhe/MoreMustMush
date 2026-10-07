using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;

namespace MoreMush
{
    // Mushroom codex modal (prototype openCodex): three category grids, special critters, detail, star
    // ability totals and set bonuses.
    public class CodexPanel : MonoBehaviour
    {
        public TMP_Text count;
        public TMP_Text[] headCounts = new TMP_Text[3];
        public TMP_Text specialsHead;
        public GameObject detailEmpty, detail;
        public Image detailIcon;
        public TMP_Text detailName, detailDesc, detailStars, detailAb, detailTrait, detailWarn, detailRec;
        public TMP_Text abHead, abList, setList;

        readonly List<CodexCell> cells = new List<CodexCell>();
        string selected;
        bool inRound;

        void Awake()
        {
            GetComponentsInChildren(true, cells);
        }

        public void Open(bool fromRound)
        {
            inRound = fromRound; selected = null;
            GameFlow.I.OpenModal(this, fromRound ? (System.Action)(() => { if (RoundSim.R != null) RoundSim.R.paused = false; }) : () => GameFlow.I.tree.Refresh());
            Render();
        }

        public void Select(string id) { selected = id; Render(); }

        void Render()
        {
            count.text = $"{CodexCount()}/{SP_TOTAL}";
            for (int i = 0; i < 3; i++) { string c = CAT_KEYS[i]; headCounts[i].text = $"{SPECIES.Count(s => s.c == c && Harvested(s.id))}/{SP_PER_CAT}"; }
            specialsHead.text = $"특수 버섯 <size=70%><color=#8a6a4a>{SPECIALS.Count(s => HasSpecial(s.id))}/{SPECIALS.Length}</color></size>";
            foreach (var c in cells) c.Render(selected);
            RenderDetail();
            var abSum = StarAbilities();
            abHead.text = $"★ 별 능력 합계 <size=70%><color=#8a6a4a>({SPECIES.Sum(x => StarOf(x.id))}/{SP_TOTAL * 5}별)</color></size>";
            abList.text = string.Join("\n", AB.Keys.Select(k => abSum[k] > 0 ? $"{AB[k].n}<pos=75%><color=#2a6a9a><b>{AbFmt(k, abSum[k])}</b></color>" : $"<color=#b8a890>{AB[k].n}<pos=75%>{AbFmt(k, 0)}</color>"));
            int gc = GoldenCount();
            var rows = SETS.Select(s =>
            {
                int n = s.ids.Count(Harvested); bool done = n == s.ids.Count;
                return $"<color={(done ? "#3a8a2a" : "#3b2414")}>{(done ? "● " : "")}{s.n}</color> <color={(done ? "#3a8a2a" : "#8a7a6a")}>{n}/{s.ids.Count}</color>  <size=80%><color={(done ? "#3a8a2a" : "#8a7a6a")}>{s.d}</color></size>";
            }).ToList();
            rows.Add($"<color={(gc >= 5 ? "#3a8a2a" : "#3b2414")}>{(gc >= 5 ? "● " : "")}황금 도감</color> <color=#8a7a6a>{gc}/{SP_TOTAL}</color>  <size=80%><color=#8a7a6a>5종 골드 +10% · 15종 +25%·황금 수확기 · {SP_TOTAL}종 +60%</color></size>");
            setList.text = "<size=120%>세트 보너스</size>\n" + string.Join("\n", rows);
        }

        void RenderDetail()
        {
            detailEmpty.SetActive(selected == null);
            detail.SetActive(selected != null);
            if (selected == null) return;
            foreach (var t in new[] { detailStars, detailAb, detailTrait, detailWarn, detailRec }) UIUtil.Show(t, true);
            if (selected.StartsWith("sp:"))
            {
                var k = SPC[selected.Substring(3)]; bool own = HasSpecial(k.id);
                UIUtil.SetImage(detailIcon, SpriteDB.Get($"Characters/Critters/{k.id}_ref"), own ? 0 : 1);
                detailName.text = $"{(own ? k.n : "???")} <size=50%><mark=#b07bff>  특수 버섯  </mark></size>";
                detailDesc.text = own ? "숲을 깡총깡총 뛰어다니던 캐릭터 버섯. 지금은 버섯 농장에서 뛰어놀고 있어요." : $"라운드 중 가끔({Mathf.RoundToInt(SPECIAL_P * 100)}%) 나타나요. {SPECIAL_LIFE}초 안에 잡지 못하면 숲으로 도망쳐요.";
                detailStars.gameObject.SetActive(false); detailAb.gameObject.SetActive(false); detailWarn.gameObject.SetActive(false);
                detailTrait.text = own ? "보유 중 · " + k.perk : "잡으면 영구 능력을 얻어요";
                detailRec.text = "이미 잡은 특수 버섯을 또 잡으면 이번 사이클 세금의 30%만큼 보너스 골드";
                return;
            }
            var sp = SP[selected]; G.codex.TryGetValue(sp.id, out var e);
            bool h = Harvested(sp.id), un = IsUnlockedSp(sp);
            string trait = sp.jelly ? TRAIT_TEXT["jelly"] : sp.spore != null ? TRAIT_TEXT[sp.spore] : sp.wander ? TRAIT_TEXT["wander"] + " · 항상 떠돌이" : "";
            string tierPill = $"<size=50%><mark={TIERS[sp.t].color}55>  {TIERS[sp.t].name}  </mark></size>";
            if (h || un)
            {
                int s = h ? StarOf(sp.id) : 0; double n = e?.n ?? 0;
                UIUtil.SetImage(detailIcon, SpriteDB.Single(sp.id), h ? (e != null && e.gold ? 3 : 0) : 2);
                detailName.text = $"{sp.n} {tierPill} <size=50%><mark={CATS[sp.c].color}>  {CATS[sp.c].name}  </mark></size>";
                detailDesc.text = h ? sp.d : "해금됐어요! 필드에서 찾아 수확해 보세요.";
                string starStr = s > 0 ? $"<color=#e8a900>{new string('★', s)}</color><color=#d8ccb4>{new string('★', 5 - s)}</color>" : "☆☆☆☆☆";
                detailStars.text = $"{starStr} " + (h ? (s < 5 ? $"다음 별까지 {U.Fmt(STAR_N[s] - n)}개 수확" : "최대 5성") : $"{STAR_N[0]}개 수확하면 첫 별");
                double per = AbPerStar(sp);
                detailAb.text = $"★ 별 능력: <b>{AB[sp.ab].n}</b> 별마다 {AbFmt(sp.ab, per)} · 지금 {AbFmt(sp.ab, per * s)}{(s < 5 ? $" (5성 {AbFmt(sp.ab, per * 5)})" : "")}";
                detailTrait.text = trait; UIUtil.Show(detailTrait, trait != "");
                UIUtil.Show(detailWarn, sp.c == "ps");
                detailRec.text = $"{UnlockText(sp)} · HP {TIERS[sp.t].hp} · 수확량 {TIERS[sp.t].drop} (무더기: HP ×5, 수확 ×7~10)" +
                    (h ? $" · 처음 딴 라운드 {e.first} · 누적 수확 {U.Fmt(e.n)}개 · 창고 {U.Fmt(InvCount(sp.id))}개 · 황금 {(e.gold ? "○" : "×")} · 거대 {(e.giant ? "○" : "×")}" : "");
            }
            else
            {
                UIUtil.SetImage(detailIcon, SpriteDB.Single(sp.id), 1);
                detailName.text = $"??? {tierPill}";
                detailDesc.text = "아직 해금되지 않은 버섯이에요. 이름과 특징은 해금 전까지 가려져 있어요.";
                detailStars.gameObject.SetActive(false); detailWarn.gameObject.SetActive(false); detailRec.gameObject.SetActive(false);
                detailTrait.text = UnlockText(sp);
                detailAb.text = $"★ 별 능력: {AB[sp.ab].n} (별마다 {AbFmt(sp.ab, AbPerStar(sp))})";
            }
        }
    }
}
