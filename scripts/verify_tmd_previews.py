#!/usr/bin/env python3
"""Verify real original polygon counts of each authored preview, not playability."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(root=ROOT):
    directory = root / "Assets/GameContent/TexturedPreviewV82"
    data = json.loads((directory / "manifest.json").read_text("utf-8"))
    orig = json.loads((root / "Assets/GameContent/ExtractedV82/manifest.json").read_text("utf-8"))
    expected = {model["vehicleId"]: model for model in orig["vehicleModels"]}
    if len(data["vehicles"]) != 18 or {m["vehicleId"] for m in data["vehicles"]} != set(expected):
        raise ValueError("Missing official source-derived previews")
    for record in data["vehicles"]:
        name = record["vehicleId"]
        obj = (directory / (name + ".obj")).read_text("utf-8")
        mtl = (directory / (name + ".mtl")).read_text("utf-8")
        if obj.count("\nf ") != expected[name]["triangles"] or "mtllib " + name + ".mtl" not in obj:
            raise ValueError("Missing triangle or original material source for " + name)
        if record["status"] != "textured_preview_not_verified_game_asset":
            raise ValueError("Unverified playable gameplay claim for " + name)
        if record["mappedOriginalBitmapMaterials"] and "map_Kd ../LegacyTextureBank/" not in mtl:
            raise ValueError("Original source bitmap not linked for " + name)
    return data


if __name__ == "__main__":
    data = verify()
    total = sum(r["mappedOriginalBitmapMaterials"] for r in data["vehicles"])
    print(f"PASS: all 18 original PS1 car previews verified, {total} linked BMP materials")
    print("Unity gameplay, effects, PS1 part transforms and shader rendering remain unverified")
