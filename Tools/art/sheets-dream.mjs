// "Dream watercolor" art set (resource_test, 2026-10-15 5차) for Tools/art/build-art.mjs --set dream.
// Flat, matte, outline-free watercolor with a misty enchanted-forest mood (Tools/art-style-dream.md).
// Same subjects and output keys as the doodle set; only the style changes, plus:
//   - mushrooms are drawn true to the real species (field-guide plates)
//   - scenery objects come in depth layers (far / mid / near + fog) so scenes are built with real depth
// Raw sheets: Art/Raw_Dream/<id>.png. Sliced: Assets/Resources/Art/Dream/<same keys>. SpriteDB: Dream > Doodle > Soft > Generated.

import { SHEETS as DOODLE } from "./sheets-doodle.mjs";
import { MUSHROOMS } from "./sheets.mjs";

const PILOT = "Art/Raw_Dream/concept/title.png";     // approved style (title screen concept)
const OBJ = "Art/Raw_Dream/concept/objects.png";     // object style sample
const ROUND = "Art/Raw_Dream/concept/round.png";     // play field sample
const MUSH_CUTE = "Art/Raw_Dream/concept/mush_cute.png"; // the 4 cute mushrooms cropped from objects.png
const raw = (id) => `Art/Raw_Dream/${id}.png`;

// Remove the doodle set's outline / cel wording; the style file and refs decide the look.
const clean = (t) => t
  .replace(/,?\s*bold (dark )?outline[s]?/gi, "")
  .replace(/Same bold dark outline and flat cel shading as the attached sample sheet\./g, "Same flat matte watercolor technique as the attached style reference.")
  .replace(/bold-outline cel style/gi, "flat matte watercolor style")
  .replace(/\b(glowing|glow|shining|sparkles?)\b/gi, "softly lit");

const KEEP = ["icons_a", "icons_b", "harvesters", "props", "decos", "bosses",
  "critter_parts", "critter_acc", "npcs", "ui_kit2", "logo", "fix_misc"];
// critters_full / critter_skin_caps / critter_build are replaced by the per-critter designs below (same output keys).
// chains inside the set keep pointing at this set's own raw sheets
const remap = (r) => r.startsWith("Art/Raw_Doodle/") && !r.includes("/concept/") && !r.includes("critter2/") && !r.includes("pilot") && !r.includes("ui_pc")
  ? r.replace("Art/Raw_Doodle/", "Art/Raw_Dream/") : null;

// The generator laid these out transposed (items still in reading order); match the raw sheets so slicing lines up.
const GRID = { critter_parts: [5, 4],props: [5, 4], harvesters: [5, 4], icons_b: [6, 5], fix_misc: [3, 2] };

function fromDoodle(id) {
  const s = DOODLE.find((x) => x.id === id);
  if (!s) throw new Error(`no doodle sheet ${id}`);
  const chain = (s.refs || []).map(remap).filter(Boolean);
  return {
    ...s,
    ...(GRID[id] ? { rows: GRID[id][0], cols: GRID[id][1] } : {}),
    subject: clean(s.subject || ""),
    items: s.items.map(([n, d]) => [n, clean(d)]),
    refs: [...chain, PILOT, OBJ].slice(0, 3),
  };
}

const CAT_NAME = { ed: "edible", md: "medicinal", ps: "poisonous" };
const mushrooms = ["ed", "md", "ps"].flatMap((cat) => [
  {
    id: `mush_single_${cat}`, rows: 5, cols: 5, out: "Mushrooms/Single", search: true, refs: [MUSH_CUTE],
    subject: `25 different real ${CAT_NAME[cat]} mushroom species as CUTE CHIBI TOY-LIKE watercolor icons, drawn EXACTLY like the 4 attached sample mushrooms (same chunky proportions, same simple flat washes, same low detail). Each species must stay recognizable only by its BIG traits (cap profile, real colors, one signature marking such as spots / net / ink rim / ring / volva, clustering habit). Strong deformation: 2 heads tall at most, the cap is about 2/3 of the whole figure, the stem is shorter than half the cap width and thick and soft, everything is a round squishy blob like a jelly toy or a soft plush. Detail budget: only 3 to 6 big simple markings per mushroom, NO fine texture, NO scales drawn one by one, NO gill lines more than 5, NO bark or dirt texture, NO realistic shading. Flat matte watercolor, no outlines, no faces. One mushroom or its natural small group (max 3-5 chubby caps) per cell, seen slightly from the side.`,
    items: MUSHROOMS[cat].map(([id, kr, sci, look]) => [id, `${sci} (${kr}): ${look}`]),
  },
  {
    id: `mush_colony_${cat}`, rows: 5, cols: 5, out: "Mushrooms/Colony", search: true, refs: [raw(`mush_single_${cat}`), OBJ],
    subject: `25 mushroom colonies, each the SAME real species as the same cell of the attached single-mushroom sheet (same colors and shape): many of that species growing together in their real habit (tufted cluster, ring, shelf brackets on a small log, scattered group) on a small mound of moss and soil. Same chibi toy-like proportions and low detail as the attached singles: round chunky heap silhouette, 3-6 big markings per cap, no fine texture, flat matte watercolor, no faces.`,
    items: MUSHROOMS[cat].map(([id, kr, sci]) => [id, `colony of ${sci} (${kr})`]),
  },
]);

