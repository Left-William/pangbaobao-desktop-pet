"""Install cleaned legacy frames and the 24 FPS stretch candidate.

The first run archives the original action frames in source-assets so this
operation can be repeated without stacking alpha cleanup on prior output.
"""
from argparse import ArgumentParser
from pathlib import Path
from shutil import copy2

from repair_matte import clean_frame


ACTIONS = ("stretch", "chest", "bend", "twist", "tiptoe", "sidebend")
# The old stretch GIF has two arms-up frames before a lower arm pose. Reorder
# them to make a continuous rising and falling motion for the comparison mode.
CLASSIC_ORDER = (1, 2, 4, 3, 5, 6, 7, 8, 9, 11, 10, 12, 13)


def replace_frames(target: Path, frames: list[Path], cleaner: str | None = None) -> None:
    target.mkdir(parents=True, exist_ok=True)
    for old in target.glob("frame_*.png"):
        old.unlink()
    for index, source in enumerate(frames, 1):
        destination = target / f"frame_{index:03d}.png"
        if cleaner:
            clean_frame(source, destination, cleaner)
        else:
            copy2(source, destination)


def main() -> None:
    parser = ArgumentParser()
    parser.add_argument("--smooth", type=Path, required=True)
    parser.add_argument("--keys", type=Path, required=True)
    parser.add_argument("--twist", type=Path, required=True)
    parser.add_argument("--tiptoe", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    assets = root / "PangBaoBaoPet" / "Assets"
    originals = root / "source-assets" / "original-action-frames"
    for action in ACTIONS:
        source_dir = originals / action
        if not source_dir.exists():
            source_dir.mkdir(parents=True)
            for source in sorted((assets / action).glob("frame_*.png")):
                copy2(source, source_dir / source.name)
        if not list(source_dir.glob("frame_*.png")):
            raise ValueError(f"Missing original frames: {action}")

    keys = sorted(args.keys.glob("key_*.png"))
    smooth = sorted(args.smooth.glob("frame_*.png"))
    if len(keys) != 7 or len(smooth) != 36:
        raise ValueError(f"Expected 7 keys and 36 smooth frames; got {len(keys)}, {len(smooth)}")
    twist = sorted(args.twist.glob("frame_*.png"))
    tiptoe = sorted(args.tiptoe.glob("frame_*.png"))
    if len(twist) != 24 or len(tiptoe) != 20:
        raise ValueError(f"Expected 24 twist and 20 tiptoe frames; got {len(twist)}, {len(tiptoe)}")
    key_archive = root / "source-assets" / "smooth-stretch-keyframes"
    key_archive.mkdir(parents=True, exist_ok=True)
    for key in keys:
        copy2(key, key_archive / key.name)

    for action in ACTIONS:
        frames = sorted((originals / action).glob("frame_*.png"))
        if action == "stretch":
            frames = [frames[i - 1] for i in CLASSIC_ORDER]
            replace_frames(assets / "stretch_classic", frames, cleaner="stretch")
        elif action in ("twist", "tiptoe"):
            replace_frames(assets / f"{action}_classic", frames, cleaner=action)
        else:
            replace_frames(assets / action, frames, cleaner=action)
    replace_frames(assets / "stretch", smooth)
    replace_frames(assets / "twist", twist)
    replace_frames(assets / "tiptoe", tiptoe)
    print("Installed 36 stretch, 24 twist, 20 tiptoe, and 42 cleaned original exercise frames")


if __name__ == "__main__":
    main()
