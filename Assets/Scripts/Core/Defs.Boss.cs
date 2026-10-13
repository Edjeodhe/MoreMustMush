using System;
using System.Collections.Generic;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 보스 (전투 기획서 Ver05 · 03. 보스 기믹) =====
        // 10스테이지마다 보스 스테이지. 제한 시간 안에 보스(분신 포함)를 모두 쓰러뜨리면 다음 스테이지로 간다.
        // 못 쓰러뜨리면 그 판 정산 골드 ×BOSS_FAIL_GOLD, 스테이지는 그대로이고 보스 도전이 꺼진다(그 스테이지에서 파밍).
        // 보스 도전은 균사 트리에서 언제든 다시 켤 수 있다.
        public const int BOSS_EVERY = 10;
        public const double BOSS_FAIL_GOLD = 0.8;
        public const float BOSS_R = 110;   // 일반 버섯(반지름 20~33)보다 훨씬 크게. 군락이 아닌 단일 버섯으로 그린다

        // 처치 보상 (보상 설계: 일반 사이클 보상의 최소 2배). 이번 판에 번 골드·딴 버섯의 BOSS_REWARD_MUL배를 더 준다 → 합계 ×(1+MUL).
        // 균사석은 세계수에서만 얻던 희귀 재화를 보스 동기부여로 준다. [임시값: 테스트 뒤 밸런스]
        public const double BOSS_REWARD_MUL = 2;
        public static int BossGem(int stage) => 5 + 5 * (stage / BOSS_EVERY);

        public class BossDef
        {
            public string id, cat, look, col;   // look: 그림으로 쓰는 버섯 (도감 원문 이름)
            public string nKo, gimmickKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string gimmick { get => Loc.T(gimmickKo); set => gimmickKo = value; }
            public double hpMul = 1;
        }

        // 순서대로 돌아가며 나온다 (10 대왕송이 · 20 청소부 · 30 죽음의 천사 · 40 환상 버섯 · 50 움직이는 버섯 · 60 대왕송이 …)
        public static readonly List<BossDef> BOSSES = new List<BossDef>
        {
            new BossDef { id = "king", cat = "ed", look = "송이버섯", col = "#c9a27a", n = "대왕송이", gimmick = "다른 보스보다 체력이 훨씬 높아요", hpMul = 1.6 },
            new BossDef { id = "cleaner", cat = "md", look = "선녀낙엽버섯", col = "#7ad07a", n = "숲 속의 청소부", gimmick = "3초마다 최대 체력의 5%를 회복해요" },
            new BossDef { id = "angel", cat = "ps", look = "독우산광대버섯", col = "#b48cff", n = "죽음의 천사", gimmick = "2초마다 필드의 임시 수확기를 모두 없애요" },
            new BossDef { id = "phantom", cat = "ps", look = "광대버섯", col = "#ff6b6b", n = "환상 버섯", gimmick = "체력 66%·33%에서 분신을 불러요. 분신까지 모두 쓰러뜨려야 해요", hpMul = 0.6 },
            new BossDef { id = "mover", cat = "md", look = "동충하초", col = "#f2a64a", n = "움직이는 버섯", gimmick = "쉴 새 없이 좌우로 움직여요" },
        };
        public static readonly Dictionary<string, BossDef> BOSS = new Dictionary<string, BossDef>();

        // 기믹 수치 (기획서: 개발 단계에서 조정 가능)
        public const float CLEANER_HEAL = 0.05f, CLEANER_INT = 3;
        public const float ANGEL_PURGE_INT = 2;   // 죽음의 천사: 이 간격(초)마다 임시 수확기를 모두 없앤다 (2026-10-13, 예전 독 안개 기믹 대체)
        public static readonly float[] PHANTOM_SPLIT = { 0.66f, 0.33f };
        public const float MOVER_SPD = 260;

        public static bool IsBossStage(int stage) => stage > 0 && stage % BOSS_EVERY == 0;
        public static BossDef BossAt(int stage) => IsBossStage(stage) ? BOSSES[(stage / BOSS_EVERY - 1) % BOSSES.Count] : null;
        // 다음 보스 스테이지 (지금이 보스 스테이지면 지금)
        public static int NextBossStage(int stage) => (stage + BOSS_EVERY - 1) / BOSS_EVERY * BOSS_EVERY;

        // 보스 체력 = 레전드리 버섯 체력 × BOSS_HP_K × 지역·스테이지 배율 × 보스별 배율. [임시값: 밸런스 팀이 시뮬로 맞춘다]
        // tuned 기준 스테이지 10 대왕송이 ≈ 105 (공격력 5.5로 보스만 노려 맞히면 약 16초). 봇(바 오차 ±60%)은 첫 보스에서 여러 번 진다
        public static double BOSS_HP_K = 1.5;
        public static double BossHp(BossDef b, string themeId, int stage) => TIERS[3].hp * BOSS_HP_K * ZoneMul(themeId, stage) * b.hpMul;

        static void InitBoss()
        {
            foreach (var b in BOSSES)
            {
                if (!SP_BY_NAME.ContainsKey(b.look)) throw new InvalidOperationException($"보스 {b.id}의 그림 버섯 '{b.look}'이 도감에 없습니다");
                BOSS[b.id] = b;
            }
        }
    }
}
