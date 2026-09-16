"""Remove baked neutral checkerboard and align generated Attacker sheets.
Requires Python, Pillow, NumPy, SciPy. RGB source files remain unchanged.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SOURCES = {"Idle": "Attacker_Filtered_Idle_raw_v1.png", "ClearTheWay": "Attacker_Filtered_ClearTheWay_raw_v1.png"}

def extract(path):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    colors = rgb.astype(np.int16)
    chroma = colors.max(axis=2) - colors.min(axis=2)
    background = (chroma < 27) & (colors.min(axis=2) > 105)
    labels, count = ndimage.label(~background)
    sizes = np.bincount(labels.ravel())
    ids = sorted(range(1, count + 1), key=lambda i: sizes[i], reverse=True)[:4]
    regions = []
    for region_id in ids:
        mask = labels == region_id
        filled = ndimage.binary_fill_holes(mask)
        hole_labels, hole_count = ndimage.label(filled & ~mask)
        hole_sizes = np.bincount(hole_labels.ravel())
        small_ids = [i for i in range(1, hole_count + 1) if hole_sizes[i] <= 24]
        if small_ids:
            mask |= np.isin(hole_labels, small_ids)
        ys, xs = np.where(mask)
        bbox = (int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1))
        rgba = np.dstack((rgb, mask.astype(np.uint8) * 255))
        rgba[~mask, :3] = 0
        regions.append((bbox, Image.fromarray(rgba).crop(bbox)))
    regions.sort(key=lambda item: item[0][0])
    assert len(regions) == 4
    return regions

def main():
    frames = {name: extract(ROOT / "source" / file) for name, file in SOURCES.items()}
    largest_width = max(frame.width for group in frames.values() for _, frame in group)
    scale = min(0.65, 356 / largest_width)
    scaled_groups = {}
    foot_anchor = 268
    for name, group in frames.items():
        scaled_groups[name] = []
        for bbox, frame in group:
            scaled = frame.resize((round(frame.width * scale), round(frame.height * scale)), Image.Resampling.NEAREST)
            feet = np.asarray(scaled.getchannel("A"))[-32:] > 0
            foot_right = int(np.where(feet)[1].max())
            foot_anchor = min(foot_anchor, 369 - scaled.width + foot_right)
            scaled_groups[name].append((bbox, scaled, foot_right))
    report = {"scale": scale, "cell": [384, 512], "baseline": 501, "front_foot_right_x": foot_anchor, "sheets": {}}
    for name, group in scaled_groups.items():
        sheet = Image.new("RGBA", (1536, 512), (0, 0, 0, 0))
        info = []
        for index, (source_box, scaled, foot_right) in enumerate(group):
            x = index * 384 + foot_anchor - foot_right
            y = 502 - scaled.height
            sheet.paste(scaled, (x, y))
            info.append({"source_bbox": source_box, "output_bbox": [x, y, x + scaled.width, 502]})
        file = ROOT / f"Attacker_Filtered_{name}_384x512x4_v1.png"
        sheet.save(file)
        preview = Image.new("RGBA", sheet.size, (40, 43, 46, 255))
        preview.alpha_composite(sheet)
        preview.convert("RGB").save(ROOT / "source" / f"{name}_dark_preview.jpg", quality=95)
        alpha = np.asarray(sheet.getchannel("A"))
        assert alpha.min() == 0 and alpha.max() == 255
        assert not alpha[:, ::384].any()
        assert not alpha[:, 383::384].any()
        assert not alpha[:10].any() and not alpha[502:].any()
        report["sheets"][name] = {"file": file.name, "size": list(sheet.size), "mode": sheet.mode, "transparent_pixels": int((alpha == 0).sum()), "frames": info, "sha256": hashlib.sha256(file.read_bytes()).hexdigest()}
    (ROOT / "source" / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))

if __name__ == "__main__":
    main()

