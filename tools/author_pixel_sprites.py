#!/usr/bin/env python3
"""One-shot authoring helper for atlas sprites (not used at pack time).

Preferred entry points after the initial authored set:
  - tools/regen_ui_icons.py  — clear 32x32 UI glyphs
  - Content builder --pack-atlases — packs content/source/textures/sprites/

Full candidate extraction lived in this script during the art pass; sprites under
content/source/textures/sprites/ are now the source of truth.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    ui = ROOT / "tools" / "regen_ui_icons.py"
    if not ui.exists():
        print("missing tools/regen_ui_icons.py", file=sys.stderr)
        return 1
    subprocess.check_call([sys.executable, str(ui)])
    print("UI glyphs refreshed. Run: dotnet run --project tools/ShipGame.ContentBuilder -- --pack-atlases")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
