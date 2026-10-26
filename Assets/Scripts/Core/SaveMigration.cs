using System;
using System.Collections.Generic;

namespace MoreMush
{
    // 세이브 버전 올리기. SaveIO.Load가 Sanitize 끝에서 부르며, 한 버전씩 차례로 올린다.
    public static class SaveMigration
    {
        public const int CURRENT = 11;

        // 이번 로드에서 플레이어에게 알릴 문장 (App.FlushMigrationNotice가 알리고 비운다)
        public static string Notice;

        // v3에서 지운 노드(시간 증가·숲 장치 생성 스킬)의 레벨별 가격. 지운 가격표를 여기에 남겨 환불에 쓴다
        static readonly Dictionary<string, double[]> RETIRED_V3 = new Dictionary<string, double[]>
        {
            ["tspore"] = new double[] { 9800 },
            ["tspore_rate"] = new double[] { 35000, 130000 },
            ["tspore_pow"] = new double[] { 35000, 130000 },
            ["sip"] = new double[] { 3100000 },
            ["sip_rate"] = new double[] { 29000000, 99000000 },
            ["sip_pow"] = new double[] { 35000000, 120000000 },
            ["device"] = new double[] { 4400000 },
            ["device_rate"] = new double[] { 44000000, 140000000 },
        };

        public static void Run(SaveData g)
        {
            if (g.v < 3) ToV3(g);
            if (g.v < 4) ToV4(g);
            if (g.v < 5) ToV5(g);
            if (g.v < 6) ToV6(g);
            if (g.v < 7) ToV7(g);
            if (g.v < 8) ToV8(g);
            if (g.v < 9) ToV9(g);
            if (g.v < 10) ToV10(g);
            if (g.v < 11) ToV11(g);
            g.v = Math.Max(g.v, CURRENT);
        }

        static void ToV3(SaveData g)
        {
            double refund = 0;
            foreach (var kv in RETIRED_V3)
            {
                if (!g.nodes.TryGetValue(kv.Key, out int L)) continue;
                for (int i = 0; i < Math.Min(L, kv.Value.Length); i++) refund += Math.Ceiling(kv.Value[i] * Defs.COST_MUL);
                g.nodes.Remove(kv.Key);
            }
            if (refund > 0)
            {
                g.gold += refund;
                AddNotice(Loc.F("없어진 스킬에 쓴 골드 {0}을 돌려받았어요", U.Fmt(refund)));
            }
            FitHarvestersToSlots(g);
            // 다이아몬드는 없어졌다. 남은 다이아는 균사석으로 1:1 (포자·창고 버섯·마을 의뢰는 세이브에서 빠져 그냥 사라진다)
            if (g.legacyDia >= 1)
            {
                double d = Math.Floor(g.legacyDia);
                g.gem += d;
                AddNotice(Loc.F("다이아몬드 {0}개를 균사석으로 바꿨어요", U.Fmt(d)));
            }
            g.legacyDia = 0;
        }

        // v4: 스테이지가 판 수와 따로 간다(보스를 못 깨면 멈춘다). 지나온 보스는 쓰러뜨린 것으로 친다
        // v4에서 지운 노드: 숲 장치 수(장치는 종류별 해금 노드로), 맑은 포자막 발동률(바에 맞으면 늘 발동)
        static readonly Dictionary<string, double[]> RETIRED_V4 = new Dictionary<string, double[]>
        {
            ["ed_dev"] = new double[] { 150000, 410000 },
            ["clear_rate"] = new double[] { 13000000, 47000000 },
        };

        static void ToV4(SaveData g)
        {
            g.stage = g.rounds + 1;
            g.bossOn = true;
            double refund = Refund(g, RETIRED_V4);
            if (refund > 0) AddNotice(Loc.F("바뀐 강화 노드에 쓴 골드 {0}을 돌려받았어요", U.Fmt(refund)));
            // 예전엔 숲 장치가 늘 나왔다. 이미 진행한 세이브는 장치 셋을 해금해 준다
            if (g.rounds > 0) foreach (var id in new[] { "dev_stump", "dev_moss", "dev_acorn" }) g.nodes[id] = 1;
        }