// Scenery in depth layers. far_* are pale hazy silhouettes, near_* are big dark framing pieces cut by the screen edge.
const ENV = "Scenery objects for a dreamy misty watercolor forest game scene, each drawn alone on its own base so it can be placed separately to build depth. Flat matte watercolor shapes, no outlines, no glow. Same scale family.";
const env = [
  {
    id: "env_forest", rows: 6, cols: 6, out: "Env", refs: [PILOT, ROUND],
    subject: ENV + " Forest set: rows 1-2 are MID-distance objects in normal colors (moss green, blue-teal, warm brown); rows 3-4 are FAR-distance versions, pale and hazy (desaturated lavender-blue fog colors, very low contrast, almost silhouettes); rows 5-6 are NEAR foreground framing pieces in deep dark teal, large and simple.",
    items: [
      ["tree_big", "a big tall forest tree with a round soft canopy and a thick trunk with roots"],
      ["tree_mid", "a medium round tree"],
      ["tree_pine", "a soft rounded fir tree"],
      ["tree_small", "a small young tree"],
      ["bush_big", "a big round bush"],
      ["bush_mid", "a medium bush with a few muted red berries"],
      ["bush_small", "a small round bush"],
      ["fern", "a fern clump"],
      ["grass_tall", "a tuft of tall grass"],
      ["rock_big", "a big rounded mossy boulder"],
      ["stump", "a cut tree stump with a small sprout"],
      ["log", "a fallen mossy log"],
      ["far_tree_a", "FAR: a very tall slim tree silhouette, pale hazy lavender-blue"],
      ["far_tree_b", "FAR: a group of three tall tree silhouettes, pale hazy blue"],
      ["far_tree_c", "FAR: a wide round tree silhouette, pale hazy sage"],
      ["far_shroom", "FAR: a giant mushroom silhouette taller than a tree, pale hazy rose-lavender"],
      ["far_rock", "FAR: a tall rock spire silhouette, pale hazy blue-grey"],
      ["far_bush", "FAR: a long low bush line silhouette, pale hazy sage (wide)"],
      ["far_arch", "FAR: an old stone bridge arch silhouette, pale hazy lavender"],
      ["far_hill", "FAR: a soft forest hill silhouette, very pale fog blue (wide)"],
      ["fog_a", "a long soft horizontal fog band, pale cream-lavender watercolor wash with soft edges (wide and short)"],
      ["fog_b", "a round soft fog puff, pale blue-white watercolor wash with soft edges"],
      ["light_patch", "a soft oval patch of pale warm light on the ground, very faint apricot wash with soft edges"],
      ["far_pine", "FAR: two soft fir silhouettes, pale hazy blue"],
      ["near_leaves_l", "NEAR: a big cluster of dark teal broad leaves and ferns growing from the bottom-left corner, cut flat along its left and bottom edges"],
      ["near_leaves_r", "NEAR: a big cluster of dark teal leaves and a curled fern, cut flat along its right and bottom edges"],
      ["near_trunk", "NEAR: a huge dark tree trunk with roots, tall, cut flat at the top"],
      ["near_branch", "NEAR: a dark leafy branch hanging down from the top, cut flat at the top (wide)"],
      ["near_shroom", "NEAR: a big dark-toned real fly agaric silhouette with muted red cap, cut flat at the bottom"],
      ["near_grass", "NEAR: a long strip of dark grass and small ferns along the bottom, cut flat at the bottom (very wide and short)"],
      ["roots", "a curved exposed mossy tree root arching out of the ground"],
      ["flowers_white", "a small patch of white wood anemones"],
      ["flowers_yellow", "a small patch of pale yellow flowers"],
      ["flowers_pink", "a small patch of dusty pink flowers"],
      ["clover", "a clump of wood sorrel leaves"],
      ["mush_deco", "a tiny cluster of three small real brown wild mushrooms"],
    ],
  },
  {
    id: "env_themes", rows: 5, cols: 5, out: "Env", refs: [raw("env_forest"), PILOT],
    subject: ENV + " Region sets for a moonlit night forest, a windy meadow, a beach and old mushroom-town ruins, painted with the same palette and haze as the attached forest set.",
    items: DOODLE.find((x) => x.id === "env_themes").items.map(([n, d]) => [n, clean(d)]),
  },
  {
    id: "env_misc", rows: 4, cols: 3, out: "Env", refs: [raw("env_forest")],
    subject: ENV,
    items: [
      ["rock_mid", "two rounded mossy rocks with a grass tuft"],
      ["rock_small", "three small pebbles"],
      ["grass_small", "a small tuft of grass"],
      ["pond", "a small round pond with a lily pad and reeds"],
      ["fence", "a short old wooden fence segment (two posts, two rails)"],
      ["fence_post", "a single old wooden fence post"],
      ["path", "a small patch of light dirt path with two stepping stones"],
      ["moss", "a soft round patch of moss on the ground"],
      ["leaf_litter", "a few fallen leaves on the ground"],
      ["twig", "a small fallen twig with two leaves"],
      ["night_moth", "a pale moth resting (tiny ambient creature)"],
      ["frog", "a small sleepy green frog (tiny ambient creature)"],
    ],
  },
];

