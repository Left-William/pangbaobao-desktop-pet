"""Render an action on the current WPF stage for visual frame review.

This review tool uses the manifest's per-frame height and offset. It does not
modify runtime PNGs and intentionally keeps repeated poses visible.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src" / "PangBaoBaoPet.Desktop" / "Assets"
STAGE = (430, 490)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skin", required=True)
    parser.add_argument("--action", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--background", choices=("gray", "white", "black"), default="gray")
    args = parser.parse_args()

    actions = json.loads((ASSETS / "actions.json").read_text(encoding="utf-8"))
    action = next((entry for entry in actions if entry.get("skinId", "pajamas") == args.skin
                   and entry["id"] == args.action), None)
    if action is None:
        parser.error("unknown skin/action")

    count = action["frames"]
    heights = action.get("frameDisplayHeights", [action.get("displayHeight", 362)] * count)
    offsets = action.get("frameOffsets", [[0, 0]] * count)
    directory = ASSETS / action.get("assetDirectory", action["id"])
    frames = sorted(directory.glob("frame_*.png"))
    if len(frames) != count:
        parser.error("manifest and file frame counts differ")

    backgrounds = {"gray": (115, 115, 115, 255), "white": (255, 255, 255, 255),
                   "black": (15, 15, 15, 255)}
    cols = min(count, 5)
    rows = (count + cols - 1) // cols
    contact = Image.new("RGBA", (cols * STAGE[0], rows * STAGE[1]), backgrounds[args.background])
    draw = ImageDraw.Draw(contact)
    for index, path in enumerate(frames):
        with Image.open(path) as opened:
            frame = opened.convert("RGBA")
        height = round(heights[index])
        width = round(height * frame.width / frame.height)
        rendered = frame.resize((width, height), Image.Resampling.LANCZOS)
        offset_x, offset_y = offsets[index]
        x = index % cols * STAGE[0] + round((STAGE[0] - width) / 2 + offset_x)
        y = index // cols * STAGE[1] + round(STAGE[1] - 18 - height + offset_y)
        contact.alpha_composite(rendered, (x, y))
        draw.text((index % cols * STAGE[0] + 8, index // cols * STAGE[1] + 8),
                  f"{index + 1:02d}  {heights[index]} DIP", fill=(255, 235, 40, 255),
                  stroke_width=1, stroke_fill=(0, 0, 0, 255))

    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    contact.convert("RGB").save(output)
    print(output)


if __name__ == "__main__":
    main()
