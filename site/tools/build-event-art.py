#!/usr/bin/env python3
"""Build the web derivative of the EVT-01 event illustration.

The landing page shows this image at 678 CSS px at most, so 1400 px wide
covers high-density screens at 2x. The source PNG stays untouched.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = (
    ROOT
    / "art/production/graphics-remake/wave-a/event-illustrations"
    / "2026-09-09/EVT-01_Cold_Camp_v1.png"
)
OUTPUT = ROOT / "site/preview/assets/event-cold-camp.webp"

SOURCE_SHA256 = "faedf2fc237cd09decb14744e80db130ae768a346c29b00668cad04f9d32c265"
TARGET_WIDTH = 1400
QUALITY = 85


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"Source illustration is missing: {SOURCE}")

    actual = digest(SOURCE)
    if actual != SOURCE_SHA256:
        raise SystemExit(
            "Source illustration does not match the approved fingerprint.\n"
            f"  expected {SOURCE_SHA256}\n  actual   {actual}"
        )

    with Image.open(SOURCE) as source:
        image = source.convert("RGB")
        height = round(image.height * TARGET_WIDTH / image.width)
        resized = image.resize((TARGET_WIDTH, height), Image.LANCZOS)
        resized.save(OUTPUT, "WEBP", quality=QUALITY, method=6)

    print(f"{OUTPUT.relative_to(ROOT)}  {TARGET_WIDTH}x{height}  {OUTPUT.stat().st_size:,} B")
    print(f"sha256 {digest(OUTPUT)}")


if __name__ == "__main__":
    main()