// Critters: each one gets its own silhouette instead of one shared body with recolored caps.
// The cap is a cute version of a REAL mushroom tied to the critter's role / favorite mushrooms (Defs.Farm.cs KIDS),
// plus its own body tint, face and one small accessory. Parts feed CritterRig per-kind parts (body_<id>, arm_<id> ...).
const CRIT = [
  ["leaf", "LEAF (sprout, likes oyster and shiitake, lives at the tree roots): cap = a soft lopsided OYSTER-MUSHROOM fan in moss green, tilted to one side like a beret, a few pale green spots, two little sprout leaves on top; pale green-cream body; cheerful open smile; a tiny vine scarf with one small white flower",
    "cherry blossom: dusty pink with cream petal spots"],
  ["dew", "DEW (regrows mushrooms, likes jelly ear fungi, loves drizzle): cap = a rounded wobbly JELLY-FUNGUS bell, pale misty blue-teal, softly translucent-looking at the rim, one dewdrop hanging from the rim and one on top; pale blue-tinted body; gentle sleepy smile; a small round lily-pad leaf worn as a tiny cape",
    "snowflake: pale icy white with soft blue snowflake spots"],
  ["wind", "WIND (fast runner, likes parasol mushrooms and puffballs): cap = a wide flat PARASOL-MUSHROOM disc with a small bump in the middle and a ring of soft scale spots, mint sage, the rim swept back as if blown by wind, two white wind curls off the back; slimmer white-mint body leaning forward; lively wide eyes; a long fluttering scarf the color of dandelion fluff",
    "cloud: cream white with pale sky-blue cloud spots"],
  ["fire", "FIRE (finds gems, likes reishi and bracket fungi): cap = a kidney-shaped REISHI cap with concentric bands from deep coral at the center to ochre then cream at the rim, the rim curling up into three soft flame-like lobes, a tiny ember on top; warm apricot body; brave little eyebrows and a confident grin; a tiny muted red neckerchief",
    "lava: deep red-brown with soft orange crack bands"],
  ["rock", "ROCK (strong carrier, likes hoof fungus and turkey tail): cap = a thick HOOF-FUNGUS bracket like a little stone shelf, grey-beige with stacked growth bands and green moss patches, a smooth pebble on top; WIDER sturdy grey-beige body; calm strong face with thick eyebrows; a small woven basket on its back with straps over the shoulders",
    "gem: pale aqua with a few soft faceted patches"],
  ["spark", "SPARK (chain harvests, likes chaga and red antler fungus): cap = a short bushy ANTLER-FUNGUS crown of chubby branches whose tips zigzag like little lightning bolts, butter yellow with muted coral tips; cream body with a soft static fuzz on the chest; wide excited eyes and a toothy grin with one tiny fang; round amber goggles pushed up on the cap",
    "neon: dusty rose with soft cyan spots"],
  ["moon", "MOON (night glow, likes jack-o-lantern mushrooms and matsutake): cap = a soft drooping NIGHTCAP-shaped funnel cap in deep blue-lavender with pale butter star spots, its tip flopping to one side with a little pompom, a crescent moon on top; pale periwinkle body; sleepy half-closed content eyes; a tiny round paper lantern hanging from a belt",
    "sun: soft apricot orange with pale butter spots"],
  ["star", "STAR (aura that helps everyone, likes truffles and cordyceps): cap = a tall pointed LIBERTY-CAP cone like a wizard hat in dusty violet, the tip curling over into a small golden star, a few lilac spots; lavender-tinted body; star-shaped catchlights in the eyes, a kind smile; a short dark-blue cape with a star clasp",
    "galaxy: deep midnight blue with pale butter star spots"],
  ["coin", "COIN (merchant): cap = a wide flat golden CHANTERELLE cap with a wavy rim and soft ridges underneath, a coin standing on top like in a slot; cream body; smug happy closed-mouth smile with one eye winking; a tiny brown money pouch on a belt",
    "rich: deep forest green with gold spots"],
  ["chest", "CHESTNUT (playful): cap = a round brown CHESTNUT-like cap with a pale beige base patch, a small acorn on top, and a soft spiky chestnut-husk collar around the neck; warm tan body; cheeky grin with a tiny tongue; carries an acorn in one hand",
    "strawberry chocolate: chocolate brown with dusty pink spots"],
];
const CRIT_A = CRIT.slice(0, 5), CRIT_B = CRIT.slice(5);
const CRIT_IDS = CRIT.map(([id]) => id);
const CRIT_STYLE = "Tiny mushroom-critter pets for a cozy dreamy forest game, in the attached flat matte watercolor style (no outlines, no gloss, no glow; see the little red-capped critter in the attached object sheet). Chunky chibi creatures, 2 heads tall: a big mushroom cap as the head-top, a chubby round body with the face on it (two small dark dot eyes, a tiny mouth, faint rose blush), two stubby arms and two little feet. Every critter has the same height so they form one family, but each must be instantly told apart by its SILHOUETTE: its cap is a cute version of a different real mushroom, plus its own body tint, face and one small accessory. Soft low-saturation palette, readable at 64 px.";
const critters = [
  ...[["a", CRIT_A], ["b", CRIT_B]].map(([k, list]) => ({
    id: `critter_design_${k}`, rows: 2, cols: 3, out: "Characters/Critters", refs: [OBJ, PILOT],
    subject: `${CRIT_STYLE} Character design sheet: one critter per cell, standing front view in a neutral idle pose with arms slightly away from the body and feet apart (it will be cut into parts). No ground, no shadow.`,
    items: list.map(([id, look]) => [`${id}_ref`, look]),
  })),
  ...[["a", CRIT_A], ["b", CRIT_B]].map(([k, list]) => ({
    id: `critter_rig_${k}`, rig: true, rows: 5, cols: 6, out: "Characters/Critters", refs: [raw(`critter_design_${k}`)],
    subject: "Cut-out animation PARTS of the five mushroom critters in the attached design sheet (same designs, colors, flat matte watercolor and scale for every part, no outlines), front view. ONE ROW PER CRITTER, in this order: (1) the mushroom CAP alone with its decoration and anything worn on the cap, including the cap's underside rim; (2) the BODY alone: the chubby body with its full FACE (open eyes, mouth, cheeks) and its body accessory (scarf, belt, cape, collar, basket), but NO cap, NO arms and NO feet, the top of the body drawn complete because the cap overlaps it; (3) a small BLINK PATCH: the same two eyes CLOSED as happy curves on a patch of that critter's body color, same size and spacing as on the body; (4) ONE stubby ARM (the critter's left arm, shoulder end rounded); (5) ONE little FOOT; (6) a SKIN version of the same cap: exactly the same cap shape and size, recolored as described, WITHOUT the decoration. Each part complete and whole, rounded joint ends.",
    items: list.flatMap(([id, , skin]) => [
      [`rigcap_${id}`, `${id} critter: its cap`], [`rigbody_${id}`, `${id} critter: its body with face`], [`blink_${id}`, `${id} critter: closed-eye patch`],
      [`arm_${id}`, `${id} critter: one arm`], [`foot_${id}`, `${id} critter: one foot`], [`rigskin_${id}`, `${id} critter: skin cap, ${skin}`],
    ]),
  })),
  // Rig bodies (user direction 2026-10-15): a round CYLINDER body the cap simply sits on, two bodies per critter
  // (eyes open / closed, swapped whole instead of a face patch) and ONE soft nub used for both hands and feet.
  // Caps and skin caps stay from critter_rig_a|b; build_critter_rig.py stacks the parts.
  ...[["a", CRIT_A], ["b", CRIT_B]].map(([k, list]) => ({
    id: `critter_body_${k}`, rig: true, rows: 5, cols: 3, out: "Characters/Critters", refs: [raw(`critter_design_${k}`), raw(`critter_rig_${k}`)],
    subject: "Cut-out BODY parts for the five mushroom critters in the attached design sheet (same colours, faces, blush and body accessories; flat matte watercolor, no outlines), front view, ONE ROW PER CRITTER in the design sheet's order. Every body is a soft rounded CYLINDER like a thick plump mushroom stem or a marshmallow: straight soft sides, slightly wider at the bottom, a flat gently domed top (a mushroom cap will sit on it), rounded bottom edge, no neck, no head shape; all five the same height, the rock critter wider. The face sits in the upper half just under the top. Each row: (1) the BODY with the face, eyes OPEN, and its body accessory (scarf, cape, basket, belt, collar, held acorn); NO cap, NO arms, NO feet; (2) the SAME body drawn exactly the same (same size, shape, colours, accessory) with ONE change: both eyes CLOSED as soft happy upward curves and a small gentle closed smile; (3) ONE small soft round NUB in the body's colour (a plump oval blob with faint shading) used for both hands and feet. Leave clear space between parts.",
    items: list.flatMap(([id]) => [[`body_${id}`, `${id} critter: cylinder body, eyes open`], [`body_${id}_closed`, `${id} critter: same body, eyes closed`], [`nub_${id}`, `${id} critter: one hand/foot nub`]]),
  })),
  // Caps alone with NO stem (user 2026-10-15): the parts sheet's caps had a stem stub under the gills, so the body
  // could not be seated cleanly. build_critter_rig.py cuts the socket for the cylinder body into these.
  ...[["a", CRIT_A], ["b", CRIT_B]].map(([k, list]) => ({
    id: `critter_cap_${k}`, rig: true, rows: 5, cols: 2, out: "Characters/Critters", refs: [raw(`critter_rig_${k}`), raw(`critter_design_${k}`)],
    subject: "The mushroom CAPS of the five critters in the attached parts sheet (first column) and their recoloured SKIN caps (last column), redrawn the same: same shape, size, colours, decoration, flat matte watercolor, no outlines. ONE ROW PER CRITTER in the parts sheet's order: (1) the cap with its decoration; (2) the same cap shape and size recoloured as described, WITHOUT the decoration. Each cap is ONLY the cap: front view seen slightly from below, the shell on top and the underside gills visible as a shallow dish under the rim. ABSOLUTELY NO STEM, no stem stub, no neck, nothing hanging below the gills: the middle of the underside is just gills. Clear space between all caps.",
    items: list.flatMap(([id, , skin]) => [[`cap_${id}`, `${id} critter: its cap, no stem`], [`skin_${id}`, `${id} critter: skin cap, ${skin}, no stem`]]),
  })),
  {
    id: "critter_build", rows: 4, cols: 5, out: "Characters/Critters/Build", refs: [raw("critter_design_a"), raw("critter_design_b")],
    subject: `Two-frame hammering animation for the ten mushroom critters in the attached design sheets, working as tiny builders, flat matte watercolor, no outlines. Keep each critter's cap, body, face and accessory exactly as designed, same scale for all. Every critter: three-quarter side view facing RIGHT, a small yellow hard hat worn tilted on the back of the cap. Rows 1-2 (frame A, critters in the order ${CRIT_IDS.join(", ")}): a wooden hammer held RAISED HIGH above and behind the head. Rows 3-4 (frame B, same order): the SAME critter in the same cell position and size, hammer SWUNG DOWN to the right, hammer head at foot level. Nothing else: no ground, no planks, no effects.`,
    items: [...CRIT_IDS.map((id) => [`build_${id}_0`, `${id} critter, hammer raised`]), ...CRIT_IDS.map((id) => [`build_${id}_1`, `${id} critter, hammer swung down`])],
  },
];

