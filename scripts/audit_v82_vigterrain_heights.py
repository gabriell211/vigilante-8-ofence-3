#!/usr/bin/env python3
"""Audit real V82 RSEG terrain heights using the archived VigTerrain grid.

No approximation from Geo.asset triangles or physics raycasts. The fixed-point
height is a direct translation of archived VigTerrain.FUN_1B750, sampled using
the original Unity scene's serialized chunks and vertices arrays. Missing
terrain source or unsafe samples remain unresolved, never fabricated.
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
from audit_v82_road_segments import audit as road_audit

ROOT = Path(__file__).resolve().parents[1]
TERRAIN_SCRIPT = "LegacySources/Vigilante2Unity/Assets/Scripts/VigTerrain.cs"
TERRAIN_META = TERRAIN_SCRIPT + ".meta"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$")
GUID = re.compile(r"\bguid: ([0-9a-fA-F]{32})\b")
HEX = re.compile(r"^[0-9a-fA-F]+$")
MAX_COORD = 0x7000000
GRID_CHUNKS = 1024
VERTICES_PER_ZONE = 4096


def original_function_verified(source: str) -> None:
    """Reject source code changes rather than silently changing the height model."""
    signature = "public int FUN_1B750(uint x, uint z)"
    begin = source.find(signature)
    end = source.find("public Vector3Int FUN_1BB50(", begin)
    if begin < 0 or end < begin:
        raise ValueError("Archived original VigTerrain.FUN_1B750 missing")
    function = source[begin:end]
    if function.count("return vertexH >> 5;") != 2 or not all(
        part in function for part in (
            "if (iVar1 + uVar3 < 0x10000)",
            "int chunkID = chunks[(uVar7 + x) / 4];",
            "int chunk1ID = chunks[(uVar + uVar10) / 4];",
            "vertices[chunkID * 4096 + vertexID / 2] & 0x7ff",
            "if (vertexH < 0)",
            "return 0;",
        )
    ):
        raise ValueError("Original terrain interpolation source changed")


def little_endian_ushorts(value: str | None, max_entries: int) -> tuple[int, ...] | None:
    if value is None or len(value) % 4 != 0 or len(value) > max_entries * 4 or \
            not HEX.fullmatch(value):
        return None
    raw = bytes.fromhex(value)
    if len(raw) == 0:
        return None
    return struct.unpack("<" + str(len(raw) // 2) + "H", raw)


def read_original_terrain(scene: str, script_guid: str) -> dict | None:
    """Parse ONLY original VigTerrain MonoBehaviour, not VigMesh.vertices count."""
    if not scene.startswith("%YAML 1.1\n"):
        raise ValueError("Not archived source scene")
    entries: list[dict] = []
    kind = identity = ""
    item: dict = {}

    def finish() -> None:
        if kind == "114" and item.get("scriptGuid") == script_guid:
            entries.append({"fileId": identity, **item})

    for line in scene.splitlines():
        if (match := HEADER.match(line)):
            finish()
            kind, identity = match.groups()
            item = {}
            continue
        if kind != "114":
            continue
        if line.startswith("  m_Script: "):
            match = GUID.search(line)
            item["scriptGuid"] = match.group(1).lower() if match else None
        elif line.startswith("  vertices: "):
            item["verticesRaw"] = line[len("  vertices: "):]
        elif line.startswith("  chunks: "):
            item["chunksRaw"] = line[len("  chunks: "):]
        elif line.startswith("  zoneCount: "):
            try:
                item["zoneCount"] = int(line.partition(":")[2].strip())
            except ValueError:
                item["zoneCount"] = None
    finish()
    if not entries:
        return None
    if len(entries) != 1:
        raise ValueError("Ambiguous original VigTerrain components in scene")
    row = entries[0]
    zones = row.get("zoneCount")
    if not isinstance(zones, int) or not 1 <= zones <= GRID_CHUNKS:
        raise ValueError("Original VigTerrain zone count invalid")
    chunks = little_endian_ushorts(row.get("chunksRaw"), GRID_CHUNKS)
    vertices = little_endian_ushorts(row.get("verticesRaw"), zones * VERTICES_PER_ZONE)
    if chunks is None or len(chunks) != GRID_CHUNKS or vertices is None or \
            len(vertices) != zones * VERTICES_PER_ZONE or any(c >= zones for c in chunks):
        raise ValueError("Original VigTerrain grid is incomplete or malformed")
    return {
        "fileId": row["fileId"],
        "zoneCount": zones,
        "chunks": chunks,
        "vertices": vertices,
    }


def terrain_height(terrain: dict, x: int, z: int) -> int | None:
    """Bit-exact linear triangle interpolation of VigTerrain.FUN_1B750.

    Source accepts uint; the original driver calls with fixed-point x/z.
    The source returns 0 for out-of-domain coordinates. Overflow safety
    rejects damaged/unsupported grids, rather than manufacturing heights.
    """
    if type(x) is not int or type(z) is not int or \
            not -(2 ** 31) <= x <= 0xFFFFFFFF or \
            not -(2 ** 31) <= z <= 0xFFFFFFFF:
        return None
    if x <= 0 or z <= 0 or x >= MAX_COORD or z >= MAX_COORD:
        return 0
    chunks = terrain["chunks"]
    vertices = terrain["vertices"]
    def height(xcell: int, zcell: int) -> int | None:
        # Matches ((x >> 22) << 5) + (z >> 22) chunk addressing.
        chunk_at = (xcell >> 6) * 32 + (zcell >> 6)
        if chunk_at < 0 or chunk_at >= len(chunks):
            return None
        vertex_at = chunks[chunk_at] * 4096 + ((xcell & 63) << 6) + (zcell & 63)
        if not 0 <= vertex_at < len(vertices):
            return None
        return vertices[vertex_at] & 0x7FF

    cx, cz = x >> 16, z >> 16
    fx, fz = x & 0xFFFF, z & 0xFFFF
    if fx + fz < 65536:
        h00, h10, h01 = height(cx, cz), height(cx + 1, cz), height(cx, cz + 1)
        if None in (h00, h10, h01):
            return None
        raw = (h00 << 16) + fx * (h10 - h00) + fz * (h01 - h00)
    else:
        h11, h01, h10 = height(cx + 1, cz + 1), height(cx, cz + 1), height(cx + 1, cz)
        if None in (h11, h01, h10):
            return None
        raw = (h11 << 16) + (65536 - fx) * (h01 - h11) + \
              (65536 - fz) * (h10 - h11)
    # Original code has signed int32 intermediate operations and correction.
    if not -(2 ** 31) <= raw < 2 ** 31:
        return None
    if raw < 0:
        raw += 31
    return raw >> 5


def add_terrain_evidence(segments: list[dict], terrain: dict | None,
                         divisor: int) -> tuple[list[dict], dict]:
    counts: collections.Counter[str] = collections.Counter()
    records = []
    for segment in segments:
        record = {
            "rsegFileId": segment["componentFileId"],
            "originalRsegReferenceStatus": segment["referenceStatus"],
            "originalSourceHeights": None,
            "originalSourceXYZ": None,
            "debugScaleReviewXYZ": None,
        }
        samples = segment["sourceCurveSamples"]
        if segment["referenceStatus"] != "linked_original_rseg_junc_fixed_point_source" or not samples:
            status = "unresolved_original_rseg_reference"
        elif not segment["sourceHeightNeedsVigTerrain"]:
            status = "original_height_already_known_from_rseg_flags"
            ys = [p["sourcePosition"]["y"] for p in samples]
        elif terrain is None:
            status = "original_vigterrain_source_missing"
        else:
            ys = [terrain_height(terrain, p["sourcePosition"]["x"],
                                 p["sourcePosition"]["z"]) for p in samples]
            status = "original_vigterrain_source_heights_sampled" \
                if all(y is not None for y in ys) else "terrain_grid_sample_unresolved"
        if status in ("original_height_already_known_from_rseg_flags",
                      "original_vigterrain_source_heights_sampled"):
            record["originalSourceHeights"] = ys
            record["originalSourceXYZ"] = [
                {"x": s["sourcePosition"]["x"], "y": y, "z": s["sourcePosition"]["z"]}
                for s, y in zip(samples, ys)
            ]
            record["debugScaleReviewXYZ"] = [
                {"x": s["sourcePosition"]["x"] / divisor,
                 "y": -y / divisor,
                 "z": s["sourcePosition"]["z"] / divisor}
                for s, y in zip(samples, ys)
            ]
        record["heightStatus"] = status
        counts[status] += 1
        records.append(record)
    return records, dict(sorted(counts.items()))


def audit(root: Path = ROOT) -> dict:
    original_function_verified((root / TERRAIN_SCRIPT).read_text("utf-8-sig"))
    terrain_guid = meta_guid((root / TERRAIN_META).read_text("utf-8"))
    road = road_audit(root)
    stages = {}
    for stage_id, (relative, expected_hash) in sorted(STAGES.items()):
        path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / relative
        if git_blob_sha(path) != expected_hash:
            raise ValueError("Original stage scene has changed: " + stage_id)
        terrain = read_original_terrain(path.read_text("utf-8-sig"), terrain_guid)
        old = road["stages"][stage_id]
        records, counts = add_terrain_evidence(
            old["segments"], terrain, road["debugSerializedScale"])
        stages[stage_id] = {
            "sourceScene": old["sourceScene"],
            "sceneGitBlobSha": expected_hash,
            "terrainComponentFileId": terrain["fileId"] if terrain else None,
            "originalTerrainZones": terrain["zoneCount"] if terrain else None,
            "terrainVertexCount": len(terrain["vertices"]) if terrain else None,
            "sourceSegments": old["sourceSegmentCount"],
            "heightStatuses": counts,
            "records": records,
        }
    return {
        "schemaVersion": 1,
        "status": "archived_vigterrain_fun_1b750_fixed_point_height_evidence_not_a_playable_arena",
        "originalTerrainScriptGuid": terrain_guid,
        "sourceSceneCount": len(stages),
        "debugScale": road["debugSerializedScale"],
        "sourceHeightFromOriginalVigTerrain": True,
        "worldPlacementCertified": False,
        "officialRoadGeometryCertified": False,
        "originalTerrainCollisionCertified": False,
        "originalSpawnsCertified": False,
        "stages": stages,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    options = parser.parse_args()
    report = audit()
    if options.json:
        print(json.dumps(report, ensure_ascii=False, allow_nan=False, indent=2))
    else:
        for stage, row in report["stages"].items():
            print(stage, row["terrainVertexCount"], "terrain vertices;",
                  row["heightStatuses"])
        print("Source fixed-point terrain heights only; NO finished roads or Unity runtime.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
