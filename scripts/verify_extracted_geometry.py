#!/usr/bin/env python3
"""Check exported real TMD triangles against the 18 official sequel entries."""
import hashlib
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/official-asset-sources.json"
DEST = ROOT / "Assets/GameContent/ExtractedV82"


def verify(root=ROOT):
    expected = json.loads((root / "docs/official-asset-sources.json").read_text())["vehicles"]
    folder = root / "Assets/GameContent/ExtractedV82"
    data = json.loads((folder / "manifest.json").read_text())
    models = data["vehicleModels"]
    if len(models) != 18 or len({m["vehicleId"] for m in models}) != 18:
        raise ValueError("Not all 18 official vehicle geometry files were generated")
    if {m["vehicleId"] for m in models} != set(expected):
        raise ValueError("Unexpected vehicle IDs or missing models")
    for model in models:
        if model["prefabReady"] or model["materialsReady"] or model["gameplayTested"]:
            raise ValueError("Unverified runtime readiness claimed")
        if model["vertices"] <= 2 or model["triangles"] <= 0:
            raise ValueError("Empty or fictional geometry")
        source = root / expected[model["vehicleId"]]["sourcePath"]
        if hashlib.sha256(source.read_bytes()).hexdigest() != model["sha256"]:
            raise ValueError("Source mesh changed since conversion")
        obj = folder / (model["vehicleId"] + ".obj")
        if not obj.is_file() or obj.stat().st_size < 100:
            raise ValueError("Missing OBJ geometry")
        if sum(line.startswith("v ") for line in obj.open(encoding="utf-8")) != model["vertices"]:
            raise ValueError("Unexpected exported vertex count")
        if sum(line.startswith("f ") for line in obj.open(encoding="utf-8")) != model["triangles"]:
            raise ValueError("Unexpected exported triangle count")
    return data


if __name__ == "__main__":
    manifest = verify()
    print("PASS: 18 official source-derived OBJ files match original TMD vertex and polygon counts")
    print("Not a certified playable or textured Unity prefab release.")
