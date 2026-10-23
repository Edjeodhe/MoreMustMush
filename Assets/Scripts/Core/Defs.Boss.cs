using System;
using System.Collections.Generic;

namespace MoreMush
{
    public static partial class Defs
    {
        // ===== 보스 (전투 기획서 Ver05 · 03. 보스 기믹) =====
        // 보스 스테이지: 첫 보스 10, 그 뒤 10스테이지마다(20·30…). 표시(콘텐츠) 스테이지 기준 — 튜토리얼 판(TUT_ROUNDS) 뒤라 첫 보스는 12번째 판 (Defs.ContentStage). 제한 시간 안에 보스(분신 포함)를 모두 쓰러뜨리면 다음 스테이지로 간다.
        // 못 쓰러뜨리면 그 판 정산 골드 ×BOSS_FAIL_GOLD, 스테이지는 그대로이고 보스 도전이 꺼진다(그 스테이지에서 파밍).
        // 보스 도전은 균사 트리에서 언제든 다시 켤 수 있다.
        // 밸런스 상수는 엑셀 DB Const 시트에서 읽는다
        public static readonly int BOSS_EVERY = Db.CInt("BOSS_EVERY", 1);
        public static readonly double BOSS_FAIL_GOLD = Db.C("BOSS_FAIL_GOLD");
        public const float BOSS_R = 110;   // 일반 버섯(반지름 20~33)보다 훨씬 크게. 군락이 아닌 단일 버섯으로 그린다

        // 처치 보상 (보상 설계: 일반 사이클 보상의 최소 2배). 골드는 max(이번 판, 직전 일반 판)의, 버섯은 이번 판에 딴 것의 BOSS_REWARD_MUL배를 더 준다.
        // 균사석은 세계수에서만 얻던 희귀 재화를 보스 동기부여로 준다. [임시값: 테스트 뒤 밸런스]
        public static readonly double BOSS_REWARD_MUL = Db.C("BOSS_REWARD_MUL");
        static readonly int BOSS_GEM_BASE = Db.CInt("BOSS_GEM_BASE", 0), BOSS_GEM_STEP = Db.CInt("BOSS_GEM_STEP", 0);
        public static int BossGem(int stage) =>   // stage = 콘텐츠 스테이지
            BOSS_GEM_BASE + BOSS_GEM_STEP * (stage / BOSS_EVERY);

        public class BossDef
        {
            public string id, cat, look, col;   // look: 그림으로 쓰는 버섯 (도감 원문 이름)
            public string nKo, gimmickKo;
            public string n { get => Loc.T(nKo); set => nKo = value; }
            public string gimmick { get => Loc.T(gimmickKo); set => gimmickKo = value; }
            public double hpMul = 1;
        }

        // 순서대로 돌아가며 나온다 (10 · 20 · 30 … 스테이지). DB Boss 시트의 행 순서, 기믹 문구는 String 시트 boss:<키>:gimmick
        public static readonly List<BossDef> BOSSES = new List<BossDef>();
        public static readonly Dictionary<string, BossDef> BOSS = new Dictionary<string, BossDef>();

        // 기믹 수치 (기획서: 개발 단계에서 조정 가능)
        public static readonly float CLEANER_HEAL = (float)Db.C("CLEANER_HEAL"), CLEANER_INT = (float)Db.CPos("CLEANER_INT");
        public static readonly float ANGEL_PURGE_INT = (float)Db.CPos("ANGEL_PURGE_INT");   // 죽음의 천사: 이 간격(초)마다 임시 수확기를 모두 없앤다 (2026-10-13, 예전 독 안개 기믹 대체)
        public static readonly float[] PHANTOM_SPLIT = { 0.66f, 0.33f };
        public static readonly float MOVER_SPD = (float)Db.C("MOVER_SPD");

        // 보스는 BOSS_EVERY(10)의 배수 스테이지마다: 10 · 20 · 30 … (사용자 결정 2026-10-16, 첫 보스 14 → 10)
        // 아래 함수의 stage는 모두 콘텐츠(표시) 스테이지다. 내부 판 번호(G.stage)는 ContentStage로 바꿔 넣는다 (5회차 P-2: 튜토리얼 판 오프셋 TUT_ROUNDS)
        public static int FIRST_BOSS_STAGE => BOSS_EVERY;
        public static bool IsBossStage(int stage) => stage >= BOSS_EVERY && stage % BOSS_EVERY == 0;
        public static BossDef BossAt(int stage) => !IsBossStage(stage) ? null : BOSSES[(stage / BOSS_EVERY - 1) % BOSSES.Count];
        // 다음 보스 스테이지 (지금이 보스 스테이지면 지금)
        public static int NextBossStage(int stage) => System.Math.Max(BOSS_EVERY, (stage + BOSS_EVERY - 1) / BOSS_EVERY * BOSS_EVERY);

        // 보스 체력 = 레전드리 버섯 체력 × BOSS_HP_K × 지역·스테이지 배율 × 보스별 배율. [임시값: 밸런스 팀이 시뮬로 맞춘다]
        // tuned 기준 스테이지 10 대왕송이 ≈ 105 (공격력 5.5로 보스만 노려 맞히면 약 16초). 봇(바 오차 ±60%)은 첫 보스에서 여러 번 진다
        public static double BOSS_HP_K = Db.CPos("BOSS_HP_K");
        // 보스가 한 바퀴 돈 뒤(스테이지 60~)는 BOSS_LOOP_HP_MUL을 쓴다 (첫 보스로 돌아가도 쉬워지지 않게, 커지는 건 ZoneMul이 맡는다).
        // 2026-10-14 마지막 보스(움직이는 버섯) 배율과 분리: 50을 3시간 벽으로 올려도 60~가 같이 단단해지지 않게(theory R2-14 안 B)
        public static double BOSS_LOOP_HP_MUL = Db.CPos("BOSS_LOOP_HP_MUL");
        public static double BossHp(BossDef b, string themeId, int stage)
        {
            double mul = stage / BOSS_EVERY > BOSSES.Count ? BOSS_LOOP_HP_MUL : b.hpMul;
            return TIERS[3].hp * BOSS_HP_K * ZoneMul(themeId, stage) * mul;
        }

        // 보스 타격 점수 배율 (2회차, 엑셀 DB Const). 1보다 작으면 보스를 못 잡은 판이 일반 판보다 손해
        public static readonly double BOSS_SCORE_MUL = Db.C("BOSS_SCORE_MUL");

        static void InitBoss()
        {
            foreach (var r in Db.Data.Boss)
                BOSSES.Add(new BossDef { id = r.Boss_Key, nKo = r.Boss_Name, gimmickKo = Db.Text($"boss:{r.Boss_Key}:gimmick"), cat = r.Cat, look = r.Look, col = r.Color, hpMul = r.Hp_Mul });
            if (BOSSES.Count == 0) throw new InvalidOperationException("[DB] Boss 시트가 비어 있어요");
            foreach (var b in BOSSES)
            {
                if (!SP_BY_NAME.ContainsKey(b.look)) throw new InvalidOperationException($"보스 {b.id}의 그림 버섯 '{b.look}'이 도감에 없습니다");
                BOSS[b.id] = b;
            }
        }
    }
}
