#!/usr/bin/env python3
"""Build the 1200x630 Open Graph cover from the published landing assets."""

# Input assets and SHA-256 fingerprints (REQ-LAND-004):
# site/preview/assets/forest-scene.webp       f5520b2247edb71a147bfc5706945d7fda4e4a302cde7236379b390ca4790f84
# site/preview/assets/companion-defender.webp 72cf1d11466152ed50de01baadd08ab88c424fda4e2d734acdfc99c3f33ba1f4
# site/preview/assets/companion-caster.webp   8c950337a6b9e838896d9322f89cc88c092e242df59f186ad43077759a0d3b7b
# site/preview/assets/enemy-goblin.webp       cf86d60c5297956921ebd2487b253b992cf870caaf4a22305400a165a465120f
# site/preview/assets/enemy-raider.webp       ca3b4d3130f5758ac4fec7c4d5c066f1dc674d0f293692b10ab17e1495c8ee01

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = ROOT / "site" / "preview" / "assets"
OUTPUT = ASSET_DIR / "og-cover.webp"
CANVAS_SIZE = (1200, 630)
BACKGROUND_COLOR = (15, 17, 21)

INPUT_HASHES = {
    "forest-scene.webp": "f5520b2247edb71a147bfc5706945d7fda4e4a302cde7236379b390ca4790f84",
    "companion-defender.webp": "72cf1d11466152ed50de01baadd08ab88c424fda4e2d734acdfc99c3f33ba1f4",
    "companion-caster.webp": "8c950337a6b9e838896d9322f89cc88c092e242df59f186ad43077759a0d3b7b",
    "enemy-goblin.webp": "cf86d60c5297956921ebd2487b253b992cf870caaf4a22305400a165a465120f",
    "enemy-raider.webp": "ca3b4d3130f5758ac4fec7c4d5c066f1dc674d0f293692b10ab17e1495c8ee01",
}


def checked_asset(name: str) -> Image.Image:
    path = ASSET_DIR / name
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    expected = INPUT_HASHES[name]
    if digest != expected:
        raise RuntimeError(f"SHA-256 mismatch for {path}: {digest} != {expected}")
    return Image.open(path).convert("RGBA")


def gradient_layer(size: tuple[int, int], horizontal: bool) -> Image.Image:
    """Return the CSS color overlay for one of the two hero shade gradients."""
    width, height = size
    layer = Image.new("RGBA", size)
    pixels = layer.load()
    for y in range(height):
        for x in range(width):
            t = (x / (width - 1)) if horizontal else (y / (height - 1))
            if horizontal:
                # linear-gradient(90deg, color/.48, transparent 42%, color/.25)
                if t <= 0.42:
                    alpha = 0.48 * (1 - t / 0.42)
                else:
                    alpha = 0.25 * ((t - 0.42) / 0.58)
            else:
                # linear-gradient(180deg, color/.08, color/.7)
                alpha = 0.08 + (0.70 - 0.08) * t
            pixels[x, y] = (*BACKGROUND_COLOR, round(alpha * 255))
    return layer


def shadow_for(unit: Image.Image) -> Image.Image:
    """Create drop-shadow(0 12px 16px rgb(0 0 0 / .5)); sigma is 16/2 = 8."""
    alpha = unit.getchannel("A")
    shadow_alpha = alpha.filter(ImageFilter.GaussianBlur(radius=8))
    shadow_alpha = shadow_alpha.point(lambda value: round(value * 0.5))
    return Image.merge("RGBA", (Image.new("L", unit.size),) * 3 + (shadow_alpha,))


def place_with_shadow(canvas: Image.Image, unit: Image.Image, xy: tuple[int, int]) -> None:
    shadow = shadow_for(unit)
    shadow_xy = (xy[0], xy[1] + 12)
    canvas.alpha_composite(shadow, shadow_xy)
    canvas.alpha_composite(unit, xy)


def main() -> None:
    background = checked_asset("forest-scene.webp")
    if background.size != (1100, 619):
        raise RuntimeError(f"Unexpected background size: {background.size}")
    background = ImageEnhance.Brightness(background).enhance(0.66)
    background = ImageEnhance.Color(background).enhance(0.78)

    canvas = Image.new("RGBA", CANVAS_SIZE, (*BACKGROUND_COLOR, 255))
    hero_xy = (50, 5)
    canvas.alpha_composite(background, hero_xy)
    hero_size = background.size
    canvas.alpha_composite(gradient_layer(hero_size, horizontal=True), hero_xy)
    canvas.alpha_composite(gradient_layer(hero_size, horizontal=False), hero_xy)

    # CSS z-index order: caster/goblin (3), then defender/raider (4).
    placements = [
        ("companion-caster.webp", (297, 325), (270, 274)),
        ("enemy-goblin.webp", (286, 270), (644, 329)),
        ("companion-defender.webp", (319, 319), (67, 280)),
        ("enemy-raider.webp", (319, 348), (815, 251)),
    ]
    for name, size, xy in placements:
        unit = checked_asset(name)
        unit = unit.resize(size, Image.Resampling.LANCZOS)
        place_with_shadow(canvas, unit, xy)

    canvas.convert("RGB").save(OUTPUT, format="WEBP", quality=85, method=6)
    print(f"wrote {OUTPUT} ({OUTPUT.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