// Grandpa redrawn for the dream set (user 2026-10-16): same family as the critters and the title concept. One full
// drawing in the pose the rig cutter expects (Tools/art/build_grandpa_rig_soft.py --set dream): the scythe is held in
// his right fist at chest height on the viewer's left, the handle runs diagonally up behind his shoulder and head to
// the blade at the top right, so the scythe can be cut off as one piece hinged at the wrist.
const grandpa = {
  id: "grandpa_full", rows: 1, cols: 1, out: "Characters/Grandpa", refs: [PILOT, raw("critter_design_a"), OBJ],
  subject: "One character alone, full body, filling the cell: the kind old mushroom-hunter grandpa from the attached title screen concept, drawn in the same flat matte watercolor as the attached critter sheet (no outlines, no gloss, no glow, big simple shapes, soft low-saturation palette, little texture). Cute storybook proportions, about 3 heads tall: a big round head, a huge soft white beard ending in one braid, rosy cheeks and a round nose, small dark dot eyes and a gentle smile, grey hair tufts at the sides. A wide floppy brown hat with a sprig of leaves and one pale feather. A long moss-green coat with a cream fur collar, a cream shirt, a leather strap across the chest with three small potion bottles, dark brown trousers and sturdy boots, a brown backpack peeking out behind his shoulder. Standing, three-quarter view facing slightly right, feet apart. POSE: his right hand (on the viewer's LEFT) holds a long wooden scythe handle in a closed fist at chest height; the handle runs in one straight diagonal line from below his fist, past his fist, UP and BEHIND his shoulder and head to the curved pale steel blade at the TOP RIGHT, the blade pointing right; the whole handle and blade clearly visible and separate from the hat. His left arm (viewer's right) hangs relaxed at his side with an open hand.",
  items: [["grandpa_ref", "the grandpa, full body, in the pose described"]],
};

// World tree (세계수) Lv.1-8: one tree growing up. FarmTreeView scales each stage to WT.width and places
// mushroom slots by zones of the sprite bounds, so every stage keeps base at the bottom, trunk centered, canopy on top.
const tree = {
  // World-tree growth (user 2026-10-16: growth must read at a glance, each stage adds more of the tree village; the
  // approved final look is concept/tree_final_v2.png, which is tree_7 itself — see build_tree_final below).
  id: "mush_tree", rig: true, rows: 2, cols: 4, out: "Farm/Tree", refs: [raw("concept/tree_final_v2"), PILOT],
  subject: "Seven earlier growth stages of the SAME magical mushroom world-tree as the FIRST attached image (its final, fully grown form). Same soft matte watercolor, big simple chunky rounded shapes, soft warm-brown painted rim, no glow, no gloss. SAME palette as the final form so it stands out from a cool green forest background: warm cinnamon-brown trunk, canopy clumps in golden yellow-green and soft peach-apricot with pale blossom pink, warm olive shading (no blue-teal leaves). Left to right, top row first, smallest to largest; each stage stands on its own small oval grassy base centered at the bottom of its cell, trunk centered. Growth reads by size AND by more and more of the final tree's village objects appearing each stage (each stage keeps everything from the previous one). Leave the last cell empty. No characters or creatures, no text.",
  items: [
    ["tree_0", "stage 1: a tiny sprout with two round warm-green leaves on a little moss mound with two tiny mushrooms"],
    ["tree_1", "stage 2: a small sapling with a thin curved trunk, two soft golden-green leaf clumps, a few small flowers and tiny mushrooms at its foot"],
    ["tree_2", "stage 3: a young tree with three canopy clumps (one peach), two small salmon shelf fungi on the trunk, one hanging paper lantern"],
    ["tree_3", "stage 4: a medium tree, sturdier trunk, four clumps with a little blossom pink, a small round doorway at the trunk base, three shelf fungi, two lanterns, the first small wooden platform with a tiny ladder"],
    ["tree_4", "stage 5: a large tree, thick trunk with roots, five clumps, the doorway now has a lantern inside, shelf-fungus steps up the trunk, two platforms with rope railings, a birdhouse, vines and flowers"],
    ["tree_5", "stage 6: a big tree, layered canopy, three platforms with ladders and little mushroom clusters, hanging lanterns and a few pale-blue crystals, a rope swing, shelves of jars in the trunk opening, a small trickle of water between the roots"],
    ["tree_6", "stage 7: a grand tree almost like the final one: a small waterfall into a pebble pool, a tiny mushroom cottage at the left foot, all platforms, lanterns, crystals and swing; only the market stall and the crown mushrooms are still missing"],
  ],
};
// tree_7 = the approved final concept, cleaned (faint halo dropped) and cropped by Tools/art/build_tree_final.py


