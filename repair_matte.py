"""Deterministically repair chroma/white matte residue without redrawing the person.

This is an offline alpha cleanup tool. Original frames remain untouched in the
source output folders; write a separate candidate directory for QA.
"""
from pathlib import Path
from argparse import ArgumentParser
from PIL import Image, ImageFilter
import numpy as np


def clean_frame(source: Path, destination: Path, action: str) -> None:
    image = Image.open(source).convert("RGBA")
    rgba = np.asarray(image, dtype=np.float32).copy()
    rgb = rgba[:, :, :3]
    alpha = rgba[:, :, 3] / 255.0

    if action == "stretch":
        # The key poses have a white matte in their partially transparent pixels.
        semi = (alpha > 0.02) & (alpha < 0.98)
        a = alpha[semi, None]
        rgb[semi] = np.clip((rgb[semi] - (1.0 - a) * 255.0) / a, 0, 255)
    else:
        # Older GIFs are binary-alpha but retain a few opaque magenta key pixels.
        magenta = (rgb[:, :, 0] > 140) & (rgb[:, :, 2] > 140) & (
            rgb[:, :, 1] < 0.62 * np.minimum(rgb[:, :, 0], rgb[:, :, 2]))
        alpha[magenta] = 0

    edge = Image.fromarray(np.uint8(np.clip(alpha * 255, 0, 255)))
    edge = edge.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.35))
    rgba[:, :, 3] = np.asarray(edge, dtype=np.float32)
    result = Image.fromarray(np.uint8(np.clip(rgba, 0, 255)))
    destination.parent.mkdir(parents=True, exist_ok=True)
    result.save(destination)


def main() -> None:
    parser = ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    count = 0
    for action_dir in sorted(args.input.iterdir()):
        if not action_dir.is_dir():
            continue
        for frame in sorted(action_dir.glob("frame_*.png")):
            clean_frame(frame, args.output / action_dir.name / frame.name, action_dir.name)
            count += 1
    print(f"Cleaned {count} frames into {args.output}")


if __name__ == "__main__":
    main()
