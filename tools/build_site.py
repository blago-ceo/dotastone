"""Собирает index.html для GitHub Pages из web/dotastone.html.

Исходник написан без <html>/<head> (так его публикует Claude), поэтому здесь
добавляется полноценная шапка: кодировка, viewport и базовый сброс стилей.

Запуск из корня проекта:  python tools/build_site.py
"""
import io
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "web", "dotastone.html")
OUT = os.path.join(ROOT, "index.html")

HEAD = """<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="description" content="DotaStone — карточная дуэль героев Dota 2 по правилам Hearthstone. Игра против бота или с другом онлайн.">
<style>
  :root { color-scheme: dark; padding-top: env(safe-area-inset-top, 0px); padding-bottom: env(safe-area-inset-bottom, 0px); }
  body { margin: 0; font: 14px system-ui, sans-serif; }
  img { max-width: 100%; }
  [hidden] { display: none !important; }
</style>
</head>
<body>
"""

src = io.open(SRC, encoding="utf-8").read()
io.open(OUT, "w", encoding="utf-8", newline="\n").write(HEAD + src + "\n</body>\n</html>\n")
print("index.html:", os.path.getsize(OUT) // 1024, "KB")
