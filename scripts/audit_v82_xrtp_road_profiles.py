#!/usr/bin/env python3
"""Cross-link original RSEG road indices to serialized LevelManager XRTP prefabs.

Every width, longitudinal step and FAR material reference originates from
an archived prefab selected by the original stage's xrtpList order.
No reconstructed mesh, texture atlas, collision or production road is claimed.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
from pathlib import Path

from audit_legacy_v82_scene_layouts import STAGES, git_blob_sha
from audit_v82_road_segments import audit as road_audit
from audit_v82_scene_mesh_references import meta_guid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = Path("LegacySources/Vigilante2Unity/Assets")
LEVELS = ASSETS / "LEVELS"
LEVEL_MANAGER = ASSETS / "Scripts/LevelManager.cs"
XRTP_SCRIPT = ASSETS / "Scripts/DB/XRTP_DB.cs"
XRTP_META = Path(str(XRTP_SCRIPT) + ".meta")
MANAGER_META = Path(str(LEVEL_MANAGER) + ".meta")
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$", re.M)
GUID = re.compile(r"\bguid: ([a-fA-F0-9]{32})\b")
REF = re.compile(r"\{fileID: (-?\d+), guid: ([a-fA-F0-9]{32}), type: (\d+)\}")
SCALARS = ("DAT_24", "DAT_28", "DAT_1C", "DAT_2C", "DAT_2E", "DAT_30")
INT32_MIN, INT32_MAX = -(2 ** 31), 2 ** 31 - 1


def components(source: str):
    if not source.startswith("%YAML 1.1\n"):
        raise ValueError("Expected original Unity YAML")
    matches = list(HEADER.finditer(source))
    for i, match in enumerate(matches):
        if match.group(1) == "114":
            yield match.group(2), source[match.end():(
                matches[i + 1].start() if i + 1 < len(matches) else len(source))]


def script_id(block: str) -> str | None:
    m = re.search(r"^  m_Script: ([^\r\n]+)", block, re.M)
    if m is None:
        return None
    guid = GUID.search(m.group(1))
    return guid.group(1).lower() if guid else None


def ordered_refs(block: str, name: str) -> list[dict] | None:
    lines = block.splitlines()
    for i, line in enumerate(lines):
        if line == f"  {name}:":
            refs = []
            for item in lines[i + 1:]:
                if not item.startswith("  - "):
                    break
                m = REF.search(item)
                refs.append({
                    "fileId": m.group(1) if m else None,
                    "guid": m.group(2).lower() if m else None,
                    "type": int(m.group(3)) if m else None,
                })
            return refs
    return None


def scene_xrtp_references(scene: str, manager_guid: str) -> list[dict]:
    rows = [(fid, block) for fid, block in components(scene)
            if script_id(block) == manager_guid]
    if len(rows) != 1:
        raise ValueError("Scene must contain one original LevelManager")
    refs = ordered_refs(rows[0][1], "xrtpList")
    if refs is None:
        raise ValueError("Original LevelManager.xrtpList missing")
    return refs


def verify_source_contract(level_code: str, xrtp_code: str):
    for exact in (
        "xrtpList[dbVar1.DAT_0A].timFarList.Count > 0",
        "FUN_719C(dbVar1);",
        "iVar20 = iVar20 + param2.DAT_28 / iVar21;",
        "iVar23 = ((iVar13 * param2.DAT_24) / 2) / iVar15;",
        "iVar24 = ((iVar17 * param2.DAT_24) / 2) / iVar15;",
    ):
        if exact not in level_code:
            raise ValueError("Original road surface math changed: " + exact)
    for exact in ("public int DAT_24;", "public int DAT_28;",
                  "public List<Material> timFarList;", "DAT_24 = uVar5;",
                  "DAT_28 = uVar5;"):
        if exact not in xrtp_code:
            raise ValueError("Original XRTP profile contract changed: " + exact)


def original_prefab_map(root: Path) -> dict[str, Path]:
    index = {}
    for meta in sorted((root / LEVELS).rglob("XRTP[0-9][0-9].prefab.meta")):
        guid = meta_guid(meta.read_text(encoding="utf-8-sig"))
        if guid in index:
            raise ValueError("Multiple authentic XRTP prefabs have the same GUID")
        index[guid] = meta
    return index


def original_xrtp_profile(prefab: str, script_guid: str, file_id: str) -> dict | None:
    rows = [block for fid, block in components(prefab)
            if fid == file_id and script_id(block) == script_guid]
    if len(rows) != 1:
        return None
    block = rows[0]
    values = {}
    for name in SCALARS:
        match = re.search(r"^  " + name + r": (-?\d+)\s*$", block, re.M)
        if match is None:
            return None
        value = int(match.group(1))
        if not INT32_MIN <= value <= INT32_MAX:
            return None
        values[name] = value
    materials = ordered_refs(block, "timFarList")
    if materials is None or not materials or any(
        item["fileId"] in (None, "0") or item["guid"] is None
        for item in materials
    ):
        return None
    if values["DAT_24"] <= 0 or values["DAT_28"] <= 0 or \
            values["DAT_1C"] < 0 or not 0 <= values["DAT_2C"] <= 65535:
        return None
    return {
        "sourceHalfWidthParameterFormula": "DAT_24 / 2 (FUN_5850; perpendicular normalized separately)",
        "sourceWidthParameterFixed": values["DAT_24"],
        "sourceLongitudinalStepFixed": values["DAT_28"],
        "sourceTextureFlags": values["DAT_2C"],
        "sourceTextureCountField": values["DAT_1C"],
        "sourceTextureRefs": materials,
        "sourceOtherScalars": {"DAT_2E": values["DAT_2E"], "DAT_30": values["DAT_30"]},
    }


def resolved_profile(ref: dict, stage_folder: str, index: dict[str, Path],
                     root: Path, script_guid: str) -> dict:
    if ref["guid"] is None or ref["fileId"] in (None, "0") or ref["type"] != 3:
        return {"status": "invalid_external_xrtp_reference"}
    meta = index.get(ref["guid"])
    if meta is None:
        return {"status": "xrtp_source_prefab_guid_unresolved"}
    if meta.parent.name.lower() != stage_folder.lower():
        return {"status": "xrtp_cross_stage_prefab_reference_rejected"}
    path = meta.with_suffix("")  # drops ".meta" -> original .prefab
    if not path.is_file():
        return {"status": "xrtp_prefab_source_missing"}
    profile = original_xrtp_profile(path.read_text(encoding="utf-8-sig"),
                                    script_guid, ref["fileId"])
    if profile is None:
        return {"status": "xrtp_component_or_parameters_invalid"}
    return {
        "status": "original_source_xrtp_profile_verified",
        "originalPrefabPath": path.relative_to(root).as_posix(),
        "originalPrefabGitBlobSha": git_blob_sha(path),
        "originalPrefabMetaGitBlobSha": git_blob_sha(meta),
        "originalPrefabGuid": ref["guid"],
        "originalComponentFileId": ref["fileId"],
        **profile,
    }


def audit(root: Path = ROOT, roads: dict | None = None) -> dict:
    verify_source_contract((root / LEVEL_MANAGER).read_text(encoding="utf-8-sig"),
                           (root / XRTP_SCRIPT).read_text(encoding="utf-8-sig"))
    source_guid = meta_guid((root / XRTP_META).read_text(encoding="utf-8-sig"))
    manager_guid = meta_guid((root / MANAGER_META).read_text(encoding="utf-8-sig"))
    originals = original_prefab_map(root)
    road_graph = road_audit(root) if roads is None else roads
    stages = {}
    for stage_id, (relative, expected_hash) in sorted(STAGES.items()):
        source_scene = root / ASSETS / "Levels" / relative
        if git_blob_sha(source_scene) != expected_hash:
            raise ValueError("Original XRTP stage scene hash differs: " + stage_id)
        references = scene_xrtp_references(
            source_scene.read_text(encoding="utf-8-sig"), manager_guid)
        profiles = [resolved_profile(ref, Path(relative).parent.name, originals,
                                     root, source_guid) for ref in references]
        segments = []
        for segment in road_graph["stages"][stage_id]["segments"]:
            profile_index = segment.get("sourceProfileIndex")
            if not isinstance(profile_index, int) or not 0 <= profile_index <= 65535:
                status = "rseg_xrtp_index_invalid_or_missing"
                profile = None
            elif profile_index >= len(profiles):
                status = "rseg_xrtp_index_outside_source_list"
                profile = None
            else:
                profile = profiles[profile_index]
                status = profile["status"]
            flags = segment["sourceFlags"]
            segments.append({
                "rsegComponentFileId": segment["componentFileId"],
                "junctionComponentFileIds": segment["junctionComponentFileIds"],
                "rsegReferenceStatus": segment["referenceStatus"],
                "rsegSourceFlags": flags,
                "roadGenerationDisabledBySourceFlag": bool(
                    isinstance(flags, int) and flags & 0x10),
                "sourceProfileIndex": profile_index,
                "originalXRTPProfileStatus": status,
                "originalXRTPPrefab": profile.get("originalPrefabPath")
                    if profile and status == "original_source_xrtp_profile_verified" else None,
                "sourceWidthParameterFixed": profile.get("sourceWidthParameterFixed")
                    if profile and status == "original_source_xrtp_profile_verified" else None,
                "sourceLongitudinalStepFixed": profile.get("sourceLongitudinalStepFixed")
                    if profile and status == "original_source_xrtp_profile_verified" else None,
                "materialReferencesAreNotFinalRenderedRoadTextures": True,
            })
        stages[stage_id] = {
            "originalGitBlobSha": expected_hash,
            "sourceXrtpListLength": len(references),
            "sourceProfiles": profiles,
            "sourceSegmentCount": len(segments),
            "roadProfileStatuses": dict(sorted(collections.Counter(
                r["originalXRTPProfileStatus"] for r in segments).items())),
            "segments": segments,
        }
    return {
        "schemaVersion": 1,
        "status": "archived_rseg_to_levelmanager_xrtp_width_and_material_provenance_only",
        "sceneCount": len(stages),
        "originalXRTPComponentGuid": source_guid,
        "originalLevelManagerGuid": manager_guid,
        "originalRoadSurfaceMeshCertified": False,
        "originalRoadUVsCertified": False,
        "originalRoadCollisionCertified": False,
        "materialReferencesAreNotFinalRenderedRoadTextures": True,
        "stages": stages,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = audit()
    if args.json:
        print(json.dumps(result, indent=2, allow_nan=False, ensure_ascii=False))
    else:
        for stage_id, stage in result["stages"].items():
            print(stage_id, "source XRTP profiles:", stage["sourceXrtpListLength"],
                  "linked:", stage["roadProfileStatuses"])
        print("Original road widths/material references only, NOT playable roads.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
