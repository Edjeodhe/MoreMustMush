---
name: codex-image
description: Generate game art (icons, sprites, backgrounds) with the Codex CLI image generator and apply it in the Unity project. Use when asked to make, draw, or replace an image/sprite/icon for MoreMush.
---

# Codex image generation → Unity

`Tools/codex-image.mjs` runs `codex exec` (Codex desktop app's CLI, ChatGPT login) with its built-in
image generation tool, then copies the newest image from `~/.codex/generated_images/` into the project.
One image takes about 1–2 minutes and uses the user's ChatGPT quota, so don't generate speculatively.

## Generate

```bash
node Tools/codex-image.mjs \
  --prompt "<subject, pose, details>" \
  --out Assets/Art/Generated/<category>/<snake_name>.png \
  --style Tools/art-style.md \
  --transparent            # sprites/icons; omit for backgrounds
  # --ref <existing.png>   # repeatable; keeps a series visually consistent
```

- Prints `{"out": ..., "source": ..., "rgba": ...}`. With `--transparent`, `rgba: false` means the background came back opaque — regenerate.
- Look at the result with the Read tool before applying it. Image viewers may show transparent areas as black.
- Output is ~1254px square; Unity caps it at 2048 on import.

## Apply in Unity

- Everything under `Assets/Art/Generated/` imports as a Single-mode Sprite with no mipmaps and alpha-as-transparency (`Assets/Editor/GeneratedArtPostprocessor.cs`). Settings apply on first import only.
- Unity picks up new files when the editor regains focus. If the unityMCP server is connected, trigger a refresh and assign the sprite through it (e.g. set `SpriteRenderer.sprite` / `UI.Image.sprite` to the asset path).
- Keep throwaway experiments out of git. Delete rejected images together with their `.meta`.
