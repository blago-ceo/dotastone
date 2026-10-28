"""Зашивает картинки из art/*.webp в страницу как data: URI.

Блок между маркерами <!-- art-data --> ... <!-- /art-data --> пересоздаётся при каждом запуске.
"""
import sys, io, os, base64, json, re

page, art_dir = sys.argv[1], sys.argv[2]
data = {}
for name in sorted(os.listdir(art_dir)):
    if name.endswith(".webp"):
        with open(os.path.join(art_dir, name), "rb") as f:
            data[name[:-5]] = "data:image/webp;base64," + base64.b64encode(f.read()).decode("ascii")

block = "<!-- art-data -->\n<script>window.DOTA_ART = " + json.dumps(data, separators=(",", ":")) + ";</script>\n<!-- /art-data -->"
s = io.open(page, encoding="utf-8").read()
if "<!-- art-data -->" in s:
    s = re.sub(r"<!-- art-data -->.*?<!-- /art-data -->", lambda m: block, s, flags=re.S)
else:
    anchor = '<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>'
    assert anchor in s
    s = s.replace(anchor, anchor + "\n" + block)
io.open(page, "w", encoding="utf-8", newline="\n").write(s)
print(len(data), "images,", len(s) // 1024, "KB page")
