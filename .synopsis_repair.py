import sys
from pathlib import Path
from io import BytesIO
from zipfile import ZipFile, ZIP_STORED

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

SOURCE = Path("C:/Users/user/Desktop/박지수/숨쉬었음청년/output/SynopsisCutscene")
sys.path.insert(0, str(SOURCE))
from finish_character_cutouts import original_page, load_layer, png
import export_layers as e

STAGING = Path("output/SynopsisRepair")
SIZE = e.SIZE
GRID = e.REFERENCE_SIZE


def color(image):
    return np.asarray(image.resize(GRID, Image.Resampling.LANCZOS), dtype=np.float32).transpose(2, 0, 1)


def mask_from(values):
    return Image.fromarray(np.uint8(np.clip(values, 0, 1) * 255)).resize(SIZE, Image.Resampling.LANCZOS)


def times(a, b):
    return ImageChops.multiply(a, b)


def rgba(image, mask):
    result = image.resize(SIZE, Image.Resampling.LANCZOS).convert("RGBA")
    result.putalpha(mask)
    return result


def repair(archive, king_path, heart_path):
    p2, p3, p4 = (original_page(p) for p in (2, 3, 4))
    r, g, b = color(p2)
    leaves = np.maximum(np.clip((104 - r) / 24, 0, 1), np.clip((122 - (r+g+b)/3) / 26, 0, 1))
    leaves *= np.clip((g-r+10)/15, 0, 1)
    plants = times(times(e.P2_PLANTS, mask_from(leaves)), e.P2_R)
    key = "page_02/panel_03/02_giant_mushrooms.png"
    existing = load_layer(archive, key)
    king_plate = e.load(king_path)
    kr, kg, kb = color(king_plate)
    flesh = mask_from(np.clip((kr-kb-19)/27, 0, 1) * np.clip((kr-104)/27, 0, 1))
    hole = times(e.P2_GIANTS.filter(ImageFilter.MaxFilter(17)), e.union(e.P2_GRANDPA, e.P2_PLANTS).filter(ImageFilter.MaxFilter(9)))
    restored = rgba(king_plate, times(times(flesh, hole), e.P2_R))
    restored.alpha_composite(existing)

    r, g, b = color(p3)
    shake = mask_from(np.clip((r-123)/58,0,1) * np.clip((r-b-12)/49,0,1))
    shake = times(times(shake, e.P3_SHAKE.filter(ImageFilter.MaxFilter(11))), e.P3_BL)
    coral = np.clip((r-g-41)/21,0,1)
    gold = np.clip((g-b-66)/23,0,1)
    heart_guide = e.P3_HEARTS.filter(ImageFilter.MaxFilter(17))
    visible = times(times(heart_guide, mask_from(np.maximum(coral,gold))), ImageChops.invert(e.P3_JOY))
    heart_plate = e.load(heart_path)
    hr, hg, hb = color(heart_plate)
    hidden = e.mask("789,105 816,89 856,82 879,97 871,134 843,163 799,154", blur=3)
    filled = rgba(heart_plate, times(hidden, mask_from(np.clip((hr-hg-42)/22,0,1))))
    filled.alpha_composite(rgba(p3, visible))

    r, g, b = color(p4)
    cast = load_layer(archive, "page_04/03_grandfather_with_scythe.png")
    ca = cast.getchannel("A")
    # Clear every open gap around the scythe, including the space under its handle.
    gaps = e.mask(
        "632,344 657,324 681,310 709,297 748,289 788,285 843,293 861,304 853,426 827,423 763,397 701,372 679,354",
        "713,404 738,415 774,431 801,443 820,453 818,462 797,453 777,436 759,431 741,431 724,442 710,459 703,455 709,438",
        "890,492 905,476 908,486 907,499 895,517 881,531 876,519",
        blur=.75,
    )
    ca = ImageChops.subtract(ca, gaps)
    # Retain the repaired wooden pixels from the registered local inpaint.
    cast.putalpha(ca)

    # Trace the balloon as a filled object, with its pointed tail and no scenery.
    balloon = e.mask(
        "672,106 749,96 751,46 818,63 854,10 907,54 994,10 995,70 1056,65 1032,107 1097,146 1047,167 1015,190 1054,247 994,237 962,280 928,257 835,472 862,259 842,274 816,240 758,247 762,204 685,208 728,158",
        blur=.7,
    )
    hearts = e.mask(
        "547,94 554,63 592,68 617,43 641,49 651,78 643,109 625,145 573,122",
        "625,168 639,148 651,155 663,143 680,145 681,173 669,195 640,187",
        "1094,69 1115,44 1135,45 1148,61 1171,61 1185,79 1174,104 1116,138 1104,117",
        "1071,205 1081,184 1094,187 1100,197 1116,191 1126,207 1118,226 1081,240",
        blur=1,
    )
    hearts = times(hearts, mask_from(np.clip((r-g-41)/21,0,1)))
    sparkles = e.mask(
        "552,163 571,156 577,139 585,155 600,166 585,175 577,190 568,174",
        "671,42 690,33 696,12 706,31 724,42 706,52 697,72 690,51",
        "1048,37 1066,29 1075,6 1084,27 1102,38 1084,47 1075,63 1067,45",
        "1149,151 1164,142 1173,125 1182,141 1198,151 1181,159 1172,177 1163,158",
        "1014,288 1028,281 1036,264 1044,281 1058,289 1044,298 1036,313 1028,297",
        blur=.8,
    )
    sparks = times(sparkles, mask_from(np.clip((g-b-72)/25,0,1)))
    mushroom_key = "page_04/02_fleeing_mushrooms.png"
    mushrooms = load_layer(archive, mushroom_key)
    # At the bottom, neutralise olive grass in the existing mask while retaining cream feet.
    yy, xx = np.indices(r.shape)
    ground_band = ((yy > 818) & (xx < 590)) | ((yy > 845) & (xx > 1060))
    ground = np.clip((g-r+25)/20,0,1) * np.clip((168-r)/37,0,1)
    ma = times(mushrooms.getchannel("A"), mask_from(1-ground_band*ground))
    mushrooms.putalpha(ma)
    return {
        "page_02/panel_03/04_foreground_plants.png": rgba(p2, plants),
        key: restored,
        "page_03/panel_02/03_tremble_effect.png": rgba(p3, shake),
        "page_03/panel_03/02_heart_effect.png": filled,
        "page_04/03_grandfather_with_scythe.png": cast,
        "page_04/04_speech_balloon_and_hearts.png": rgba(p4, e.union(balloon, hearts, sparks)),
        mushroom_key: mushrooms,
    }


