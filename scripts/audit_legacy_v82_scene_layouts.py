#!/usr/bin/env python3
"""Audit preserved V82 Unity scene YAML without claiming playable stage placement.

Read-only: never imports prefab instances, never fabricates coordinates.
A Transform.m_LocalPosition is LOCAL Unity serialization, not a verified world
spawn or terrain-object placement. PrefabInstances may contain overrides not
resolved by this report. This script provides trustworthy source inventory
for further original arena reconstruction, NOT full stage conversion.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGES = {
    "v82_alaskan_pipeline": ("Oilfield/OILFIELD.unity", "eeabc7174370d1482f7c8aa2c61df60739b13925"),
    "v82_ghastly_bayou": ("Bayou/BAYOU.unity", "391c654649366c665d3ecfb283b87d3a04182045"),
    "v82_launch_site": ("Launch/LAUNCH.unity", "df6ac4f87de0e7d7a50087bfe2c0b7902a234334"),
    "v82_meteor_crater": ("Route66/ROUTE66.unity", "3669c932afbcc1548cdb7635f7a139793bfaadbf"),
    "v82_nuclear_plant": ("Nuclear/NUCLEAR.unity", "1327a7cad46cba445d2a286c4edccb23f20912eb"),
    "v82_steel_mill": ("Steelmil/STEELMIL.unity", "2d39a1a504c443605a85ab205c78715287d72116"),
    "v82_winter_games": ("Olympic/OLYMPIC.unity", "3e1f9d9e94b5c43330555a69b21fe5a8f9669796"),
    "v82_pacific_harbor": ("Harbor/HARBOR.unity", "b2ad393114f437357f092f3f4f17f17cac430f0b"),
}

# Unity YAML object sections, not the nested tmdList inside MonoBehaviour.
SECTION = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$")
POSITION = re.compile(
    r"^  m_LocalPosition: \{x: ([^,}]+), y: ([^,}]+), z: ([^,}]+)\}$"
)
FILE_ID = re.compile(r"fileID: (-?\d+)")
GUID = re.compile(r"guid: ([a-fA-F0-9]{32})")


def git_blob_sha(path: Path) -> str:
    size = path.stat().st_size
    digest = hashlib.sha1()
    digest.update(f"blob {size}\0".encode("ascii"))
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def inspect_scene(path: Path) -> dict:
    """Parse recognized YAML headers in O(file size), bounded in-memory output."""
    objects: dict[str, str] = {}
    transforms: dict[str, tuple[str, tuple[float, float, float], str]] = {}
    prefab_guids: set[str] = set()
    section_kind = ""
    current_id = ""
    current_name = ""
    owner_id = ""
    local_pos = None
    father_id = None
    source_guid = ""
    section_count = prefab_count = 0
    yaml_header = False

    def finish() -> None:
        nonlocal prefab_count
        if section_kind == "1" and current_id and current_name:
            objects[current_id] = current_name
        elif section_kind == "4" and current_id and owner_id and local_pos is not None:
            transforms[current_id] = (owner_id, local_pos, father_id or "")
        elif section_kind == "1001":
            prefab_count += 1
            if source_guid:
                prefab_guids.add(source_guid.lower())

    with path.open("r", encoding="utf-8-sig", errors="strict") as stream:
        for raw in stream:
            line = raw.rstrip("\r\n")
            if not yaml_header:
                if not line.startswith("%YAML 1.1"):
                    raise ValueError(f"Not an archived Unity YAML scene: {path}")
                yaml_header = True
            header = SECTION.match(line)
            if header:
                finish()
                section_kind, current_id = header.groups()
                current_name = owner_id = father_id = source_guid = ""
                local_pos = None
                section_count += 1
                continue
            if section_kind == "1" and line.startswith("  m_Name: "):
                current_name = line[len("  m_Name: "):][:160]
            elif section_kind == "4":
                if line.startswith("  m_GameObject: "):
                    found = FILE_ID.search(line)
                    owner_id = found.group(1) if found else ""
                elif line.startswith("  m_Father: "):
                    found = FILE_ID.search(line)
                    father_id = found.group(1) if found else ""
                else:
                    match = POSITION.match(line)
                    if match:
                        coordinates = tuple(float(x) for x in match.groups())
                        if not all(-1e12 < value < 1e12 for value in coordinates):
                            raise ValueError(f"Nonfinite or invalid Unity Transform: {path}")
                        local_pos = coordinates
            elif section_kind == "1001" and line.startswith("  m_SourcePrefab: "):
                found = GUID.search(line)
                source_guid = found.group(1) if found else ""
    finish()

    roots = []
    named = 0
    nonzero_local = 0
    records = []
    for transform_id, (owner, local, father) in sorted(
        transforms.items(), key=lambda item: int(item[0])
    ):
        name = objects.get(owner)
        if name:
            named += 1
        if any(value != 0.0 for value in local):
            nonzero_local += 1
        if father == "0" and name:
            roots.append(name)
        records.append({
            "transformFileId": transform_id,
            "gameObjectFileId": owner,
            "gameObjectName": name,
            "parentTransformFileId": father,
            "localPosition": {"x": local[0], "y": local[1], "z": local[2]},
        })
    return {
        "yamlSections": section_count,
        "namedGameObjects": len(objects),
        "explicitTransforms": len(transforms),
        "namedTransforms": named,
        "nonzeroLocalTransforms": nonzero_local,
        "prefabInstances": prefab_count,
        "referencedSourcePrefabs": len(prefab_guids),
        "rootNamesSample": sorted(set(roots))[:12],
        "localTransformRecords": records,
        "note": "Unity LOCAL positions only, prefab modifications unresolved; not certified world placements.",
    }


def audit(root: Path = ROOT) -> dict:
    stages = {}
    for stage_id, (relative, sha) in sorted(STAGES.items()):
        path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / relative
        if not path.is_file():
            raise FileNotFoundError(f"Missing real original Unity scene: {path}")
        if git_blob_sha(path) != sha:
            raise ValueError(f"Archived original scene Git blob SHA mismatch: {stage_id}")
        record = inspect_scene(path)
        if record["yamlSections"] == 0 or record["explicitTransforms"] == 0:
            raise ValueError(f"No serialized Unity transforms: {stage_id}")
        stages[stage_id] = {
            "sourceScene": str(path.relative_to(root)).replace("\\", "/"),
            "originalGitBlobSha": sha,
            "sourceBytes": path.stat().st_size,
            **record,
        }
    return {
        "status": "preserved_unity_yaml_transform_metadata_only_not_playable_arenas",
        "sceneCount": len(stages),
        "scenes": stages,
        "missingProductionAssetsAreNotSynthesized": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = audit()
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        for stage, data in result["scenes"].items():
            print(f"{stage}: {data['explicitTransforms']} local transforms, "
                  f"{data['prefabInstances']} prefab instances, "
                  f"{data['nonzeroLocalTransforms']} nonzero local positions")
        print("Source-only inspection: NONE of these are certified world object positions.")
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as out:
            out.write("\n### Archived V82 Unity scene transforms (nonrelease)\n")
            for name, row in result["scenes"].items():
                out.write(f"- {name}: {row['explicitTransforms']} explicit transforms, "
                          f"{row['prefabInstances']} prefab instances\n")
            out.write("Original source metadata only; no stage placement certification.\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