        // v5: 트리 개편. 가지 코어(식용·약용·독 균사)가 없어지고 기운 북돋기가 시작 노드가 됐다.
        // 스킬의 발동률·위력 하위 노드(2레벨씩)는 스킬 노드 4레벨로 합친다: 새 레벨 = 1 + 하위 노드 레벨 합(최대 4). 회전 칼날도 같은 식
        // 진행한 세이브는 시작 노드만 1레벨로 준다. 부모가 바뀐 노드는 새 부모를 사야 더 올릴 수 있다 (조상을 공짜로 채우지 않는다)
        static void ToV5(SaveData g)
        {
            int Get(string id) => g.nodes.TryGetValue(id, out var L) ? L : 0;
            foreach (var s in Defs.SKILL_IDS)
            {
                int sub = Get(s + "_rate") + Get(s + "_pow");
                g.nodes.Remove(s + "_rate"); g.nodes.Remove(s + "_pow");
                if (Get(s) > 0) g.nodes[s] = Math.Min(Defs.SKILL_MAX, 1 + sub);
            }
            int bl = Get("bl_range") + Get("bl_spd") + Get("bl_pow");
            foreach (var k in new[] { "bl_range", "bl_spd", "bl_pow", "core_ed", "core_md", "core_ps" }) g.nodes.Remove(k);
            if (Get("bl") > 0) g.nodes["bl"] = Math.Min(Defs.SKILL_MAX, 1 + (bl + 1) / 2);
            bool any = false; foreach (var kv in g.nodes) if (kv.Value > 0) { any = true; break; }
            if (any && Get(Defs.ROOT_NODE) <= 0) g.nodes[Defs.ROOT_NODE] = 1;
            if (g.nodes.Count > 0) AddNotice(Loc.T("균사 트리가 바뀌었어요. 스킬 강화는 스킬 노드의 레벨로 합쳤어요"));
        }

        // v6: 꼬마버섯 세계수 (농장 개편). 나무 Lv → 성장 단계(최대 8, 나이테 Lv.1), 경험치는 그대로.
        // 건물·꼬마 별 등급·부탁·호감도 하트·진화는 없어졌다 → 건물에 쓴 균사석과 꼬마 별에 쓴 골드·균사석을 돌려준다
        static readonly Dictionary<string, int> RETIRED_BUILDING_GEM = new Dictionary<string, int>
        {
            ["dc_table"] = 20, ["dc_lamp"] = 15, ["dc_bed"] = 15, ["dc_fire"] = 25, ["dc_swing"] = 30, ["dc_well"] = 40, ["dc_house"] = 50, ["dc_statue"] = 80,
        };
        static readonly double[] RETIRED_CSTAR_GOLD = { 10000, 100000, 1000000, 10000000, 100000000 };
        static readonly int[] RETIRED_CSTAR_GEM = { 5, 10, 20, 40, 80 };

        // v7: 튜토리얼·단계별 기능 해금. 이미 하던 세이브는 튜토리얼을 건너뛰고 기능을 모두 연다
        static void ToV7(SaveData g)
        {
            if (g.rounds <= 0) return;
            g.tut = 3;
            Unlock.OpenAll(g);
        }

        // v8 (밸런스 3회차): 군락 수·라운드 시간 노드가 4레벨로 늘고 기본값이 줄었다 (군락 12 → 4, 기본 시간 15 → 10초).
        // 옛 레벨은 효과가 줄지 않는 가장 낮은 새 레벨로 옮긴다: 0·1 → 3, 2·3 → 4. 새로 생긴 희귀 포자는 7스테이지를 넘긴 세이브면 다 산 것으로 (지금과 같은 등급 확률)
        static readonly int[] V8_LEVEL = { 3, 3, 4, 4 };
        static void ToV8(SaveData g)
        {
            if (g.rounds <= 0) return;
            foreach (var id in new[] { "ed_cols", "ps_dur" })
            {
                g.nodes.TryGetValue(id, out int L);
                g.nodes[id] = V8_LEVEL[Math.Max(0, Math.Min(V8_LEVEL.Length - 1, L))];
            }
            if (g.stage >= 7) g.nodes["ed_rare0"] = 3;
        }

