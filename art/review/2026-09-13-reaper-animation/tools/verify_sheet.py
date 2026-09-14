#!/usr/bin/env python3
"""Check a sheet + JSON pair produced by build_sheet.py against its own claims.

Technical checks only. This cannot tell you whether the motion reads well —
that still needs a human looking at the preview page.

Checks performed
  1. JSON parses and carries the expected keys
  2. the sheet PNG opens and matches sheetSize
  3. the sheet is RGBA and actually carries a non-opaque alpha channel
  4. every frame rectangle lies inside the sheet
  5. cell geometry agrees with cols/rows/margin/spacing
  6. no frame region is fully transparent unless the JSON already says so
  7. durations are positive and totalDurationMs adds up
  8. recorded sheetSha256 still matches the file on disk
  9. if the source frame folder is present, each frame matches the sheet region

Exit code is 0 when every check passes, 1 otherwise.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

try:
    from PIL import Image, ImageChops
except ImportError:
    sys.exit("Pillow is required:  python3 -m pip install Pillow")

REQUIRED = ("sheet", "sheetSize", "cell", "cols", "rows", "frameCount", "frames")


class Report:
    def __init__(self) -> None:
        self.passed: list[str] = []
        self.failed: list[str] = []
        self.skipped: list[str] = []

    def ok(self, msg: str) -> None:
        self.passed.append(msg)

    def bad(self, msg: str) -> None:
        self.failed.append(msg)

    def skip(self, msg: str) -> None:
        self.skipped.append(msg)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("json_path", type=Path, help="the .json written by build_sheet.py")
    ap.add_argument("--quiet", action="store_true", help="print only failures and the summary")
    args = ap.parse_args()

    r = Report()

    if not args.json_path.is_file():
        print(f"FAIL  metadata not found: {args.json_path}")
        return 1
    try:
        meta = json.loads(args.json_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"FAIL  metadata is not valid JSON: {exc}")
        return 1
    r.ok("metadata parses as JSON")

    missing = [k for k in REQUIRED if k not in meta]
    if missing:
        print(f"FAIL  metadata is missing keys: {', '.join(missing)}")
        return 1
    r.ok(f"metadata carries all required keys ({len(REQUIRED)})")

    sheet_path = args.json_path.parent / meta["sheet"]
    if not sheet_path.is_file():
        print(f"FAIL  sheet image not found: {sheet_path}")
        return 1
    try:
        sheet = Image.open(sheet_path)
        sheet.load()
    except Exception as exc:
        print(f"FAIL  sheet image will not open: {exc}")
        return 1
    r.ok(f"sheet opens ({sheet_path.name})")

    want_w, want_h = meta["sheetSize"]
    if sheet.size != (want_w, want_h):
        r.bad(f"sheet is {sheet.size[0]}x{sheet.size[1]} but JSON says {want_w}x{want_h}")
    else:
        r.ok(f"sheet size matches JSON ({want_w}x{want_h})")

    if sheet.mode != "RGBA":
        r.bad(f"sheet mode is {sheet.mode}, expected RGBA")
    else:
        alpha = sheet.getchannel("A")
        lo, hi = alpha.getextrema()
        if lo == 255:
            r.bad("alpha channel is fully opaque — the background was not cut out")
        else:
            r.ok(f"RGBA with a real alpha channel (min {lo}, max {hi})")
        sheet = sheet.convert("RGBA")

    cw, ch = meta["cell"]
    cols, rows = meta["cols"], meta["rows"]
    margin, spacing = meta.get("margin", 0), meta.get("spacing", 0)
    calc_w = margin * 2 + cols * cw + max(cols - 1, 0) * spacing
    calc_h = margin * 2 + rows * ch + max(rows - 1, 0) * spacing
    if (calc_w, calc_h) != (want_w, want_h):
        r.bad(f"cell geometry implies {calc_w}x{calc_h} but sheetSize is {want_w}x{want_h}")
    else:
        r.ok(f"cell geometry consistent ({cols}x{rows} cells of {cw}x{ch})")

    frames = meta["frames"]
    if len(frames) != meta["frameCount"]:
        r.bad(f"frameCount says {meta['frameCount']} but the frames list has {len(frames)}")
    else:
        r.ok(f"frameCount matches the frames list ({len(frames)})")

    if len(frames) > cols * rows:
        r.bad(f"{len(frames)} frames cannot fit in a {cols}x{rows} grid")

    declared_empty = set(meta.get("emptyFrames", []))
    total = 0
    for i, f in enumerate(frames):
        tag = f"frame {i + 1}"
        if f.get("index") != i:
            r.bad(f"{tag}: index field is {f.get('index')}, expected {i}")
        x, y, w, h = f["x"], f["y"], f["w"], f["h"]
        if (w, h) != (cw, ch):
            r.bad(f"{tag}: cell is {w}x{h}, expected {cw}x{ch}")
        if x < 0 or y < 0 or x + w > want_w or y + h > want_h:
            r.bad(f"{tag}: rectangle ({x},{y},{w},{h}) falls outside the sheet")
            continue
        dur = f.get("durationMs", 0)
        if not isinstance(dur, int) or dur <= 0:
            r.bad(f"{tag}: durationMs is {dur!r}, expected a positive integer")
        else:
            total += dur
        region = sheet.crop((x, y, x + w, y + h))
        if region.getchannel("A").getbbox() is None and (i + 1) not in declared_empty:
            r.bad(f"{tag}: region is fully transparent but is not listed in emptyFrames")

    if "totalDurationMs" in meta and total and meta["totalDurationMs"] != total:
        r.bad(f"totalDurationMs says {meta['totalDurationMs']} but the frames add up to {total}")
    elif total:
        r.ok(f"durations are positive and sum to {total} ms")

    recorded = meta.get("sheetSha256")
    if recorded:
        actual = hashlib.sha256(sheet_path.read_bytes()).hexdigest()
        if actual != recorded:
            r.bad("sheetSha256 does not match the file on disk — the sheet changed after the JSON was written")
        else:
            r.ok("sheetSha256 matches the sheet on disk")
    else:
        r.skip("sheetSha256 not recorded")

    folder = meta.get("sourceFrameFolder")
    src = Path(folder) if folder else None
    if src and src.is_dir():
        mismatched = []
        for i, f in enumerate(frames):
            p = src / f["file"]
            if not p.is_file():
                mismatched.append(f"{f['file']} (missing)")
                continue
            with Image.open(p) as raw:
                one = raw.convert("RGBA")
            region = sheet.crop((f["x"], f["y"], f["x"] + f["w"], f["y"] + f["h"]))
            if one.size != region.size or ImageChops.difference(one, region).getbbox() is not None:
                mismatched.append(f["file"])
        if mismatched:
            r.bad(f"sheet regions differ from the source frames: {', '.join(mismatched)}")
        else:
            r.ok(f"every sheet region is byte-identical to its source frame ({len(frames)})")
    else:
        r.skip("source frame folder not available — sheet/frame comparison skipped")

    if not args.quiet:
        for m in r.passed:
            print(f"PASS  {m}")
        for m in r.skipped:
            print(f"SKIP  {m}")
    for m in r.failed:
        print(f"FAIL  {m}")
    print(f"\n{len(r.passed)} passed, {len(r.failed)} failed, {len(r.skipped)} skipped")
    return 1 if r.failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
