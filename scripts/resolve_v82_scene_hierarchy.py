#!/usr/bin/env python3
"""Resolve ONLY explicit Unity YAML Transform hierarchy from eight archived V82 scenes.

Read-only. A verified world translation of a serialized Transform is NOT a
verified arena prop, collision, original spawn, prefab instance, or Unity 6 scene.
All positions are computed from full parent TRS matrices, without assuming
unresolved parent transforms are at the origin.
"""
from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path

from audit_legacy_v82_scene_layouts import STAGES, git_blob_sha

ROOT = Path(__file__).resolve().parents[1]
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$")
XYZ = re.compile(r"^  (m_LocalPosition|m_LocalScale): \{x: ([^,}]+), y: ([^,}]+), z: ([^,}]+)\}$")
QUAT = re.compile(r"^  m_LocalRotation: \{x: ([^,}]+), y: ([^,}]+), z: ([^,}]+), w: ([^,}]+)\}$")
FILE_ID = re.compile(r"fileID: (-?\d+)")


def numeric(values: tuple[str, ...]) -> tuple[float, ...] | None:
    try:
        result = tuple(float(value) for value in values)
    except ValueError:
        return None
    return result if all(math.isfinite(value) and abs(value) < 1e12 for value in result) else None


def read_explicit_transforms(text: str) -> dict[str, dict]:
    """Read both Transform (4) and RectTransform (224), with no synthetic defaults."""
    if not text.startswith("%YAML 1.1\n"):
        raise ValueError("Not a serialized original Unity YAML scene")
    objects: dict[str, str] = {}
    transforms: dict[str, dict] = {}
    kind = file_id = ""
    item: dict = {}

    def finish() -> None:
        if kind == "1" and file_id and "name" in item:
            objects[file_id] = item["name"]
        elif kind in ("4", "224") and file_id:
            if file_id in transforms:
                raise ValueError("Duplicate serialized transform fileID: " + file_id)
            transforms[file_id] = {"componentType": "RectTransform" if kind == "224"
                                   else "Transform", **item}

    for line in text.splitlines():
        header = HEADER.match(line)
        if header:
            finish()
            kind, file_id = header.groups()
            item = {}
            continue
        if kind == "1" and line.startswith("  m_Name: "):
            item["name"] = line[len("  m_Name: "):][:160]
        if kind not in ("4", "224"):
            continue
        if line.startswith("  m_GameObject: "):
            match = FILE_ID.search(line)
            if match:
                item["gameObjectFileId"] = match.group(1)
        elif line.startswith("  m_Father: "):
            match = FILE_ID.search(line)
            if match:
                item["parentTransformFileId"] = match.group(1)
        elif (match := XYZ.match(line)):
            item["localPosition" if match.group(1) == "m_LocalPosition"
                 else "localScale"] = numeric(match.groups()[1:])
        elif (match := QUAT.match(line)):
            item["localRotation"] = numeric(match.groups())
    finish()
    for item in transforms.values():
        item["gameObjectName"] = objects.get(item.get("gameObjectFileId", ""))
    return transforms


def local_matrix(row: dict) -> tuple[float, ...] | None:
    """Affine Unity TRS in row-major 3x4, preserving nonuniform parent scaling."""
    pos = row.get("localPosition")
    rot = row.get("localRotation")
    scale = row.get("localScale")
    if pos is None or rot is None or scale is None:
        return None
    n = math.sqrt(sum(v * v for v in rot))
    if not math.isfinite(n) or n <= 1e-9:
        return None
    x, y, z, w = (v / n for v in rot)
    sx, sy, sz = scale
    m = (
        (1 - 2 * (y*y + z*z)) * sx, 2 * (x*y - z*w) * sy,
        2 * (x*z + y*w) * sz, pos[0],
        2 * (x*y + z*w) * sx, (1 - 2 * (x*x + z*z)) * sy,
        2 * (y*z - x*w) * sz, pos[1],
        2 * (x*z - y*w) * sx, 2 * (y*z + x*w) * sy,
        (1 - 2 * (x*x + y*y)) * sz, pos[2],
    )
    return m if all(math.isfinite(value) for value in m) else None