        // v9 (5회차 P-2): 튜토리얼 T1~T4 4판이 스테이지 앞에 붙었다. G.stage는 내부 판 번호라 표시 스테이지 = stage − 4 (v10부터 2).
        // 튜토리얼 판(1스테이지)을 이미 지난 세이브는 stage를 그때 튜토리얼 판 수(V9_TUT_ROUNDS = 4)만큼 올려 같은 콘텐츠(보스·지역·체력) 위치를 지킨다.
        // (예: 옛 10스테이지 보스 앞 → 내부 14 = 표시 10). 스테이지 번호를 담은 다른 세이브 필드는 없다 (lastNormalGold·rec는 골드·기록 값)
        static void ToV9(SaveData g)
        {
            if (g.stage <= 1 && g.tut < 3) return;   // 아직 첫 판(튜토리얼) 전: 새 흐름대로 T1부터
            g.stage += V9_TUT_ROUNDS;
        }
        const int V9_TUT_ROUNDS = 4;   // v9 때 튜토리얼 판 수 (지금 Defs.TUT_ROUNDS와 다를 수 있어 따로 둔다)

        // v10 (사용자 2026-10-23): 튜토리얼이 4판 → 2판. 튜토리얼을 지난 세이브는 같은 콘텐츠(보스·지역·체력) 위치를 지키도록 판 번호를 2 내린다.
        // 옛 T3·T4(콘텐츠 1)는 새 내부 3판(표시 1)으로
        static void ToV10(SaveData g)
        {
            int cut = V9_TUT_ROUNDS - Defs.TUT_ROUNDS;
            if (g.stage > V9_TUT_ROUNDS) g.stage -= cut;
            else if (g.stage > Defs.TUT_ROUNDS) g.stage = Defs.TUT_ROUNDS + 1;
        }

