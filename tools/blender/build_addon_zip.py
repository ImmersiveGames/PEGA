"""Empacota o add-on PEGA Voxel Tools num zip instalável no Blender.

Uso: python tools/blender/build_addon_zip.py
"""

import os
import re
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "pega_voxel_tools")
DIST = os.path.join(HERE, "dist")


def main():
    with open(os.path.join(SRC, "blender_manifest.toml"), encoding="utf-8") as f:
        version = re.search(r'^version\s*=\s*"([^"]+)"', f.read(), re.M).group(1)
    os.makedirs(DIST, exist_ok=True)
    out = os.path.join(DIST, "pega_voxel_tools-%s.zip" % version)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
        for name in sorted(os.listdir(SRC)):
            if name.endswith((".py", ".toml")):
                # Pasta no zip: funciona como extensão (4.2+) e add-on legado (4.0/4.1).
                zf.write(os.path.join(SRC, name), "pega_voxel_tools/" + name)
    print(out)


if __name__ == "__main__":
    main()
