"""Assemble the 24 FPS stretch experiment from RIFE's offline output.

The seven generated key poses are played up and then mirrored back down. RIFE
fills two frames between adjacent keys. It is run on RGB and alpha separately;
this script removes the white matte from the alpha pass and repairs the few
nearly black/white pixels RIFE produced at the moving hands.
"""
from argparse import ArgumentParser
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import distance_transform_edt


def repair_hand_colors(rgba: np.ndarray) -> np.ndarray:
    alpha = rgba[:, :, 3]
    rgb = rgba[:, :, :3]
    height, width = alpha.shape
    yy, xx = np.mgrid[:height, :width]
    # Hands travel outside the torso, so the face and shirt are left untouched.
    arm_zone = ((xx < 145) | (xx > 215)) & (yy < 240)
    skin = (rgb[:, :, 0] > rgb[:, :, 1] * 1.08) & (
        rgb[:, :, 1] > rgb[:, :, 2] * 1.03
    ) & (rgb[:, :, 0] > 85) & (alpha > 170) & arm_zone
    pale = (rgb.min(axis=2) > 224) & (alpha > 20)
    dark = (rgb.max(axis=2) < 70) & (alpha > 20)
    defect = (pale | dark) & arm_zone
    if np.any(defect) and np.any(skin):
        distance, indices = distance_transform_edt(~skin, return_indices=True)
        selected = defect & (distance < 100)
        nearest = rgb[indices[0], indices[1]]
        rgb[selected] = nearest[selected]
    return rgba


def build_frame(trial: Path, name: str) -> Image.Image:
    # Alpha-specific interpolation preserves the complete outline. RGB from
    # the white-background pass preserves shirt texture in the torso.
    white = np.asarray(Image.open(trial / "white-out" / name).convert("RGB"), dtype=np.float32)
    matte = np.asarray(Image.open(trial / "alpha-out" / name).convert("RGB"), dtype=np.float32)
    alpha = np.clip(matte.mean(axis=2) / 255.0, 0, 1)
    color = np.clip((white - (1 - alpha[:, :, None]) * 255) / np.maximum(alpha[:, :, None], 0.07), 0, 255)
    rgba = np.empty((*alpha.shape, 4), np.uint8)
    rgba[:, :, :3] = np.rint(color).astype(np.uint8)
    rgba[:, :, 3] = np.rint(alpha * 255).astype(np.uint8)
    rgba = repair_hand_colors(rgba)
    # Remove isolated one-pixel white halo but retain hands and glasses.
    a = Image.fromarray(rgba[:, :, 3]).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.32))
    rgba[:, :, 3] = np.asarray(a)
    return Image.fromarray(rgba)


def main() -> None:
    parser = ArgumentParser()
    parser.add_argument("--trial", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    # Rising motion: 1..19. Falling motion mirrors 18..2 to avoid the extra
    # separately interpolated return pass, which had more visible hand ghosts.
    sequence = list(range(1, 20)) + list(range(18, 1, -1))
    for number, source_index in enumerate(sequence, 1):
        name = f"{source_index:04d}.png"
        build_frame(args.trial, name).save(args.output / f"frame_{number:03d}.png")
    print(f"Wrote {len(sequence)} frames to {args.output}")


if __name__ == "__main__":
    main()