        // v11 (균사 트리 A안, 2026-10-19): 예전 트리 노드가 모두 바뀌었다. 예전 노드에 쓴 골드·균사석을 돌려주고 지운다.
        // 사냥 준비(power, 예전 기운 북돋기)는 1레벨만 남기고 2~4레벨 값을 돌려준다. 세계수 노드(md_chef·gm_farm·gm_spore·gm_herb·gm_gourmet)는 그대로.
        // 가격표는 예전 DB(MMM_DB_Ver.05)의 Upgrade_Cost (gold, gem 레벨별)
        static readonly Dictionary<string, (double[] gold, double[] gem)> RETIRED_V11 = new Dictionary<string, (double[], double[])>
        {
            ["ed_score"] = (new double[] { 16, 60, 230 }, null),
            ["ed_cols"] = (new double[] { 22, 84, 250, 650 }, null),
            ["ed_regen"] = (new double[] { 280, 700, 1700 }, null),
            ["ed_price"] = (new double[] { 40, 150, 380 }, null),
            ["ed_bonus"] = (new double[] { 550, 1300, 2900 }, null),
            ["ed_score1p"] = (new double[] { 4500, 17000, 64000 }, null),
            ["ed_bonus1p"] = (new double[] { 9600, 32000, 110000 }, null),
            ["ed_score2"] = (new double[] { 210000, 770000, 2900000 }, null),
            ["ed_bonus2"] = (new double[] { 370000, 1400000, 5200000 }, null),
            ["ed_score2p"] = (new double[] { 11000000, 42000000, 150000000 }, null),
            ["ed_bonus2p"] = (new double[] { 19000000, 72000000, 270000000 }, null),
            ["ed_cols2"] = (new double[] { 4100000, 15000000 }, null),
            ["ed_price2"] = (new double[] { 13000000, 50000000 }, null),
            ["ed_rare"] = (new double[] { 720000, 2600000, 9400000 }, null),
            ["ed_size"] = (new double[] { 410000, 1300000, 5000000 }, null),
            ["ed_score3"] = (new double[] { 940000000, 3500000000, 13000000000 }, null),
            ["ed_bonus3"] = (new double[] { 1700000000, 6100000000, 23000000000 }, null),
            ["ed_score3p"] = (new double[] { 50000000000, 190000000000, 660000000000 }, null),
            ["ed_bonus3p"] = (new double[] { 82000000000, 310000000000, 1200000000000 }, null),
            ["ed_trade"] = (new double[] { 120000000000, 430000000000 }, null),
            ["ed_gold"] = (new double[] { 120000000000, 430000000000 }, null),
            ["ed_giant"] = (new double[] { 30000000000, 120000000000 }, null),
            ["ed_multi"] = (new double[] { 370000000000, 600000000000 }, null),
            ["md_map"] = (new double[] { 340, 750, 1700, 4000 }, null),
            ["md_bar"] = (new double[] { 55, 200, 440 }, null),
            ["md_combo"] = (new double[] { 260, 600, 1300 }, null),
            ["dev_stump"] = (new double[] { 220 }, null),
            ["dev_moss"] = (new double[] { 420 }, null),
            ["dev_acorn"] = (new double[] { 900 }, null),
            ["md_kid"] = (new double[] { 1300, 3400, 6800 }, null),
            ["md_combo2"] = (new double[] { 2400000, 8800000 }, null),
            ["md_templife"] = (new double[] { 13000000, 50000000 }, null),
            ["md_stump"] = (new double[] { 61000000000, 230000000000 }, null),
            ["accel"] = (new double[] { 700, 2100, 3700, 14000 }, null),
            ["sharpen"] = (new double[] { 1100000, 4100000, 8300000, 31000000 }, null),
            ["split"] = (new double[] { 43000000000, 160000000000, 330000000000, 1300000000000 }, null),
            ["clear"] = (new double[] { 8800000, 33000000, 130000000, 350000000 }, null),
            ["burst"] = (new double[] { 1600, 5000, 10000, 38000 }, null),
            ["clone"] = (new double[] { 370000000000, 600000000000, 600000000000, 1800000000000 }, null),
            ["magnet"] = (new double[] { 7700000, 30000000, 66000000, 250000000 }, null),
            ["bolt"] = (new double[] { 770000000000, 880000000000, 880000000000, 2400000000000 }, null),
            ["tornado"] = (new double[] { 880000000000, 990000000000, 990000000000, 2400000000000 }, null),
            ["shock"] = (new double[] { 600000000000, 770000000000, 770000000000, 2000000000000 }, null),
            ["pierce"] = (new double[] { 1000, 2500, 3700, 14000 }, null),
            ["homing"] = (new double[] { 26000, 52000, 82000, 310000 }, null),
            ["barclone"] = (new double[] { 19000000, 37000000, 61000000, 230000000 }, null),
            ["fest"] = (new double[] { 1200000000000, 1300000000000, 1400000000000, 4100000000000 }, null),
            ["md_buffdur"] = (new double[] { 32000000, 100000000 }, null),
            ["ps_atk"] = (new double[] { 370, 1400, 5200, 20000 }, null),
            ["ps_size"] = (new double[] { 450, 900, 1800 }, null),
            ["ps_spd"] = (new double[] { 480, 960, 1900 }, null),
            ["ps_dur"] = (new double[] { 30, 110, 300, 800 }, null),
            ["ps_ball"] = (new double[] { 2400 }, null),
            ["ps_atk2"] = (new double[] { 14000, 52000, 200000, 720000 }, null),
            ["ps_ball2"] = (new double[] { 61000000 }, null),
            ["ps_atk2p"] = (new double[] { 2700000, 9900000, 37000000 }, null),
            ["ps_dur2"] = (new double[] { 14000000, 52000000 }, null),
            ["ps_crit"] = (new double[] { 1000000, 3900000, 15000000 }, null),
            ["ps_resist"] = (new double[] { 2400000, 8800000 }, null),
            ["ps_rush"] = (new double[] { 16000000, 61000000 }, null),
            ["bl"] = (new double[] { 20000000000, 77000000000, 130000000000, 500000000000 }, null),
            ["ps_bar"] = (new double[] { 11000, 28000, 66000 }, null),
            ["ps_barspd"] = (new double[] { 18000, 43000, 110000 }, null),
            ["ps_bardur"] = (new double[] { 26000, 66000, 180000 }, null),
            ["ps_barrate"] = (new double[] { 2500000, 6100000, 15000000 }, null),
            ["ps_power"] = (new double[] { 13000000, 31000000, 77000000 }, null),
            ["ps_atk3"] = (new double[] { 45000000, 160000000, 600000000, 2300000000 }, null),
            ["ps_atk3p"] = (new double[] { 8300000000, 31000000000, 120000000000 }, null),
            ["ps_atk4"] = (new double[] { 140000000000, 540000000000, 1400000000000 }, null),
            ["ps_ball3"] = (new double[] { 990000000000, 1400000000000 }, null),
            ["ps_crit2"] = (new double[] { 55000000000, 72000000000 }, null),
            ["ps_heavy"] = (new double[] { 5500000000, 22000000000 }, null),
            ["gm_harvest"] = (new double[] { 3000, 6000, 12000 }, new double[] { 10, 20, 40 }),
            ["gm_meteor"] = (new double[] { 5000, 10000, 20000 }, new double[] { 25, 50, 100 }),
            ["gm_toxin"] = (new double[] { 2000, 4000, 8000 }, new double[] { 8, 16, 32 }),
            ["gm_atk"] = (new double[] { 3000, 6000, 12000 }, new double[] { 12, 24, 48 }),
            ["gm_boss"] = (new double[] { 3000, 6000, 12000 }, new double[] { 15, 30, 60 }),
            ["gm_score"] = (new double[] { 3000, 6000, 12000 }, new double[] { 12, 24, 48 }),
            ["gm_bossgem"] = (new double[] { 2500, 5000, 10000 }, new double[] { 10, 20, 40 }),
            ["gm_skill"] = (new double[] { 3000, 6000, 12000 }, new double[] { 12, 24, 48 }),
            ["gm_bar"] = (new double[] { 2500, 5000, 10000 }, new double[] { 8, 16, 32 }),
            ["ed_rare0"] = (new double[] { 320, 800, 2000 }, null),
        };
        static readonly double[] RETIRED_V11_POWER = { 12, 46, 170, 510 };
        const double RETIRED_V11_SINK0 = 3630000000000, RETIRED_V11_SINK_K = 1.076;   // 끝없는 투지·끝없는 풍년: Cost0 × K^L

