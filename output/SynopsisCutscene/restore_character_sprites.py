from io import BytesIO
from pathlib import Path
from zipfile import ZipFile, ZIP_STORED
import sys

sys.path.insert(0, str(Path.home() / "AppData/Local/Temp/mmm-cutout-deps"))
import cv2
import numpy as np
from PIL import Image, ImageChops, ImageDraw

import export_layers as e


def cleaned(path):
    with Image.open(path) as source:
        sprite = source.convert("RGBA")
    alpha = np.array(sprite.getchannel("A"))
    n, labels, stats, _ = cv2.connectedComponentsWithStats((alpha > 125).astype(np.uint8))
    valid = stats[:, cv2.CC_STAT_AREA] >= 100
    valid[0] = False
    alpha = np.clip((alpha.astype(np.float32) - 120) / 130, 0, 1)
    support = cv2.dilate(valid[labels].astype(np.uint8), np.ones((3, 3), np.uint8))
    sprite.putalpha(Image.fromarray((alpha * support * 255).astype(np.uint8)))
    return sprite


def trim(sprite):
    return sprite.crop(sprite.getchannel("A").getbbox())


def place(canvas, sprite, box):
    x, y, w, h = box
    scale_x = e.SIZE[0] / 1672
    scale_y = e.SIZE[1] / 942
    sprite = sprite.resize((round(w * scale_x), round(h * scale_y)), Image.Resampling.LANCZOS)
    canvas.alpha_composite(sprite, (round(x * scale_x), round(y * scale_y)))


def blank():
    return Image.new("RGBA", e.SIZE)


def centered(sprite):
    sprite = trim(sprite)
    sprite.thumbnail((2350, 1320), Image.Resampling.LANCZOS)
    canvas = blank()
    canvas.alpha_composite(sprite, ((e.SIZE[0] - sprite.width) // 2,
                                   (e.SIZE[1] - sprite.height) // 2))
    return canvas


def split(sprite):
    middle = sprite.width // 2
    return trim(sprite.crop((0, 0, middle, sprite.height))), trim(sprite.crop((middle, 0, sprite.width, sprite.height)))


def png(sprite):
    data = BytesIO()
    sprite.save(data, format="PNG", dpi=(72, 72))
    return data.getvalue()


def main():
    giant, running, fleeing, turning, people = map(cleaned, sys.argv[1:])
    cast = {}
    canvas = blank()
    place(canvas, trim(giant), (816, 14, 858, 775))
    canvas.putalpha(ImageChops.multiply(canvas.getchannel("A"), e.P2_R))
    cast["page_02/panel_03/02_giant_mushrooms.png"] = canvas
    canvas = blank()
    place(canvas, trim(running), (566, 249, 440, 626))
    cast["page_04/03_grandfather_with_scythe.png"] = canvas
    left, right = split(fleeing)
    canvas = blank()
    place(canvas, left, (-209, 28, 774, 928))
    place(canvas, right, (1076, 221, 816, 729))
    cast["page_04/02_fleeing_mushrooms.png"] = canvas

    extras = {
        "restored_full/page_02/giant_mushrooms.png": centered(giant),
        "restored_full/page_02/grandfather_turning.png": centered(turning),
        "restored_full/page_02/fleeing_people.png": centered(people),
        "restored_full/page_04/fleeing_mushrooms_left.png": centered(left),
        "restored_full/page_04/fleeing_mushrooms_right.png": centered(right),
    }
    archive = e.ROOT / "MoreMustMush_Synopsis_Layers_v3.zip"
    with ZipFile(e.ROOT / "MoreMustMush_Synopsis_Layers_v2.zip") as old, ZipFile(archive, "x", ZIP_STORED) as new:
        for key in old.namelist():
            new.writestr(key, png(cast[key]) if key in cast else old.read(key))
        for key, image in extras.items():
            new.writestr(key, png(image))
    with ZipFile(archive) as result:
        assert len(result.namelist()) == 30 and result.testzip() is None
        for key in cast | extras:
            with Image.open(BytesIO(result.read(key))) as layer:
                assert layer.size == e.SIZE and layer.mode == "RGBA"
                assert all(abs(d - 72) < .02 for d in layer.info["dpi"])
                assert layer.getchannel("A").getextrema() == (0, 255)

    preview = Image.new("RGB", (1200, 920), "#bfc4cc")
    for i, (key, sprite) in enumerate((cast | extras).items()):
        small = trim(sprite)
        small.thumbnail((370, 270))
        x, y = (i % 3) * 400, (i // 3) * 300
        preview.paste(small, (x+(400-small.width)//2, y+20), small)
        ImageDraw.Draw(preview).text((x+8, y+5), key, fill="#21242b")
    preview.save(e.ROOT / "characters_restored_preview.jpg", quality=93)


if __name__ == "__main__":
    main()
