"""Offline RIFE trial for the short twist and tiptoe source loops.

Pass the official rife-ncnn-vulkan executable location. The program never
ships with the pet; only accepted generated PNGs are copied to Assets.
"""
from argparse import ArgumentParser
from pathlib import Path
import subprocess

import numpy as np
from PIL import Image, ImageFilter


def render_inputs(frames: list[Path], target: Path) -> None:
    for layer in ("white", "alpha"):
        (target / layer).mkdir(parents=True, exist_ok=True)
        (target / f"{layer}-out").mkdir(parents=True, exist_ok=True)
    for index, source in enumerate(frames + [frames[0]], 1):
        rgba = Image.open(source).convert("RGBA")
        white = Image.new("RGBA", rgba.size, "white")
        white.alpha_composite(rgba)
        white.convert("RGB").save(target / "white" / f"{index:04d}.png")
        rgba.getchannel("A").convert("RGB").save(target / "alpha" / f"{index:04d}.png")


def assemble(target: Path, count: int) -> None:
    out = target / "frames"
    out.mkdir(exist_ok=True)
    for index in range(1, count + 1):
        name = f"{index:04d}.png"
        white = np.asarray(Image.open(target / "white-out" / name).convert("RGB"), dtype=np.float32)
        matte = np.asarray(Image.open(target / "alpha-out" / name).convert("RGB"), dtype=np.float32)
        alpha = np.clip(matte.mean(axis=2) / 255, 0, 1)
        color = np.clip((white - (1 - alpha[:, :, None]) * 255) / np.maximum(alpha[:, :, None], 0.08), 0, 255)
        image = np.empty((*alpha.shape, 4), dtype=np.uint8)
        image[:, :, :3] = np.rint(color).astype(np.uint8)
        a = Image.fromarray(np.rint(alpha * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3))
        image[:, :, 3] = np.asarray(a)
        Image.fromarray(image).save(out / f"frame_{index:03d}.png")


def main() -> None:
    parser = ArgumentParser()
    parser.add_argument("--exe", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--gpu", default="1")
    args = parser.parse_args()
    frames = sorted(args.source.glob("frame_*.png"))
    if len(frames) not in (5, 6):
        raise ValueError(f"Expected 5 or 6 source frames; got {len(frames)}")
    render_inputs(frames, args.output)
    count = len(frames) * 4
    for layer in ("white", "alpha"):
        subprocess.run([str(args.exe.resolve()), "-i", str((args.output / layer).resolve()),
                        "-o", str((args.output / f"{layer}-out").resolve()),
                        "-n", str(count + 1), "-m", "rife-v4.6", "-g", args.gpu,
                        "-f", "%04d.png"], cwd=args.exe.resolve().parent, check=True)
    assemble(args.output, count)
    print(f"Wrote {count} trial frames to {args.output / 'frames'}")


if __name__ == "__main__":
    main()
