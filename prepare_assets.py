"""Extract already approved local preview frames for the standalone WPF app."""
from pathlib import Path
from PIL import Image
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
DEST = Path(__file__).resolve().parent / "PangBaoBaoPet" / "Assets"
PREVIEWS = ROOT / "outputs" / "胖宝宝-广播体操预览"
STRETCH = ROOT / "outputs" / "胖宝宝-高帧数样片" / "OpenAnima-伸展动作-手臂修正版"

actions = [
    {"id": "stretch", "name": "伸展", "source": "手臂修正版透明帧", "fps": 8},
]
target = DEST / "stretch"
target.mkdir(parents=True, exist_ok=True)
for frame in sorted(STRETCH.glob("frame_*.png")):
    shutil.copy2(frame, target / frame.name)
actions[0]["frames"] = len(list(target.glob("frame_*.png")))
with Image.open(target / "frame_001.png") as first:
    body_height = first.convert("RGBA").getchannel("A").getbbox()[3] - first.convert("RGBA").getchannel("A").getbbox()[1]
    actions[0]["displayHeight"] = round(first.height * 310 / body_height)

gif_actions = [
    ("chest", "扩胸", "扩胸.gif"),
    ("bend", "体前屈", "体前屈.gif"),
    ("twist", "转体", "转体.gif"),
    ("tiptoe", "踮脚", "踮脚.gif"),
    ("sidebend", "侧弯", "伸展与侧弯.gif"),
]
for action_id, name, filename in gif_actions:
    source = PREVIEWS / filename
    target = DEST / action_id
    target.mkdir(parents=True, exist_ok=True)
    durations = []
    with Image.open(source) as gif:
        for index in range(gif.n_frames):
            gif.seek(index)
            frame = gif.convert("RGBA")
            frame.save(target / f"frame_{index + 1:03}.png")
            durations.append(max(40, int(gif.info.get("duration", 150))))
    with Image.open(target / "frame_001.png") as first:
        bbox = first.convert("RGBA").getchannel("A").getbbox()
        display_height = round(first.height * 310 / (bbox[3] - bbox[1]))
    actions.append({"id": action_id, "name": name, "source": filename,
                    "durationsMs": durations, "frames": len(durations),
                    "displayHeight": display_height})

(DEST / "actions.json").write_text(json.dumps(actions, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Prepared {sum(action['frames'] for action in actions)} frames in {len(actions)} actions")
