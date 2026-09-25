"""Create a review candidate with rembg; never overwrite the approved master.

Install rembg and onnxruntime in an isolated Python environment before use.
The model is a local production dependency and is not bundled with the pet.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MASTERS = (ROOT / "art" / "masters").resolve()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("target", type=Path)
    parser.add_argument("--model", default="u2net_human_seg")
    args = parser.parse_args()
    source = args.source.resolve(strict=True)
    target = args.target.resolve()
    if source == target or target == MASTERS or MASTERS in target.parents:
        parser.error("write to a new review path; approved master files are immutable")
    if target.exists():
        parser.error("target exists; choose a new review path")
    if source.suffix.lower() != ".png" or target.suffix.lower() != ".png":
        parser.error("source and target must be PNG")

    from rembg import new_session, remove

    data = remove(source.read_bytes(), session=new_session(args.model))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    print(f"source SHA-256: {hashlib.sha256(source.read_bytes()).hexdigest().upper()}")
    print(f"output SHA-256: {hashlib.sha256(data).hexdigest().upper()}")
    print(target)


if __name__ == "__main__":
    main()
