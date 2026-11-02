#!/usr/bin/env python3
"""Resolve real V82 XRTP road material GUIDs to original stage bitmap pixels.

Never assigns guessed asphalt textures, UVs or runtime shaders. Mat/BMP GUID
chains are verified within the original stage and reported with Git blob SHA.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
import struct
from pathlib import Path

from audit_legacy_v82_scene_layouts import STAGES, git_blob_sha
from audit_v82_scene_mesh_references import meta_guid
from audit_v82_xrtp_road_profiles import ROOT, ASSETS, audit as profile_audit

MAIN = re.compile(r"(?m)^    - _MainTex:\r?\n"
                  r"        m_Texture: \{fileID: (-?\d+), guid: ([0-9a-fA-F]{32}), type: 3\}")
LOD = re.compile(r"(?m)^    - _LODTex:\r?\n"
                 r"        m_Texture: \{fileID: (-?\d+), guid: ([0-9a-fA-F]{32}), type: 3\}")
MATERIAL_HEADER = re.compile(r"(?m)^--- !u!21 &(-?\d+)$")


def indexed_guids(folder: Path, suffix: str) -> dict[str, Path]:
    """Index only the authenticated stage's immediate asset folder."""
    result = {}
    for meta in sorted(folder.iterdir()):
        if not meta.is_file() or not meta.name.lower().endswith(suffix.lower()):
            continue
        guid = meta_guid(meta.read_text(encoding="utf-8-sig"))
        if guid in result:
            raise ValueError("Duplicated stage material or texture GUID: " + guid)
        result[guid] = meta
    return result


def bitmap_dimensions(raw: bytes) -> tuple[int, int] | None:
    if len(raw) < 54 or raw[:2] != b"BM":
        return None
    header_size = struct.unpack_from("<I", raw, 14)[0]
    width, height = struct.unpack_from("<ii", raw, 18)
    if header_size < 40 or not 0 < width <= 16384 or not 0 < abs(height) <= 16384:
        return None
    return width, abs(height)


def texture_by_ref(path: Path, reference: re.Match | None,
                   bitmap_guids: dict[str, Path], root: Path) -> dict:
    if reference is None:
        return {"status": "material_bitmap_slot_missing"}
    file_id, guid = reference.groups()
    if file_id != "2800000":
        return {"status": "material_bitmap_fileid_not_original_texture"}
    meta = bitmap_guids.get(guid.lower())
    if meta is None:
        return {"status": "material_bitmap_guid_not_in_source_stage"}
    bitmap = meta.with_suffix("")
    if not bitmap.is_file():
        return {"status": "material_bitmap_file_missing"}
    data = bitmap.read_bytes()
    dims = bitmap_dimensions(data)
    if dims is None:
        return {"status": "material_bitmap_header_invalid"}
    return {
        "status": "original_stage_bmp_source_verified",
        "textureGuid": guid.lower(),
        "textureAssetPath": bitmap.relative_to(root).as_posix(),
        "textureMetaPath": meta.relative_to(root).as_posix(),
        "textureGitBlobSha": git_blob_sha(bitmap),
        "textureMetaGitBlobSha": git_blob_sha(meta),
        "sourceBitmapWidth": dims[0],
        "sourceBitmapHeight": dims[1],
    }


def resolve_material(ref: dict, materials: dict[str, Path],
                     bitmaps: dict[str, Path], root: Path) -> dict:
    if ref["type"] != 2 or ref["fileId"] != "2100000" or not ref["guid"]:
        return {"status": "original_road_material_reference_invalid"}
    meta = materials.get(ref["guid"])
    if meta is None:
        return {"status": "original_road_material_guid_not_in_source_stage"}
    source = meta.with_suffix("")
    if not source.is_file():
        return {"status": "original_road_material_file_missing"}
    yaml = source.read_text(encoding="utf-8-sig")
    if not yaml.startswith("%YAML 1.1\n") or MATERIAL_HEADER.findall(yaml) != ["2100000"]:
        return {"status": "original_road_material_yaml_invalid"}
    main = texture_by_ref(source, MAIN.search(yaml), bitmaps, root)
    lod = texture_by_ref(source, LOD.search(yaml), bitmaps, root)
    status = ("original_road_material_main_texture_verified"
              if main["status"] == "original_stage_bmp_source_verified"
              else "original_road_material_main_texture_unresolved")
    return {
        "status": status,
        "materialGuid": ref["guid"],
        "materialAssetPath": source.relative_to(root).as_posix(),
        "materialMetaPath": meta.relative_to(root).as_posix(),
        "materialGitBlobSha": git_blob_sha(source),
        "materialMetaGitBlobSha": git_blob_sha(meta),
        "originalMainTexture": main,
        "originalLodTexture": lod,
        "originalUVMappingCertified": False,
        "originalRendererShaderCertified": False,
    }


def audit(root: Path = ROOT, profiles: dict | None = None) -> dict:
    original = profile_audit(root) if profiles is None else profiles
    stages = {}
    for stage_id, (source, expected_scene_hash) in sorted(STAGES.items()):
        stage_path = root / ASSETS / "LEVELS" / Path(source).parent.name
        if not stage_path.is_dir():
            raise ValueError("Original stage road material folder is missing")
        mats = indexed_guids(stage_path, ".mat.meta")
        images = indexed_guids(stage_path, ".bmp.meta")
        profiles_out = []
        for index, profile in enumerate(original["stages"][stage_id]["sourceProfiles"]):
            if profile["status"] != "original_source_xrtp_profile_verified":
                profiles_out.append({
                    "sourceXrtpIndex": index, "status": profile["status"],
                    "sourceMaterials": [],
                })
                continue
            entries = [resolve_material(ref, mats, images, root)
                       for ref in profile["sourceTextureRefs"]]
            profiles_out.append({
                "sourceXrtpIndex": index,
                "sourceXrtpPrefab": profile["originalPrefabPath"],
                "status": ("original_road_source_materials_verified"
                           if entries and all(e["status"] ==
                               "original_road_material_main_texture_verified"
                               for e in entries) else
                           "original_road_source_materials_partially_unresolved"),
                "sourceMaterials": entries,
            })
        stages[stage_id] = {
            "sceneGitBlobSha": expected_scene_hash,
            "sourceXrtpListLength": len(profiles_out),
            "originalMaterialStatuses": dict(sorted(collections.Counter(
                p["status"] for p in profiles_out).items())),
            "sourceProfiles": profiles_out,
        }
    return {
        "schemaVersion": 1,
        "status": "original_stage_xrtp_material_to_bitmap_guid_chain_only",
        "sceneCount": len(stages),
        "originalRoadUVsCertified": False,
        "originalRoadSurfaceMeshCertified": False,
        "originalRoadRendererCertified": False,
        "originalRoadCollisionCertified": False,
        "stages": stages,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    data = audit()
    if args.json:
        print(json.dumps(data, indent=2, ensure_ascii=False, allow_nan=False))
    else:
        for name, stage in data["stages"].items():
            print(name, "source road materials:", stage["originalMaterialStatuses"])
        print("Archival road-material textures ONLY; no final UV, road mesh or physics")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
