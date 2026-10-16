using System.Collections.Generic;

namespace MoreMush
{
    // Which icon says what a node or skill does (Dream set 2026-10-15, sheets icons_node / icons_skill).
    // Art mapping only: the DB Icon column is the fallback for anything not listed here.
    public static partial class Defs
    {
        // node id without its tier suffix (ed_score2p → ed_score) → icon key under Icons/
        static readonly Dictionary<string, string> NODE_ICON = new Dictionary<string, string>
        {
            ["ed_score"] = "n_score", ["ed_bonus"] = "n_bonus", ["ed_cols"] = "n_cols", ["ed_size"] = "n_size",
            ["ed_regen"] = "n_regen", ["ed_price"] = "n_price", ["ed_rare"] = "n_rare", ["ed_trade"] = "n_trade",
            ["ed_gold"] = "n_gold", ["ed_giant"] = "n_giant", ["ed_multi"] = "n_multi",
            ["md_map"] = "n_map", ["md_bar"] = "n_bar", ["ps_bar"] = "n_bar", ["md_combo"] = "n_combo",
            ["md_templife"] = "n_templife", ["md_chef"] = "n_chef", ["md_stump"] = "n_stump", ["md_buffdur"] = "n_buffdur",
            ["dev_stump"] = "n_dev_stump", ["dev_moss"] = "n_dev_moss", ["dev_acorn"] = "n_dev_acorn", ["md_kid"] = "s_kid",
            ["ps_atk"] = "n_atk", ["ps_size"] = "n_hvsize", ["ps_spd"] = "n_spd", ["ps_dur"] = "n_dur", ["ps_ball"] = "n_ball",
            ["ps_crit"] = "n_crit", ["ps_resist"] = "n_resist", ["ps_rush"] = "n_rush", ["ps_barspd"] = "n_barspd",
            ["ps_bardur"] = "n_bardur", ["ps_barrate"] = "n_barrate", ["ps_power"] = "s_power", ["ps_heavy"] = "n_heavy",
            ["inf_atk"] = "n_inf_atk", ["inf_gold"] = "n_inf_gold", ["bl"] = "s_blade",
        };
        // ids whose suffix is not a tier (치명타 피해 is its own stat, not 치명타 II)
        static readonly Dictionary<string, string> NODE_ICON_EXACT = new Dictionary<string, string> { ["ps_crit2"] = "n_crit2" };
        // skills that borrowed another skill's icon in the DB
        static readonly Dictionary<string, string> SKILL_ICON = new Dictionary<string, string>
        {
            ["homing"] = "s_homing", ["power"] = "s_power", ["barclone"] = "s_barclone",
        };

        static readonly Dictionary<string, string> nodeIconCache = new Dictionary<string, string>();

        // Icons/ key for a node's inner picture, or null to keep the branch mushroom
        public static string NodeIcon(Node n)
        {
            if (nodeIconCache.TryGetValue(n.id, out var hit)) return hit;
            string key = null;
            if (NODE_ICON_EXACT.TryGetValue(n.id, out var exact)) key = exact;
            else
            {
                string id = n.id;
                if (id.EndsWith("p") && id.Length > 1 && char.IsDigit(id[id.Length - 2])) id = id.Substring(0, id.Length - 1);
                id = id.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (NODE_ICON.TryGetValue(id, out var k)) key = k;
            }
            key = key ?? n.icon ?? (n.skill != null ? SKILLS[n.skill].icon : null);
            nodeIconCache[n.id] = key;
            return key;
        }

        static string SkillIcon(string id, string dbIcon) => SKILL_ICON.TryGetValue(id, out var k) ? k : dbIcon;
    }
}
