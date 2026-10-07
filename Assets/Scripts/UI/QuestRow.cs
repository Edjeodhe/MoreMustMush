using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MoreMush.Defs;
using static MoreMush.Game;
using Quest = MoreMush.SaveData.Quest;

namespace MoreMush
{
    // One village quest card (prototype .questrow). Cloned from the hidden template in ShopPanel.
    public class QuestRow : MonoBehaviour
    {
        public Image frame, faceBg, face, mushFrame, mush, barFill;
        public TMP_Text npcName, npcJob, talk, needName, tier, haveText, rewardLabel, rewardText, buttonText;
        public UIAction give;   // act quest, arg index

        public void Set(int index, Quest q)
        {
            var sp = SP[q.id]; var npc = QuestNpc(q);
            double have = InvCount(q.id); bool ok = have >= q.cnt;
            frame.color = ok ? U.Hex("#fff8dc") : U.Hex("#fffaf0");
            faceBg.color = U.Hex(npc.bg);
            face.sprite = SpriteDB.Get("Characters/NPC/" + npc.id);
            npcName.text = npc.n; npcJob.text = npc.job;
            string line = npc.lines[Mathf.Clamp(q.line, 0, npc.lines.Length - 1)];
            talk.text = line.Replace("{m}", $"<color=#b8560e><b>{sp.n}</b></color>").Replace("{n}", $"<color=#b8560e><b>{U.Fmt(q.cnt)}</b></color>");
            mushFrame.color = sp.t == 0 ? U.Hex("#d9c6a2") : U.Hex(new[] { "", "#7fc4ff", "#c77dff", "#ffb627" }[sp.t]);
            mush.sprite = SpriteDB.Single(sp.id);
            needName.text = $"{sp.n} <color=#8a6a4a>×{U.Fmt(q.cnt)}</color>";
            tier.text = $"<color={TIERS[sp.t].color}>{TIERS[sp.t].name}</color>";
            barFill.fillAmount = (float)System.Math.Min(1, have / q.cnt);
            haveText.text = $"<color={(ok ? "#2a8a2a" : "#8a6a4a")}>창고 {U.Fmt(have)} / {U.Fmt(q.cnt)}</color>";
            rewardLabel.text = $"보상 (판매가의 {U.FmtN(QuestMul())}배)";
            rewardText.text = $"{UIUtil.Ic("gold")}+{U.Fmt(QuestReward(q))}";
            buttonText.text = ok ? "전달하기" : "버섯 부족";
            give.GetComponent<Button>().interactable = ok;
            give.arg = index.ToString();
        }
    }
}
