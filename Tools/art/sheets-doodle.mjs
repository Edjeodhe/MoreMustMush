// "Doodle cel" art set (resource_test, 2026-10-15 3차) for Tools/art/build-art.mjs --set doodle.
// Objects (mushrooms, characters, weapons, icons, props) in the bold-outline cel style of Doodle Pets: Survival
// (Tools/art-style-doodle.md); backgrounds are pale flat grounds (Tools/art-style-story.md rules, see GROUND)
// with the scenery placed on top as separate env objects (Env/<name>) for depth.
// Raw sheets: Art/Raw_Doodle/<id>.png. Sliced: Assets/Resources/Art/Doodle/<same keys>. SpriteDB: Doodle > Soft > Generated.

import { SHEETS as SOFT } from "./sheets-soft.mjs";

const PILOT = "Art/Raw_Doodle/pilot.png";       // approved object style sample
const UI_REF = "Art/Raw_Doodle/ui_pc.png";       // approved PC UI mockup
const UI_REF2 = "Art/Raw_Doodle/concept/title.png"; // 4th pass layout concept (beige pastel UI + accent buttons)
const raw = (id) => `Art/Raw_Doodle/${id}.png`;

// Object sheets keep the soft set's subjects and items; only the style reference changes.
// Character chains point at this set's own raw sheets.
const REMAP = {
  "Art/Raw_Soft/critters_full.png": raw("critters_full"),
  "Art/Raw_Soft/critter_parts.png": raw("critter_parts"),
  "Art/Raw_Soft/grandpa_full.png": raw("grandpa_full"),
  "Art/Raw_Soft/icons_a.png": raw("icons_a"),
};
const OBJECT_SHEETS = ["mush_single_ed", "mush_single_md", "mush_single_ps", "mush_colony_ed", "mush_colony_md", "mush_colony_ps",
  "icons_a", "icons_b", "harvesters", "props", "decos", "bosses", "mush_tree",
  "grandpa_full", "critters_full", "critter_parts", "critter_skin_caps", "critter_acc", "critter_build", "npcs"];

function fromSoft(id) {
  const s = SOFT.find((x) => x.id === id);
  const refs = (s.refs || []).map((r) => REMAP[r] ?? null).filter(Boolean);
  // first sheets in a chain get the approved pilot as their style reference
  return { ...s, refs: refs.length ? refs : [PILOT], ...(GRID[id] ?? {}) };
}
// layouts the generator actually drew for this set (it often swaps rows and cols)
const GRID = { critters_full: { rows: 2, cols: 5 } };

// Pale flat ground for every play / content area: objects on top must clearly pop.
const GROUND = "flat picture-book ground with a subtle fine grain, PALE and low-saturation, low contrast, very calm and even, only a few very subtle flat patches; no border, no objects, no plants taller than tiny grass ticks, no characters, no text, no UI";

const ENV_RULES = "Environment objects for a cute top-down 3/4-view game scene, each drawn alone, standing on its own base, to be placed separately on a pale ground to build depth. Same bold dark outline and flat cel shading as the attached sample sheet. Same scale family (a big tree is about 3x the height of a bush).";

const CRIT_A = [["fire", "lava: dark red with glowing orange cracks"], ["spark", "neon: hot pink with cyan spots"], ["dew", "snowflake: icy white with pale blue snowflake spots"], ["coin", "rich: deep green with gold spots"], ["star", "galaxy: midnight blue with pale yellow star spots"]];
const CRIT_B = [["leaf", "cherry blossom: pink with white petal spots"], ["moon", "sun: bright orange with pale yellow spots"], ["wind", "cloud: white with light blue spots"], ["rock", "gem: aqua crystal with faceted shine"], ["chest", "strawberry-chocolate: chocolate brown with pink spots"]];
const CRIT_IDS = [...CRIT_A, ...CRIT_B].map(([id]) => id);