// Tintable effect textures (white + alpha, slice_fx.py; FX sprites are one world unit wide) and bar pieces.
const FX_STYLE = "Tintable effect textures for a dreamy flat watercolor game, painted in PURE WHITE and very light grey watercolor on a fully transparent background (the game tints them with colour). Each fills its own cell, centered, with a soft hand-painted watercolor edge, faint paper grain and slight pigment pooling at the rim; matte, no gloss, no glow halo, no sparkles.";
const fx = [
  {
    id: "fx", fx: true, rows: 4, cols: 2, out: "FX", refs: [PILOT],
    subject: FX_STYLE + " 4 rows of 2.",
    items: [
      ["circle", "a solid filled round watercolor disc, evenly white with a slightly darker soft pigment rim"],
      ["soft~", "a soft round watercolor bloom: white center fading smoothly to fully transparent at the edge, slightly uneven like wet paint"],
      ["ring", "a thick round watercolor brush ring (donut) with a slightly uneven wet edge, empty transparent center"],
      ["ring_thin", "a thin round watercolor brush ring, delicate and slightly uneven, empty transparent center"],
      ["flame_tongue@128x256", "a single tall soft flame tongue like a watercolor petal, white, rounded bottom and a softly curling pointed top"],
      ["splash", "a round watercolor splat: an irregular soft blob with three or four small droplets around it"],
      ["brush@512x64", "one long horizontal watercolor brush stroke, even width, soft dry-brush ends (very wide and short)"],
      ["leaf", "a single small soft leaf shape, white, for falling-leaf particles"],
    ],
  },
  {
    // critter skins (user): the accessory is painted ONTO the rig part instead of a separate sticker. Each cell
    // redraws the part in the same cell of skin_acc_ref.png (skin caps, then the dew / moon bodies) wearing its
    // accessory; Tools/art/build_skin_acc.py registers it back onto the original part and re-cuts the cap socket.
    // Slices go to Art/Raw_Dream/skinacc (not into Assets; only the registered results are used).
    id: "critter_skin_acc", lettering: false, rig: true, rows: 2, cols: 5, out: "../../../Art/Raw_Dream/skinacc", refs: [raw("skin_acc_ref"), PILOT],
    subject: "Character skin parts for a soft dreamy watercolor game, same flat matte watercolor as the attached style references (paper grain, soft painted edges, no hard black lines, no gloss). The FIRST attached image is a reference sheet with the same 2x5 layout: in EVERY cell, redraw EXACTLY the part shown in that cell of the reference sheet (same shape, same proportions, same colours and pattern, same orientation and same open underside) and add the listed accessory worn on it naturally: it wraps around, sits on or tucks into the part with a soft contact shadow, sized to fit that part, painted in the same watercolor style. Do not change, crop or restyle the part itself, do not add faces to caps, do not add stems, bodies or limbs.",
    items: [
      ["fire", "the lava cap wearing a red pirate bandana with white dots tied around the cap like a headscarf, the knot and two short tails at its right side"],
      ["spark", "the coral-shaped neon cap wearing big round lilac-and-teal headphones: the band arching over the top, the ear cups on the left and right ends of the cap rim"],
      ["coin", "the wide green rich cap with a small black top hat with a gold band sitting on the centre of its crown, slightly tilted"],
      ["star", "the starry wizard-hat cap with a soft glowing golden halo ring floating just above its curled tip"],
      ["leaf", "the cherry-blossom cap with a pink cherry-blossom hairpin (two flowers and a little leaf) tucked on its upper right side"],
      ["wind", "the cloud cap with a small fluffy white cloud resting on top of its crown"],
      ["rock", "the gem cap with a small golden jeweled crown sitting on top of its crown"],
      ["chest", "the strawberry-chocolate cap with a big pink ribbon bow on its upper left side"],
      ["dew_body", "the white face body wearing a cozy cream knitted scarf around its lower neck/chest area, below the mouth, one tail hanging down at the side; the face stays fully visible"],
      ["moon_body", "the lilac face body (keep its brown strap and lantern) wearing small round black sunglasses exactly over its eyes, the smile still visible"],
    ],
  },
  {
    // fever edge fire (ComboHUD.DrawFlames): replaces the single petal flame_tongue that read as a brown blob once
    // tinted. Variants so neighbours differ; the code lays a smaller copy tinted with the palette core on top.
    id: "fx_flame", fx: true, rig: true, rows: 1, cols: 4, out: "FX", refs: [PILOT],
    subject: FX_STYLE + " Four different stylised FIRE flames for a cute game, 1 row of 4, each standing upright in a tall narrow cell. Each flame: a slim teardrop body that rises from a narrow rounded base and splits into 2-3 sharp licking tips that curl in an S-curve, with one or two small detached flame wisps above the tips. Crisp readable flame silhouette (clearly FIRE, not a petal, not a blob, not a drop), light watercolor texture only inside, the inner centre a touch whiter than the edges.",
    items: [
      ["flame_0@128x256", "a tall flame with two tips leaning slightly left and one small wisp above"],
      ["flame_1@128x256", "a tall flame with three tips, the middle one highest, one wisp"],
      ["flame_2@128x256", "a shorter wider flame with two tips leaning right and two small wisps"],
      ["flame_3@128x256", "a very tall thin flame with one long curling tip and a side tip low on the left"],
    ],
  },
  {
    // hit / spawn / bar-bounce juice (RoundView FieldFx), white so the code tints them per mushroom and tier
    id: "fx_hit", fx: true, rig: true, rows: 2, cols: 4, out: "FX", refs: [PILOT],
    subject: FX_STYLE + " Impact and spawn effect shapes for a cute action game, 2 rows of 4.",
    items: [
      ["impact", "a punchy impact burst: a round soft core with six to eight rounded watercolor petals shooting outward, like a splashed star"],
      ["impact_ring", "a thick broken impact ring made of six short curved brush dashes around an empty centre"],
      ["puff~", "a fluffy round cloud puff of three overlapping soft blobs, white with light grey shading"],
      ["dust@256x128", "a low wide dust cloud of four or five small round puffs lying along the ground (wide and short)"],
      ["streak@256x128", "three short tapered speed streaks fanning out from the left, like a quick swoosh (wide and short)"],
      ["chip", "a small irregular rounded chip like a broken bit of mushroom cap"],
      ["sprout", "a tiny two-leaf sprout popping up, white"],
      ["drop", "a single plump rounded water drop shape"],
    ],
  },
  {
    // flipbook hit burst (RoundView plays hit_0..4 in ~0.18 s) and a twinkle for sparkle bursts; white, tinted in code
    id: "fx_hitanim", fx: true, rig: true, rows: 2, cols: 4, out: "FX", refs: [PILOT, raw("fx_hit")],
    subject: FX_STYLE + " A 5-frame flipbook of ONE watercolor impact burst, then a twinkle, a heart-free soft star and a ring of dots. Frames in reading order, same centre and same canvas scale for all five burst frames.",
    items: [
      ["hit_0", "burst frame 1: a small tight bright splat, a round core with short stubby rays"],
      ["hit_1", "burst frame 2: the splat opens up, a bigger star-shaped burst with 8 long rounded rays and a few droplets"],
      ["hit_2", "burst frame 3: the biggest burst, rays stretched far out, the core opening into a ring"],
      ["hit_3", "burst frame 4: the burst breaks apart into a loose ring of separate rounded blobs and droplets, empty centre"],
      ["hit_4", "burst frame 5: only a few small scattered droplets remain in a wide loose ring"],
      ["twinkle", "a soft four-point twinkle star with rounded tips"],
      ["dot_ring", "a ring of eight small round dots"],
    ],
  },
  {
    id: "ui_bars", fx: true, rows: 1, cols: 4, out: "UI", refs: [PILOT],
    subject: FX_STYLE + " Gauge bar pieces, each one long horizontal rounded pill shape (very wide and short) filling its row edge to edge; the ends are round so they can be 9-sliced.",
    items: [
      ["bar_fill@512x96", "a gauge FILL: a long rounded pill, evenly white with a faint lighter streak along the top third and a slightly darker soft pigment edge"],
      ["bar_back@512x96", "a gauge TROUGH: a long rounded pill, light grey watercolor wash, a bit darker toward the bottom edge"],
      ["hp_fill@512x48", "a thin HP FILL: a slimmer long rounded pill, evenly white with a slightly darker soft edge"],
      ["hp_back@512x48", "a thin HP TROUGH: a slimmer long rounded pill, light grey watercolor wash"],
    ],
  },
];

