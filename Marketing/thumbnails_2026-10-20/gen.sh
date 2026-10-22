#!/usr/bin/env bash
# 2026-10-20 itch 커버 재구성 — 시안 3개 동시 생성 (Codex built-in image_gen)
cd "$(dirname "$0")/../.." || exit 1
D=Marketing/thumbnails_2026-10-20
STYLE=Marketing/PV/art/pv2_style.md
R1=Marketing/thumbnails_2026-10-19/spirit/refs/ref1_spirits.jpg
R2=Marketing/thumbnails_2026-10-19/spirit/refs/ref5_harvester.jpg
R3=Marketing/itch_dream/out/shot1_harvest.jpg

COMMON="itch.io game cover art for the incremental harvest game More Must Mush. LANDSCAPE image, aspect ratio about 1.26:1 (e.g. 1536x1218 or wider 3:2 is fine; important content must stay inside the central 1.26:1 area). This must read instantly at a tiny 315x250 listing size: ONE clear focal subject, few big simple shapes, strong light/dark value contrast between the lit subject and a deeper teal-green forest around the edges (soft dark vignette), warm golden sunlight glow directly behind the subject. Limited palette: coral red + cream + deep teal/moss green + honey gold. TOP 30% of the image must be a calm, simple, slightly darker deep teal/blue-green area with NO important objects (a logo will be placed there later). No text, no letters, no numbers, no logo, no UI, no frame, no watermark. Keep the game's dreamy watercolor storybook look from the references: transparent washes, paper grain, soft edges, no black outlines, chubby cute rounded shapes. Reference images: 1 = mushroom spirit character designs, 2 = the harvesting tool (round wooden hub, curled wooden handle, silver crescent blade), 3 = real gameplay colors."

run() { # $1=name $2=prompt
  node Tools/codex-image.mjs --style "$STYLE" --ref "$R1" --ref "$R2" --ref "$R3" \
    --prompt "$COMMON $2" --out "$D/$1_raw.png" --timeout 420 > "$D/_$1.log" 2>&1
  echo "EXIT $?" >> "$D/_$1.log"
}

run A_hero "CONCEPT A — hero in action. The red white-spotted mushroom-cap spirit girl from reference 1 (cream long wavy hair, cream frilly dress, dot eyes, pink blush, big open happy smile) is the hero, large, centered slightly right in the lower 70%, her body about 45% of the image height. She joyfully throws/spins the round harvesting tool from reference 2, which whirls in a bright cream motion arc at lower left; along that arc several plump mushrooms pop up and burst into a fountain of glowing honey-gold sparkles and little gold coins rising toward her, conveying 'more and more harvest'. Background: soft sunny meadow clearing fading into deep teal forest at the edges. Keep everything big and uncluttered." &
run B_pile "CONCEPT B — abundance. A huge overflowing heap of freshly harvested mushrooms (mostly red white-spotted caps, some golden chanterelles and cream oyster mushrooms) piled into a soft mound filling the lower-middle of the image, golden sparkles and a few gold coins spilling from it. On top of the heap, three chubby round little mushroom critters from the bottom row of reference 1 (red spotted cap in the middle and biggest, brown cap, yellow chanterelle cap) cheer with raised tiny arms and happy faces. In the foreground lower left, the harvesting tool from reference 2 spins in a cream motion swirl, slicing a mushroom stem, the cap flying up onto the heap. Background: sunbeam clearing, deep teal forest edges." &
run C_face "CONCEPT C — big face. ONE giant chubby round red white-spotted mushroom critter (the red cap round little one from reference 1's bottom row) fills the lower 65% of the image, seen from the chest up, centered, huge sparkly delighted eyes, pink blush, open smile, tiny arms raised in excitement. Its big glossy coral-red cap with cream spots is the dominant shape. In front of it at lower left, the harvesting tool from reference 2 whooshes past in a bright cream arc, and a few small harvested mushrooms and golden sparkles fly up around the critter. Background: simple deep teal forest with soft golden halo light behind the cap. Very simple, iconic, mascot-portrait composition." &
wait
echo ALL DONE
