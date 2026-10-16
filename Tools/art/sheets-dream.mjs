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
  {
    // 2026-10-16 UI 버튼 아이콘 + 도감 세트효과(Defs.World SETS, set_<id>)
    id: "icons_ui", rig: true, rows: 4, cols: 4, out: "Icons", refs: [raw("icons_a"), raw("icons_node"), OBJ],
    subject: ICON_RULE + " Menu-button icons and mushroom-collection set badges.",
    items: [
      ["option", "SETTINGS: a chunky wooden cogwheel with a small leaf tucked on it"],
      ["lang", "LANGUAGE: a small round earth globe in soft blue and green with a tiny speech bubble"],
      ["quest", "QUESTS: a rolled parchment scroll tied with a red ribbon and a small checkmark"],
      ["stage", "GO HARVEST: a wooden signpost arrow pointing right planted on a mossy mound"],
      ["mushtree", "UPGRADE TREE: a glowing mycelium sprout with three round branching nodes"],
      ["set_amanita", "AMANITA FAMILY: a red fly-agaric mushroom with white dots and a smaller white amanita beside it"],
      ["set_shelf", "SHELF FUNGI: three brown bracket fungi stacked on the side of a short log"],
      ["set_cordy", "CORDYCEPS BROTHERS: two thin orange club-shaped cordyceps stalks rising from a tiny caterpillar"],
      ["set_jelly", "JELLY FRIENDS: a translucent amber wood-ear jelly fungus wobbling beside a white jelly blob"],
      ["set_fart", "PUFF AND SMOKE: a round puffball mushroom puffing a soft grey cloud"],
      ["set_glow", "GLOWING FOREST: a pale mushroom with a soft mint glow halo at night beside a tiny crescent moon"],
      ["set_coral", "CORAL AND ANTLERS: a red coral fungus branching like deer antlers"],
      ["set_cook", "MUSHROOM CHEF: a white chef hat over a small pan holding a mushroom"],
      ["set_herb", "HERBALIST: a mortar and pestle with a mushroom and a green herb leaf"],
      ["set_poison", "POISON RESEARCHER: a small round purple potion flask bubbling, a tiny purple mushroom beside it"],
    ],
  },
  {
    // 2026-10-16 도감(MushDex) 전용 판·슬롯. 9-slice 값은 MoreMushSetup Configure 9-Slice (UI/Dex/*)
    id: "ui_dex", rows: 3, cols: 3, out: "UI/Dex", refs: [raw("ui_kit2"), PILOT],
    subject: "A UI kit of EMPTY interface pieces for the mushroom field-guide (herbarium) screen of a cozy watercolor PC game, flat front view, no icons, no symbols, no text inside. Flat matte watercolor (no outlines, no gloss, no glow, no drop shadow), low-saturation pastel parchment, moss and warm-wood colours matching the attached UI kit. Every frame keeps its decoration only near the edges and corners, with a large plain even centre, and even borders on all four sides so it can be 9-sliced.",
    items: [
      ["dex_window", "the big field-guide window, wide landscape: pale parchment page fill, a soft moss-green cloth border with slightly darker stitched edge, a tiny pressed fern leaf in each of the four corners"],
      ["dex_card", "a COLLECTED specimen card, portrait: cream parchment card with soft rounded corners, a thin warm-brown edge and a small washi-tape strip at the top centre"],
      ["dex_card_empty", "an EMPTY specimen slot, portrait, same size as the card: a slightly recessed pale grey-beige rounded rectangle with a soft dashed sand-coloured edge, nothing inside"],
      ["dex_card_sel", "the SELECTED specimen card, portrait, same size as the card: cream parchment card with a thicker soft butter-yellow glowing-free edge and a small washi-tape strip at the top centre"],
      ["dex_page", "the detail page, tall portrait: pale parchment page with a gently deckled soft edge and a thin moss-green rule along the top"],
      ["dex_frame", "a picture frame for a specimen illustration, square: rounded wooden frame with a thin vine and two small leaves wrapping one corner, pale cream mat in the middle"],
      ["dex_tab", "a folder bookmark tab, wide and short, rounded top corners, flat bottom: light cream-beige with a thin soft brown edge"],
      ["dex_tab_on", "the same folder bookmark tab but selected: moss-green fill with a slightly darker green edge and a tiny leaf on the top left"],
      ["dex_medal", "a round set-bonus medallion frame: honey-wood ring with tiny leaves around a pale cream centre (empty centre for an icon)"],
    ],
  },
  {
    // 2026-10-16 이름표 라벨 (도감 카드 이름 · 공방 카드 이름). 가운데는 비우고 장식은 양 끝에만 → 가로 9-slice
    id: "ui_tags", rows: 1, cols: 3, out: "UI/Dex", refs: [raw("ui_dex"), raw("ui_kit2"), PILOT],
    subject: "Three EMPTY name-tag labels for a cozy flat matte watercolor PC game UI (no outlines, no gloss, no glow, no drop shadow, no text, no letters), flat front view, each one a very wide and short horizontal shape filling its row. The middle of each tag is a long plain even strip where a name will be written; any decoration sits only at the two short ends so the tag can be stretched sideways.",
    items: [
      ["tag_name", "a paper NAME TAG: pale cream parchment label with softly rounded ends, a thin warm-brown edge, a tiny round punched hole with a short twine loop at the left end"],
      ["tag_wood", "a small WOODEN NAME PLATE: light honey-wood board with rounded ends, a thin darker wood edge and a tiny brass nail at each end"],
      ["tag_ribbon", "a soft moss-green RIBBON NAME BANNER: a flat straight ribbon band with small folded swallow-tail ends"],
    ],
  },
  // 세계수 탭 창(FarmTabWindow) 줄 아이콘 (2026-10-16) — 아이콘은 규격이 같아 한 장에 몰아 뽑는다
  {
    id: "icons_farm", rows: 3, cols: 4, grid: true, sliceAs: [4, 3], out: "Icons",   // 4행×3열로 와서 그대로 자른다 (읽는 순서 같음)
    refs: [raw("icons_a"), OBJ],
    subject: "Menu icons for the cozy World Tree screen of a mushroom-spirit idle game, readable at 48 px, in the flat matte watercolor style of the attached icon sheet (no outlines, no gloss, no glow, no sparkles, no text). Each icon = ONE big chunky main object, simple big shapes, soft low-saturation palette with one warm accent, centered with empty space around it.",
    items: [
      ["f_ring", "TREE RINGS: a round cross-section slice of a tree trunk showing concentric growth rings, a tiny green sprout on its bark edge"],
      ["f_tribute", "OFFERING: a small woven tray holding a neat heap of three little mushrooms, offered up"],
      ["f_zone", "ZONES: a small folded treasure-map style leaf map with three dotted round spots on it"],
      ["f_request", "REQUEST: a round cream speech bubble with a small red mushroom drawn inside it"],
      ["f_box", "LITTLE ONE'S BOX: a small wooden gift chest tied with a moss-green ribbon bow"],
      ["f_bless", "BLESSING: a single big glowing-looking green leaf held in a soft cupped pale-gold halo shape (painted, no glow effect)"],
      ["f_beauty", "BEAUTY: a pink blossom flower with five round petals and a tiny butterfly beside it"],
      ["f_set", "SET: three small round wooden charms hanging together from one short twig"],
      ["f_evolve", "EVOLVE: a small sapling in a clay pot with a bold upward arrow beside it"],
      ["f_slot", "EMPTY SLOT: a round empty wooden picture frame ring with a small brass hook on top"],
      ["f_recipe", "RECIPE: a rolled parchment scroll tied with twine, a tiny pencil across it"],
      ["f_craft", "CRAFTING: a small wooden mallet and a chisel crossed over a little wood block"],
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

// 2026-10-16 꼬마버섯 통짜 그림 (갓이 몸통에 붙은 한 장, 부품 리그 없음). 한 시트 = 꼬마 4종(열) × 모습 5가지(행):
// 기본 · 따기(pick) · 오르기(climb) · 나르기(carry) · 스킨(skin). 같은 꼬마는 같은 열이라 모습끼리 맞는다.
const KID_LOOK = {
  fire: "FIRE SPIRIT: tall and lively; the whole body is living flame in warm coral, apricot and butter yellow; hair is one big flickering flame swept upward; a few tiny ember sparks around; below the waist the body thins into a single curling tongue of flame; bold eyebrows and a cheeky grin",
  dew: "WATER SPIRIT: round and soft; the body is clear flowing sky-blue water with tiny bubbles inside and lighter ripples; hair is a splashing wave ponytail with a few droplets; below the waist a swirling little whirlpool tail; gentle smile",
  wind: "WIND SPIRIT: the slimmest and tallest; the body is pale translucent mint air with soft swirl lines; long hair streams sideways like wind ribbons; below the waist a thin spiraling whirlwind tail; playful wink",
  rock: "EARTH SPIRIT: the widest and heaviest, broad shoulders and big stone fists; the body is smooth warm grey-beige stone with thin amethyst crystal veins; hair is a cap of green moss with a small crystal; below the waist it sits on a floating chunk of rock instead of a tail; calm steady face with thick eyebrows",
  spark: "LIGHTNING SPIRIT: sharp angular zigzag silhouette; the body is crackling pale yellow-white electricity with small blue arcs; hair is spiky zigzag bolts; below the waist a jagged lightning-bolt tail; wide excited eyes and a toothy grin",
  leaf: "NATURE SPIRIT: the smallest and plumpest; the body is soft fresh leaf-green with a pale cream belly and fine leaf veins; hair is layered round leaves with one small white flower; below the waist a curl of vine with two tiny leaves; cheerful open smile",
  moon: "MOON SPIRIT: serene and elegant; the body is translucent night-blue and periwinkle; a thin silver crescent moon ornament crowns the hair; a long starry night veil flows behind; below the waist the veil trails into a soft tail of tiny stars; calm half-closed eyes",
  star: "STAR SPIRIT: sparkly and bright; the body is pale gold and lilac starlight; hair spreads into five soft rounded star points; below the waist a twinkling comet tail of tiny dots; big starry eyes and a happy smile",
};
const KID_SKIN = {
  fire: "LAVA skin: the flame body turns deep magma red with dark basalt flecks and molten orange cracks",
  dew: "SNOWFLAKE skin: the water body turns frosty pale ice-blue with little snowflake patterns, the ponytail ends in an icicle",
  wind: "CLOUD skin: the air body becomes fluffy white cloud puffs with a tiny rainbow in the swirl tail",
  rock: "JEWEL skin: the stone body is studded with faceted pastel crystals and a big pink gem on top",
  spark: "NEON skin: the lightning turns candy pink and electric teal, small round sunglasses",
  leaf: "CHERRY-BLOSSOM skin: the leaf hair turns pale sakura pink with small blossoms, a petal on the cheek",
  moon: "SUN skin: the crescent becomes a warm little smiling sun and the night veil turns sunflower yellow",
  star: "GALAXY skin: the starlight body turns deep night-blue sprinkled with tiny stars and a pastel nebula swirl",
};
const KID_ROWS = [
  ["", (k) => `${KID_LOOK[k]}. BASE LOOK: no clothes, tiny arms slightly out`],
  ["_pick", (k) => `${KID_LOOK[k]}. PICKING OUTFIT: a small straw sun hat perched on its head, a short canvas apron with a pocket, holding a little woven harvest basket in both hands`],
  ["_climb", (k) => `${KID_LOOK[k]}. CLIMBING OUTFIT: a small round yellow safety helmet perched on its head, a rope harness around the body with a coiled rope over one shoulder, reaching one hand up as if climbing`],
  ["_carry", (k) => `${KID_LOOK[k]}. CARRYING OUTFIT: a wooden A-frame back carrier (jige) on its back loaded with a big cloth bundle of mushrooms, a sweat towel around the neck, leaning slightly forward`],
  ["_skin", (k) => `${KID_LOOK[k]}. ${KID_SKIN[k]}, otherwise exactly the same spirit`],
];
const KID_SUBJECT = "Cute little ELEMENTAL SPIRITS who live in a giant world tree in a cozy dreamy forest game, in the spirit of MapleStory Arcana spirits and Pixar Elemental characters: a small humanoid upper body (head, torso, tiny arms and hands) whose WHOLE BODY IS MADE OF ITS OWN ELEMENT, and instead of legs the lower body flows into an elemental wisp tail, so they float. Big expressive eyes with catchlights, small mouth, cute chibi head about one third of the height. Painted in the flat matte watercolor style of the attached title-screen painting (soft paper grain, soft colors, no hard outlines, no gloss, no outer glow halo; any light is painted inside the body). They are NOT mushrooms and NOT round blobs: each spirit has a clearly different silhouette, body build and size as described, so all eight are told apart at 64 px. Every picture is centered in its cell and fills it the same way, front three-quarter view, no ground, no shadow.";
const SPIRIT_REF = "Art/Raw_Dream/concept/spirits.png";   // 확정 시안 (2026-10-16, 4행 × 2열: 불·물 / 바람·땅 / 번개·새싹 / 달·별)
// 기본 모습 8종 한 장(먼저 승인) → 복장·스킨은 기본 시트를 참고로 4종 × 4행 두 장
const KID_ORDER = ["fire", "dew", "wind", "rock", "spark", "leaf", "moon", "star"];
const kidBase = {
  // 시안을 그대로 참고하면 어두운 빛 번짐 배경까지 따라 그려서, 투명하게 나온 복장 시트 두 장을 참고로 쓴다
  id: "spirits_base", rows: 2, cols: 4, out: "Characters/Kids", refs: [raw("spirits_a"), raw("spirits_b")],
  subject: KID_SUBJECT + " Draw the eight spirits EXACTLY as they look in the two attached sheets (same shapes, colors, faces) but with NO hat, NO clothes, NO bag, NO tools: just the bare spirit, one per cell, neutral floating pose, arms slightly out. Order left to right, top to bottom: FIRE, WATER, WIND, EARTH / LIGHTNING, NATURE, MOON, STAR. FULLY TRANSPARENT BACKGROUND: no dark backdrop, no colored glow or light bloom behind or around the spirits.",
  items: KID_ORDER.map((k) => [k, KID_ROWS[0][1](k)]),
};
const kidSheet = (id, kinds) => ({
  id, rows: 4, cols: 4, out: "Characters/Kids", refs: [SPIRIT_REF, PILOT],
  subject: KID_SUBJECT + " FULLY TRANSPARENT BACKGROUND: no dark backdrop, no colored glow or light bloom around the spirits. Draw each spirit EXACTLY as it looks in the attached spirit concept sheet. Each COLUMN is one spirit (same shape, colors, face); each ROW is one look. Columns left to right: " + kinds.map((k) => k.toUpperCase()).join(", ") + ".",
  items: KID_ROWS.slice(1).flatMap(([suf, desc]) => kinds.map((k) => [k + suf, desc(k)])),
});
const kids = [kidBase, kidSheet("spirits_a", ["fire", "dew", "wind", "rock"]), kidSheet("spirits_b", ["spark", "leaf", "moon", "star"])];


// 2026-10-16 정령 부품 리그: 머리(얼굴+머리카락) · 몸통+꼬리(팔·머리 없음) · 팔(보는 쪽 왼팔 하나, 반대는 뒤집음).
// 복장은 몸통만 바꾼다 (모자는 공용 그림을 머리에 얹음). 스킨은 머리·몸통·팔 셋 다. 자리는 통짜 그림에 대어 자동으로 맞춘다
// (Tools/art/fit_spirit_parts.py). 한 정령 = 3행: [head, torso, arm] / [torso_pick, torso_climb, torso_carry] / [head_skin, torso_skin, arm_skin]
const PART_RULE = "A CUT-OUT PARTS SHEET for 2D puppet animation of cute elemental spirits, on a FULLY TRANSPARENT background, no glow, no shadow. Every part is drawn separately with clear empty space around it, at EXACTLY the same scale as the matching spirit in the attached sheets, front view, so the parts can be stacked back into the same character. HEAD part = the head with its face and all of its hair/flame/crown, cut cleanly at the neck (nothing below the chin except a short neck stub). TORSO part = the body from the neck down INCLUDING the whole lower wisp tail (or floating rock base), with NO head and NO arms: just smooth round shoulders where the arms attach. ARM part = only the spirit's LEFT arm as seen by the viewer, hanging down, from the shoulder to the tiny hand. Outfit torsos wear the outfit painted on the torso exactly as in the attached outfit sheet but without the hat. Keep the flat matte watercolor look of the attached sheets.";
const partRows = (k, name) => [
  [`p_${k}_head`, `${name} HEAD (face and hair, no hat)`], [`p_${k}_torso`, `${name} TORSO with tail, no arms, no clothes`], [`p_${k}_arm`, `${name} LEFT ARM`],
  [`p_${k}_torso_pick`, `${name} TORSO wearing the picking outfit: canvas apron, a woven basket held against the tummy, no arms`],
  [`p_${k}_torso_climb`, `${name} TORSO wearing the climbing outfit: rope harness and a coiled rope over one shoulder, no arms`],
  [`p_${k}_torso_carry`, `${name} TORSO wearing the carrying outfit: wooden A-frame back carrier loaded with a cloth bundle of mushrooms behind the back, towel around the neck, no arms`],
  [`p_${k}_head_skin`, `${name} HEAD in its skin colors (see the skin row of the attached outfit sheet)`], [`p_${k}_torso_skin`, `${name} TORSO with tail in its skin colors, no arms`], [`p_${k}_arm_skin`, `${name} LEFT ARM in its skin colors`],
];
// style: 이미 잘 나온 부품 시트 하나를 그림체 참고로 더 붙인다 (번개·새싹 첫 시트가 외곽선 있는 애니풍으로 나와서, 2026-10-16)
const spiritParts = (id, kinds, names, ref, style) => ({
  id, rig: true, rows: kinds.length * 3, cols: 3, out: "Characters/Spirits", refs: [raw("spirits_base"), raw(ref), ...(style ? [raw(style)] : [])],
  subject: PART_RULE + (style ? " PAINTING STYLE: match the attached cut-out parts sheet of other spirits EXACTLY (soft textured watercolor with visible paper grain and faceted color patches, NO outlines, NO line art, NO cel shading, the same soft eyes with no dark outline around the face), and paint the faces exactly like the matching spirit in the attached full-spirit sheet." : "") + " Rows top to bottom: " + kinds.map((k, i) => `three rows for the ${names[i]}`).join(", ") + ".",
  items: kinds.flatMap((k, i) => partRows(k, names[i])),
});
const spiritPartSheets = [
  spiritParts("parts_pilot", ["fire", "rock"], ["FIRE SPIRIT", "EARTH SPIRIT"], "spirits_a"),
  { ...spiritParts("parts_dew_wind", ["dew", "wind"], ["WATER SPIRIT", "WIND SPIRIT"], "spirits_a"), sliceAs: [3, 6] },   // 3행 × 6열로 나옴
  { ...spiritParts("parts_spark_leaf", ["spark", "leaf"], ["LIGHTNING SPIRIT", "NATURE SPIRIT"], "spirits_b", "parts_dew_wind"), sliceAs: [3, 6] },
  { ...spiritParts("parts_moon_star", ["moon", "star"], ["MOON SPIRIT", "STAR SPIRIT"], "spirits_b"), sliceAs: [3, 6] },
];

// 2026-10-16 정령 표정: 머리는 눈·눈썹·입을 지운 빈 얼굴(build_spirit_faces.py가 원래 머리에서 잘라 냄), 표정은 얼굴 층만 갈아 끼운다.
// 기본 얼굴은 원래 머리에서 떼어 낸 것, 나머지 6개는 여기서 "이목구비만" 그려서 기본 얼굴 크기·자리에 맞춘다.
// 한 정령 = 2행 × 3열: [blink, happy, surprise] / [love, effort, tired]. 참고 그림 = 원래 머리 | 떼어 낸 기본 얼굴 (정령마다 한 줄)
const FACE_RULE = "A FACE-PARTS SHEET for 2D puppet animation: ONLY the facial features (the two eyes, the two eyebrows and the mouth) of the spirits in the attached reference, on a FULLY TRANSPARENT background. NO head, NO skin, NO face outline, NO hair, NO blush, NO sweat drops, NO tears, NO symbols or effects: just the separate painted feature marks floating in empty space, laid out exactly as they sit on the face (eyes level, eyebrows above them, mouth centred below). The reference shows each spirit's head and, next to it, its neutral face features alone: copy that spirit's exact feature style (shape language, stroke colours, eye colour, eyelashes or fangs), eye size, eye spacing and eyebrow position, and draw every expression at the SAME size as that neutral face, filling its cell the same way; only what the expression changes may change. Flat matte watercolor like the reference, no outlines added, no glow.";
const FACE_EXPR = [
  ["blink", "BLINK: both eyes gently closed as soft downward curves, the usual calm eyebrows, the usual mouth"],
  ["happy", "HAPPY: eyes closed in happy upward arcs (like ^ ^), a big open smile"],
  ["surprise", "SURPRISED: eyes wide open and round, eyebrows raised high, a small round open 'o' mouth"],
  ["love", "LOVE: both eyes are small pink-red hearts, relaxed eyebrows, a wide open smile"],
  ["effort", "EFFORT: determined slightly narrowed eyes, eyebrows angled down toward the middle, mouth clenched in a straight grit"],
  ["tired", "TIRED: droopy half-closed eyes, eyebrows slanting up in the middle, a small wavy mouth"],
];
const spiritFaces = (id, kinds, names, ref) => ({
  id, grid: true, rows: kinds.length * 2, cols: 3, out: "../../../../Art/Raw_Dream/faces", refs: [ref],
  subject: FACE_RULE + " Rows top to bottom: " + kinds.map((k, i) => `two rows for the ${names[i]} (reference row ${i + 1})`).join(", ") + ".",
  items: kinds.flatMap((k, i) => FACE_EXPR.map(([e, d]) => [`f_${k}_${e}`, `${names[i]} face features, ${d}`])),
});
const spiritFaceSheets = [
  spiritFaces("faces_pilot", ["fire", "rock"], ["FIRE SPIRIT", "EARTH SPIRIT"], raw("faces_ref_pilot")),
  spiritFaces("faces_dew_wind", ["dew", "wind"], ["WATER SPIRIT", "WIND SPIRIT"], raw("faces_ref_dew_wind")),
  spiritFaces("faces_spark_leaf", ["spark", "leaf"], ["LIGHTNING SPIRIT", "NATURE SPIRIT"], raw("faces_ref_spark_leaf")),
  spiritFaces("faces_moon_star", ["moon", "star"], ["MOON SPIRIT", "STAR SPIRIT"], raw("faces_ref_moon_star")),
];

// 2026-10-16 정령 진화 컨셉 (사용자): ★0 그냥 버섯 → ★3 귀여운 수인형 버섯 정령, 4모습이 점점 변하는 게 보이게. 그림체는 지금 게임(몽환 수채).
// 원소별 실제 버섯: leaf 이끼·녹색 · dew 젖버섯(쪽빛) · wind 느타리(흰) · fire 광대버섯(빨강) · rock 그물버섯(갈색) · spark 꾀꼬리버섯(노랑) · moon 발광 버섯 · star 먼지버섯(별)
// 리깅 메모: 버섯 모습(★0·★1)은 갓과 대를 나누지 않고 몸통 한 장(갓 붙은 대)으로 리깅한다 — 갓을 따로 떼면 갓 밑면에 몸통 자리를
// 파내야 했고(갓마다 손으로 좌표, 시트를 다시 그리면 다시 찍기, 납작한 갓은 못 팜) 문제가 많았다 (2026-10-15_몽환수채화_2.md)
const EVO_SPECIES = {
  fire: "FIRE: a FLY AGARIC (Amanita muscaria): glossy-matte red cap with soft white warts, white stem with a little skirt ring; warm ember accents",
  dew: "WATER: an INDIGO MILK CAP (Lactarius indigo): soft indigo-blue funnel cap with darker blue rings, pale blue gills that drip tiny dew drops; cool watery accents",
  leaf: "NATURE: a MOSSY GREEN mushroom (like a green russula with soft moss on the cap and a tiny sprout on top): sage-green cap, cream stem; leafy accents",
  wind: "WIND: an OYSTER MUSHROOM (Pleurotus): pale cream-white fan-shaped wavy cap that sweeps to one side as if in a breeze, soft gills; airy breezy accents",
  rock: "EARTH: a PORCINI (Boletus edulis): round chestnut-brown bun cap, thick pale stout stem with a fine net pattern; small pebble accents",
  spark: "LIGHTNING: a GOLDEN CHANTERELLE (Cantharellus): bright golden-yellow trumpet cap with wavy edge and forked ridges; little zigzag spark accents",
  moon: "MOON: a GLOWING night mushroom (bioluminescent, like Mycena): slender bell cap in soft lavender-night blue that glows faint mint from within (painted, no halo), thin pale stem; crescent moon accents",
  star: "STAR: an EARTHSTAR (Geastrum): a round puffball sitting on star-shaped outer rays like petals, warm cream and soft gold with a star tip; twinkling star accents",
};
const EVO_V1 = "Art/Raw_Dream/concept/evo_v1.png";   // 확정 시안 (불·물 4모습)
// 사용자 확정 (2026-10-16): 첫 시안(v1) 느낌으로. 무드보드(유아 카와이 덩어리 · 성장 흐느적 버섯 사람 · 최종 버섯 요정)로 다시 그린 v2는
// Art/Raw_Dream/_rejected/evo_pilot_v2.png 에 보관 (선택 안 됨)
const EVO_FORMS = [
  "FORM 0 (★0): just a small cute MUSHROOM with a face: the cap and a short chubby stem in one piece, a simple cute face on the stem under the cap, tiny nub feet, NO arms, NO hair, it still reads mostly as a mushroom",
  "FORM 1 (★1): a mushroom sprout-critter: the same cap a little bigger, the stem grows into a small round body with two tiny stubby arms and tiny feet, the face a bit more expressive; still clearly a mushroom",
  "FORM 2 (★2): a mushroom child spirit: about 2 heads tall, a small head with a face and short hair-like strands of gill under the cap, the cap worn like a big hat, the stem becomes a little dress-like body with a gill ruffle, short arms with small hands, short legs",
  "FORM 3 (★3): the final cute HUMANOID MUSHROOM SPIRIT: about 2.5 heads tall chibi, the cap is a big elegant hat-like cap over soft hair, the gills become a flowing ruffled skirt, the stem pattern becomes the outfit, slender arms with hands, small legs and feet, a gentle magical charm and a small element motif; graceful and cute, not scary",
];
// 그림체 (2026-10-16 사용자: 기존 그림체와 이질적 → 코드 필터 말고 그림 자체를 다시): 펠트·인형 질감과 입체 음영이 나왔던 첫 판을
// 배경·기존 정령처럼 맑은 수채로 다시 그린다. 디자인은 확정 시안 그대로 (첫 판은 concept/evo_*_v1.png 로 남겨 디자인 참고로 씀)
const EVO_STYLE = "PAINTING STYLE (most important): paint exactly like the attached title-screen painting and the attached watercolor spirit sheet of this game: light transparent watercolor washes on paper, visible paper grain, soft faceted colour patches inside each area, a thin slightly darker pigment edge where a wash dried, gentle pastel colours, flat soft lighting. NOT felt, NOT plush, NOT fuzzy or fabric texture, NOT 3D rendered, no airbrush gradients, no hard black outlines. Faces: small soft dark-brown dot eyes, soft blush.";
const EVO_STYLE_REFS = [PILOT, raw("spirits_base")];
const evoDesign = (id) => `Art/Raw_Dream/concept/${id}_v1.png`;   // 첫 판 (디자인만 참고)
const evoConcept = {
  id: "evo_pilot", rows: 2, cols: 4, grid: true, out: "Characters/Evo", refs: [evoDesign("evo_pilot"), ...EVO_STYLE_REFS],
  subject: "EVOLUTION LINE sheet for a cozy dreamy forest game: each ROW is ONE mushroom spirit evolving left to right through four forms, from a plain cute mushroom to a cute humanoid mushroom spirit. Keep EXACTLY the designs, proportions, poses and sizes of the attached evolution concept (the first attached image), only repaint them in the game's watercolor style. " + EVO_STYLE + " Front three-quarter view, centered in each cell, FULLY TRANSPARENT BACKGROUND, no ground, no shadow, no text. Rows top to bottom: FIRE spirit, WATER spirit.",
  items: ["fire", "dew"].flatMap((k) => EVO_FORMS.map((f, i) => [`evo_${k}_${i}`, `${EVO_SPECIES[k]}. ${f}`])),
};

// 나머지 6종 진화 줄: 정령마다 한 줄 × 4모습. 이 통짜 그림이 칸·아이콘 그림이자 부품 시트의 기준 (크기 k도 여기서)
const evoLine = (id, kinds) => ({
  id, rows: kinds.length, cols: 4, grid: true, out: "Characters/Evo", refs: [evoDesign(id), ...EVO_STYLE_REFS],
  subject: "EVOLUTION LINE sheet for a cozy dreamy forest game: each ROW is ONE mushroom spirit evolving left to right through four forms (plain cute mushroom, mushroom sprout-critter with stubby arms, mushroom child spirit with hair and a gill dress, final humanoid mushroom spirit with long hair, a ruffled gill skirt and a small element motif by one hand). Keep EXACTLY the designs, proportions, poses and sizes of the first attached sheet, only repaint them in the game's watercolor style. " + EVO_STYLE + " Front three-quarter view, centered in each cell, FULLY TRANSPARENT BACKGROUND, no ground, no shadow, no text. Rows top to bottom: " + kinds.map((k) => k.toUpperCase()).join(", ") + ".",
  items: kinds.flatMap((k) => EVO_FORMS.map((f, i) => [`evo_${k}_${i}`, `${EVO_SPECIES[k]}. ${f}`])),
});
const evoLines = [evoLine("evo_line_a", ["leaf", "wind", "rock"]), evoLine("evo_line_b", ["spark", "moon", "star"])];

// 진화 부품 시트 (2026-10-16 사용자: 다리도 리깅 · 머리카락은 확실히 뒤로 — 손·몸통을 가리지 않게 따로 그림). 정령 하나 = 3행 × 5열 고정 격자:
// [★2 머리 · 뒷머리 · 몸통 · 팔 · 다리] / [★3 같은 순서] / [★0 몸 · ★1 몸] (★0·★1은 갓과 대를 나누지 않은 한 장)
const EVO_PART_RULE = "A CUT-OUT PARTS SHEET for 2D puppet animation, on a FULLY TRANSPARENT background, no glow, no shadow, no text. Every part is drawn separately with clear empty space around it, at EXACTLY the same scale and colors as the matching form of this spirit in the attached evolution line, front view, so the parts stack back into the same character. " + EVO_STYLE + " PART RULES: HEAD = the face, the cap worn on it and ONLY the short front bangs that frame the face; the long hair is NOT part of the head; cut cleanly at the neck with a short neck stub under the chin. BACK HAIR = only the long hair mass as it hangs BEHIND the head and shoulders (the back layer of the hairstyle, the full hair silhouette), with NO face, NO cap, NO body. TORSO = the dress or skirt from a short neck stub at the top down to the hem, with smooth round shoulder nubs on both sides, and NO head, NO arms, NO hands, NO legs, NO feet (just the clean hem at the bottom). ARM = only the viewer's LEFT arm, hanging straight down, from a round shoulder end to the small hand. LEG = only the viewer's LEFT leg with its little shoe, hanging straight down, with a round hip end at the top (the legs come out from under the skirt). BODY (first two forms only) = the cap and the stem/body as ONE piece, never cut the cap off, with the face and the tiny feet.";
const evoParts = (k) => ({
  id: `evo_parts_${k}`, grid: true, rows: 3, cols: 5, out: "Characters/Evo",
  refs: [raw(["fire", "dew"].includes(k) ? "evo_pilot" : ["leaf", "wind", "rock"].includes(k) ? "evo_line_a" : "evo_line_b"), ...EVO_STYLE_REFS],
  subject: EVO_PART_RULE + ` The spirit: ${EVO_SPECIES[k]}. Use the ${k.toUpperCase()} row of the first attached evolution line.`,
  items: [
    [`e_${k}_2_head`, "FORM 2 HEAD: face, cap and short front bangs only, short neck stub"],
    [`e_${k}_2_hair`, "FORM 2 BACK HAIR: the short bob hair mass behind the head, no face, no cap"],
    [`e_${k}_2_torso`, "FORM 2 TORSO: little gill dress, neck stub, shoulder nubs, no arms, no legs"],
    [`e_${k}_2_arm`, "FORM 2 LEFT arm, hanging down"],
    [`e_${k}_2_leg`, "FORM 2 LEFT short leg with shoe, hanging down, round hip end"],
    [`e_${k}_3_head`, "FORM 3 HEAD: face, big cap and short front bangs only, short neck stub"],
    [`e_${k}_3_hair`, "FORM 3 BACK HAIR: the long flowing hair mass behind the head and shoulders, no face, no cap"],
    [`e_${k}_3_torso`, "FORM 3 TORSO: ruffled gill skirt gown, neck stub, shoulder nubs, no arms, no hands, no legs"],
    [`e_${k}_3_arm`, "FORM 3 LEFT slender arm with hand, hanging down"],
    [`e_${k}_3_leg`, "FORM 3 LEFT leg with small shoe, hanging down, round hip end"],
    [`e_${k}_0_body`, "FORM 0 whole body: cap and stem in one piece, face, tiny feet, no arms"],
    [`e_${k}_1_body`, "FORM 1 whole body: cap and round body in one piece, face, tiny stubby arms and tiny feet painted on"],
  ],
});
const evoPartSheets = ["fire", "dew", "leaf", "wind", "rock", "spark", "moon", "star"].map(evoParts);
// 진화 정령 표정: 모든 정령·모습이 같은 점눈 얼굴이라 표정 한 벌을 같이 쓴다 (fit_evo_parts.py가 모습마다 기본 얼굴 눈 간격에 맞춤)
const evoFaces = {
  id: "evo_faces", grid: true, rows: 2, cols: 3, out: "../../../../Art/Raw_Dream/faces", refs: [raw("faces_ref_evo")],
  subject: FACE_RULE.replace("The reference shows each spirit's head and, next to it, its neutral face features alone", "The reference shows two spirits and, next to each, its neutral face features alone (simple round dark dot eyes and a small curved mouth)") + " All six cells are the SAME character's face features (the simple dot-eyed face of the reference), drawn at the same size as the reference's neutral features.",
  items: FACE_EXPR.map(([e, d]) => [`fe_${e}`, d]),
};

// 공용 맨몸 틀 + 부위별 한 장씩 (2026-10-16 사용자: 시트에 몰아 그리면 칸이 빠짐 · 복장은 정령끼리 공용).
// 층: 맨몸(공용 몸통 바탕·팔·다리) / 복장(공용: 옷·신발) / 정령 머리(갓+얼굴+앞머리) / 정령 뒷머리 / 얼굴(공용 표정).
// 먼저 공용 마네킹(kit_base_<★>) 한 장 → 모든 부위가 그 크기를 기준으로 그린다. 지금은 ★3 불 예시만.
const KIT_ONE = "ONE single cut-out part for 2D puppet animation, alone and centered, on a FULLY TRANSPARENT background, no glow, no shadow, no ground, no text. Front view, drawn at EXACTLY the scale of the attached base mannequin (the first attached image) so it stacks back onto it. " + EVO_STYLE;
// 조립 재료(EvoKit)는 게임이 직접 쓰지 않으니 Resources 밖에 둔다 (kit_preview.py --export가 Characters/Evo/k_* 로 내보냄). 경로는 Dream gen 루트 기준
const KIT_OUT = "../../../ArtSource/Dream/EvoKit";
const KIT_BASE_3 = "kit_base_3";
const kitOne = (id, refs, subject) => ({ id, rows: 1, cols: 1, grid: true, out: KIT_OUT, refs: [...refs, ...EVO_STYLE_REFS], subject, items: [[id, subject.slice(0, 80)]] });

// 정령별 머리·뒷머리 (★2·★3). 진화 줄 그림에서 그 정령의 줄·칸을 짚어 준다. ★2 뒷머리는 턱선 단발 (2026-10-16 사용자: 머리 짧게)
const EVO_KIDS = ["fire", "dew", "leaf", "wind", "rock", "spark", "moon", "star"];
const EVO_ROW = { fire: ["evo_pilot", "top"], dew: ["evo_pilot", "bottom"], leaf: ["evo_line_a", "top"], wind: ["evo_line_a", "middle"], rock: ["evo_line_a", "bottom"], spark: ["evo_line_b", "top"], moon: ["evo_line_b", "middle"], star: ["evo_line_b", "bottom"] };
const EVO_COL = { 2: "third column", 3: "rightmost column" };
const kitBaseOf = (f) => (f === 2 ? "kit_base_2" : KIT_BASE_3);
const kitHead = (k, f) => kitOne(`kit_${k}_${f}_head`, [raw(kitBaseOf(f)), raw(EVO_ROW[k][0])], KIT_ONE + ` PART: the ${k.toUpperCase()} spirit's HEAD for the mannequin: the face (small dark dot eyes, soft blush, small smile), its mushroom cap worn like a hat (${EVO_SPECIES[k]}), and ONLY the short front bangs framing the face, copied from the FORM ${f} ${k.toUpperCase()} spirit (${EVO_ROW[k][1]} row, ${EVO_COL[f]}) of the attached evolution sheet; the back hair is NOT included; cut cleanly at the neck with a short neck stub under the chin.`);
const kitHair = (k, f) => kitOne(`kit_${k}_${f}_hair`, [raw(kitBaseOf(f)), raw(EVO_ROW[k][0])], KIT_ONE + ` PART: the ${k.toUpperCase()} spirit's BACK HAIR only: the ${f === 3 ? "long hair mass" : "SHORT chin-length bob hair"} of the FORM ${f} ${k.toUpperCase()} spirit (${EVO_ROW[k][1]} row, ${EVO_COL[f]}) of the attached evolution sheet as it hangs BEHIND the head ${f === 3 ? "and shoulders down to the waist" : "ending at the chin and jaw line, NOT reaching the shoulders, small and neat, only a little wider than the face"}, the full back silhouette of the hairstyle in that spirit's hair colour, with NO face, NO cap, NO body.`);
// ★2 공용 틀: 마네킹 · 몸통 · 드레스 · 신발 신은 다리 (팔은 생성기 필터에 막혀 예전 불 ★2 팔을 공용으로 씀)
const kitBase2 = [
  { id: "kit_base_2", rows: 1, cols: 1, grid: true, out: KIT_OUT, refs: [raw("evo_pilot"), raw(KIT_BASE_3), ...EVO_STYLE_REFS],
    subject: "A SHARED BASE MANNEQUIN for a 2D puppet: the plain body of the FORM 2 (★2) mushroom child spirit of the attached evolution sheet (top row, third column), same height and proportions (about 2 heads tall, smaller and chubbier than the second attached mannequin) and pale cream skin, drawn in the SAME way as the second attached mannequin: NO mushroom cap, NO hair, NO face (a smooth blank round head), wearing only a plain simple sleeveless cream short slip dress, arms hanging down slightly away from the body, legs slightly apart. Front view, symmetric, standing straight. " + EVO_STYLE + " FULLY TRANSPARENT BACKGROUND, no ground, no shadow, no text.",
    items: [["kit_base_2", "base mannequin 2"]] },
  kitOne("kit_body_2_torso", [raw("kit_base_2")], KIT_ONE + " PART: the mannequin's TORSO only, in its plain cream slip: from a short neck stub at the top down to the hem, with smooth round shoulder nubs, NO head, NO arms, NO legs."),
  kitOne("kit_outfit_gill_2_dress", [raw("kit_base_2"), raw("evo_pilot")], KIT_ONE + " PART: an OUTFIT piece worn over the mannequin's torso: the little cream mushroom-gill dress of the FORM 2 fire spirit in the attached evolution sheet (top row, third column) with a petal collar and a short gill ruffle skirt, shaped to fit EXACTLY over the mannequin's torso: from a neck opening at the top to the skirt hem, covering the shoulder nubs, with NO head, NO arms, NO legs, NO body visible."),
  kitOne("kit_outfit_gill_2_leg", [raw("evo_pilot")], KIT_ONE.replace("the attached base mannequin (the first attached image)", "the FORM 2 spirit of the attached evolution sheet") + " PART: a cartoon puppet piece: ONE very short stubby LEG of the chibi mushroom child spirit (top row, third column of the attached sheet) WEARING its little soft brown shoe, exactly as the legs peek out under the skirt: cream, slightly rounded top end, the shoe toe pointing a little outward, hanging straight down. Simple, cute, toy-like."),
];
const kitSheets = [
  { id: KIT_BASE_3, rows: 1, cols: 1, grid: true, out: KIT_OUT, refs: [raw("evo_pilot"), ...EVO_STYLE_REFS],
    subject: "A SHARED BASE MANNEQUIN for a 2D puppet: the plain body of the FORM 3 (★3) mushroom spirit girl of the attached evolution sheet (top row, rightmost), same height, proportions (about 2.5 heads tall chibi) and pale cream skin, but with NO mushroom cap, NO hair, NO face (a smooth blank round head), wearing only a plain simple sleeveless cream knee-length slip dress, bare arms hanging down slightly away from the body, legs slightly apart, bare feet. Front view, symmetric, standing straight. " + EVO_STYLE + " FULLY TRANSPARENT BACKGROUND, no ground, no shadow, no text.",
    items: [[KIT_BASE_3, "base mannequin"]] },
  kitOne("kit_body_3_torso", [raw(KIT_BASE_3)], KIT_ONE + " PART: the mannequin's TORSO only, in its plain cream slip: from a short neck stub at the top down to the hem, with smooth round shoulder nubs, NO head, NO arms, NO legs."),
  // 팔(kit_body_3_arm)은 생성기 안전 필터에 계속 막혀(맨팔·소매·인형 팔 모두) 정의를 뺐다 — 예전 불 팔(e_fire_3_arm·e_fire_2_arm)을 EvoKit/kit_body_<모습>_arm.png로 복사해 공용으로 씀
  // 다리는 신발까지 한 장 (2026-10-16 사용자: 발을 따로 나눌 필요 없음) → 복장 부품: 복장을 바꾸면 다리째 바꾼다
  kitOne("kit_outfit_gill_3_leg", [raw("evo_pilot")], KIT_ONE.replace("the attached base mannequin (the first attached image)", "the FORM 3 spirit of the attached evolution sheet") + " PART: a cartoon puppet piece: ONE short LEG of the chibi mushroom spirit (top row, rightmost of the attached sheet) WEARING its little soft brown shoe, exactly as the legs peek out under the skirt in the attached sheet: slim and gently tapered, cream, slightly rounded top end, the shoe toe pointing a little outward, hanging straight down. Simple, cute, toy-like."),
  kitOne("kit_outfit_gill_3_dress", [raw(KIT_BASE_3), raw("evo_pilot")], KIT_ONE + " PART: an OUTFIT piece that is worn over the mannequin's torso: the cream ruffled mushroom-gill dress of the FORM 3 fire spirit in the attached evolution sheet (petal collar, layered ruffled gill skirt), shaped to fit EXACTLY over the mannequin's torso: from a neck opening at the top to the skirt hem, covering the shoulder nubs, with NO head, NO arms, NO legs, NO body visible."),
  // (예전 신발 따로) kitOne("kit_outfit_gill_3_shoe", [raw(KIT_BASE_3), raw("evo_pilot")], KIT_ONE + " PART: an OUTFIT piece: ONE small soft brown shoe for the mannequin's LEFT foot (like the shoes of the FORM 3 spirit in the attached evolution sheet), front view, fitting EXACTLY over the mannequin's bare foot."),
  ...kitBase2,
  // ★2 뒷머리는 kitHair2Sheets (짧은 단발 4칸 시트)
  ...EVO_KIDS.flatMap((k) => [2, 3].flatMap((f) => (k === "fire" && f === 3 ? [] : f === 2 ? [kitHead(k, f)] : [kitHead(k, f), kitHair(k, f)]))),
  // 불 ★3 (첫 예시, 문구 그대로 둬야 다시 뽑히지 않는다)
  kitOne("kit_fire_3_head", [raw(KIT_BASE_3), raw("evo_pilot")], KIT_ONE + " PART: the FIRE spirit's HEAD for the mannequin: the face (small dark dot eyes, soft blush, small smile), the big red fly agaric cap with white warts worn like a hat, and ONLY the short cream front bangs framing the face, copied from the FORM 3 fire spirit (top row, rightmost) of the attached evolution sheet; the long back hair is NOT included; cut cleanly at the neck with a short neck stub under the chin."),
  kitOne("kit_fire_3_hair", [raw(KIT_BASE_3), raw("evo_pilot")], KIT_ONE + " PART: the FIRE spirit's BACK HAIR only: the long wavy cream hair mass of the FORM 3 fire spirit (top row, rightmost) of the attached evolution sheet as it hangs BEHIND the head and shoulders down to the waist, the full back silhouette of the hairstyle, with NO face, NO cap, NO body."),
];

// 공용 작업복 (CritterRig.outfit: pick 따기 · climb 오르기 · carry 나르기). 기본 드레스처럼 마네킹 몸통에 덮어 입는 한 장 — 모자는 갓과 겹쳐 뺀다
const KIT_OUTFITS = {
  pick: "a PICKING outfit: the cream gill dress with a short canvas apron with a front pocket tied over it",
  climb: "a CLIMBING outfit: the cream gill dress with a brown rope harness around the waist and a small coiled rope over one shoulder",
  carry: "a CARRYING outfit: the cream gill dress with a little sweat towel around the neck and the shoulder straps of a back carrier",
};
const kitOutfit = (o, f) => kitOne(`kit_outfit_${o}_${f}_dress`, [raw(kitBaseOf(f)), raw(`kit_outfit_gill_${f}_dress`)], KIT_ONE + ` PART: an OUTFIT piece worn over the mannequin's torso: ${KIT_OUTFITS[o]}, based on the second attached dress (same shape, size and collar), shaped to fit EXACTLY over the mannequin's torso: from a neck opening at the top to the skirt hem, covering the shoulder nubs, with NO head, NO arms, NO legs, NO hat, NO body visible.`);
const kitOutfitSheets = ["pick", "climb", "carry"].flatMap((o) => [2, 3].map((f) => kitOutfit(o, f)));

// ★2 짧은 뒷머리 다시 뽑기 (2026-10-16 사용자: ★2는 머리 짧게) — 토큰 아끼려 4칸 시트 2장으로 묶음 (12칸에서만 칸이 빠졌음)
const kitHair2 = (id, kids) => ({
  id, rows: 2, cols: 2, grid: true, out: KIT_OUT, refs: [raw("kit_base_2"), raw("evo_pilot"), raw("evo_line_a"), raw("evo_line_b"), ...EVO_STYLE_REFS],
  subject: KIT_ONE.replace("ONE single cut-out part", "Cut-out parts").replace("alone and centered", "each alone and centered in its cell") + " Each cell is the BACK HAIR only of one FORM 2 mushroom child spirit from the attached evolution sheets (third column of its row): a SHORT chin-length bob ending at the chin and jaw line, NOT reaching the shoulders, small and neat, only a little wider than the face, the back layer of the hairstyle as it sits BEHIND the head, in that spirit's hair colour, with NO face, NO cap, NO body.",
  items: kids.map((k) => [`kit_${k}_2_hair`, `${k.toUpperCase()} spirit (${EVO_ROW[k][0]}, ${EVO_ROW[k][1]} row): short bob back hair`]),
});
const kitHair2Sheets = [kitHair2("kit_hair2_a", ["fire", "dew", "leaf", "wind"]), kitHair2("kit_hair2_b", ["rock", "spark", "moon", "star"])];

export const SHEETS = [...mushrooms, ...KEEP.map(fromDoodle), ...critters, grandpa, tree, slots, ...fx, ...icons, ...lettering, ...env, ...bgs, ...scenes, ...kids, ...spiritPartSheets, ...spiritFaceSheets, evoConcept, ...evoLines, ...evoPartSheets, evoFaces, ...kitSheets, ...kitOutfitSheets, ...kitHair2Sheets];