        static readonly Dictionary<string, (double c0, double k)> RETIRED_V11_INF9 = new Dictionary<string, (double, double)>
        {
            ["inf_sdmg"] = (3e12, 1.10), ["inf_srate"] = (1e13, 1.12), ["inf_spd"] = (1e12, 1.10), ["inf_ball"] = (2e13, 2),
        };

        static void ToV11(SaveData g)
        {
            double gold = 0, gem = 0;
            foreach (var kv in RETIRED_V11)
            {
                if (!g.nodes.TryGetValue(kv.Key, out int L)) continue;
                for (int i = 0; i < Math.Min(L, kv.Value.gold.Length); i++)
                {
                    gold += Math.Ceiling(kv.Value.gold[i] * Defs.COST_MUL);
                    if (kv.Value.gem != null) gem += Math.Ceiling(kv.Value.gem[i] * Defs.COST_MUL);
                }
                g.nodes.Remove(kv.Key);
            }
            foreach (var id in new[] { "inf_atk", "inf_gold" })
            {
                if (!g.nodes.TryGetValue(id, out int L)) continue;
                for (int i = 0; i < Math.Min(L, 20000) && !double.IsInfinity(gold); i++) gold += Math.Ceiling(RETIRED_V11_SINK0 * Math.Pow(RETIRED_V11_SINK_K, i) * Defs.COST_MUL);
                g.nodes.Remove(id);
            }
            if (g.nodes.TryGetValue(Defs.ROOT_NODE, out int p) && p > 1)
            {
                for (int i = 1; i < Math.Min(p, RETIRED_V11_POWER.Length); i++) gold += Math.Ceiling(RETIRED_V11_POWER[i] * Defs.COST_MUL);
                g.nodes[Defs.ROOT_NODE] = 1;
            }
            if (double.IsInfinity(gold) || double.IsNaN(gold)) gold = double.MaxValue / 4;
            g.gold += gold; g.gem += gem;
            FitHarvestersToSlots(g);   // 장착 칸이 장인의 도구(균사석)로 바뀌었다
            if (gold > 0 || gem > 0)
                AddNotice(Loc.T("균사 트리가 새로 바뀌었어요!") + "\n" + Loc.F("예전 트리에 쓴 골드 {0}·균사석 {1}을 돌려받았어요", U.Fmt(gold), U.Fmt(gem)));
        }

