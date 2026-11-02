#!/usr/bin/env python3
"""Cross-reference authentic V82 scene VigMesh components to real XOBF TMD groups.

This is source-evidence only, NOT placement or collision certification.
Original gameplay uses VigObject.DAT_00 fixed-point coordinates, which are not
interchangeable with the archived Unity Transform.worldPosition.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
from pathlib import Path

from audit_legacy_v82_scene_layouts import STAGES, git_blob_sha
from resolve_v82_scene_hierarchy import read_explicit_transforms, resolve_hierarchy

ROOT = Path(__file__).resolve().parents[1]
BANK_MANIFEST = "Assets/GameContent/ExtractedStageObjectsV82/manifest.json"
VIGMESH_META = "LegacySources/Vigilante2Unity/Assets/Scripts/VigMesh.cs.meta"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$")
GUID = re.compile(r"\bguid: ([a-fA-F0-9]{32})\b")
FILEID = re.compile(r"\bfileID: (-?\d+)\b")
GROUP = re.compile(r"^g (v82_[a-z0-9_]+_tmd_\d{3})$", re.MULTILINE)


def meta_guid(text: str) -> str:
    match = re.search(r"^guid: ([a-fA-F0-9]{32})$", text, re.MULTILINE)
    if not match:
        raise ValueError("An original .meta GUID is missing or malformed")
    return match.group(1).lower()


def parse_vigmesh_components(text: str, mesh_script_guid: str) -> list[dict]:
    """Only original VigMesh MonoBehaviours; ignore other scripts and MeshFilters."""
    if not text.startswith("%YAML 1.1\n"):
        raise ValueError("Not original Unity YAML")
    found: list[dict] = []
    kind = identity = ""
    fields: dict = {}

    def finish() -> None:
        if kind != "114" or fields.get("scriptGuid") != mesh_script_guid:
            return
        found.append({
            "componentFileId": identity,
            "gameObjectFileId": fields.get("gameObjectFileId"),
            "tmdId": fields.get("tmdId"),
            "referencedXobfGuid": fields.get("xobfGuid"),
            "enabled": fields.get("enabled"),
        })

    for line in text.splitlines():
        if (match := HEADER.match(line)):
            finish()
            kind, identity = match.groups()
            fields = {}
            continue
        if kind != "114":
            continue
        if line.startswith("  m_GameObject: "):
            f = FILEID.search(line)
            fields["gameObjectFileId"] = f.group(1) if f else None
        elif line.startswith("  m_Script: "):
            g = GUID.search(line)
            fields["scriptGuid"] = g.group(1).lower() if g else None
        elif line.startswith("  xobf: "):
            g = GUID.search(line)
            fields["xobfGuid"] = g.group(1).lower() if g else None
        elif line.startswith("  tmdID: "):
            try:
                fields["tmdId"] = int(line.split(":", 1)[1].strip())
            except ValueError:
                fields["tmdId"] = None
        elif line.startswith("  m_Enabled: "):
            n = line.split(":", 1)[1].strip()
            fields["enabled"] = n == "1" if n in ("0", "1") else None
    finish()
    return found


def map_components(
    scene: str, stage_id: str, original_bank_guid: str,
    tmd_groups: set[str], mesh_script_guid: str
) -> list[dict]:
    if not stage_id.startswith("v82_") or not tmd_groups:
        raise ValueError("Missing source stage identity or original TMD groups")
    components = parse_vigmesh_components(scene, mesh_script_guid)
    resolved = resolve_hierarchy(read_explicit_transforms(scene))
    transforms: dict[str, list[dict]] = collections.defaultdict(list)
    for record in resolved:
        if record["componentType"] == "Transform" and record["gameObjectFileId"]:
            transforms[record["gameObjectFileId"]].append(record)
    result = []
    for mesh in components:
        tmd = mesh["tmdId"]
        group = f"{stage_id}_tmd_{tmd:03d}" if tmd is not None and 0 <= tmd <= 999 else None
        obj_transforms = transforms.get(mesh["gameObjectFileId"], [])
        transform = obj_transforms[0] if len(obj_transforms) == 1 else None
        if mesh["referencedXobfGuid"] is None:
            status = "missing_xobf_reference"
        elif mesh["referencedXobfGuid"] != original_bank_guid:
            status = "different_xobf_source_bank"
        elif group not in tmd_groups:
            status = "tmd_not_extracted_from_source_bank"
        elif transform is None:
            status = "missing_or_ambiguous_serialized_transform"
        elif transform["worldPosition"] is None:
            status = "unresolved_serialized_transform_hierarchy"
        else:
            status = "matched_original_bank_and_tmd_with_serialized_transform"

        result.append({
            "componentFileId": mesh["componentFileId"],
            "gameObjectFileId": mesh["gameObjectFileId"],
            "gameObjectName": transform["gameObjectName"] if transform else None,
            "transformFileId": transform["transformFileId"] if transform else None,
            "enabledInArchivedScene": mesh["enabled"],
            "tmdId": tmd,
            "originalTmdGroup": group if group in tmd_groups else None,
            "referencedXobfGuid": mesh["referencedXobfGuid"],
            "serializedLocalPosition": transform["localPosition"] if transform else None,
            "serializedWorldPosition": transform["worldPosition"] if transform else None,
            "serializedTransformResolution": transform["resolutionStatus"] if transform else None,
            "referenceStatus": status,
        })
    return sorted(result, key=lambda r: int(r["componentFileId"]))


def audit(root: Path = ROOT) -> dict:
    manifest = json.loads((root / BANK_MANIFEST).read_text(encoding="utf-8"))
    entries = manifest.get("stages")
    if manifest.get("schemaVersion") != 1 or manifest.get("stageCount") != 8 or \
            not isinstance(entries, list) or {r["stageId"] for r in entries} != set(STAGES):
        raise ValueError("Original stage bank manifest does not match the 8 preserved scenes")
    script_guid = meta_guid((root / VIGMESH_META).read_text(encoding="utf-8"))
    stages = {}
    for bank in sorted(entries, key=lambda r: r["stageId"]):
        stage_id = bank["stageId"]
        relative, scene_sha = STAGES[stage_id]
        scene_path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / relative
        if git_blob_sha(scene_path) != scene_sha:
            raise ValueError("Original archived scene changed: " + stage_id)
        if bank["sourceScenePath"] != scene_path.relative_to(root).as_posix():
            raise ValueError("Stage bank points to a different original scene: " + stage_id)
        bank_path = root / bank["sourcePath"]
        if git_blob_sha(bank_path) != bank["sourceGitBlobSha"]:
            raise ValueError("Original XOBF bank changed: " + stage_id)
        bank_guid = meta_guid((root / (bank["sourcePath"] + ".meta")).read_text("utf-8"))
        obj_path = root / bank["objPath"]
        groups = set(GROUP.findall(obj_path.read_text(encoding="utf-8")))
        if len(groups) != bank["emittedGroups"] or \
                not all(x.startswith(stage_id + "_tmd_") for x in groups):
            raise ValueError("Original extracted TMD groups mismatch: " + stage_id)
        source = scene_path.read_text(encoding="utf-8-sig")
        records = map_components(source, stage_id, bank_guid, groups, script_guid)
        statuses = dict(sorted(collections.Counter(r["referenceStatus"] for r in records).items()))
        stages[stage_id] = {
            "sourceScene": bank["sourceScenePath"],
            "sceneGitBlobSha": scene_sha,
            "sourceBank": bank["sourcePath"],
            "bankGitBlobSha": bank["sourceGitBlobSha"],
            "sourceBankGuid": bank_guid,
            "originalTmdGroups": len(groups),
            "vigMeshComponents": len(records),
            "matchStatuses": statuses,
            "records": records,
        }
    return {
        "schemaVersion": 1,
        "status": "original_vigmesh_xobf_tmd_reference_audit_not_playable_arenas",
        "sceneCount": len(stages),
        "originalVigMeshScriptGuid": script_guid,
        "stages": stages,
        "inGamePositionVerified": False,
        "collisionVerified": False,
        "spawnPointsVerified": False,
        "unitySixArenaPrefabsCertified": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    options = parser.parse_args()
    report = audit()
    if options.json:
        print(json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False))
    else:
        for stage_id, item in report["stages"].items():
            print(stage_id, item["vigMeshComponents"], "VigMesh components;",
                  item["matchStatuses"])
        print("Source mesh references only. NO original in-game prop coordinates certified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
