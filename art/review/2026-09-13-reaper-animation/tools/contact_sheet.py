#!/usr/bin/env python3
"""Lay an animation's frames out in a row with frame numbers, for eyeballing.

Draws every frame at --scale on a checkerboard, numbers each cell, and can draw
a vertical pivot guide so body drift is visible across the strip.

This is a review aid. It is not a game asset and must not be shipped.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("Pillow is required:  python3 -m pip install Pillow")


def checker(w: int, h: int, size: int = 16) -> Image.Image:
    img = Image.new("RGB", (w, h), (150, 150, 158))
    d = ImageDraw.Draw(img)
    for y in range(0, h, size):
        for x in range(0, w, size):
            if (x // size + y // size) % 2 == 0:
                d.rectangle([x, y, x + size - 1, y + size - 1], fill=(118, 118, 126))
    return img


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("frames", type=Path)
    ap.add_argument("--out", required=True, type=Path)
    ap.add_argument("--scale", type=float, default=0.5)
    ap.add_argument("--pivot-x", type=int, help="draw a vertical guide at this X in frame coordinates")
    ap.add_argument("--label", help="caption drawn top-left (default: folder name)")
    ap.add_argument("--force", action="store_true", help="overwrite --out if it already exists")
    args = ap.parse_args()

    if args.out.exists() and not args.force:
        sys.exit(f"{args.out} already exists — pass --force to overwrite")
    if not args.frames.is_dir():
        sys.exit(f"frame folder not found: {args.frames}")
    if not (0 < args.scale <= 4):
        sys.exit("--scale must be in (0, 4]")

    files = sorted(f for f in args.frames.iterdir() if re.fullmatch(r"\d+\.png", f.name, re.I))
    if not files:
        sys.exit(f"no frames named 01.png, 02.png ... in {args.frames}")

    with Image.open(files[0]) as probe:
        fw, fh = probe.size
    cw, ch = int(fw * args.scale), int(fh * args.scale)
    pad, top = 4, 20
    sheet = Image.new("RGB", (len(files) * (cw + pad) + pad, ch + top + pad), (26, 29, 36))
    d = ImageDraw.Draw(sheet)
    d.text((6, 5), args.label or args.frames.name, fill=(226, 220, 208))

    for i, f in enumerate(files):
        with Image.open(f) as raw:
            im = raw.convert("RGBA")
        if im.size != (fw, fh):
            sys.exit(f"{f.name} is {im.size[0]}x{im.size[1]} but the first frame is {fw}x{fh}")
        cell = checker(cw, ch)
        cell.paste(im.resize((cw, ch), Image.LANCZOS), (0, 0), im.resize((cw, ch), Image.LANCZOS))
        x = pad + i * (cw + pad)
        sheet.paste(cell, (x, top))
        if args.pivot_x is not None:
            px = x + int(args.pivot_x * args.scale)
            d.line([(px, top), (px, top + ch)], fill=(226, 96, 80), width=1)
        d.rectangle([x, top, x + cw - 1, top + ch - 1], outline=(70, 78, 90))
        d.text((x + 3, top + 2), str(i + 1), fill=(255, 214, 120))

    args.out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.out)
    print(f"{args.out}  {sheet.width}x{sheet.height}  {len(files)} frames @ {cw}x{ch}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
