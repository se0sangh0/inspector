"""Normalize the user-approved 8-pose imagegen sources without redrawing them.

Requires Pillow, NumPy and SciPy. Source PNGs and existing game assets are read-only.
Output is a review candidate, not an imported or approved Unity animation.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
MANIFEST = HERE / "manifest.json"
OUTPUT = REPO / "art/production/graphics-remake/wave-a/animation-8f/2026-09-10/normalized_v1"
CONNECT = np.ones((3, 3), dtype=bool)
CELL_W, CELL_H, BASELINE, PAD = 384, 512, 500, 12


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rect(mask):
    y, x = np.where(mask)
    return [int(x.min()), int(y.min()), int(x.max()) + 1, int(y.max()) + 1]


def extract(item):
    source = REPO / item["raw_file"]
    assert digest(source) == item["raw_sha256"], "Source changed: " + str(source)
    rgb = np.asarray(Image.open(source).convert("RGB"))
    signed = rgb.astype(np.int16)
    mean = signed.mean(axis=2)
    chroma = signed.max(axis=2) - signed.min(axis=2)
    # The baked backgrounds are neutral bright checkerboards. Outlines, faces,
    # bronze, wood, teal ornaments and warm fabric are deliberately not keyed.
    candidate = (chroma <= 16) & (mean >= 100)
    # Seal one-pixel gaps in dark outlines before flood fill, so a bright
    # blade/cloth interior is not mistaken for the exterior checkerboard.
    candidate = ~ndi.binary_closing(~candidate, structure=CONNECT)
    labels, count = ndi.label(candidate, structure=CONNECT)
    sizes = np.bincount(labels.ravel())
    edges = np.unique(np.concatenate((labels[0], labels[-1], labels[:, 0], labels[:, -1])))
    edges = edges[edges != 0]
    remove = np.isin(labels, edges)
    border_values = np.concatenate((mean[:30].ravel(), mean[:, :8].ravel(), mean[:, -8:].ravel()))
    bg_floor = max(110, float(np.percentile(border_values, 10)) - 15)
    centers = np.percentile(border_values, [20, 80])
    for _ in range(6):
        which = np.abs(border_values[:, None] - centers[None, :]).argmin(axis=1)
        centers = np.array([border_values[which == k].mean() for k in range(2)])
    centers.sort()
    enclosed_removed = []
    # Checker patches enclosed by a bent arm/staff also need alpha. Preserve
    # smooth enclosed neutral material (metal highlights, gray cloth). Record
    # every interior removal so the matte can be inspected against the source.
    objects = ndi.find_objects(labels)
    edge_set = set(edges.tolist())
    for label in range(1, count + 1):
        if label in edge_set or sizes[label] < 9:
            continue
        sl = objects[label - 1]
        local = labels[sl] == label
        values = mean[sl][local]
        core = ndi.binary_erosion(local, structure=CONNECT)
        core_values = mean[sl][core] if core.sum() >= 4 else values
        low = np.abs(core_values - centers[0]) < 12
        high = np.abs(core_values - centers[1]) < 12
        two_checker_tones = low.mean() >= .08 and high.mean() >= .08 and (low | high).mean() >= .75
        large_checker_gap = sizes[label] >= 1000 and float(values.std()) >= 12
        if float(values.mean()) >= bg_floor and (two_checker_tones or large_checker_gap):
            remove[sl] |= local
            enclosed_removed.append({"area": int(sizes[label]), "box": [sl[1].start, sl[0].start, sl[1].stop, sl[0].stop]})

    foreground = ~remove
    flabels, n = ndi.label(foreground, structure=CONNECT)
    fsizes = np.bincount(flabels.ravel())
    order = np.argsort(fsizes[1:])[::-1] + 1
    majors = order[:8]
    assert len(majors) == 8 and min(fsizes[majors]) > 5000, (item["id"], "missing character")
    assert len(order) == 8 or fsizes[order[8]] < min(fsizes[majors]) * .15, (item["id"], "ambiguous components")
    bboxes = {int(k): rect(flabels == k) for k in majors}
    sorted_y = sorted(majors.tolist(), key=lambda k: (bboxes[k][1] + bboxes[k][3]) / 2)
    majors = sorted(sorted_y[:4], key=lambda k: bboxes[k][0]) + sorted(sorted_y[4:], key=lambda k: bboxes[k][0])
    masks = [flabels == k for k in majors]
    # Retain tiny disconnected drawing details only immediately adjacent to a
    # character, not arbitrary neutral-background specks within its rectangle.
    owned = set(majors)
    for index, mask in enumerate(masks):
        nearby = np.unique(flabels[ndi.binary_dilation(mask, structure=CONNECT, iterations=3)])
        for label in nearby:
            if label and label not in owned and fsizes[label] <= 800:
                masks[index] |= flabels == label
                owned.add(int(label))

    frames = []
    for index, mask in enumerate(masks):
        x0, y0, x1, y1 = rect(mask)
        assert (y0 + y1) / 2 // 512 == index // 4, (item["id"], "row order")
        # Center on feet rather than the whole silhouette: extended weapons
        # must not drag the body's root from side to side in playback.
        band_height = max(24, round((y1 - y0) * .08))
        band = mask[max(y0, y1 - band_height):y1]
        projection = band.sum(axis=0)
        spans, span_count = ndi.label(projection > 0)
        feet = []
        for k in range(1, span_count + 1):
            xs = np.where(spans == k)[0]
            area = int(projection[xs].sum())
            if len(xs) >= 8 and area >= 40:
                feet.append((area, float(np.average(xs, weights=projection[xs]))))
        feet.sort(reverse=True)
        anchor = float(np.mean([v for _, v in feet[:2]])) if feet else float(np.average(np.arange(len(projection)), weights=projection))
        rgba = np.dstack((rgb[y0:y1, x0:x1], mask[y0:y1, x0:x1].astype(np.uint8) * 255))
        frames.append({"image": Image.fromarray(rgba), "box": [x0, y0, x1, y1], "anchor_x": anchor, "ground_y": y1 - 1, "alpha_pixels": int(mask.sum())})
    return frames, {"key_chroma": 16, "key_luminance": 100, "enclosed_bg_regions": enclosed_removed, "ignored_island_pixels": int(sum(fsizes[k] for k in order if int(k) not in owned))}


def reference_height(item):
    im = Image.open(REPO / item["reference_file"]).convert("RGBA")
    return float(np.median([im.crop((i * 384, 0, (i + 1) * 384, 512)).getchannel("A").getbbox()[3] - im.crop((i * 384, 0, (i + 1) * 384, 512)).getchannel("A").getbbox()[1] for i in range(4)]))


def main():
    data = json.loads(MANIFEST.read_text())
    # A new processing run must not inherit a previous visual/test pass.
    data.pop("validation", None)
    data.pop("preview_validation", None)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    extracted = {}
    for item in data["items"]:
        frames, info = extract(item)
        extracted[item["id"]] = (frames, info)
        print(item["id"], "8 silhouettes", [f["box"] for f in frames], flush=True)

    configs = {}
    for character in sorted({x["character"] for x in data["items"]}):
        pair = [x for x in data["items"] if x["character"] == character]
        frames = [f for x in pair for f in extracted[x["id"]][0]]
        idle = next(x for x in pair if x["motion"] == "idle")
        idle_frames = extracted[idle["id"]][0]
        left = max(f["anchor_x"] - f["box"][0] for f in frames)
        right = max(f["box"][2] - f["anchor_x"] for f in frames)
        up = max(f["box"][3] - f["box"][1] for f in frames)
        target = reference_height(idle)
        idle_height = float(np.median([f["image"].height for f in idle_frames]))
        scale = min(1., target / idle_height, (CELL_W - 2 * PAD) / (left + right), (BASELINE - PAD) / up)
        # The common pivot is allowed to be off-center, reserving weapon room
        # on the facing side without shifting feet differently in each pose.
        pivot = PAD + left * scale + (CELL_W - 2 * PAD - (left + right) * scale) / 2
        configs[character] = {"scale": scale, "pivot_x": round(pivot), "baseline_y": BASELINE, "reference_idle_height": target}

    for item in data["items"]:
        config = configs[item["character"]]
        frames, info = extracted[item["id"]]
        sheet = Image.new("RGBA", (1536, 1024))
        directory = OUTPUT / "frames" / item["id"]
        directory.mkdir(parents=True, exist_ok=True)
        records = []
        for index, frame in enumerate(frames):
            sprite = frame["image"]
            scale = config["scale"]
            size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
            sprite = sprite.resize(size, Image.Resampling.NEAREST)
            local_anchor = (frame["anchor_x"] - frame["box"][0]) * scale
            x = round(config["pivot_x"] - local_anchor)
            y = BASELINE + 1 - sprite.height
            assert x >= PAD - 1 and y >= PAD - 1 and x + sprite.width <= CELL_W - PAD + 1
            canvas = Image.new("RGBA", (CELL_W, CELL_H))
            canvas.alpha_composite(sprite, (x, y))
            alpha = np.asarray(canvas.getchannel("A"))
            assert set(np.unique(alpha)) <= {0, 255}
            assert not alpha[0].any() and not alpha[-1].any() and not alpha[:, 0].any() and not alpha[:, -1].any()
            frame_file = directory / f"{index + 1:02d}.png"
            canvas.save(frame_file)
            sheet.alpha_composite(canvas, ((index % 4) * CELL_W, (index // 4) * CELL_H))
            records.append({"frame": index + 1, "source_box": frame["box"], "source_foot_x": frame["anchor_x"], "normalized_box": list(canvas.getchannel("A").getbbox()), "file": str(frame_file.relative_to(REPO)), "sha256": digest(frame_file)})
        dest = OUTPUT / f"{item['id']}_384x512x8_v1.png"
        sheet.save(dest)
        item["normalized_file"] = str(dest.relative_to(REPO))
        item["normalized_sha256"] = digest(dest)
        item["frames_files"] = [x["file"] for x in records]
        item["processing"] = {**config, **info, "alpha_verified": True, "resampling": "nearest", "frame_records": records, "unity_pivot_suggestion": [config["pivot_x"] / CELL_W, (CELL_H - 1 - BASELINE) / CELL_H], "status": "technical_review_candidate"}
        item["runtime_ready"] = False
    data["postprocessing_authorization"] = "User approved code processing on 2026-09-10."
    data["postprocessing_status"] = "normalized_awaiting_checks"
    data["normalization"] = {"cell": [CELL_W, CELL_H], "grid": [4, 2], "baseline": BASELINE, "source_pixels_redrawn": False, "method": "connected neutral-background mask, conservative internal checker detection, eight components, per-character common nearest-neighbor scale and foot anchor", "characters": configs}
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    print("Wrote 12 transparent sheets and 96 individual frames.", flush=True)


if __name__ == "__main__":
    main()
