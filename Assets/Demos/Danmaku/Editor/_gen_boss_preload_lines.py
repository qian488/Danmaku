# Prints Resources paths (no extension) under Img/boss for GamePreloadList.txt (UTF-8 stdout).
import os

root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Resources", "Demos", "Danmaku", "Img", "boss")
res_root = os.path.join(os.path.dirname(__file__), "..", "..", "..", "Resources")
root = os.path.normpath(root)
res_root = os.path.normpath(res_root)
exts = {".png", ".jpg", ".jpeg", ".tga", ".psd"}
paths = []
for dirpath, _, files in os.walk(root):
    for f in files:
        if os.path.splitext(f)[1].lower() in exts:
            full = os.path.join(dirpath, f)
            rel = os.path.relpath(full, res_root).replace("\\", "/")
            noext = os.path.splitext(rel)[0].replace("\\", "/")
            paths.append(noext)
for p in sorted(set(paths)):
    print(p)
