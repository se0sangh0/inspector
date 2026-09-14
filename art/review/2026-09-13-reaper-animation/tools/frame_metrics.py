#!/usr/bin/env python3
"""Measure per-frame geometry across an animation and flag suspicious jumps.

Numbers only. This narrows down where to look; it cannot judge whether motion
reads well. Always follow up with the contact sheet and the preview page.

Reported per frame: opaque bounding box, its width/height, its centre, the
opaque pixel count, and the delta from the previous frame.

Flags raised
  BODY DRIFT     bbox centre X moves more than --max-drift px between frames
  SIZE JUMP      bbox width or height changes more than --max-size px
  AREA JUMP      opaque pixel count changes more than --max-area percent
  LOOP GAP       for looping animations, frame N to frame 1 exceeds the same limits
  EMPTY          a frame has no opaque pixels

A flag is a place to look, not proof of a defect: a wind-up genuinely changes
the bounding box. Judge each one on the contact sheet.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required:  python3 -m pip install Pillow")


def load_frames(folder: Path) -> list[tuple[str, Image.Image]]:
    if not folder.is_dir():
        sys.exit(f"frame folder not found: {folder}")
    items = []
    for f in sorted(folder.iterdir()):
        if re.fullmatch(r"\d+\.png", f.name, re.IGNORECASE):
            items.append((f.name, Image.open(f).convert("RGBA")))
    if not items:
        sys.exit(f"no frames named 01.png, 02.png ... in {folder}")
    return items


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("frames", type=Path)
    ap.add_argument("--loop", action="store_true", help="also compare the last frame back to the first")
    ap.add_argument("--max-drift", type=float, default=12.0)
    ap.add_argument("--max-size", type=float, default=40.0)
    ap.add_argument("--max-area", type=float, default=25.0, help="percent")
    ap.add_argument("--json", type=Path, help="also write the measurements here")
    args = ap.parse_args()

    frames = load_frames(args.frames)
    rows, flags = [], []
    for i, (name, im) in enumerate(frames):
        bbox = im.getchannel("A").getbbox()
        if bbox is None:
            rows.append({"frame": i + 1, "file": name, "empty": True})
            flags.append(f"EMPTY      frame {i + 1} ({name}) has no opaque pixels")
            continue
        x0, y0, x1, y1 = bbox
        area = sum(1 for p in im.getchannel("A").getdata() if p > 8)
        rows.append({"frame": i + 1, "file": name, "empty": False, "bbox": [x0, y0, x1, y1],
                     "w": x1 - x0, "h": y1 - y0,
                     "cx": round((x0 + x1) / 2, 1), "cy": round((y0 + y1) / 2, 1),
                     "opaquePx": area})

    def compare(a, b, label):
        if a.get("empty") or b.get("empty"):
            return
        d = abs(b["cx"] - a["cx"])
        if d > args.max_drift:
            flags.append(f"BODY DRIFT {label}: centre X moves {d:.1f}px (limit {args.max_drift})")
        dw, dh = abs(b["w"] - a["w"]), abs(b["h"] - a["h"])
        if dw > args.max_size or dh > args.max_size:
            flags.append(f"SIZE JUMP  {label}: bbox changes {dw}x{dh}px (limit {args.max_size})")
        if a["opaquePx"]:
            pct = abs(b["opaquePx"] - a["opaquePx"]) / a["opaquePx"] * 100
            if pct > args.max_area:
                flags.append(f"AREA JUMP  {label}: opaque area changes {pct:.1f}% (limit {args.max_area})")

    for i in range(len(rows) - 1):
        compare(rows[i], rows[i + 1], f"frame {i + 1}->{i + 2}")
    if args.loop and len(rows) > 1:
        compare(rows[-1], rows[0], f"loop {len(rows)}->1")

    print(f"{args.frames.name}   {len(rows)} frames")
    print(f"{'#':>2} {'bbox':>22} {'w':>4} {'h':>4} {'cx':>7} {'cy':>7} {'opaque':>8}")
    for r in rows:
        if r.get("empty"):
            print(f"{r['frame']:>2} {'(empty)':>22}")
            continue
        print(f"{r['frame']:>2} {str(r['bbox']):>22} {r['w']:>4} {r['h']:>4} "
              f"{r['cx']:>7} {r['cy']:>7} {r['opaquePx']:>8}")
    cx = [r["cx"] for r in rows if not r.get("empty")]
    cy = [r["cy"] for r in rows if not r.get("empty")]
    if cx:
        print(f"\ncentre X range {min(cx):.1f}..{max(cx):.1f}  (spread {max(cx)-min(cx):.1f}px)")
        print(f"centre Y range {min(cy):.1f}..{max(cy):.1f}  (spread {max(cy)-min(cy):.1f}px)")
    print(f"\n{len(flags)} flag(s)")
    for f in flags:
        print("  " + f)
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps({"frames": rows, "flags": flags}, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
