"""Просмотр и подготовка картинок для DotaStone.

sheet <src_dir> <out.jpg>   — коллаж всех картинок с подписями (для проверки)
build <src_dir> <out_dir>   — переименовать по id карт и сжать в webp 512px
"""
import sys, os
from PIL import Image, ImageDraw, ImageFont

MAP = {
    "shadowfiend": "hero_sf", "invoker": "hero_invoker", "pudge": "hero_pudge", "tinker": "hero_tinker",
    "(способность сфа)": "power_sf", "способность инвокера": "power_invoker",
    "способность пуджа": "power_pudge", "способность тинкера": "power_tinker",
    "Radiant_melee_creep": "melee_creep", "Dire_super_melee_creep": "ranged_creep",
    "Гнев богов": "wrath", "аббадон": "abaddon", "акс": "axe", "андаинг": "undying",
    "барабан выносливости": "drum", "бруда": "brood", "варлок": "warlock", "голем": "golem",
    "дагон": "dagon", "даззл": "dazzle", "джаггернот": "jugg", "зомби": "zombie",
    "кентавр": "centaur", "курьер": "courier", "свен": "sven", "кольт": "bkb", "лагуна блейд": "laguna",
    "ледяное поле": "freezing", "маска безумия": "mom", "монетка": "coin", "огрмаг": "ogre",
    "палец смерти": "finger", "паучок бруды": "spider", "робот": "robot", "рошан": "roshan",
    "снайпер": "sniper", "тайдхантер": "tide", "танго": "tango", "тараска": "heart",
    "течис": "techies", "том знаний": "tome", "фантом ассасин": "phantom",
    "рики": "riki", "веномансер": "venomancer", "лайфстилер": "lifestealer", "буря молота": "stormhammer",
    "мкб": "mkb", "дезолятор": "deso", "батлфьюри": "battlefury", "рапира": "rapier",
    "способность рошана": "power_roshanboss",
    "Рики": "riki", "Веномастер": "venomancer", "Лайфстиллер": "lifestealer", "Буря молота": "stormhammer",
    "монкей кинг бар": "mkb", "Дезолятор": "deso", "Батлфьюри": "battlefury",
    "Шадоурейз": "ult_sf", "Сан Страй": "ult_invoker", "Ульта Пуджа": "ult_pudge", "Ребут": "ult_tinker",
    "Землятрясение Рошана": "ult_roshanboss", "Ульта Лизы": "ult_liza",
    "Руна дд": "rune_dd", "Руна реген": "rune_regen", "Руна ускорения": "rune_haste", "Руна богатства": "rune_bounty",
}


def files(src):
    out = []
    for name in sorted(os.listdir(src)):
        stem, ext = os.path.splitext(name)
        if ext.lower() in (".jpg", ".jpeg", ".png", ".webp"):
            out.append((stem, os.path.join(src, name)))
    return out


def sheet(src, dst):
    items = files(src)
    cell, cols = 200, 7
    rows = (len(items) + cols - 1) // cols
    img = Image.new("RGB", (cols * cell, rows * (cell + 28)), (20, 20, 20))
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
    for i, (stem, path) in enumerate(items):
        im = Image.open(path).convert("RGB")
        im.thumbnail((cell - 8, cell - 8))
        x, y = (i % cols) * cell, (i // cols) * (cell + 28)
        img.paste(im, (x + (cell - im.width) // 2, y + 4))
        d.text((x + 4, y + cell + 4), f"{stem} -> {MAP.get(stem, '?')}", fill=(230, 220, 160), font=font)
    img.save(dst, quality=85)
    print("sheet", len(items), "images")


def build(src, dst):
    os.makedirs(dst, exist_ok=True)
    total = 0
    for stem, path in files(src):
        cid = MAP.get(stem)
        if not cid:
            print("skip", stem)
            continue
        im = Image.open(path).convert("RGB")
        im.thumbnail((512, 512), Image.LANCZOS)
        out = os.path.join(dst, cid + ".webp")
        im.save(out, "WEBP", quality=82, method=6)
        total += os.path.getsize(out)
    print("built", len(os.listdir(dst)), "files,", total // 1024, "KB")


if __name__ == "__main__":
    {"sheet": sheet, "build": build}[sys.argv[1]](sys.argv[2], sys.argv[3])