        static void ToV6(SaveData g)
        {
            var f = g.farm;
            if (f.legacyTree != null)
            {
                f.stage = Math.Max(1, Math.Min(Defs.WT.stages, f.legacyTree.lv));
                f.ring = 1;
                if (f.legacyTree.exp > 0 && !double.IsInfinity(f.legacyTree.exp)) f.exp = f.legacyTree.exp;
            }
            double gem = 0, gold = 0;
            if (f.legacyBlds != null)
                foreach (var b in f.legacyBlds) if (b?.id != null && RETIRED_BUILDING_GEM.TryGetValue(b.id, out var p)) gem += p;
            if (f.legacyStar != null)
                foreach (var kv in f.legacyStar)
                    for (int i = 0; i < Math.Min(kv.Value, RETIRED_CSTAR_GOLD.Length); i++) { gold += RETIRED_CSTAR_GOLD[i]; gem += RETIRED_CSTAR_GEM[i]; }
            g.gem += gem; g.gold += gold;
            // 이미 있던 꼬마는 선호 구역(열렸고 자리가 있으면)으로 보낸다
            foreach (var k in Defs.KIDS)
            {
                if (!g.specials.TryGetValue(k.id, out var has) || !has || f.zone.ContainsKey(k.id) || f.stage < Defs.ZONE[k.zone].open) continue;
                int inZone = 0; foreach (var z in f.zone.Values) if (z == k.zone) inZone++;
                if (inZone < Defs.HARV.ZoneCap(f.stage)) f.zone[k.id] = k.zone;
            }
            if (g.legacyAutoT > 0 && g.legacyAutoT < Game.Now()) f.bag.t = g.legacyAutoT;  // 예전 자동 수확 시계부터 보따리가 쌓인 것으로 (꼬마는 FarmEnsure가 합류·배치)
            f.legacyTree = null; f.legacyBlds = null; f.legacyStar = null; g.legacyAutoT = 0;
            if (g.rounds > 0 || gem > 0 || gold > 0)
                AddNotice(Loc.T("농장이 꼬마버섯 세계수로 바뀌었어요! 꼬마를 구역에 보내 나무버섯을 모아 보세요")
                    + (gem > 0 || gold > 0 ? "\n" + Loc.F("건물·꼬마 별에 쓴 균사석 {0}·골드 {1}을 돌려받았어요", U.Fmt(gem), U.Fmt(gold)) : ""));
        }

        static double Refund(SaveData g, Dictionary<string, double[]> retired)
        {
            double refund = 0;
            foreach (var kv in retired)
            {
                if (!g.nodes.TryGetValue(kv.Key, out int L)) continue;
                for (int i = 0; i < Math.Min(L, kv.Value.Length); i++) refund += Math.Ceiling(kv.Value[i] * Defs.COST_MUL);
                g.nodes.Remove(kv.Key);
            }
            g.gold += refund;
            return refund;
        }

        // 예전엔 수확기를 모두 켤 수 있었다. 장착 칸(영구 핀볼 수)을 넘으면 별이 높은 것부터 칸만큼만 남기고 끈다
        static void FitHarvestersToSlots(SaveData g)
        {
            int Lv(string id) => g.nodes.TryGetValue(id, out var L) ? L : 0;
            int slots = 1 + Lv("ps_tools");   // 장인의 도구 (균사 트리 A안. 예전엔 ps_ball·ps_ball2·ps_ball3)
            var on = new List<string>();
            foreach (var h in Defs.HARVESTERS)
                if (g.hv.ContainsKey(h.id) && (!g.hvOn.TryGetValue(h.id, out var o) || o)) on.Add(h.id);
            if (on.Count <= slots) return;
            on.Sort((a, b) => g.hv[b].CompareTo(g.hv[a]));   // List.Sort는 불안정하지만 같은 별끼리 순서는 상관없다
            for (int i = slots; i < on.Count; i++) g.hvOn[on[i]] = false;
            AddNotice(Loc.F("수확기 장착 칸이 생겼어요. 별이 높은 수확기 {0}개만 켜 두었어요 (공방에서 바꿀 수 있어요)", slots));
        }

        static void AddNotice(string s) => Notice = string.IsNullOrEmpty(Notice) ? s : Notice + "\n" + s;
    }
}
