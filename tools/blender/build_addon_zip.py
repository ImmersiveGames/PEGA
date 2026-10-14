"""Empacota o add-on Voxel Tool num zip instalável no Blender.

Uso: python tools/blender/build_addon_zip.py
"""

import os
import re
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "voxel_tool")
DIST = os.path.join(HERE, "dist")


def main():
    with open(os.path.join(SRC, "blender_manifest.toml"), encoding="utf-8") as f:
        version = re.search(r'^version\s*=\s*"([^"]+)"', f.read(), re.M).group(1)
    os.makedirs(DIST, exist_ok=True)
    out = os.path.join(DIST, "voxel_tool-%s.zip" % version)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
        for name in sorted(os.listdir(SRC)):
            if name.endswith((".py", ".toml")):
                # Pasta no zip: funciona como extensão (4.2+) e add-on legado (4.0/4.1).
                zf.write(os.path.join(SRC, name), "voxel_tool/" + name)
    print(out)


if __name__ == "__main__":
    main()
