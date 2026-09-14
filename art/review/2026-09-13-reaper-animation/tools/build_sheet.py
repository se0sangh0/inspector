#!/usr/bin/env python3
"""Assemble a sprite sheet + timing JSON from a directory of numbered frames.

Reads PNG frames named 01.png, 02.png, ... from --frames, pastes them into a
grid without resampling, and writes the sheet plus a JSON describing cell
geometry, frame order and per-frame durations.

The JSON is this project's own format. It is NOT a Unity/Aseprite/TexturePacker
format and no engine reads it directly.

Frames are pasted at their native size. If a frame does not match --cell the
script fails instead of silently scaling, so a mismatched export is caught here
rather than in-game.

Example
-------
python3 build_sheet.py \
    --frames  frames/reaper_idle_float \
    --out     sheets/reaper_idle_float \
    --cell    384x512 --cols 4 \
    --durations 120,120,140,160,160,140,120,120 \
    --name reaper_idle_float --loop
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required:  python3 -m pip install Pillow")

FRAME_RE = re.compile(r"^(\d+)\.png$", re.IGNORECASE)


def parse_size(text: str) -> tuple[int, int]:
    m = re.fullmatch(r"\s*(\d+)\s*[xX]\s*(\d+)\s*", text)
    if not m:
        raise argparse.ArgumentTypeError(f"expected WxH, got {text!r}")
    w, h = int(m.group(1)), int(m.group(2))
    if w <= 0 or h <= 0:
        raise argparse.ArgumentTypeError("width and height must be positive")
    return w, h


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def collect_frames(folder: Path) -> list[Path]:
    if not folder.is_dir():
        sys.exit(f"frame folder not found: {folder}")
    found: list[tuple[int, Path]] = []
    for entry in folder.iterdir():
        m = FRAME_RE.match(entry.name)
        if m:
            found.append((int(m.group(1)), entry))
    if not found:
        sys.exit(f"no frames named 01.png, 02.png ... in {folder}")
    found.sort()
    numbers = [n for n, _ in found]
    expected = list(range(1, len(found) + 1))
    if numbers != expected:
        missing = sorted(set(expected) - set(numbers))
        sys.exit(
            f"frame numbering must be a gapless 1..N sequence.\n"
            f"  found:   {numbers}\n"
            f"  missing: {missing or 'none, but numbering starts wrong'}"
        )
    return [p for _, p in found]


def parse_durations(text: str | None, count: int, default: int) -> list[int]:
    if not text:
        return [default] * count
    parts = [p.strip() for p in text.split(",") if p.strip()]
    try:
        values = [int(p) for p in parts]
    except ValueError:
        sys.exit(f"--durations must be comma separated integers, got {text!r}")
    if len(values) != count:
        sys.exit(f"--durations has {len(values)} values but there are {count} frames")
    if any(v <= 0 for v in values):
        sys.exit("every duration must be greater than 0 ms")
    return values


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--frames", required=True, type=Path, help="folder holding 01.png ...")
    ap.add_argument("--out", required=True, type=Path,
                    help="output path without extension; .png and .json are written")
    ap.add_argument("--cell", required=True, type=parse_size, help="cell size, e.g. 384x512")
    ap.add_argument("--cols", type=int, default=4, help="cells per row (default 4)")
    ap.add_argument("--durations", help="comma separated ms per frame; default --default-duration for all")
    ap.add_argument("--default-duration", type=int, default=125, help="ms per frame when --durations is omitted")
    ap.add_argument("--name", help="animation name recorded in the JSON (default: --out stem)")
    ap.add_argument("--loop", action="store_true", help="mark the animation as looping")
    ap.add_argument("--margin", type=int, default=0, help="transparent border around the whole sheet")
    ap.add_argument("--spacing", type=int, default=0, help="transparent gap between cells")
    args = ap.parse_args()

    if args.cols <= 0:
        sys.exit("--cols must be positive")
    if args.margin < 0 or args.spacing < 0:
        sys.exit("--margin and --spacing cannot be negative")

    frames = collect_frames(args.frames)
    cw, ch = args.cell
    durations = parse_durations(args.durations, len(frames), args.default_duration)

    cols = min(args.cols, len(frames))
    rows = (len(frames) + cols - 1) // cols
    sheet_w = args.margin * 2 + cols * cw + (cols - 1) * args.spacing
    sheet_h = args.margin * 2 + rows * ch + (rows - 1) * args.spacing
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))

    entries = []
    for index, path in enumerate(frames):
        with Image.open(path) as raw:
            img = raw.convert("RGBA")
            if img.size != (cw, ch):
                sys.exit(f"{path.name} is {img.size[0]}x{img.size[1]} but --cell is {cw}x{ch}. "
                         f"Fix the export; this script does not resample.")
            alpha = img.getchannel("A")
            bbox = alpha.getbbox()
            col, row = index % cols, index // cols
            x = args.margin + col * (cw + args.spacing)
            y = args.margin + row * (ch + args.spacing)
            sheet.paste(img, (x, y))
        entries.append({
            "index": index,
            "file": path.name,
            "col": col,
            "row": row,
            "x": x,
            "y": y,
            "w": cw,
            "h": ch,
            "durationMs": durations[index],
            "opaqueBBox": list(bbox) if bbox else None,
        })

    out_png = args.out.with_suffix(".png")
    out_json = args.out.with_suffix(".json")
    out_png.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_png)

    empty = [e["index"] + 1 for e in entries if e["opaqueBBox"] is None]
    meta = {
        "format": "guitamsa.spritesheet/1",
        "note": "Project-local format. No engine imports this directly.",
        "name": args.name or args.out.stem,
        "sheet": out_png.name,
        "sheetSize": [sheet_w, sheet_h],
        "cell": [cw, ch],
        "cols": cols,
        "rows": rows,
        "margin": args.margin,
        "spacing": args.spacing,
        "frameCount": len(frames),
        "loop": bool(args.loop),
        "totalDurationMs": sum(durations),
        "sourceFrameFolder": str(args.frames),
        "frames": entries,
        "emptyFrames": empty,
        "sheetSha256": sha256(out_png),
    }
    out_json.write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(f"sheet  {out_png}  {sheet_w}x{sheet_h}  {cols}x{rows} cells")
    print(f"json   {out_json}  {len(frames)} frames  {sum(durations)} ms total")
    if empty:
        print(f"WARNING fully transparent frames: {empty}")


if __name__ == "__main__":
    main()
