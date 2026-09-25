"""Assemble the two ImageGen sprite strips into aligned transparent key poses.

The source strips are generated art. This script only segments and registers
their four subjects per strip; it does not synthesize new body parts.
"""
from pathlib import Path
from PIL import Image, ImageFilter
from scipy.ndimage import label, find_objects, distance_transform_edt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / "work" / "generated"
OUTPUT = INPUT / "stretch-keyframes"
OUTPUT.mkdir(parents=True, exist_ok=True)

sources = [
    (INPUT / "stretch-arms-low-strip.png", 0.945),
    (INPUT / "stretch-arms-high-strip.png", 1.0),
]
sequences = []
for path, scale in sources:
    image = Image.open(path).convert("RGBA")
    rgba = np.asarray(image)
    components, count = label(rgba[:, :, 3] > 16)
    sizes = np.bincount(components.ravel())
    regions = find_objects(components)
    ids = [i for i in range(1, count + 1) if sizes[i] > 10_000]
    ids.sort(key=lambda i: regions[i - 1][1].start)
    if len(ids) != 4:
        raise RuntimeError(f"Expected four people in {path}, found {len(ids)}")

    extracted = []
    for subject_id in ids:
        yy, xx = regions[subject_id - 1]
        crop = rgba[yy, xx].copy()
        mask = components[yy, xx] == subject_id
        crop[:, :, 3] = np.where(mask, crop[:, :, 3], 0)
        foreground = crop[:, :, 3] > 0
        core = crop[:, :, 3] > 245
        _, nearest = distance_transform_edt(~core, return_indices=True)
        fringe = foreground & ~core
        crop[fringe, :3] = crop[nearest[0][fringe], nearest[1][fringe], :3]
        subject = Image.fromarray(crop)
        matte = subject.getchannel("A").filter(ImageFilter.MinFilter(5)).filter(ImageFilter.GaussianBlur(0.45))
        subject.putalpha(matte)
        width = round(subject.width * scale)
        height = round(subject.height * scale)
        subject = subject.resize((width, height), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (720, 724))
        canvas.alpha_composite(subject, ((720 - width) // 2, 690 - height))
        extracted.append(canvas.resize((360, 362), Image.Resampling.LANCZOS))
    sequences.append(extracted)

# Low strip frames 1–3 and high strip frames 1–4. The last frame in the low
# strip is a T-pose but touches the image boundary, so use the complete high one.
keys = sequences[0][:3] + sequences[1]
for index, image in enumerate(keys, 1):
    image.save(OUTPUT / f"key_{index:02}.png")
print(f"Saved {len(keys)} aligned key poses to {OUTPUT}")
