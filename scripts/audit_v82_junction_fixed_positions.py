#!/usr/bin/env python3
"""Extract original JUNC_DB fixed-point positions and trace each genuine VigMesh.

This emits source-derived *preview-local* coordinates only. Archived Unity scene
Transform values, JUNC_DB.DAT_00 and gameplay-world placements are distinct.
Nothing is instantiated or certified as a release arena.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
from pathlib import Path

from audit_legacy_v82_scene_layouts import STAGES, git_blob_sha
from audit_v82_scene_mesh_references import (
    BANK_MANIFEST, VIGMESH_META, GROUP, audit as mesh_audit,
    map_components, meta_guid,
)

ROOT = Path(__file__).resolve().parents[1]
JUNC_SOURCE = "LegacySources/Vigilante2Unity/Assets/Scripts/DB/JUNC_DB.cs"
JUNC_META = JUNC_SOURCE + ".meta"
GAMEMANAGER_SOURCE = "LegacySources/Vigilante2Unity/Assets/Scripts/GameManager.cs"
GAMEMANAGER_META = GAMEMANAGER_SOURCE + ".meta"
DEBUG_SCENE = "LegacySources/Vigilante2Unity/Assets/DEBUG.unity"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$")
GUID = re.compile(r"\bguid: ([0-9a-fA-F]{32})\b")
FILE_ID = re.compile(r"\bfileID: (-?\d+)\b")
VECTOR = re.compile(r"^  DAT_00: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}$")


def read_source_components(scene: str, script_guid: str) -> list[dict]:
    """Read only exact MonoBehaviour source GUID and signed 32-bit DAT_00."""
    if not scene.startswith("%YAML 1.1\n"):
        raise ValueError("Not an archived Unity scene")
    entries: list[dict] = []
    kind = identity = ""
    row: dict = {}

    def finish() -> None:
        if kind == "114" and row.get("scriptGuid") == script_guid:
            entries.append({
                "componentFileId": identity,
                "gameObjectFileId": row.get("gameObjectFileId"),
                "rawDat00": row.get("rawDat00"),
                "vigMeshComponentFileId": row.get("vigMeshComponentFileId"),
                "referencedXobfGuid": row.get("referencedXobfGuid"),
                "enabled": row.get("enabled"),
            })

    for line in scene.splitlines():
        if (match := HEADER.match(line)):
            finish()
            kind, identity = match.groups()
            row = {}
            continue
        if kind != "114":
            continue
        if line.startswith("  m_Script: "):
            m = GUID.search(line)
            row["scriptGuid"] = m.group(1).lower() if m else None
        elif line.startswith("  m_GameObject: "):
            m = FILE_ID.search(line)
            row["gameObjectFileId"] = m.group(1) if m else None
        elif line.startswith("  DAT_18: "):
            m = FILE_ID.search(line)
            row["vigMeshComponentFileId"] = m.group(1) if m else None
        elif line.startswith("  DAT_0C: "):
            m = GUID.search(line)
            row["referencedXobfGuid"] = m.group(1).lower() if m else None
        elif line.startswith("  DAT_00: "):
            m = VECTOR.match(line)
            if m:
                numbers = tuple(int(n) for n in m.groups())
                if all(-(2 ** 31) <= n < 2 ** 31 for n in numbers):
                    row["rawDat00"] = numbers
                else:
                    row["rawDat00"] = None
            else:
                row["rawDat00"] = None
        elif line.startswith("  m_Enabled: "):
            n = line.partition(":")[2].strip()
            row["enabled"] = n == "1" if n in ("0", "1") else None
    finish()
    return sorted(entries, key=lambda row: int(row["componentFileId"]))


def source_to_unity_local(raw: tuple[int, int, int] | None,
                          divisor: int) -> dict | None:
    """Literal JUNC_DB.Update axis and scale; NOT world position/collision."""
    if not isinstance(divisor, int) or isinstance(divisor, bool) or divisor <= 0:
        raise ValueError("translateFactor must be a known positive integer")
    if raw is None or len(raw) != 3 or any(
        not isinstance(value, int) or isinstance(value, bool) or
        not -(2 ** 31) <= value < 2 ** 31 for value in raw
    ):
        return None
    x, y, z = raw
    return {"x": x / divisor, "y": -y / divisor, "z": z / divisor}


def debug_scene_translate_factor(scene: str, script_guid: str) -> int:
    """Read explicit GameManager inspector values, not the C# class default."""
    if not scene.startswith("%YAML 1.1\n"):
        raise ValueError("Missing archived DEBUG.unity")
    found: list[int] = []
    kind = ""
    data: dict = {}

    def finish() -> None:
        if kind == "114" and data.get("scriptGuid") == script_guid:
            value = data.get("translateFactor")
            if not isinstance(value, int) or value <= 0:
                raise ValueError("DEBUG GameManager lacks valid translateFactor")
            found.append(value)

    for line in scene.splitlines():
        if (m := HEADER.match(line)):
            finish()
            kind = m.group(1)
            data = {}
            continue
        if kind != "114":
            continue
        if line.startswith("  m_Script: "):
            m = GUID.search(line)
            data["scriptGuid"] = m.group(1).lower() if m else None
        elif line.startswith("  translateFactor: "):
            try:
                data["translateFactor"] = int(line.partition(":")[2].strip())
            except ValueError:
                data["translateFactor"] = None
    finish()
    if len(found) != 1:
        raise ValueError("Exactly one serialized DEBUG GameManager scale is required")
    return found[0]


