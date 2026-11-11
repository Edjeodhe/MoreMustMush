using UnityEngine;
using UnityEngine.UI;

namespace MoreMush
{
    // 보스 등장·격파 연출 (사용자 2026-11-11)
    //   등장: 보스 뒤 플립북 Art/effect/boss/entrance (0.5초, RoundSim FxShape.BossIn). 보스는 #463765 실루엣으로 덮였다가 원래 색으로 (ShroomView) + 0.15초 뒤 화면 맨 앞에 언어별 "보스 등장!"
    //         글자 그림 Art/effect/text/boss_entrance_<lang>: 작게 나타나 확 커졌다가 줄어들며 땅! 박힌다 (0.3초), 잠깐 머물다 빠르게 사라진다
    //   격파: 게임이 멈추고 보스 뒤 플립북 Art/effect/boss/defeat (0.5초, FxShape.BossOut), 0.3초에 보스가 터진다 (RoundSim.StepBossDeath)
    public partial class RoundView
    {
        static Sprite[] bossInFrames, bossOutFrames;
        RoundSim prewarmedFor;

        // 라운드가 시작될 때 연출 그림을 미리 읽어 둔다: 처음 쓰는 순간(첫 수확·첫 치명타·보스 등장·격파) Resources에서 읽느라 한 프레임이 끊기지 않게
        void Prewarm(RoundSim R)
        {
            ImpactFrames("splash_skyblue"); ImpactFrames("splash_orange"); ImpactFrames("splash_beige");
            BannerBurst();
            if (R.BossRound)
            {
                BossFrames(true); BossFrames(false);
                if (bossTitleLang != Loc.Lang) { bossTitleLang = null; }
                BossTitleSprite(Loc.Lang);
            }
        }
        Image bossTitle;
        string bossTitleLang;

        static Sprite[] BossFrames(bool entrance)
        {
            ref var arr = ref entrance ? ref bossInFrames : ref bossOutFrames;
            if (arr != null) return arr;
            string dir = entrance ? "Art/effect/boss/entrance/frame_" : "Art/effect/boss/defeat/frame_";
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 1; i <= 64; i++)
            {
                var sp = Resources.Load<Sprite>(dir + i.ToString("00"));
                if (sp == null) break;
                list.Add(sp);
            }
            return arr = list.ToArray();
        }

        // Loc 언어 코드 → 글자 그림 접미사 (포르투갈어 그림은 pt-BR)
        static Sprite BossTitleSprite(string lang)
        {
            string key = lang == "pt" ? "pt-BR" : lang;
            return Resources.Load<Sprite>("Art/effect/text/boss_entrance_" + key) ?? Resources.Load<Sprite>("Art/effect/text/boss_entrance_en");
        }

        const float TITLE_W = 760;                    // 글자 그림 폭 (필드 px)
        const float TITLE_HOLD = 1.0f, TITLE_FADE = 0.1f;   // 박힌 뒤 머무는 시간 · 사라지는 시간

        void DrawBossTitle(RoundSim R, float z)
        {
            float t = R.bossIntroT - RoundSim.BOSS_TITLE_AT;
            const float IN = 0.3f;
            bool on = R.bossIntroT >= 0 && t >= 0 && t < IN + TITLE_HOLD + TITLE_FADE;
            if (!on)
            {
                if (bossTitle != null && bossTitle.enabled) bossTitle.enabled = false;
                return;
            }
            if (bossTitle == null)
            {
                var go = new GameObject("BossTitle", typeof(RectTransform), typeof(Image));
                go.layer = textLayer.gameObject.layer;
                go.transform.SetParent(textLayer, false);
                bossTitle = go.GetComponent<Image>();
                bossTitle.raycastTarget = false;
                bossTitle.preserveAspect = true;
                var r = bossTitle.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0.5f, 0.5f);
            }
            if (bossTitleLang != Loc.Lang || bossTitle.sprite == null)
            {
                bossTitleLang = Loc.Lang;
                bossTitle.sprite = BossTitleSprite(Loc.Lang);
            }
            if (bossTitle.sprite == null) { bossTitle.enabled = false; return; }
            if (!bossTitle.enabled) { bossTitle.enabled = true; bossTitle.transform.SetAsLastSibling(); }

            // 작게(0.25) → 확 커짐(1.4, 0.12초) → 줄어들며 박힘(0.9, 0.2초 = 땅!) → 제자리(1, 0.3초)
            float s, a = 1;
            if (t < 0.12f) { s = Mathf.Lerp(0.25f, 1.4f, EaseOut(t / 0.12f)); a = Mathf.Clamp01(t / 0.05f); }
            else if (t < RoundSim.BOSS_TITLE_LAND) { float k = (t - 0.12f) / (RoundSim.BOSS_TITLE_LAND - 0.12f); s = Mathf.Lerp(1.4f, 0.9f, k * k); }
            else if (t < IN) s = Mathf.Lerp(0.9f, 1f, EaseOut((t - RoundSim.BOSS_TITLE_LAND) / (IN - RoundSim.BOSS_TITLE_LAND)));
            else { s = 1; a = 1 - Mathf.Clamp01((t - IN - TITLE_HOLD) / TITLE_FADE); }

            var sp = bossTitle.sprite;
            var rt = bossTitle.rectTransform;
            rt.sizeDelta = new Vector2(TITLE_W, TITLE_W * sp.rect.height / sp.rect.width) * z;
            rt.anchoredPosition = new Vector2(RoundSim.WW * 0.5f * z, -RoundSim.WH * 0.45f * z);
            rt.localScale = new Vector3(s, s, 1);
            bossTitle.canvasRenderer.SetAlpha(a);
        }

        void ReleaseBossTitle()
        {
            if (bossTitle != null) bossTitle.enabled = false;
        }
    }
}
