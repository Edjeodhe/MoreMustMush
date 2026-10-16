// "Soft watercolor" art set (resource_test branch) for Tools/art/build-art.mjs --set soft.
// Raw sheets: Art/Raw_Soft/<id>.png. Sliced sprites: Assets/ArtArchive/Soft/<same keys as Assets/ArtArchive/Generated>.
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

// Shared rules for the redesigned icon family (icons_a, icons_b) so every icon reads as one set.
const ICON_RULES = "Game icons drawn as ONE family: each icon is a single chunky, rounded object centered in its cell, filling about 80% of it, front or slight 3/4 view, all the same visual size and weight. Soft flat watercolor fill with at most three colors plus cream highlights, the same thin warm-brown hand-drawn outline, gentle top-left light, bright cheerful sunny-day palette. Simple shapes that read at 32 px. No frames, no badges, no backgrounds, no faces on objects, no text.";

export const SHEETS = [
  ...["ed", "md", "ps"].map((c) => mushroomSheet(c, "single")),
  ...["ed", "md", "ps"].map((c) => mushroomSheet(c, "colony")),

  // ===== 2026-10-15 2차: 아이콘·무기를 한 가족으로 자유형 재디자인 (같은 크기·선·팔레트, 버섯·숲 모티프) =====
  {
    id: "icons_a", rows: 6, cols: 6, out: "Icons", refs: [PILOT, CRITTER_REF],
    subject: `${ICON_RULES} Currencies, mushroom categories, regions and weather.`,
    items: [
      ["gold", "gold: a chubby round gold coin stamped with a tiny mushroom silhouette"],
      ["ed", "edible category: a plump mushroom with a round ORANGE cap and cream stem"],
      ["md", "medicinal category: a plump mushroom with a round GREEN cap, a tiny leaf on the cap"],
      ["ps", "poison category: a plump mushroom with a round PURPLE cap with white dots and one small purple drip"],
      ["gem", "mycelite: a rounded teal crystal stone wrapped in glowing white mycelium threads with a tiny sprout"],
      ["spore", "spores: a small tied cloth pouch with glowing golden spores floating out"],
      ["clock", "time: a round wooden stopwatch with a little leaf on the crown"],
      ["dia", "diamond: a soft pink-violet faceted gem with one sparkle"],
      ["hammer", "build: a wooden mallet with a leaf tied to the handle"],
      ["auto_harvest", "idle reward: a woven basket heaped with mushrooms and gold coins"],
      ["build", "construction: a tiny mushroom-cap cottage frame with a little scaffold"],
      ["trophy", "trophy: a golden cup whose lid is a mushroom cap"],
      ["lock", "locked: a round brass padlock shaped like an acorn"],
      ["book", "encyclopedia: an open field-guide book with a mushroom drawing"],
      ["pause", "pause: two rounded upright wooden sticks side by side"],
      ["star", "star: a plump golden star with soft rounded points"],
      ["medal", "medal: a gold medal with a mushroom emblem on a red ribbon"],
      ["core_ed", "gold income: a cloth money pouch overflowing with gold coins"],
      ["core_md", "precision: a tree-stump target (rings) with an arrow in the center"],
      ["core_ps", "attack: two small crossed sickles"],
      ["t_forest", "forest region: two round pine trees with a small mushroom between them"],
      ["t_night", "moonlit night region: a crescent moon over a round dark hill with two glowing mushrooms"],
      ["t_field", "field region: golden wheat stalks and a white daisy"],
      ["t_sea", "sea region: a curling blue wave with a pink seashell"],
      ["t_ruins", "ruins region: a broken stone arch with a big red mushroom growing through it"],
      ["w_clear", "clear weather: a round sun with soft rounded rays"],
      ["w_rain", "drizzle: a puffy white cloud with three blue raindrops"],
      ["w_moon", "full moon: a round pale yellow full moon with soft craters"],
      ["w_storm", "heavy rain: a dark grey-blue cloud with slanted heavy rain"],
      ["w_wind", "spore wind: two swirling violet wind ribbons carrying spores"],
      ["w_cold", "cold snap: a pale blue snowflake with rounded arms"],
      ["w_autumn", "autumn: two falling orange maple leaves"],
      ["w_thunder", "thunder: a dark cloud with a yellow zigzag bolt"],
      ["w_rainbow", "rainbow: a short rainbow arching between two small clouds"],
      ["w_fog", "fog: three layered soft grey mist bands"],
      ["w_drought", "drought: a cracked tan earth tile under a small hot sun"],
    ],
  },
  {
    id: "icons_b", rows: 5, cols: 6, out: "", refs: ["Art/Raw_Soft/icons_a.png"],
    subject: `${ICON_RULES} Pinball skills plus small farm request / emotion icons. Same family as the attached icon sheet.`,
    items: [
      ["Icons/s_accel", "acceleration: a spinning round token with three speed streaks"],
      ["Icons/s_sharpen", "sharpen: a grey whetstone with a gleaming steel blade and a spark"],
      ["Icons/s_split", "split: one glowing orb splitting into two smaller orbs"],
      ["Icons/s_sip", "sip of time: a small hourglass beside a steaming teacup"],
      ["Icons/s_clear", "clear spore shield: a clear blue bubble with a leaf reflected in it"],
      ["Icons/s_burst", "spore burst: a puffball mushroom bursting into a round orange spore cloud"],
      ["Icons/s_tspore", "time spore: a round spore with a little clock face"],
      ["Icons/s_clone", "clone harvester: two overlapping spinning blades, the back one see-through"],
      ["Icons/s_magnet", "mushroom magnet: a red horseshoe magnet pulling two tiny mushrooms"],
      ["Icons/s_bolt", "chain lightning: a chunky yellow zigzag lightning bolt"],
      ["Icons/s_tornado", "tornado: a mint-green whirlwind with two leaves"],
      ["Icons/s_shock", "shockwave: orange rings bursting out from a point"],
      ["Icons/s_device", "forest device: a tree stump with a green sprout"],
      ["Icons/s_pierce", "pierce: a short pointed dagger passing through a leaf"],
      ["Icons/s_fest", "bountiful harvest: a golden wheat sheaf tied with a red ribbon"],
      ["Icons/s_blade", "spinning blade: a four-point steel blade star with a red hub"],
      ["Icons/s_kid", "kid harvester: a tiny round spinning token with a little mushroom cap on top"],
      ["Icons/s_homing", "homing: a curving arrow sweeping round into a small target"],
      ["Icons/s_power", "power up: a spinning token with a glowing upward arrow above it"],
      ["Icons/s_barclone", "bar clone: two short wooden log paddles, the back one see-through"],
      ["Farm/Icons/req_water", "water request: a blue water drop"],
      ["Farm/Icons/req_pet", "pet request: an open hand with a small heart"],
      ["Farm/Icons/req_song", "song request: two musical notes"],
      ["Farm/Icons/req_snack", "snack request: a round cookie with chocolate chips"],
      ["Farm/Icons/req_gift", "gift request: a small gift box with a red ribbon"],
      ["Farm/Icons/fx_heart", "affection: a plump pink heart"],
      ["Farm/Icons/fx_zzz", "sleepy: the letters Z z made of soft cloud puffs"],
      ["Farm/Icons/fx_surprise", "surprise: a yellow exclamation burst"],
      ["Farm/Icons/fx_note", "happy tune: a single green music note"],
    ],
  },
  {
    id: "harvesters", rows: 4, cols: 5, out: "Harvesters", refs: ["Art/Raw_Soft/icons_a.png", PILOT],
    subject: "Pinball harvester weapon TOKENS for a mushroom-harvesting pinball game, all seen from directly above, all the SAME round size: each token is a chunky round disc that spins while it flies, with a soft wooden or metal rim and the weapon motif forming or filling the disc, so they read as one family at 40 px. Skins (hs_) are exact recolors of the matching base token, same shape. The last two cells are small soft effect puffs, not tokens.",
    items: [
      ["sam", "scythe token: a curved steel scythe blade wrapping around a round wooden hub"],
      ["saw", "shuriken token: four rounded gunmetal blades in a round silhouette around a red gem hub"],
      ["hoe", "herb hoe token: a black-iron hoe blade with an awl-sharp tip curled around a round wooden hub"],
      ["mill", "circular saw token: a steel disc with rounded teeth and a hole in the middle"],
      ["spore", "spore token: a round red mushroom cap seen from above with cream spots"],
      ["bell", "bell token: a round brass acorn bell seen from above with its acorn-cap top"],
      ["coin", "coin token: a big old brass coin (yeopjeon) with a square hole"],
      ["gold", "golden scythe token: the scythe token made of shining gold with two sparkles"],
      ["hs_sam", "cherry-blossom scythe token: pink blade, pale wood hub"],
      ["hs_saw", "obsidian shuriken token: near-black blades with violet edges and a purple hub"],
      ["hs_mill", "ice saw token: pale icy blue disc with white teeth"],
      ["hs_spore", "galaxy spore token: deep indigo cap with yellow star dots"],
      ["hs_bell", "golden bell token: shining gold bell with a dark gold cap"],
      ["hs_coin", "jade coin token: jade green coin with a darker green ring"],
      ["hs_gold", "dragon scythe token: red blade with a gold edge and a dark hub"],
      ["cloud_spore", "a soft puffy violet spore cloud (effect)"],
      ["puff_dust", "a small soft white dust puff (effect)"],
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
    id: "ui_kit", rows: 3, cols: 3, out: "UI", refs: [PILOT, "Art/StyleRef/logo.png", "Art/Raw_Soft/icons_a.png"],
    subject: "A cohesive UI kit of EMPTY interface frames and buttons (no icons, no symbols, no text inside them) for a bright, cute, rounded watercolor game, drawn flat and front-facing in the same soft watercolor style and warm brown line as the attached icons. Panels and buttons have even, uniform rounded borders on all four sides so they can be 9-sliced, with no decorations sticking out of the border on only one side. Fills are light and clean (dark brown text must stay readable); the cream pieces are neutral enough to be tinted by code.",
    items: [
      ["panel_wood", "a square panel: a rounded honey-brown wooden frame with soft wood grain and rounded corner pegs, around a light cream watercolor paper center"],
      ["panel_paper", "a plain square light cream watercolor paper card with a thin soft tan rounded border and a faint inner edge"],
      ["btn_wood", "a chubby pill-shaped button in light cream with a soft brown outline, a gentle top highlight and a slightly darker bottom lip"],
      ["btn_go", "the same chubby pill button in warm butter yellow with a soft brown outline, top highlight and a darker bottom lip"],
      ["btn_danger", "the same chubby pill button in soft coral red with a soft brown outline, top highlight and a darker bottom lip"],
      ["badge_round", "a round badge: a rounded wooden ring around a light cream center"],
      ["bar_log", "a long thin horizontal rounded wooden log paddle with a small green leaf at each end (very wide and short)"],
      ["sign_arrow", "a rounded wooden signpost arrow board pointing right"],
      ["basket", "a round woven harvest basket seen from the front, open top"],
    ],
  },
  {
    id: "fx", fx: true, rows: 3, cols: 2, out: "FX", refs: [PILOT],
    subject: "Tintable effect textures for a cute watercolor game, painted in PURE WHITE and very light grey watercolor on a fully transparent background (the game tints them with colour). Each fills its own cell, centered, with a soft hand-painted watercolor edge and faint pigment texture. Tall sheet: 3 rows of 2, last cell empty.",
    items: [
      ["circle", "a solid filled round watercolor disc, evenly white with a slightly darker soft pigment rim"],
      ["soft~", "a soft round watercolor bloom: bright white center fading smoothly to fully transparent at the edge"],
      ["ring", "a thick round brush-stroke ring (donut), white, with a dry-brush watercolor edge, empty transparent center"],
      ["ring_thin", "a thin round brush-stroke ring, white, light and delicate, empty transparent center"],
      ["flame_tongue@128x256", "a single tall soft flame tongue, white, rounded bottom and a pointed wavy top, watercolor edges"],
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
  // Mycelium tree screen backdrop (TreeWorld/Soil, screen-fixed, nodes pan over it). White node text with a
  // dark outline sits on top, so it stays mid-dark everywhere.
  {
    id: "tree_soil", kind: "image", out: "FX/tree_soil.png", refs: ["Art/Raw_Soft/pilot_bg.png", "Art/Raw_Soft/bg_night.png"],
    prompt: "Wide landscape 16:9 game background, top-down view: a mossy forest floor at night, seen up close. Deep olive and dark teal-green moss and soil in soft watercolor washes, a gentle darker vignette toward all four edges and a slightly lighter (but still dark, mid-low brightness) calm area in the middle. Many tiny softly glowing spores float everywhere like a starry night sky: small pale-yellow and mint-green dots with faint halos, a few slightly larger ones, denser near the edges and sparse in the middle. Very faint thin pale mycelium threads wander through the soil, barely visible. A few small dark rounded moss tufts and tiny glowing mushrooms only in the corners. No big objects, no frame of bushes or stones, no bright areas: white text with a dark outline must stay easy to read anywhere on it. Same soft rounded watercolor flat style and line as the attached backgrounds, colors like the attached night forest. No characters, no text, no UI.",
  },
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