def inspect_origin_code(source: str, manager: str) -> int:
    """Protect the formula against source drift: Y is inverted in JUNC_DB.Update."""
    signals = (
        "public Vector3Int DAT_00;",
        "public VigMesh DAT_18;",
        "transform.localPosition = new Vector3(",
        "(float)DAT_00.x / GameManager.instance.translateFactor,",
        "(float)-DAT_00.y / GameManager.instance.translateFactor,",
        "(float)DAT_00.z / GameManager.instance.translateFactor);",
    )
    if not all(signal in source for signal in signals):
        raise ValueError("Archived JUNC_DB position conversion has changed")
    match = re.search(r"\bpublic int translateFactor = (\d+);", manager)
    if not match or int(match.group(1)) <= 0:
        raise ValueError("Archived GameManager class default missing")
    return int(match.group(1))


def link_junctions(scene: str, junc_guid: str, mesh_guid: str, stage_id: str,
                   bank_guid: str, groups: set[str], divisor: int) -> list[dict]:
    meshes = map_components(scene, stage_id, bank_guid, groups, mesh_guid)
    mesh_by_component = {row["componentFileId"]: row for row in meshes}
    junctions = read_source_components(scene, junc_guid)
    output = []
    for junction in junctions:
        linked = mesh_by_component.get(junction["vigMeshComponentFileId"])
        raw = junction["rawDat00"]
        preview = source_to_unity_local(raw, divisor)
        if preview is None:
            status = "invalid_or_missing_source_dat00"
        elif not junction["vigMeshComponentFileId"] or junction["vigMeshComponentFileId"] == "0":
            status = "source_junction_without_mesh"
        elif linked is None:
            status = "vigmesh_component_not_found_or_not_original"
        elif linked["gameObjectFileId"] != junction["gameObjectFileId"]:
            status = "linked_vigmesh_belongs_to_another_gameobject"
        elif junction["referencedXobfGuid"] != bank_guid or linked["referencedXobfGuid"] != bank_guid:
            status = "junction_or_mesh_uses_different_xobf"
        elif linked["originalTmdGroup"] is None:
            status = "linked_tmd_not_extracted"
        else:
            status = "linked_junction_and_original_stage_tmd"
        output.append({
            "junctionFileId": junction["componentFileId"],
            "gameObjectFileId": junction["gameObjectFileId"],
            "gameObjectName": linked["gameObjectName"] if linked and
                              linked["gameObjectFileId"] == junction["gameObjectFileId"] else None,
            "sourceDat00Int32": dict(zip(("x", "y", "z"), raw)) if raw is not None else None,
            "debugScalePreviewLocalPosition": preview,
            "referencedVigMeshFileId": junction["vigMeshComponentFileId"],
            "referencedXobfGuid": junction["referencedXobfGuid"],
            "linkedTmdId": linked["tmdId"] if linked else None,
            "linkedOriginalTmdGroup": linked["originalTmdGroup"] if status ==
                                          "linked_junction_and_original_stage_tmd" else None,
            "enabledInArchivedScene": junction["enabled"],
            "referenceStatus": status,
        })
    return output