// Icons that say what they do: ONE big main object + at most ONE small modifier symbol.
const ICON_RULE = "Game icons for an upgrade tree and skill bar, readable at 40 px, in the flat matte watercolor style of the attached icon sheet (no outlines, no gloss, no glow, no sparkles). Each icon = ONE big chunky main object that names the effect + at most ONE small modifier symbol from this fixed set: a plus sign (more), two outward arrows (wider / bigger), a bold upward arrow (stronger), a small hourglass (lasts longer), curved motion swirls (faster), a four-leaf clover (chance). Strong value contrast between the object and the modifier, simple big shapes, soft low-saturation palette with one warm accent. The 'harvester' is the round spinning scythe disc token from the attached harvester sheet.";
const icons = [
  {
    id: "icons_skill", rig: true, rows: 4, cols: 5, out: "Icons", refs: [raw("icons_a"), raw("harvesters"), OBJ],
    subject: ICON_RULE + " Skill icons (effects that trigger during a round).",
    items: [
      ["s_accel", "ACCELERATE: a harvester disc rushing to the right with three bold speed streaks behind it"],
      ["s_sharpen", "SHARPEN: a curved scythe blade resting on a grey whetstone, a tiny bright nick on the edge"],
      ["s_split", "SPLIT: one harvester disc dividing into two smaller discs flying apart"],
      ["s_clear", "CLEAR SPORE SHIELD: a round pale-blue bubble shield with a small leaf inside"],
      ["s_burst", "SPORE BURST: a puffball mushroom bursting into a round apricot spore cloud"],
      ["s_clone", "CLONE HARVESTER: a harvester disc with a see-through pale ghost copy offset behind it"],
      ["s_magnet", "MUSHROOM MAGNET: a red horseshoe magnet pulling two tiny mushrooms toward it"],
      ["s_bolt", "CHAIN LIGHTNING: a butter-yellow zigzag bolt jumping between three small mushroom caps"],
      ["s_tornado", "TORNADO: a mint-green whirlwind funnel carrying two leaves"],
      ["s_shock", "SHOCKWAVE: three expanding curved rings bursting out from a small mushroom at the center"],
      ["s_pierce", "PIERCE: a pointed blade passing straight through two mushrooms in a row"],
      ["s_homing", "HOMING: a harvester disc following a curved dotted path that ends at a mushroom"],
      ["s_power", "EMPOWER: a harvester disc with a bold upward arrow rising from it and warm wavy heat lines"],
      ["s_barclone", "BAR CLONE: a wooden log paddle with a see-through pale ghost copy floating above it"],
      ["s_fest", "BOUNTIFUL HARVEST: a golden wheat sheaf tied with a red ribbon leaning on a full basket"],
      ["s_blade", "SPINNING BLADE: a four-point steel blade star with a red hub, motion swirls around it"],
      ["s_kid", "KID HARVESTER: a small harvester disc with a tiny mushroom-critter cap on top"],
      ["s_device", "FOREST DEVICE: a mossy tree stump bumper with a small sprout"],
    ],
  },
  {
    id: "icons_node", rig: true, rows: 6, cols: 6, out: "Icons", refs: [raw("icons_a"), raw("harvesters"), OBJ],
    subject: ICON_RULE + " Upgrade-node icons (permanent stat upgrades).",
    items: [
      ["n_score", "HIT SCORE: a harvester disc bumping a mushroom cap, three small score stars popping out"],
      ["n_bonus", "HARVEST YIELD: a woven basket overflowing with mushrooms, bold upward arrow"],
      ["n_cols", "MORE COLONIES: three small mossy mounds each with a tiny mushroom clump, plus sign"],
      ["n_size", "BIGGER COLONY: one mossy mound packed with many crowded mushrooms, two outward arrows"],
      ["n_regen", "COLONY REGROWTH: a young mushroom sprouting inside a curved green circular arrow"],
      ["n_price", "SETTLEMENT BONUS: a paper receipt scroll with a gold coin, plus sign"],
      ["n_rare", "VARIANT CHANCE: a mushroom with a two-tone teal and pink cap, four-leaf clover"],
      ["n_trade", "REGULAR CUSTOMER: a small market stall with a striped awning and a coin on the counter"],
      ["n_gold", "GOLDEN VARIANT: a golden mushroom with a soft warm gold wash"],
      ["n_giant", "GIANT MUSHROOM: a huge mushroom towering over a tiny one beside it"],
      ["n_multi", "MULTIPLIER: one mushroom fanning out into three identical copies"],
      ["n_map", "WIDER FIELD: an unrolled leaf map, two outward arrows"],
      ["n_bar", "WIDER BAR: a wooden log paddle, two outward arrows at its ends"],
      ["n_combo", "COMBO POWER: three linked chain rings, the last one with a small flame"],
      ["n_templife", "TEMP HARVESTER LIFE: a see-through pale harvester disc, small hourglass"],
      ["n_chef", "LITTLE BUILDER: a tiny mushroom critter wearing a yellow hard hat, holding a little hammer"],
      ["n_stump", "DEVICE BOOST: a mossy tree stump bumper, bold upward arrow"],
      ["n_dev_stump", "STUMP DEVICE: a round mossy tree stump bumper with a springy top"],
      ["n_dev_moss", "MOSS PATH DEVICE: a curving strip of soft green moss path with two tiny stones"],
      ["n_dev_acorn", "ACORN SWITCH DEVICE: an acorn lamp on a short stalk, its cap lit warm yellow"],
      ["n_buffdur", "BUFF DURATION: a small green leaf charm, small hourglass"],
      ["n_atk", "ATTACK: a curved scythe blade with a round impact burst behind it"],
      ["n_hvsize", "HARVESTER SIZE: a harvester disc, two outward arrows"],
      ["n_spd", "ATTACK SPEED: a spinning harvester disc, curved motion swirls"],
      ["n_dur", "ROUND TIME: a round wooden stopwatch, plus sign"],
      ["n_ball", "MORE HARVESTERS: two harvester discs side by side, plus sign"],
      ["n_crit", "CRITICAL CHANCE: a mushroom cap with a sharp jagged strike mark, four-leaf clover"],
      ["n_crit2", "CRITICAL DAMAGE: a big jagged coral-red strike burst splitting a mushroom cap, bold upward arrow"],
      ["n_resist", "SPORE RESIST: a round leaf shield blocking small purple spore puffs"],
      ["n_rush", "FIRST STRIKE: a harvester disc dashing forward leaving a pointed arrow-shaped trail"],
      ["n_barspd", "BAR BOUNCE SPEED: a wooden log paddle bouncing a harvester disc, curved motion swirls"],
      ["n_bardur", "BAR SKILL DURATION: a wooden log paddle, small hourglass"],
      ["n_barrate", "BAR SKILL CHANCE: a wooden log paddle, four-leaf clover"],
      ["n_heavy", "PILE CRUSHER: a big wooden mallet smashing a heap of mushrooms"],
      ["n_inf_atk", "ENDLESS FIGHT: an infinity-shaped ribbon looped around a scythe blade"],
      ["n_inf_gold", "ENDLESS HARVEST: an infinity-shaped ribbon looped around a gold coin"],
    ],
  },
];

