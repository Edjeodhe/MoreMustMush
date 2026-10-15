// "Soft watercolor" art set (resource_test branch) for Tools/art/build-art.mjs --set soft.
// Raw sheets: Art/Raw_Soft/<id>.png. Sliced sprites: Assets/Art/Soft/<same keys as Assets/Art/Generated>.
// SpriteDB prefers Soft over Generated, so every key drawn here replaces the old sprite in game.
// Item names may hold a sub path ("Farm/Icons/req_water") so one sheet can feed several folders (out: "").

import { SHEETS as OLD, MUSHROOMS, CRITTERS } from "./sheets.mjs";

// Old item descriptions by "<out>/<name>" so the subjects stay the same and only the style changes.
const DESC = {};
for (const s of OLD) for (const [name, desc] of s.items || []) DESC[`${s.out}/${name}`] = desc;
const d = (key) => {
  if (!(key in DESC)) throw new Error(`no old description for ${key}`);
  return [key, DESC[key]];
};

// Every sprite sheet shares the approved pilot sheet as its style reference.
const PILOT = "Art/Raw_Soft/pilot.png";
const CRITTER_REF = "Art/StyleRef/critter.png";
const BG_REF = "Art/StyleRef/background.png";

const CAT_NAME = { ed: "edible", md: "medicinal", ps: "poisonous" };

function mushroomSheet(cat, kind) {
  const single = kind === "single";
  return {
    id: `mush_${kind}_${cat}`, rows: 5, cols: 5, out: `Mushrooms/${single ? "Single" : "Colony"}`, search: true, refs: [PILOT],
    subject: single
      ? `25 different real ${CAT_NAME[cat]} mushroom species, one single mushroom (or its natural small group) per cell, drawn as cute rounded encyclopedia icons that still show the species' true shape and colors clearly.`
      : `25 mushroom colonies of different species: each cell is a big chunky round heap of MANY mushrooms of that one species packed tightly on a small mound of soil and moss. Massiveness and cuteness matter more than detail.`,
    items: MUSHROOMS[cat].map(([id, kr, sci, look]) => [id, `${kr} (${sci}): ${look}`]),
  };
}

const DECO_LOOK = [
  ["leaf_lamp", "a lantern made of curled green leaves glowing warm yellow, hanging from a short vine loop"],
  ["leaf_crown", "a round flower wreath crown of small pink and white flowers and leaves"],
  ["leaf_house", "a small wooden birdhouse with a leaf roof and a round hole"],
  ["dew_chime", "a wind chime of hanging clear blue water-drop crystals under a leaf"],
  ["dew_spring", "a small mossy stone spring with clear water bubbling up"],
  ["dew_pond", "a small round pond with two pink water lilies and lily pads"],
  ["wind_catcher", "a dream catcher: a willow hoop with a web and hanging white feathers"],
  ["wind_mill", "a cute paper pinwheel on a stick, mint and white"],
  ["wind_swing", "a little wooden seat swing hanging on two green vines with leaves"],
  ["fire_brazier", "a round stone brazier with a cozy orange fire"],
  ["fire_lantern", "a red paper lantern with a warm flame glow, hanging from a hook"],
  ["fire_candle", "a fat candle shaped like a mushroom with a small flame on top"],
  ["rock_table", "a flat stone table with a mushroom-cap top and two pebble stools"],
  ["rock_moss", "a round grey boulder covered with soft green moss and a tiny sprout"],
  ["rock_geode", "a split-open geode rock full of purple amethyst crystals"],
  ["spark_orb", "a little brass lightning-rod post holding a glowing yellow orb with a tiny spark"],
  ["spark_lamp", "a golden crystal lamp on a short stand, glowing yellow"],
  ["spark_bell", "a shiny brass bell hanging from a ribbon"],
  ["moon_crescent", "a hanging crescent moon ornament, pale yellow, with a tiny star on a string"],
  ["moon_garland", "a garland string of little glowing stars and moons"],
  ["moon_owl", "a cute carved wooden owl figure on a small stump"],
  ["star_rune", "a small rounded standing stone with a glowing violet star rune carved in it"],
  ["star_crown", "a little golden crown with violet gems, hanging from a ribbon"],
  ["star_orb", "a glowing pastel violet orb of light in a small leafy holder, hanging"],
];

