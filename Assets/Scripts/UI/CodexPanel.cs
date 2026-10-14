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

        public void Select(string id)
        {
            if (selected == id) return;
            string previous = selected; selected = id;
            foreach (var c in cells) if (c.id == previous || c.id == selected) c.Select(selected);
            RenderDetail();
        }

        void Render()
        {
            count.text = $"{CodexCount()}/{SP_TOTAL}";
            for (int i = 0; i < 3; i++) { string c = CAT_KEYS[i]; headCounts[i].text = $"{SPECIES.Count(s => s.c == c && Harvested(s.id))}/{SP_PER_CAT}"; }
            specialsHead.text = Loc.F("특수 버섯 <size=70%><color=#8a6a4a>{0}/{1}</color></size>", SPECIALS.Count(s => HasSpecial(s.id)), SPECIALS.Length);
            foreach (var c in cells) c.Render(selected);
            RenderDetail();
            var abSum = StarAbilities();
            abHead.text = Loc.F("★ 별 능력 합계 <size=70%><color=#8a6a4a>({0}/{1}별)</color></size>", SPECIES.Sum(x => StarOf(x.id)), SP_TOTAL * 5);
            abList.text = string.Join("\n", AB.Keys.Select(k => abSum[k] > 0 ? $"{AB[k].n}<pos=75%><color=#2a6a9a><b>{AbFmt(k, abSum[k])}</b></color>" : $"<color=#b8a890>{AB[k].n}<pos=75%>{AbFmt(k, 0)}</color>"));
            int gc = GoldenCount();
            var rows = SETS.Select(s =>
            {
                int n = s.ids.Count(Harvested); bool done = n == s.ids.Count;
                return $"<color={(done ? "#3a8a2a" : "#3b2414")}>{(done ? "● " : "")}{s.n}</color> <color={(done ? "#3a8a2a" : "#8a7a6a")}>{n}/{s.ids.Count}</color>  <size=80%><color={(done ? "#3a8a2a" : "#8a7a6a")}>{s.d}</color></size>";
            }).ToList();
            rows.Add(Loc.F("<color={0}>{1}황금 도감</color> <color=#8a7a6a>{2}/{3}</color>  <size=80%><color=#8a7a6a>5종 골드 +10% · 15종 +25%·황금 수확기 · {3}종 +60%</color></size>", gc >= 5 ? "#3a8a2a" : "#3b2414", gc >= 5 ? "● " : "", gc, SP_TOTAL));
            setList.text = Loc.T("<size=120%>세트 보너스</size>") + "\n" + string.Join("\n", rows);
        }

        void RenderDetail()
        {
            detailEmpty.SetActive(selected == null);
            detail.SetActive(selected != null);
            if (selected == null) return;
            UIUtil.Show(detailStars, true); UIUtil.Show(detailAb, true); UIUtil.Show(detailTrait, true);
            UIUtil.Show(detailWarn, true); UIUtil.Show(detailRec, true);
            if (selected.StartsWith("sp:"))
            {
                var k = SPC[selected.Substring(3)]; bool own = HasSpecial(k.id);
                UIUtil.SetImage(detailIcon, SpriteDB.Get($"Characters/Critters/{k.id}_ref"), own ? 0 : 1);
                detailName.text = Loc.F("{0} <size=50%><mark=#b07bff>  특수 버섯  </mark></size>", own ? k.n : "???");
                detailDesc.text = own ? Loc.T("숲을 깡총깡총 뛰어다니던 캐릭터 버섯. 지금은 세계수에서 나무버섯을 따고 있어요.") : (JoinLv(k.id) > 0 ? Loc.F("세계수가 {0}단계가 되면 합류해요.", JoinLv(k.id)) : Loc.T("2차 업데이트에서 만날 수 있어요."));
                detailStars.gameObject.SetActive(false); detailAb.gameObject.SetActive(false); detailWarn.gameObject.SetActive(false);
                detailTrait.text = KID.TryGetValue(k.id, out var kd) ? (own ? Loc.F("보유 중 · Lv.{0} ★{1} · 특성: {2}", KLv(k.id), Awaken(k.id), kd.traitText) : Loc.F("특성: {0}", kd.traitText)) : Loc.T("2차 업데이트 꼬마");
                detailRec.text = Loc.T("세계수가 진화할 때마다 꼬마가 한 마리씩 합류해요");
                return;
            }
            var sp = SP[selected]; G.codex.TryGetValue(sp.id, out var e);
            bool h = Harvested(sp.id), un = IsUnlockedSp(sp);
            string trait = sp.jelly ? TRAIT_TEXT["jelly"] : sp.spore != null ? TRAIT_TEXT[sp.spore] : sp.wander ? Loc.F("{0} · 항상 떠돌이", TRAIT_TEXT["wander"]) : "";
            string tierPill = $"<size=50%><mark={TIERS[sp.t].color}55>  {TIERS[sp.t].name}  </mark></size>";
            if (sp.boss > 0) tierPill += " " + Loc.F("<size=50%><mark=#c0392b>  {0} 스테이지 {1} 보스  </mark></size>", UIUtil.Ic(BOSS_ICON), sp.boss);
            if (h || un)
            {
                int s = h ? StarOf(sp.id) : 0; double n = e?.n ?? 0;
                UIUtil.SetImage(detailIcon, SpriteDB.Single(sp.id), h ? (e != null && e.gold ? 3 : 0) : 2);
                detailName.text = $"{sp.n} {tierPill} <size=50%><mark={CATS[sp.c].color}>  {CATS[sp.c].name}  </mark></size>";
                detailDesc.text = h ? sp.d : Loc.T("해금됐어요! 필드에서 찾아 수확해 보세요.");
                string starStr = s > 0 ? $"<color=#e8a900>{new string('★', s)}</color><color=#d8ccb4>{new string('★', 5 - s)}</color>" : "☆☆☆☆☆";
                detailStars.text = starStr + " " + (h ? (s < 5 ? Loc.F("다음 별까지 {0}개 수확", U.Fmt(STAR_N[s] - n)) : Loc.T("최대 5성")) : Loc.F("{0}개 수확하면 첫 별", STAR_N[0]));
                double per = AbPerStar(sp);
                detailAb.text = Loc.F("★ 별 능력: <b>{0}</b> 별마다 {1} · 지금 {2}", AB[sp.ab].n, AbFmt(sp.ab, per), AbFmt(sp.ab, per * s)) + (s < 5 ? " " + Loc.F("(5성 {0})", AbFmt(sp.ab, per * 5)) : "");
                detailTrait.text = trait; UIUtil.Show(detailTrait, trait != "");
                UIUtil.Show(detailWarn, sp.c == "ps");
                detailRec.text = Loc.F("{0} · HP {1} · 수확량 {2} (무더기: HP ×5, 수확 ×7~10)", UnlockText(sp), TIERS[sp.t].hp, TIERS[sp.t].drop) +
                    (h ? " · " + Loc.F("처음 딴 라운드 {0} · 누적 수확 {1}개 · 창고 {2}개 · 황금 {3} · 거대 {4}", e.first, U.Fmt(e.n), U.Fmt(StoreOf(sp.id)), e.gold ? "○" : "×", e.giant ? "○" : "×") : "");
            }
            else
            {
                UIUtil.SetImage(detailIcon, SpriteDB.Single(sp.id), 1);
                detailName.text = $"??? {tierPill}";
                detailDesc.text = Loc.T("아직 해금되지 않은 버섯이에요. 이름과 특징은 해금 전까지 가려져 있어요.");
                detailStars.gameObject.SetActive(false); detailWarn.gameObject.SetActive(false); detailRec.gameObject.SetActive(false);
                detailTrait.text = UnlockText(sp);
                detailAb.text = Loc.F("★ 별 능력: {0} (별마다 {1})", AB[sp.ab].n, AbFmt(sp.ab, AbPerStar(sp)));
            }
        }
    }
}
