"""Check that a Windows ZIP contains exactly the action assets it advertises."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from zipfile import ZipFile


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("zip_path", type=Path)
    args = parser.parse_args()
    problems: list[str] = []

    with ZipFile(args.zip_path) as archive:
        names = [name.replace("\\", "/") for name in archive.namelist()]
        if len(names) != len(set(names)):
            problems.append("duplicate ZIP entries")
        roots = {name.split("/", 1)[0] for name in names if name}
        if len(roots) != 1:
            problems.append("ZIP must have one application root")
        root = next(iter(roots), "")
        prefix = root + "/"
        content = {name.removeprefix(prefix) for name in names if name.startswith(prefix)}
        for required in ("PangBaoBaoPet.exe", "Assets/actions.json", "Assets/pet.ico", "Defaults/persona.md"):
            if required not in content:
                problems.append(f"missing {required}")

        if "Assets/actions.json" in content:
            actions = json.loads(archive.read(prefix + "Assets/actions.json"))
            expected: set[str] = set()
            for action in actions:
                directory = action.get("assetDirectory", action["id"])
                for index in range(1, action["frames"] + 1):
                    expected.add(f"Assets/{directory}/frame_{index:03d}.png")
            shipped = {name for name in content if name.startswith("Assets/") and name.endswith(".png")}
            for missing in sorted(expected - shipped):
                problems.append(f"missing frame {missing}")
            for extra in sorted(shipped - expected):
                problems.append(f"unreferenced frame {extra}")
            print(f"Actions: {len(actions)}; referenced PNG frames: {len(expected)}; shipped: {len(shipped)}")

        forbidden = ("art/references/private", "art/candidates", "work/", "dialogue-api.key",
                     "pet-state.json", "settings.json", ".env")
        for name in sorted(content):
            lower = name.lower()
            if any(part in lower for part in forbidden):
                problems.append(f"private or build file {name}")
        corrupted = archive.testzip()
        if corrupted is not None:
            problems.append(f"CRC failed: {corrupted}")

    if problems:
        for problem in problems:
            print("ERROR", problem)
        return 1
    print(f"ZIP OK: {len(names)} entries; CRC passed; no unreferenced PNG or private paths")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