def compose(parent: tuple[float, ...], child: tuple[float, ...]) -> tuple[float, ...] | None:
    matrix = tuple(
        sum(parent[r*4+k] * child[k*4+c] for k in range(3)) +
        (parent[r*4+3] if c == 3 else 0.0)
        for r in range(3) for c in range(4)
    )
    return matrix if all(math.isfinite(value) for value in matrix) else None


def resolve_hierarchy(raw: dict[str, dict]) -> list[dict]:
    cache: dict[str, tuple[tuple[float, ...] | None, str]] = {}

    def resolve(identity: str, visiting: frozenset[str]):
        if identity in visiting:
            return None, "cycle_in_source_hierarchy"
        if identity in cache:
            return cache[identity]
        row = raw[identity]
        own = local_matrix(row)
        parent_id = row.get("parentTransformFileId")
        if own is None or not parent_id:
            answer = (None, "incomplete_or_invalid_local_trs")
        elif parent_id == "0":
            answer = (own, "resolved_explicit_hierarchy")
        elif parent_id not in raw:
            answer = (None, "unresolved_parent_not_serialized")
        else:
            parent, why = resolve(parent_id, visiting | {identity})
            if parent is None:
                answer = (None, why)
            else:
                matrix = compose(parent, own)
                answer = (matrix, "resolved_explicit_hierarchy" if matrix
                          else "invalid_composed_matrix")
        cache[identity] = answer
        return answer

    records = []
    for identity in sorted(raw, key=int):
        row = raw[identity]
        matrix, status = resolve(identity, frozenset())
        world = ({"x": matrix[3], "y": matrix[7], "z": matrix[11]}
                 if matrix is not None else None)
        records.append({
            "transformFileId": identity,
            "componentType": row["componentType"],
            "gameObjectFileId": row.get("gameObjectFileId"),
            "gameObjectName": row.get("gameObjectName"),
            "parentTransformFileId": row.get("parentTransformFileId"),
            "localPosition": as_xyz(row.get("localPosition")),
            "localRotation": as_quat(row.get("localRotation")),
            "localScale": as_xyz(row.get("localScale")),
            "worldPosition": world,
            "resolutionStatus": status,
        })
    return records


def as_xyz(values):
    return dict(zip(("x", "y", "z"), values)) if values is not None else None


def as_quat(values):
    return dict(zip(("x", "y", "z", "w"), values)) if values is not None else None


def audit(root: Path = ROOT) -> dict:
    stages = {}
    for stage_id, (path_relative, expected_blob) in sorted(STAGES.items()):
        path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / path_relative
        if not path.is_file():
            raise FileNotFoundError("Missing original Unity scene: " + str(path))
        if git_blob_sha(path) != expected_blob:
            raise ValueError("Original scene Git blob changed: " + stage_id)
        records = resolve_hierarchy(read_explicit_transforms(path.read_text("utf-8-sig")))
        if not records:
            raise ValueError("No explicit Unity Transform in: " + stage_id)
        count = sum(record["worldPosition"] is not None for record in records)
        stages[stage_id] = {
            "sourceScene": str(path.relative_to(root)).replace("\\", "/"),
            "originalGitBlobSha": expected_blob,
            "serializedTransforms": len(records),
            "resolvedWorldTranslations": count,
            "unresolvedTransforms": len(records) - count,
            "records": records,
        }
    return {
        "schemaVersion": 1,
        "status": "verified_serialized_scene_transform_world_translations_not_playable_arenas",
        "sceneCount": len(stages),
        "scenes": stages,
        "originalSpawnsCertified": False,
        "stageObjectBankMappingCertified": False,
        "prefabOverridesResolved": False,
    }


def main() -> int:
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--json", action="store_true")
    args = cli.parse_args()
    data = audit()
    if args.json:
        print(json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False))
    else:
        for stage, row in data["scenes"].items():
            print(f"{stage}: {row['resolvedWorldTranslations']}/"
                  f"{row['serializedTransforms']} resolved hierarchy translations")
        print("Serialized original scene coordinates ONLY: not playable arenas or spawns.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