const BOSS_LOOK = [
  ["king", "King Matsutake: a giant thick-stemmed matsutake mushroom monster with a brown fibrous cap wearing a small gold crown, grumpy round eyes, tiny stubby arms"],
  ["cleaner", "the Forest Cleaner: a big round tan fairy-ring mushroom monster carrying a little broom made of twigs, a ring of tiny tan mushrooms around its feet, sleepy eyes"],
  ["angel", "the Death Angel: a tall pure white destroying-angel amanita monster with a white skirt ring like a robe, a faint halo and small wing-like gills, calm half-closed eyes"],
  ["phantom", "the Phantom Mushroom: a big red fly agaric monster with white warts, a wobbly ghostly see-through lower body and mischievous eyes"],
  ["mover", "the Moving Mushroom: a bright orange cordyceps creature with several club-shaped orange stalks for a head, walking on little root legs"],
];

export const SHEETS = [
  ...["ed", "md", "ps"].map((c) => mushroomSheet(c, "single")),
  ...["ed", "md", "ps"].map((c) => mushroomSheet(c, "colony")),

  {
    id: "icons_a", rows: 6, cols: 6, out: "", refs: [PILOT],
    subject: "Square game UI icons (currencies, categories, regions, weather), each a single bold rounded object centered in its cell, consistent size, readable at 32 px.",
    items: [
      ...["gold", "ed", "md", "ps", "gem", "spore", "clock"].map((n) => d(`Icons/${n}`)),
      ...["dia", "hammer", "auto_harvest", "build"].map((n) => d(`Icons/${n}`)),
      ...["trophy", "lock", "book", "pause", "star", "medal", "core_ed", "core_md", "core_ps"].map((n) => d(`Icons/${n}`)),
      ...["t_forest", "t_night", "t_field", "t_sea", "t_ruins"].map((n) => d(`Icons/${n}`)),
      ...["w_clear", "w_rain", "w_moon", "w_storm", "w_wind", "w_cold", "w_autumn", "w_thunder", "w_rainbow", "w_fog", "w_drought"].map((n) => d(`Icons/${n}`)),
    ],
  },
  {
    id: "icons_b", rows: 6, cols: 5, out: "", refs: [PILOT],
    subject: "Square game skill icons and small farm request / emotion icons, each a single bold rounded object centered in its cell, consistent size, readable at 32 px.",
    items: [
      ...["s_accel", "s_sharpen", "s_split", "s_sip", "s_clear", "s_burst", "s_tspore", "s_clone", "s_magnet", "s_bolt", "s_tornado",
        "s_shock", "s_device", "s_pierce", "s_fest", "s_blade", "s_kid"].map((n) => d(`Icons/${n}`)),
      ["Icons/s_homing", "homing: a glowing arrow curving around to hit a small round target"],
      ["Icons/s_power", "power up: a flexed little arm with a sparkle and an upward arrow"],
      ["Icons/s_barclone", "bar clone: two short wooden log paddles, one solid and one ghostly see-through"],
      ...["req_water", "req_pet", "req_song", "req_snack", "req_gift", "fx_heart", "fx_zzz", "fx_surprise", "fx_note"].map((n) => d(`Farm/Icons/${n}`)),
    ],
  },
  {
    id: "harvesters", rows: 4, cols: 5, out: "", refs: [PILOT],
    subject: "Pinball 'harvester' weapon tokens for a mushroom-harvesting pinball game, seen from above. Each reads as a compact, roughly round spinning token (it rotates while it flies), chunky and readable at 40 px. Skins (hs_) are exact recolors of the matching base token. Plus two small effect sprites.",
    items: [
      d("Harvesters/sam"), d("Harvesters/saw"),
      ["Harvesters/hoe", "herb hoe harvester: a short wooden handle with a black-iron hoe blade whose tip is pointed like an awl, coiled into a round spinning shape"],
      ...["mill", "spore", "bell", "coin", "gold"].map((n) => d(`Harvesters/${n}`)),
      ...["hs_sam", "hs_saw", "hs_mill", "hs_spore", "hs_bell", "hs_coin", "hs_gold"].map((n) => d(`Harvesters/${n}`)),
      d("Harvesters/cloud_spore"), d("Harvesters/puff_dust"),
    ],
  },
  {
    id: "props", rows: 4, cols: 5, out: "", refs: [PILOT],
    subject: "Top-down game field props and particles for a sunny forest pinball field (seen from directly above), plus three mushroom clusters that grow on a tree branch.",
    items: [
      ...["stump", "mole", "acorn_off", "acorn_on", "moss", "stream", "mound", "warn_shadow", "clover", "flowers", "pebbles", "leaf",
        "snowflake", "sparkle", "sparkle_gold", "crack"].map((n) => d(`Props/${n}`)),
      ...["fruit_ed", "fruit_md", "fruit_ps"].map((n) => d(`Farm/Tree/${n}`)),
    ],
  },
  {
    id: "decos", rows: 5, cols: 5, out: "Farm/Props", refs: [PILOT],
    subject: "Decorations for a cozy mushroom world-tree garden: 24 small ornaments to hang on branches or set around the roots, front view, same scale, plus one large reward illustration in the last cell.",
    items: [...DECO_LOOK, ["auto_reward", d("Farm/Props/auto_reward")[1]]],
  },
  {
    id: "ui_kit", rows: 3, cols: 3, out: "UI", refs: [PILOT, "Art/StyleRef/logo.png"],
    subject: "A UI kit of EMPTY interface frames and buttons (no icons, no symbols, no text inside them) for a bright, cute, rounded watercolor game interface, drawn flat and front-facing. Panels and buttons have even, uniform rounded borders on all sides so they can be 9-sliced. Light, airy fills so dark brown text stays readable on them.",
    items: [
      ["panel_wood", "a square panel: soft rounded warm-brown wooden frame border around a light cream paper center"],
      ["panel_paper", "a plain square light cream paper card with a thin soft tan rounded border"],
      ["btn_wood", "a chubby rounded-rectangle button in light cream with a soft brown outline and a slightly darker bottom edge"],
      ["btn_go", "a chubby rounded-rectangle button in butter yellow with a soft brown outline and a slightly darker bottom edge"],
      ["btn_danger", "a chubby rounded-rectangle button in coral pink-red with a soft brown outline and a slightly darker bottom edge"],
      ["badge_round", "a round badge: soft wooden ring around a cream center"],
      ["bar_log", "a long thin horizontal rounded wooden log platform with a small green leaf at each end (very wide and short)"],
      ["sign_arrow", "a rounded wooden signpost arrow board pointing right"],
      ["basket", "a round woven harvest basket seen from the front, open top"],
    ],
  },
  {
    id: "bosses", rows: 3, cols: 2, out: "Bosses", refs: [PILOT, CRITTER_REF],
    subject: "Five big stage-boss mushroom monsters for a cute mushroom game, front view, each one big character filling its cell, 3 rows of 2. Cute-but-boss: chunky, rounded, a little menacing in a friendly way. Leave the last cell empty.",
    items: BOSS_LOOK,
  },
  {
    id: "mush_tree", rows: 2, cols: 4, out: "Farm/Tree", refs: [PILOT, BG_REF],
    subject: "Eight growth stages of the SAME magical mushroom world-tree, front view, each standing on a small round grass base, left to right and top row first, from smallest to largest; it clearly grows bigger, lusher and more magical. Rounded soft trunk and puffy cloud-like green canopy. Do NOT draw mushrooms on the branches (they are added separately); a few tiny shelf fungi low on the trunk are fine.",
    items: [
      ["tree_0", "stage 1: a tiny sprout with two round leaves"],
      ["tree_1", "stage 2: a small sapling with a thin trunk and a few leaf puffs"],
      ["tree_2", "stage 3: a young tree with a slim trunk and a small round canopy"],
      ["tree_3", "stage 4: a medium tree with a sturdier trunk and a fuller canopy"],
      ["tree_4", "stage 5: a large tree with a thick trunk, visible roots and a big lush canopy"],
      ["tree_5", "stage 6: a big tree with a mossy trunk, roots and a layered canopy with a few flowers"],
      ["tree_6", "stage 7: a grand tree with a very thick mossy trunk, big roots and a huge layered canopy sprinkled with tiny glowing spores"],
      ["tree_7", "stage 8: a majestic ancient world-tree, the biggest, with a huge layered canopy, gentle golden glow and floating spores"],
    ],
  },

  // ===== backgrounds (one image each). bg_forest is the approved pilot background =====
  {
    id: "bg_forest", kind: "image", out: "Backgrounds/bg_forest.png", copyOf: "Art/Raw_Soft/pilot_bg.png",
  },
  ...[
    ["bg_night", "a moonlit night forest-floor pinball field: deep blue-green grass under soft moonlight, a few softly glowing mushrooms and fireflies at the edges, framed by rounded dark bushes and stones; the middle a bit brighter and plain"],
    ["bg_field", "a breezy open meadow pinball field: light green grass with soft flower patches (white daisies, yellow buttercups) and a faint ring of tiny mushrooms, framed by rounded bushes and tall grass tufts"],
    ["bg_sea", "a sunny beach pinball field: soft sand with little shells and starfish, a strip of turquoise shallow water with foam along the top edge, framed by rounded rocks and beach grass"],
    ["bg_ruins", "a mushroom-ruins pinball field: old cracked cobblestone plaza overgrown with moss, framed by broken rounded stone walls and giant mushroom stalks at the edges, soft warm late-afternoon light"],
  ].map(([id, look]) => ({
    id, kind: "image", out: `Backgrounds/${id}.png`, refs: ["Art/Raw_Soft/pilot_bg.png"],
    prompt: `Wide landscape 16:9 game background, top-down view: ${look}. A thin border (about 4% of the width) frames all four edges. The middle is calm and fairly plain so small game objects read clearly. Same art style, framing and level of detail as the attached forest background. No characters, no text, no UI.`,
  })),
  {
    id: "bg_title", kind: "image", out: "Backgrounds/bg_title.png", refs: [BG_REF],
    prompt: "Wide landscape 16:9 game title background: a sunny valley like the attached reference, with rolling green hills, puffy white clouds and a blue sky, and a cozy little village in the distance overrun by giant cute mushrooms (a red one with white dots, a pink one, an orange one, a lilac one) towering over the houses, spores drifting in the air. Keep the lower-left corner and the center fairly open for a character and a title logo. No text, no characters.",
  },
  {
    id: "bg_ranch", kind: "image", out: "Backgrounds/bg_ranch.png", refs: [BG_REF],
    prompt: "Wide landscape 16:9 game background, slight top-down 3/4 view: a cozy mushroom-village garden yard where a big magical tree will stand. A big grassy area enclosed by a low rounded wooden fence fills the middle (roughly 13% to 87% of the width and 28% to 84% of the height), with a dirt path running from the bottom edge into it. Behind the fence at the top: rolling hills, a few giant cute mushrooms, puffy clouds and a blue sky like the attached reference. The inside is calm and fairly plain so small characters read clearly. No characters, no tree in the middle, no text, no UI.",
  },

  // ===== characters =====
  {
    id: "grandpa_full", rows: 1, cols: 1, out: "Characters/Grandpa", refs: [CRITTER_REF, PILOT],
    subject: "One full-body HUMAN character (an old man, NOT a mushroom creature: he has a normal human head and face), front three-quarter view, standing, cute chibi proportions (big round head, small body), drawn in the same soft watercolor style and line as the attached references.",
    items: [["grandpa_ref", "a veteran human mushroom-hunter grandpa with a human face: a round nose, rosy cheeks, bushy grey eyebrows, kind gentle eyes and a smiling mouth under a big braided grey beard; worn brown leather hunting hat with a feather, a braided grey beard, a long olive coat with a fur collar, a bandolier of little spore vials across the chest, a big scythe held in his RIGHT hand (viewer's left) resting on his shoulder, the blade high above his head on the other side, sturdy boots"]],
  },
  {
    id: "critters_full", rows: 5, cols: 2, out: "Characters/Critters", refs: [CRITTER_REF, PILOT],
    subject: "Ten cute chibi mushroom-critter characters of one family, front view, standing, exactly in the style of the attached red-capped critter: a round cream body/face, tiny dot eyes, a small smile and pink blush, two little stubby feet, and a big soft mushroom cap on top. Same body shape for all; only the cap colors and the decoration differ.",
    items: CRITTERS.map(([id, look]) => [`${id}_ref`, look]),
  },
  {
    id: "critter_parts", rows: 4, cols: 5, out: "Characters/Critters", refs: ["Art/Raw_Soft/critters_full.png"],
    subject: "Cut-out animation parts for the mushroom critters in the attached reference, all at the same scale, front view, each part complete and whole (parts will be layered, so draw hidden areas too). Caps include their decoration. Body parts are shared by all critters.",
    items: [
      ...CRITTERS.map(([id, look]) => [`cap_${id}`, `the mushroom cap only: ${look}`]),
      ["body", "the shared round cream face/body WITHOUT eyes, mouth or blush"],
      ["eyes_open", "a pair of tiny dark dot/oval eyes, spaced as on the reference face"],
      ["eyes_closed", "a pair of closed happy eyes (^ ^), spaced as on the reference face"],
      ["eyes_x", "a pair of dizzy swirl eyes"],
      ["mouth_smile", "a small curved smiling mouth"],
      ["mouth_open", "a small open 'o' mouth"],
      ["blush", "a pair of pink cheek blush ovals"],
      ["foot", "one little stubby cream foot"],
      ["sweat", "a small sweat drop"],
      ["heart", "a small pink heart"],
    ],
  },
  {
    id: "critter_skin_caps", rows: 2, cols: 5, out: "Characters/Critters", refs: ["Art/Raw_Soft/critter_parts.png"],
    subject: "Recolored skin versions of the mushroom caps in the attached critter parts sheet. Exactly the same cap shape, size and line style as the caps there, only the colors and pattern change, and NO decoration on top.",
    items: OLD.find((s) => s.id === "critter_skin_caps").items,
  },
  {
    id: "critter_acc", rows: 4, cols: 4, out: "Characters/Critters/Acc", refs: ["Art/Raw_Soft/critters_full.png"],
    subject: "Accessories and hats sized to fit the mushroom critters in the attached reference, each drawn alone in front view, to be layered on a critter's cap or face.",
    items: [...OLD.find((s) => s.id === "critter_acc").items, ["hardhat", "a small yellow construction hard hat with a front brim"]],
  },
  {
    id: "critter_build", rows: 4, cols: 5, out: "Characters/Critters/Build", refs: ["Art/Raw_Soft/critters_full.png"],
    subject: "Two-frame hammering animation for the ten mushroom critters in the attached reference, working as tiny builders. Keep each critter's body, face, cap shape, cap colors, spots and cap decoration exactly as in the reference, at the same scale for all. Every critter: three-quarter side view facing RIGHT, standing on its two little feet, two tiny stubby cream nub arms, a small yellow hard hat worn tilted on the back of the cap. Rows 1-2 (frame A): a wooden-handled hammer held RAISED HIGH above and behind the head. Rows 3-4 (frame B): the SAME critter in the same cell position and size, hammer just SWUNG DOWN to the right, head of the hammer at foot level in front of the feet. Nothing else: no ground, no planks, no effects.",
    items: [...CRITTERS.map(([id, look]) => [`build_${id}_0`, look]), ...CRITTERS.map(([id, look]) => [`build_${id}_1`, `${id} critter again (${look}), hammer swung down`])],
  },
  {
    id: "npcs", rows: 2, cols: 4, out: "Characters/NPC", refs: ["Art/Raw_Soft/grandpa_full.png"],
    subject: "Bust portraits (head and shoulders only, front three-quarter view, friendly smile, modest everyday clothing) of eight villagers of a cozy mushroom village, for a quest board. Same art style as the attached grandpa. Each portrait is a clean cut-out on a fully transparent background: no glow, no backdrop. Wide sheet: 2 rows of 4 portraits.",
    items: OLD.find((s) => s.id === "npcs").items,
  },
];
