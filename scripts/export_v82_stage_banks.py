#!/usr/bin/env python3
"""Original V82 stage XOBF object-bank conversion. NOT complete arena terrain or gameplay."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from export_legacy_geometry import export_model

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/GameContent/ExtractedStageObjectsV82"
SOURCE_DIRS = {
    "v82_alaskan_pipeline": "OILFIELD",
    "v82_ghastly_bayou": "BAYOU",
    "v82_launch_site": "LAUNCH",
    "v82_meteor_crater": "Route66",
    "v82_nuclear_plant": "NUCLEAR",
    "v82_pacific_harbor": "HARBOR",
    "v82_steel_mill": "STEELMIL",
    "v82_winter_games": "OLYMPIC"
}


def blob_sha(data: bytes) -> str:
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\0" + data).hexdigest()


def compare_obj(actual: str, expected: str, name: str) -> None:
    left, right = actual.splitlines(), expected.splitlines()
    if len(left) != len(right):
        raise ValueError(name + ": original object-bank record count mismatch")
    for line_no, (a, b) in enumerate(zip(left, right), 1):
        if a == b:
            continue
        # Python and JS may round exact half-micro unit ties in opposite
        # directions; tolerate that representation, never face/topology drift.
        if a.startswith("v ") and b.startswith("v "):
            va, vb = a.split(), b.split()
            if len(va) == len(vb) == 4 and all(
                abs(float(va[i]) - float(vb[i])) <= 0.0000011 for i in (1, 2, 3)
            ):
                continue
        raise ValueError(f"{name}: original TMD geometry mismatches at OBJ line {line_no}")


def convert(root: Path = ROOT, output: Path = OUTPUT, check: bool = False) -> dict:
    audit = json.loads((root / "Assets/GameContent/LegacyStageManifests/scene-audit.json").read_text("utf-8"))
    catalog = json.loads((root / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
    ids = {arena["id"] for arena in catalog["arenas"] if arena["game"] == "v82"}
    if ids != set(SOURCE_DIRS) or len(ids) != 8:
        raise ValueError("Original stage object banks must match all eight official arenas")
    records = []
    for stage_id, folder in sorted(SOURCE_DIRS.items()):
        source_path = Path(f"LegacySources/Vigilante2Unity/Assets/LEVELS/{folder}/XOBF00.prefab")
        if source_path.is_absolute() or ".." in source_path.parts:
            raise ValueError("Unsafe legacy source path")
        data = (root / source_path).read_bytes()
        if not data.startswith(b"%YAML 1.1"):
            raise ValueError("Missing original stage object bank: " + stage_id)
        obj, info = export_model(data.decode("utf-8-sig"), stage_id)
        obj_path = output / f"{stage_id}.obj"
        if check:
            if not obj_path.is_file():
                raise ValueError("Extracted stage OBJ missing: " + stage_id)
            compare_obj(obj_path.read_text("utf-8"), obj, stage_id)
        else:
            output.mkdir(parents=True, exist_ok=True)
            obj_path.write_text(obj, encoding="utf-8", newline="\n")
        records.append({
            "stageId": stage_id,
            "sourcePath": source_path.as_posix(),
            "sourceGitBlobSha": blob_sha(data),
            "sourceScenePath": audit["scenes"][stage_id]["sourcePath"],
            "tmdParts": info["tmdParts"],
            "emittedGroups": sum(x.startswith("g ") for x in obj.splitlines()),
            "vertices": info["vertices"],
            "triangles": info["triangles"],
            "skippedRecords": info["skippedRecords"],
            "objPath": obj_path.relative_to(root).as_posix(),
            "terrainReady": False,
            "materialsReady": False,
            "collisionReady": False,
            "gameplayTested": False
        })
    report = {
        "schemaVersion": 1,
        "sourceRevision": audit["sourceRevision"],
        "status": "authentic_v82_xobf_stage_object_geometry_not_terrain_or_playable_arena",
        "stageCount": 8,
        "stages": records
    }
    target = output / "manifest.json"
    if check:
        if not target.exists() or json.loads(target.read_text("utf-8")) != report:
            raise ValueError("Original stage object geometry report changed")
    else:
        target.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = convert(check=args.check)
    print(f"PASS: {result['stageCount']} source object banks preserved, NOT playable arenas")