// Upgrade-tree node slots (user 2026-10-16): one picture per state instead of stacked glow / ring / disc circles.
// Every slot is the same perfectly round size so nodes line up; the icon sits on the flat inner face.
// Tools/art/norm_slots.py centres and squares them after slicing.
const slots = {
  id: "node_slots", rows: 2, cols: 3, out: "UI/Slots", refs: [PILOT, raw("ui_kit2")],
  subject: "Six round upgrade-tree node slots for a dreamy flat matte watercolor game UI (no outlines, no gloss, no glow, no sparkles), seen straight from the front. ALL SIX EXACTLY THE SAME SIZE, perfectly circular and centered in their cells, the same rim width: a soft rounded rim ring about 12% of the radius wide around a flat, calm, slightly recessed pale inner face where an icon will be placed (the inner face is plain, light and even, with no pattern). Only the rim colour and a tiny ornament differ.",
  items: [
    ["slot_buy", "UPGRADABLE: pale cream inner face, soft sky-teal watercolor rim"],
    ["slot_poor", "NOT ENOUGH CURRENCY: pale warm-grey inner face, muted dusty-coral rim"],
    ["slot_max", "MAX LEVEL: pale cream inner face, moss-green rim with a few tiny butter-yellow dots on it"],
    ["slot_locked", "LOCKED: pale grey-lavender stone inner face, darker grey stone rim, a little moss on the rim"],
    ["slot_core", "BRANCH START: pale cream inner face, warm honey-wood rim with three tiny leaves on the rim"],
    ["slot_gem", "MYCELIUM STONE NODE: pale mint inner face, translucent-looking teal crystal rim"],
  ],
};