export const SHEETS = [
  ...OBJECT_SHEETS.map(fromSoft),
  {
    id: "env_forest", rows: 5, cols: 5, out: "Env", refs: [PILOT],
    subject: ENV_RULES + " Sunny forest set.",
    items: [
      ["tree_big", "a big round-canopy leafy tree with a thick trunk and roots"],
      ["tree_mid", "a medium round leafy tree"],
      ["tree_pine", "a chubby rounded pine tree"],
      ["tree_small", "a small young tree"],
      ["bush_big", "a big round leafy bush"],
      ["bush_mid", "a medium round bush with a few berries"],
      ["bush_small", "a small round bush"],
      ["fern", "a fern clump with curled leaves"],
      ["grass_tall", "a tuft of tall grass blades"],
      ["grass_small", "a small tuft of grass"],
      ["rock_big", "a big rounded grey-blue boulder with a moss patch"],
      ["rock_mid", "two rounded grey-blue rocks with a grass tuft"],
      ["rock_small", "three small pebbles"],
      ["stump", "a cut tree stump with rings and a little sprout"],
      ["log", "a fallen hollow log with moss"],
      ["flowers_white", "a small patch of white daisies with leaves"],
      ["flowers_yellow", "a small patch of yellow buttercups with leaves"],
      ["flowers_pink", "a small patch of pink flowers with leaves"],
      ["clover", "a clump of clover leaves"],
      ["mush_deco", "a tiny cluster of three small brown wild mushrooms (decoration)"],
      ["roots", "a curved exposed tree root arching out of the ground"],
      ["pond", "a small round pond with a lily pad and reeds at the edge"],
      ["fence", "a short straight wooden fence segment (two posts, two rails)"],
      ["fence_post", "a single wooden fence post"],
      ["path", "a small flat patch of light dirt path with two stepping stones"],
    ],
  },
  {
    id: "env_themes", rows: 5, cols: 5, out: "Env", refs: [raw("env_forest")],
    subject: ENV_RULES + " Region sets for a moonlit night forest, a windy meadow, a beach and old mushroom-town ruins.",
    items: [
      ["night_tree", "a dark blue-green round tree with tiny glowing yellow fireflies"],
      ["night_bush", "a deep teal bush with two small glowing blue mushrooms"],
      ["night_glowshroom", "a cluster of glowing pale-blue mushrooms"],
      ["night_rock", "a dark blue rounded rock with a glowing moss patch"],
      ["night_lantern", "a small mushroom-shaped stone lantern with a warm glow"],
      ["field_haybale", "a round golden hay bale"],
      ["field_wheat", "a clump of golden wheat stalks"],
      ["field_sunflower", "two cheerful sunflowers"],
      ["field_scarecrow", "a cute scarecrow wearing a mushroom-cap hat"],
      ["field_windmill", "a tiny wooden pinwheel windmill on a post"],
      ["sea_palm", "a short chubby palm tree"],
      ["sea_rock", "a beach rock with a pink seashell and a starfish"],
      ["sea_coral", "a cluster of pink and orange coral"],
      ["sea_driftwood", "a piece of bleached driftwood with a little seaweed"],
      ["sea_shells", "three seashells on sand"],
      ["ruins_pillar", "a broken stone pillar with moss"],
      ["ruins_wall", "a broken low stone wall chunk with ivy"],
      ["ruins_arch", "a crumbling small stone arch with a red mushroom growing on it"],
      ["ruins_giantshroom", "a giant red-capped mushroom with white spots growing out of rubble"],
      ["ruins_lantern", "an old tilted stone lantern"],
      ["night_flowers", "a patch of glowing magenta flowers"],
      ["field_flowers", "a patch of blue cornflowers and poppies"],
      ["sea_grass", "a tuft of beach grass"],
      ["ruins_rubble", "a pile of stone rubble"],
      ["ruins_bush", "an overgrown bush with ivy over a stone"],
    ],
  },
  ...[
    ["bg_forest", "soft pale mint-green grass"],
    ["bg_night", "soft pale blue-green moonlit grass (still light, only slightly cooler and darker than daytime)"],
    ["bg_field", "soft pale yellow-green meadow grass"],
    ["bg_sea", "soft pale warm sand with a pale turquoise water strip along the very top edge"],
    ["bg_ruins", "soft pale grey-green worn cobblestone plaza with grass between the stones"],
  ].map(([id, look]) => ({
    id, kind: "image", out: `Backgrounds/${id}.png`, refs: ["Art/Raw_Doodle/ground.png"],
    prompt: `Wide landscape 16:9 game background, top-down view of a pinball field ground: ${look}. ${GROUND}. Same style and lightness as the attached ground.`,
  })),
  {
    id: "bg_ranch", kind: "image", out: "Backgrounds/bg_ranch.png", refs: ["Art/Raw_Doodle/ground.png", UI_REF],
    prompt: `Wide landscape 16:9 game background, slight top-down 3/4 view: a pale mint-green meadow for a garden around a big magical tree, with a light sky strip and soft pale distant hills along the top 25%, a low wooden fence line across the middle distance and a light dirt path from the bottom edge to the center. ${GROUND.replace("no border, no objects, ", "")}. No tree in the middle (it is placed separately).`,
  },
  {
    id: "tree_soil", kind: "image", out: "FX/tree_soil.png", refs: ["Art/Raw_Doodle/ground.png"],
    prompt: `Wide landscape 16:9 game background for an upgrade-tree screen: a pale warm beige-green ground cross-section with very faint flat mycelium threads and tiny pale roots, ${GROUND}. Keep it light so dark brown text and outlined icons on top read clearly.`,
  },
  // 4th pass (2026-10-15): one packed UI kit sheet, beige pastel base + brighter pastel-candy accents (Tools/art-style-doodle-ui.md)
  {
    id: "ui_kit2", rows: 5, cols: 5, out: "UI", refs: [UI_REF2, PILOT],
    subject: "A UI kit of EMPTY interface pieces (no icons, no symbols, no text inside) for a cozy PC game, flat front view. Panels and buttons have even, uniform rounded borders on all four sides so they can be 9-sliced. Base pieces are LOW-SATURATION PASTEL BEIGE (oatmeal, cream, latte) with a thin soft warm-brown edge; accent pieces use a different hue that is slightly lighter and more saturated (pastel-candy, not neon), with a darker same-hue thin outline and a darker same-hue bottom lip. Flat fills, tiny soft top highlight, no glow, no gloss, no drop shadow.",
    items: [
      ["panel_wood", "a large window panel: light oatmeal-beige fill, thin warm-brown rounded edge, slightly darker beige inner rim"],
      ["panel_paper", "a flat content card: pale cream fill with a thin sand-colored rounded edge"],
      ["panel_accent", "an important highlight panel: soft butter-yellow (#f7dc7a) fill with a thin golden-brown rounded edge"],
      ["panel_mint", "an important highlight panel: light mint (#bfe8c8) fill with a thin sage-green rounded edge"],
      ["window_title", "a wide short window title strip: latte-beige fill with a thin soft brown edge"],
      ["btn_wood", "a neutral button: light beige fill, thin warm-brown outline, darker beige bottom lip"],
      ["btn_go", "a primary button: fresh sage-leaf green (#7cc46a) fill, darker green outline and bottom lip"],
      ["btn_orange", "a main action button: warm apricot orange (#f4a95a) fill, darker orange outline and bottom lip"],
      ["btn_blue", "an info button: soft sky blue (#78b8e8) fill, darker blue outline and bottom lip"],
      ["btn_danger", "a danger button: soft coral red (#ef7b6c) fill, darker red outline and bottom lip"],
      ["bar_top", "a very wide and short top bar: pale oatmeal-beige fill with a thin soft brown bottom edge"],
      ["rail_item", "a navigation tab: light cream-beige rounded rectangle with a thin soft edge (wide)"],
      ["rail_active", "the same navigation tab but active: sage-leaf green fill with a darker green edge"],
      ["chip", "a small pill-shaped counter chip: cream fill with a thin sand edge (wide and short)"],
      ["badge_round", "a round badge: beige ring with a thin brown outline around a light cream center"],
      ["btn_round", "a small round button: light beige fill, thin warm-brown outline, darker bottom lip"],
      ["btn_close", "a small round coral-red close button with a white X"],
      ["progress_back", "a long thin progress bar track: light latte-beige rounded bar with a thin soft brown edge"],
      ["progress_fill", "a long thin progress bar fill: sage-leaf green rounded bar with a lighter top band"],
      ["tooltip", "a small tooltip card: pale cream fill with a thin warm-brown rounded edge"],
      ["bar_log", "a long thin horizontal rounded wooden paddle with a small green leaf at each end (very wide and short), bold outline"],
      ["sign_arrow", "a rounded wooden arrow sign pointing right, bold outline"],
      ["basket", "a round woven harvest basket seen from the front, open top, bold outline"],
      ["ribbon", "a wide title ribbon banner in soft apricot with folded ends, bold outline"],
      ["divider", "a thin horizontal divider line made of a soft brown vine with two tiny leaves (very wide and short)"],
    ],
  },
  {
    id: "logo", rows: 1, cols: 1, out: "UI", refs: ["Art/Raw_Doodle/concept/title.png"],
    subject: "The game logo alone, exactly like the logo in the attached title screen, large and centered.",
    items: [["logo", "the chunky playful game logo \"More Must Mush\" on two lines: cream letters with a bold dark-brown outline, a red white-dotted mushroom cap sitting on the first M, two small green leaves"]],
  },
  {
    id: "grandpa_rig", rows: 4, cols: 4, out: "Characters/Grandpa/Parts", refs: [raw("grandpa_full"), PILOT],
    subject: "Cut-out animation PARTS of the old human mushroom-hunter grandpa in the attached reference (same design, colors, outline and scale for every part), front view facing slightly right. Each part is drawn COMPLETE and whole, including the areas that would be hidden behind other parts, with rounded joint ends so parts can overlap and rotate without gaps.",
    items: [
      ["hat", "the brown wide-brim hat with the gold buckle band and the red-white feather, alone"],
      ["head", "the head without hat and without beard: face with skin, big round nose, eyebrows, OPEN eyes, ears, grey hair at the sides"],
      ["beard", "the big fluffy white-grey beard with the braid, alone"],
      ["eyes_closed", "a small patch of the face skin with the two eyes closed as happy curves, same size and spacing as on the head"],
      ["mouth_open", "a small open happy mouth with a pink tongue, sized for the beard"],
      ["torso", "the body only: green coat with fur collar, belt with buckle and the potion bandolier, NO arms, NO legs, NO head"],
      ["backpack", "the brown leather backpack alone"],
      ["arm_back", "his left arm: green coat sleeve with fur cuff and an OPEN waving hand, straight, shoulder at the top"],
      ["arm_front", "his right arm: green coat sleeve with fur cuff ending in a closed fist (as if holding a stick), straight, shoulder at the top"],
      ["scythe", "the scythe alone: long wooden handle with the curved steel blade at the top"],
      ["leg_back", "one short leg: dark brown trousers and a brown boot, hip at the top"],
      ["leg_front", "the other short leg: dark brown trousers and a brown boot, hip at the top"],
      ["full", "the whole grandpa fully assembled from these parts, standing, holding the scythe upright in his right fist, left hand waving"],
    ],
  },
  {
    id: "bg_title2", kind: "image", out: "Backgrounds/bg_title.png", refs: ["Art/Raw_Doodle/concept/title.png", "Art/Raw_Doodle/bg_forest.png"],
    prompt: "Wide landscape 16:9 game title background, exactly the scenery of the attached title screen concept WITHOUT the logo, WITHOUT the buttons, WITHOUT the grandpa and WITHOUT the critters: a flat, muted, pale light-green rolling meadow with a pale sky band and soft flat clouds, outlined trees, bushes, rocks, stumps and giant cute mushrooms framing only the left and right edges, the middle calm and open. Same bold-outline cel style for the framing objects, flat muted ground.",
  },
  // 4th pass: redesigned detailed critters (Art/Raw_Doodle/critter2/design_a|b.png, Doodle Pets style) cut into rig parts.
  // One row per critter: cap · body+face · blink patch · arm · foot · skin cap (recolor of the same cap).
  ...[["a", CRIT_A], ["b", CRIT_B]].map(([k, list]) => ({
    id: `critter_rig_${k}`, rows: 5, cols: 6, out: "Characters/Critters", refs: [`Art/Raw_Doodle/critter2/design_${k}.png`],
    subject: "Cut-out animation PARTS of the five mushroom critters in the attached design sheet (same designs, colors, bold outline and scale for every part), front view. ONE ROW PER CRITTER, in this order: (1) the mushroom CAP alone with its decoration and anything worn on the cap, including the cap's underside rim; (2) the BODY alone: the chubby body with its full FACE (open eyes, mouth, cheeks) and its body accessory (scarf, belt, cape, collar), but NO cap, NO arms and NO feet, the top of the body drawn complete because the cap overlaps it; (3) a small BLINK PATCH: the same two eyes CLOSED as happy curves on a patch of that critter's body color, same size and spacing as on the body; (4) ONE stubby ARM (the critter's left arm, shoulder end rounded); (5) ONE little FOOT; (6) a SKIN version of the same cap: exactly the same cap shape and size, recolored as described, WITHOUT the decoration. Each part complete and whole, rounded joint ends.",
    items: list.flatMap(([id, skin]) => [
      [`cap_${id}`, `${id} critter: its cap`], [`body_${id}`, `${id} critter: its body with face`], [`blink_${id}`, `${id} critter: closed-eye patch`],
      [`arm_${id}`, `${id} critter: one arm`], [`foot_${id}`, `${id} critter: one foot`], [`skin_${id}`, `${id} critter: skin cap, ${skin}`],
    ]),
  })),
  {
    id: "critter_build2", rows: 4, cols: 5, out: "Characters/Critters/Build", refs: ["Art/Raw_Doodle/critter2/design_a.png", "Art/Raw_Doodle/critter2/design_b.png"],
    subject: "Two-frame hammering animation for the ten mushroom critters in the attached design sheets, working as tiny builders. Keep each critter's cap, body, face and accessory exactly as designed, same scale for all. Every critter: three-quarter side view facing RIGHT, a small yellow hard hat worn tilted on the back of the cap. Rows 1-2 (frame A, critters in the order fire, spark, dew, coin, star, leaf, moon, wind, rock, chestnut): a wooden hammer held RAISED HIGH above and behind the head. Rows 3-4 (frame B, same order): the SAME critter in the same cell position and size, hammer SWUNG DOWN to the right, hammer head at foot level. Nothing else: no ground, no planks, no effects.",
    items: [...CRIT_IDS.map((id) => [`build_${id}_0`, `${id} critter, hammer raised`]), ...CRIT_IDS.map((id) => [`build_${id}_1`, `${id} critter, hammer swung down`])],
  },
  {
    id: "fix_misc", rows: 2, cols: 3, out: ".", refs: [PILOT, raw("harvesters")],
    subject: "A few missing game assets in the same bold-outline cel style as the attached sheets. Round weapon tokens are spinning harvester discs seen from above, same size family as the attached tokens.",
    items: [
      ["Harvesters/hoe", "herb hoe token: a black-iron hoe blade with an awl-sharp tip curled around a round wooden hub"],
      ["Harvesters/gold", "golden scythe token: a round spinning scythe disc made of shining gold with two sparkles"],
      ["Harvesters/hs_bell", "golden bell token: a shining gold bell with a dark gold cap"],
      ["Harvesters/puff_dust", "a small soft white dust puff with a thin grey outline (effect)"],
      ["Icons/s_magnet", "mushroom magnet icon: a red horseshoe magnet pulling two tiny mushrooms"],
      ["Env/grass_small", "a small tuft of grass"],
    ],
  },
];