def audit(root: Path = ROOT) -> dict:
    junc_guid = meta_guid((root / JUNC_META).read_text("utf-8"))
    manager_guid = meta_guid((root / GAMEMANAGER_META).read_text("utf-8"))
    source_default = inspect_origin_code(
        (root / JUNC_SOURCE).read_text("utf-8"),
        (root / GAMEMANAGER_SOURCE).read_text("utf-8-sig"),
    )
    divisor = debug_scene_translate_factor(
        (root / DEBUG_SCENE).read_text("utf-8-sig"), manager_guid)
    previous = mesh_audit(root)
    if previous["sceneCount"] != 8:
        raise ValueError("Eight authentic scene and TMD bank crosswalks required")
    bank_index = {
        item["stageId"]: item for item in
        json.loads((root / BANK_MANIFEST).read_text("utf-8"))["stages"]
    }
    stages = {}
    for stage_id, (relative, expected_sha) in sorted(STAGES.items()):
        scene_path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / relative
        if git_blob_sha(scene_path) != expected_sha:
            raise ValueError("Archived stage source changed: " + stage_id)
        bank = bank_index[stage_id]
        bank_guid = previous["stages"][stage_id]["sourceBankGuid"]
        groups = set(GROUP.findall((root / bank["objPath"]).read_text("utf-8")))
        if len(groups) != previous["stages"][stage_id]["originalTmdGroups"]:
            raise ValueError("Source TMD geometry changed: " + stage_id)
        records = link_junctions(scene_path.read_text("utf-8-sig"),
                                 junc_guid, previous["originalVigMeshScriptGuid"],
                                 stage_id, bank_guid, groups, divisor)
        stages[stage_id] = {
            "sourceScene": bank["sourceScenePath"],
            "originalGitBlobSha": expected_sha,
            "junctionComponents": len(records),
            "statuses": dict(sorted(collections.Counter(
                row["referenceStatus"] for row in records).items())),
            "records": records,
        }
    return {
        "schemaVersion": 1,
        "status": "archived_junc_fixed_point_preview_positions_not_gameplay_placement",
        "sceneCount": len(stages),
        "sourceJunctionScriptGuid": junc_guid,
        "debugSceneGameManagerGuid": manager_guid,
        "gameManagerSourceDefaultTranslateFactor": source_default,
        "debugSceneSerializedTranslateFactor": divisor,
        "unityAxisMapping": "x = DAT_00.x / factor; y = -DAT_00.y / factor; z = DAT_00.z / factor",
        "activeMatchTranslateFactorVerified": False,
        "originalSpawnPositionsVerified": False,
        "arenaGeometryPlacementCertified": False,
        "stages": stages,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    options = parser.parse_args()
    report = audit()
    if options.json:
        print(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False))
    else:
        print("DEBUG GameManager scale:", report["debugSceneSerializedTranslateFactor"],
              "C# default:", report["gameManagerSourceDefaultTranslateFactor"])
        for name, stage in report["stages"].items():
            print(name, stage["junctionComponents"], "JUNC_DB records:", stage["statuses"])
        print("NO production/world-placement claim; JUNC_DB uses dynamic terrain deformation.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
