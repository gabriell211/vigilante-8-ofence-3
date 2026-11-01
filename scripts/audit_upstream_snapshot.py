#!/usr/bin/env python3
"""Verify the staged complete upstream Unity project without activating its legacy scripts.

Snapshot is stored outside Assets/ deliberately: Unity 2020 scripts/shaders
must be migrated and tested before compilation in Unity 6.6.
"""
from __future__ import annotations
import argparse
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PREFIX = ROOT / "LegacySources/Vigilante2Unity"
EXPECTED_COMMIT = "d20883df0d5da3ba517142fb4d486db1f5e673fb"
REQUIRED = (
    "Assets/Scripts/Vehicle.cs",
    "Assets/Scripts/MGun.cs",
    "Assets/Scripts/Missile.cs",
    "Assets/Scripts/Rocket.cs",
    "Assets/Scripts/Mortar.cs",
    "Assets/Scripts/Mine.cs",
    "Assets/Scripts/FlameThrower.cs",
    "Assets/SHARED/COMMON/Scripts/GloryRockets.cs",
    "Assets/SHELL/VEHICLES/BUS.prefab",
    "Assets/Levels/Route66/ROUTE66.unity",
    "Assets/Levels/Olympic/OLYMPIC.unity",
    "Assets/SHARED/SOUNDS/SOUND_0000.wav",
    "Assets/Tsunami.fbx",
    "Assets/Blue Burro Bus.fbx",
)
MINIMUM = {".cs": 310, ".prefab": 55, ".bmp": 2500,
           ".wav": 225, ".mat": 510, ".unity": 12, ".meta": 4000}


def verify(root: Path = ROOT) -> dict:
    archive = root / "LegacySources/Vigilante2Unity"
    assets = archive / "Assets"
    if not assets.is_dir():
        raise ValueError("Entire original Unity Assets archive is not staged")
    missing = [path for path in REQUIRED if not (archive / path).is_file()]
    if missing:
        raise ValueError("Missing original files: " + ", ".join(missing))
    files = [p for p in assets.rglob("*") if p.is_file()]
    counts = Counter(p.suffix.lower() for p in files)
    for ext, minimum in MINIMUM.items():
        if counts[ext] < minimum:
            raise ValueError(f"Incomplete {ext}: {counts[ext]} < {minimum}")
    provenance = json.loads((archive / "source.json").read_text("utf-8"))
    if provenance["commit"] != EXPECTED_COMMIT:
        raise ValueError("Unpinned upstream revision")
    return {
        "files": len(files),
        "bytes": sum(p.stat().st_size for p in files),
        "extensions": dict(sorted(counts.items())),
        "requiredFilesVerified": len(REQUIRED),
        "sourceCommit": provenance["commit"],
        "integrationState": "archived_for_porting_not_Unity6_compatible_or_playable",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true")
    opts = parser.parse_args()
    data = verify()
    if opts.json:
        print(json.dumps(data, indent=2))
    else:
        print(f"PASS upstream Unity source snapshot: {data['files']} files, "
              f"{data['bytes']} bytes; {data['requiredFilesVerified']} required paths")
        print("Source is staged OUTSIDE Assets, not compiled or gameplay-homologated.")


if __name__ == "__main__":
    main()
