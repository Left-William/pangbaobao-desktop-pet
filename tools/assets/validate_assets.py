"""Read-only validation for the current action catalog. --strict enforces 0.5 density."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src" / "PangBaoBaoPet.Desktop" / "Assets"
REQUIRED_ACTIONS = {
    "idle", "stretch", "chest", "bend", "twist", "tiptoe", "sidebend",
    "shy", "kiss", "roll", "long_jump", "high_jump", "pull_up", "push_up", "street_dance"
}
MIN_RELEASE_FRAMES = {
    "stretch": 12, "chest": 12, "bend": 12, "twist": 12, "tiptoe": 12,
    "sidebend": 12, "long_jump": 50, "high_jump": 40, "pull_up": 90,
    "push_up": 90, "street_dance": 160,
}
MIN_RELEASE_DISTINCT_FILES = {
    "stretch": 6, "chest": 6, "bend": 6, "twist": 6, "tiptoe": 6,
    "sidebend": 6, "long_jump": 35, "high_jump": 30, "pull_up": 55,
    "push_up": 45, "street_dance": 120,
}


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as image:
        header = image.read(26)
    if len(header) < 26 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"not a PNG: {path}")
    if header[25] != 6:
        raise ValueError(f"not an RGBA PNG: {path}")
    return struct.unpack(">II", header[16:24])


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--strict", action="store_true", help="require >=2 source pixels per display DIP")
    parser.add_argument("--release", action="store_true", help="require density and full action coverage for both skins")
    parser.add_argument("--details", action="store_true", help="show every low-density frame")
    args = parser.parse_args()
    actions = json.loads((ASSETS / "actions.json").read_text(encoding="utf-8"))
    failures: list[str] = []
    low_density: list[str] = []
    low_density_by_action: dict[str, int] = {}
    seen: set[tuple[str, str]] = set()
    master_manifest = ROOT / "art" / "masters" / "character.json"
    try:
        character = json.loads(master_manifest.read_text(encoding="utf-8"))
        if character.get("characterRevision") != "pbb-photo-05-approved":
            failures.append("character master revision is not approved")
        for skin in ("pajamas", "black-tee"):
            master = character["skins"][skin]
            path = master_manifest.parent / master["master"]
            png_size(path)
            if hashlib.sha256(path.read_bytes()).hexdigest().upper() != master["sha256"].upper():
                failures.append(f"character master hash mismatch: {skin}")
    except (OSError, ValueError, KeyError, TypeError) as error:
        failures.append(f"invalid character master manifest: {error}")
    for action in actions:
        key = (action.get("skinId", "pajamas"), action["id"])
        stage = action.get("stageDip", [430, 490])
        if (not isinstance(stage, list) or len(stage) != 2 or
            any(not isinstance(value, (int, float)) for value in stage) or
            not 430 <= stage[0] <= 1024 or not 490 <= stage[1] <= 768):
            failures.append(f"invalid stageDip: {key}")
        if key in seen:
            failures.append(f"duplicate skin/action: {key}")
        seen.add(key)
        directory = action.get("assetDirectory", action["id"])
        if "/" in directory or "\\" in directory or ".." in directory:
            failures.append(f"unsafe asset directory: {directory}")
            continue
        frames = sorted((ASSETS / directory).glob("frame_*.png"))
        if not frames or len(frames) != action.get("frames", len(frames)):
            failures.append(f"frame count mismatch: {key}")
            continue
        if args.release and key[1] in MIN_RELEASE_FRAMES and len(frames) < MIN_RELEASE_FRAMES[key[1]]:
            failures.append(f"incomplete action: {key}, {len(frames)} < {MIN_RELEASE_FRAMES[key[1]]} frames")
        if args.release and key[1] in MIN_RELEASE_DISTINCT_FILES:
            distinct = len({hashlib.sha256(frame.read_bytes()).digest() for frame in frames})
            minimum = MIN_RELEASE_DISTINCT_FILES[key[1]]
            if distinct < minimum:
                failures.append(f"repeated-frame action: {key}, {distinct} < {minimum} distinct PNGs")
        if args.release and "预览" in action.get("name", ""):
            failures.append(f"preview action in release: {key}")
        durations = action.get("durationsMs")
        if durations is not None and (len(durations) != len(frames) or any(x <= 0 for x in durations)):
            failures.append(f"invalid timing: {key}")
        offsets = action.get("frameOffsets")
        if offsets is not None and (len(offsets) != len(frames) or any(len(x) != 2 for x in offsets)):
            failures.append(f"invalid offsets: {key}")
        for field in ("frameHeadAnchors", "frameMouthAnchors"):
            anchors = action.get(field)
            if anchors is not None and (len(anchors) != len(frames) or any(
                not isinstance(anchor, list) or len(anchor) != 2 or
                any(not isinstance(value, (int, float)) or not 0 <= value <= 1 for value in anchor)
                for anchor in anchors
            )):
                failures.append(f"invalid {field}: {key}")
        heights = action.get("frameDisplayHeights", [action.get("displayHeight", 362)] * len(frames))
        if len(heights) != len(frames) or any(not isinstance(x, (int, float)) or x <= 0 for x in heights):
            failures.append(f"invalid display heights: {key}")
            continue
        for i, frame in enumerate(frames):
            try:
                width, height = png_size(frame)
                if width > 4096 or height > 4096:
                    failures.append(f"oversize PNG: {frame.relative_to(ROOT)}")
                if height / heights[i] < 2:
                    low_density.append(f"{key[0]}/{key[1]} frame {i+1}: {width}x{height} / {heights[i]} DIP")
                    action_label = f"{key[0]}/{key[1]}"
                    low_density_by_action[action_label] = low_density_by_action.get(action_label, 0) + 1
            except (OSError, ValueError) as error:
                failures.append(str(error))
    if args.release:
        for skin in ("pajamas", "black-tee"):
            for action_id in sorted(REQUIRED_ACTIONS - {id for s, id in seen if s == skin}):
                failures.append(f"missing release action: {skin}/{action_id}")
    print(f"Validated {len(actions)} action entries. Failures: {len(failures)}. Below 2 px/DIP: {len(low_density)}.")
    for item in failures:
        label = "RELEASE" if item.startswith(("missing release action", "incomplete action", "preview action", "repeated-frame action")) else "ERROR"
        print(label, item)
    for action_label, count in sorted(low_density_by_action.items()):
        print("DENSITY", action_label, count, "frames")
    if args.details:
        for item in low_density:
            print("DETAIL", item)
    return 1 if failures or (args.strict or args.release) and low_density else 0


if __name__ == "__main__":
    sys.exit(main())