// Hand-lettered words for the few fixed headlines a font cannot carry (UI/Lettering/<lang>/<id>; Lettering.cs falls
// back to the font when a language has no image). The neutral set is pale so the fever palette can tint it.
const LETTER = "Hand-lettered storybook headline words for a dreamy watercolor mushroom-forest game, each word on ONE line, centered in its own wide cell. Chunky soft rounded letters painted in flat matte watercolor, slight playful baseline bounce, letters touching nothing else. No glow, no 3D, no gloss, no drop shadow, no hard black outline.";
const LETTER_WARM = " Warm cream-butter letter fill with a soft darker warm-brown watercolor edge (not a hard outline) so it reads on light and dark backgrounds; one tiny leaf or mushroom cap ornament on one letter at most.";
const LETTER_WORDS = [
  ["harvest_end", "수확 끝!", "Harvest Over!"],
  ["settle_done", "수확 완료!", "Harvest Complete!"],
  ["settle_stop", "수확 중단", "Harvest Stopped"],
  ["pause", "일시정지", "Paused"],
  ["giant", "거대 버섯 수확!", "Giant Harvest!"],
  ["clone", "분신 소환!", "Clones Summoned!"],
  ["golden", "황금!", "Golden!"],
  ["acorn", "도토리 점등!", "Acorn Lit!"],
  ["crit", "치명!", "Crit!"],
  ["multi", "배수 획득!", "Multiplier!"],
];
const lettering = [
  ...[["ko", 1, "Korean Hangul"], ["en", 2, "English"]].map(([lang, col, script]) => ({
    id: `lettering_${lang}`, lettering: true, rows: 2, cols: 5, out: `UI/Lettering/${lang}`, refs: [PILOT],
    subject: LETTER + LETTER_WARM + ` The words are ${script}; spell every letter exactly.`,
    items: LETTER_WORDS.map((w) => [w[0], `the words "${w[col]}"`]),
  })),
  {
    id: "lettering_fx", lettering: true, rows: 1, cols: 5, out: "UI/Lettering", refs: [PILOT],
    subject: LETTER + " PALE letters: near-white cream fill with light grey watercolor shading and a soft mid-grey edge, so the game can tint them any colour. Spell every letter exactly.",
    items: [["fever", `the word "FEVER!"`], ["super_fever", `the words "SUPER FEVER!!"`], ["super_combo", `the words "SUPER COMBO!"`], ["combo", `the word "COMBO"`], ["new", `the word "NEW"`]],
  },
  {
    // field damage numbers: RoundView spells them out of these glyphs (TMP sprite asset "DmgDigits"), tinted per tier
    // NOT the bubbly headline lettering: the damage-number typeface of digits_v1 (approved shape), repainted matte and
    // muted so it sits in the watercolor field (v1 read glossy and sticker-like). Ids: u* = capital, l* = lowercase
    // (Windows file names ignore case). Also every unit suffix U.Fmt writes: K M B T Qa Qi Sx Sp Oc No Dc.
    id: "digits", lettering: true, rig: true, rows: 5, cols: 6, out: "UI/Digits", refs: [PILOT, raw("digits_v1"), raw("bg_field")],
    subject: "Damage-number font glyphs for a soft dreamy watercolor game, painted with the SAME brushes and paper as the meadow background: ONE single character per cell, all from one matching typeface. Keep the typeface of the reference numerals sheet (heavy, condensed, chunky numerals leaning forward in an italic slant with softly rounded corners) but repaint it fully MATTE: flat transparent watercolor washes on rough paper, visible paper grain and gentle pigment pooling at the stroke edges, slightly irregular hand-painted edges. Fill: very pale warm off-white paper colour with faint grey-beige watercolor mottling (no white highlight, no shine, no gradient sheen) so the game can tint it. Edge: a medium-thick soft warm sepia-brown painted rim (muted, NOT near-black, NOT a crisp vector outline), with a faint soft sepia shadow offset down-right that fades like wet paint. Capital letters and digits are the same height; lowercase letters have a smaller x-height (about two-thirds) and 'p' has a descender. No glow, no 3D bevel, no gloss, no sparkle, no saturated colour, no ornaments, no extra marks.",
    items: [
      ...[..."0123456789"].map((d) => [`d${d}`, `the digit "${d}"`]),
      ["plus", `a plus sign "+" (half the digit height)`],
      ["dot", `a small round period "." (only a dot)`],
      ["comma", `a small comma ","`],
      ["excl", `an exclamation mark "!"`],
      ["lx", `a lowercase letter "x"`],
      ...[..."KMBTGQSOND"].map((c) => [`u${c.toLowerCase()}`, `the capital letter "${c}"`]),
      ...[..."aipco"].map((c) => [`l${c}`, `the lowercase letter "${c}"`]),
    ],
  },
];

// Backgrounds: the ground / sky only; framing scenery is placed separately from Env (ArtDecor) for depth.
const GROUND = "flat matte watercolor wash with faint paper grain, calm and even with only a few large soft patches, slightly lighter in the middle; no border, no objects, no plants, no characters, no text, no UI";
const bgs = [
  ["bg_forest", "misty sage-teal mossy forest floor"],
  ["bg_night", "deep blue-teal moonlit moss with faint lavender patches (still readable, not black)"],
  ["bg_field", "soft muted yellow-green meadow grass"],
  ["bg_sea", "pale warm sand with a muted turquoise water strip along the very top edge"],
  ["bg_ruins", "worn pale grey-green cobblestones with moss between the stones, painted as soft flat washes"],
].map(([id, look]) => ({
  id, kind: "image", out: `Backgrounds/${id}.png`, refs: [ROUND, PILOT],
  prompt: `Wide landscape 16:9 game background, top-down view of a play field ground: ${look}. ${GROUND}. Same palette, haze and paper texture as the attached play field and title concept.`,
}));

const scenes = [
  {
    id: "bg_title", kind: "image", out: "Backgrounds/bg_title.png", refs: [PILOT],
    prompt: "Wide landscape 16:9 game title background: exactly the scenery of the attached title screen concept WITHOUT the logo, WITHOUT the buttons, WITHOUT the grandpa and WITHOUT the critters. Keep the misty layered forest, the faraway warm light, the giant mushrooms at the sides and the mossy ground in the lower left where characters will stand.",
  },
  {
    id: "bg_ranch", kind: "image", out: "Backgrounds/bg_ranch.png", refs: [PILOT, ROUND],
    prompt: "Wide landscape 16:9 game background, slight top-down 3/4 view: a quiet mossy glade in the misty forest for a garden around a big magical tree. Top 25%: pale hazy layered forest silhouettes and faraway warm light. Middle: a calm open mossy clearing with an old low wooden fence line in the middle distance and a light dirt path from the bottom edge to the center. No tree in the middle (placed separately), no characters, no text.",
  },
  {
    id: "tree_soil", kind: "image", out: "FX/tree_soil.png", refs: [PILOT],
    prompt: "Wide landscape 16:9 background for an upgrade-tree screen: a dark misty blue-teal underground cross-section of the forest floor with very faint pale mycelium threads and a few tiny roots, flat matte watercolor washes, calm and even so cream text and icons on top read clearly. No glow, no objects, no text.",
  },
];

// critters come after KEEP so their cap_<id> slices win over the shared-body critter_parts caps
export const SHEETS = [...mushrooms, ...KEEP.map(fromDoodle), ...critters, grandpa, tree, slots, ...fx, ...icons, ...lettering, ...env, ...bgs, ...scenes];
