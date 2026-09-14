#!/usr/bin/env python3
"""Build preview/manifest.json from a folder of uniform-grid sprite sheets.

Every sheet in --sheets is described with its cell geometry, frame order and
per-frame duration so the preview page can play it. Sheets whose pixel size
does not divide evenly into the given cell size are reported and skipped.

Pivots come from --pivots as `id=X,Y` pairs. A sheet with no matching pivot
falls back to the horizontal centre of the cell and --default-baseline.

Re-running is safe. Values a person put into the manifest by hand — the review
verdict on each animation (`status`) and the list of motions that have no frames
yet (`pending`) — are matched by id and carried over. Only the geometry is
rebuilt from the sheets. Pass --reset-status to drop them on purpose. If a kept
verdict no longer matches any sheet the script says so instead of silently
losing it.

Example
-------
python3 make_preview_manifest.py \
    --sheets ../../../production/graphics-remake/wave-a/animation-8f/2026-09-10/normalized_v1 \
    --out ../preview/manifest.json \
    --cell 384x512 --cols 4 --duration 200 \
    --pivots goblin=243,500 raider=228,500
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required:  python3 -m pip install Pillow")


def parse_size(text: str) -> tuple[int, int]:
    m = re.fullmatch(r"\s*(\d+)\s*[xX]\s*(\d+)\s*", text)
    if not m:
        raise argparse.ArgumentTypeError(f"expected WxH, got {text!r}")
    return int(m.group(1)), int(m.group(2))


def parse_pivots(pairs: list[str]) -> dict[str, list[int]]:
    out: dict[str, list[int]] = {}
    for item in pairs or []:
        m = re.fullmatch(r"([A-Za-z0-9_\-]+)=(\d+),(\d+)", item.strip())
        if not m:
            sys.exit(f"--pivots entry must look like id=X,Y — got {item!r}")
        out[m.group(1)] = [int(m.group(2)), int(m.group(3))]
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sheets", required=True, type=Path, help="folder holding the sheet PNGs")
    ap.add_argument("--out", required=True, type=Path, help="manifest.json to write")
    ap.add_argument("--cell", required=True, type=parse_size)
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--duration", type=int, default=200, help="ms per frame applied to every sheet")
    ap.add_argument("--loop-suffix", default="idle",
                    help="sheets whose name contains this are marked looping (default: idle)")
    ap.add_argument("--pivots", nargs="*", default=[], help="id=X,Y pairs")
    ap.add_argument("--default-baseline", type=int, default=500)
    ap.add_argument("--reset-status", action="store_true",
                    help="discard the review verdicts and pending list in the existing manifest")
    args = ap.parse_args()

    if not args.sheets.is_dir():
        sys.exit(f"sheet folder not found: {args.sheets}")
    if args.cols <= 0 or args.duration <= 0:
        sys.exit("--cols and --duration must be positive")

    cw, ch = args.cell
    pivots = parse_pivots(args.pivots)
    args.out.parent.mkdir(parents=True, exist_ok=True)

    entries, skipped = [], []
    for png in sorted(args.sheets.glob("*.png")):
        with Image.open(png) as im:
            w, h, mode = im.width, im.height, im.mode
        if w % cw or h % ch:
            skipped.append(f"{png.name}: {w}x{h} is not a whole number of {cw}x{ch} cells")
            continue
        cols, rows = w // cw, h // ch
        if cols != args.cols:
            skipped.append(f"{png.name}: has {cols} columns but --cols is {args.cols}")
            continue
        count = cols * rows
        key = png.stem.split("_")[0]
        rel = os.path.relpath(png.resolve(), args.out.parent.resolve()).replace(os.sep, "/")
        entries.append({
            "id": png.stem,
            "label": png.stem.replace("_", " "),
            "sheet": rel,
            "cell": [cw, ch],
            "cols": cols,
            "rows": rows,
            "frameCount": count,
            "loop": args.loop_suffix in png.stem,
            "hasAlpha": mode == "RGBA",
            "pivot": pivots.get(key, [cw // 2, args.default_baseline]),
            "pivotSource": "measured" if key in pivots else "fallback: cell centre X",
            "durations": [args.duration] * count,
        })

    if not entries:
        sys.exit("no usable sheets found — nothing written")

    # 기존 manifest 의 사람이 넣은 값은 재생성으로 지우지 않는다.
    # 시트 스캔은 기하 정보만 만들고, 검수 판정(status)과 미등록 목록(pending)은
    # id 를 맞춰 그대로 이어받는다. --reset-status 로만 비운다.
    kept_status, kept_pending, unmatched = {}, [], []
    if args.out.is_file() and not args.reset_status:
        try:
            old = json.loads(args.out.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError) as exc:
            sys.exit(f"existing manifest could not be read: {exc}\n"
                     f"Move it aside, or pass --reset-status to start from scratch.")
        for a in old.get("animations", []):
            if isinstance(a, dict) and a.get("id") and "status" in a:
                kept_status[a["id"]] = a["status"]
        kept_pending = old.get("pending", []) or []
        new_ids = {e["id"] for e in entries}
        unmatched = sorted(set(kept_status) - new_ids)

    for e in entries:
        if e["id"] in kept_status:
            e["status"] = kept_status[e["id"]]

    manifest = {
        "format": "guitamsa.previewmanifest/1",
        "note": "Preview-page input only. Not an engine import format.",
        "cell": [cw, ch],
        "animations": entries,
    }
    if kept_pending:
        manifest["pending"] = kept_pending

    args.out.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    carried = sum(1 for e in entries if "status" in e)
    print(f"{args.out}  {len(entries)} animations")
    print(f"  status carried over: {carried}/{len(entries)}"
          f"{'  (reset requested)' if args.reset_status else ''}")
    print(f"  pending entries kept: {len(kept_pending)}")
    for s in skipped:
        print(f"SKIP  {s}")
    for u in unmatched:
        print(f"WARNING  previous status for '{u}' was dropped — no sheet with that id any more")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