def main():
    STAGING.mkdir(parents=True, exist_ok=True)
    output = STAGING / "MoreMustMush_Synopsis_Layers_v4.zip"
    with ZipFile(SOURCE / "MoreMustMush_Synopsis_Layers_v3.zip") as old:
        replacements = repair(old, *sys.argv[1:])
        with ZipFile(output, "w", ZIP_STORED) as new:
            for key in old.namelist():
                new.writestr(key, png(replacements[key]) if key in replacements else old.read(key))
    with ZipFile(output) as result:
        assert len(result.namelist()) == 25 and result.testzip() is None
        for key in replacements:
            layer = load_layer(result, key)
            assert layer.size == SIZE and layer.mode == "RGBA"
            assert layer.getchannel("A").getextrema() == (0, 255)
            assert all(abs(d-72) < .02 for d in layer.info["dpi"])
    preview = Image.new("RGB", (1200, 960), "#939caa")
    draw = ImageDraw.Draw(preview)
    for i, (key, layer) in enumerate(replacements.items()):
        sprite = layer.crop(layer.getchannel("A").getbbox())
        sprite.thumbnail((385, 285))
        x, y = (i % 3)*400, (i // 3)*320
        draw.text((x+8,y+5), key, fill="#19202b")
        preview.paste(sprite, (x+(400-sprite.width)//2,y+25), sprite)
    preview.save(STAGING / "cutouts_v4_preview.jpg", quality=95)


if __name__ == "__main__":
    main()
