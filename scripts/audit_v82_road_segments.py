#!/usr/bin/env python3
"""Extract source-linked V82 RSEG_DB road topology and fixed-point curve evidence.

RSEG_DB.FUN_285E4 supplies exact X/Z polynomials, but its Y position may
require the original VigTerrain.FUN_1B750 sampler. We never invent that Y.
These are evidence samples, not driving roads, meshes, collision or spawns.
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
from audit_v82_junction_fixed_positions import (
    DEBUG_SCENE, GAMEMANAGER_META, JUNC_META,
    debug_scene_translate_factor, read_source_components,
)

ROOT = Path(__file__).resolve().parents[1]
RSEG_SOURCE = "LegacySources/Vigilante2Unity/Assets/Scripts/DB/RSEG_DB.cs"
RSEG_META = RSEG_SOURCE + ".meta"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?$")
GUID = re.compile(r"\bguid: ([0-9a-fA-F]{32})\b")
FILE_ID = re.compile(r"\bfileID: (-?\d+)\b")
INT32_MIN, INT32_MAX = -(2 ** 31), 2 ** 31 - 1
SAMPLE_TIMES = (0, 1024, 2048, 3072, 4096)
COEFFICIENTS = ("DAT_20", "DAT_24", "DAT_28", "DAT_2C", "DAT_30", "DAT_34")


def signed32(value: int) -> int | None:
    return value if isinstance(value, int) and not isinstance(value, bool) and \
        INT32_MIN <= value <= INT32_MAX else None


def encoded_pair(value: str | None) -> tuple[int, int] | None:
    if value is None or not re.fullmatch(r"[0-9a-fA-F]{16}", value):
        return None
    return struct.unpack("<ii", bytes.fromhex(value))


def read_road_segments(scene: str, script_guid: str) -> list[dict]:
    if not scene.startswith("%YAML 1.1\n"):
        raise ValueError("Not archived Unity scene source")
    found = []
    kind = identity = ""
    row: dict = {}
    reading_endpoints = False

    def finish() -> None:
        if kind == "114" and row.get("scriptGuid") == script_guid:
            found.append({
                "componentFileId": identity,
                "gameObjectFileId": row.get("gameObjectFileId"),
                "junctionComponentFileIds": row.get("endpoints", []),
                "sourceFlags": row.get("DAT_0C"),
                "sourceProfileIndex": row.get("DAT_0A"),
                "sourceRoadOrder": row.get("DAT_08"),
                "sourceTangentX": row.get("DAT_10"),
                "sourceTangentZ": row.get("DAT_14"),
                "sourcePolynomial": {key: row.get(key) for key in COEFFICIENTS},
                "enabled": row.get("enabled"),
            })

    for line in scene.splitlines():
        if (m := HEADER.match(line)):
            finish()
            kind, identity = m.groups()
            row = {}
            reading_endpoints = False
            continue
        if kind != "114":
            continue
        if line.startswith("  DAT_00:"):
            reading_endpoints = True
            row["endpoints"] = []
            continue
        if reading_endpoints:
            if line.startswith("  - "):
                match = FILE_ID.search(line)
                row["endpoints"].append(match.group(1) if match else None)
                continue
            reading_endpoints = False
        if line.startswith("  m_GameObject: "):
            m = FILE_ID.search(line)
            row["gameObjectFileId"] = m.group(1) if m else None
        elif line.startswith("  m_Script: "):
            m = GUID.search(line)
            row["scriptGuid"] = m.group(1).lower() if m else None
        elif line.startswith("  m_Enabled: "):
            value = line.partition(":")[2].strip()
            row["enabled"] = value == "1" if value in ("0", "1") else None
        elif line.startswith("  DAT_10: ") or line.startswith("  DAT_14: "):
            key, value = line.strip().split(": ", 1)
            row[key] = encoded_pair(value)
        else:
            for key in (*COEFFICIENTS, "DAT_0C", "DAT_0A", "DAT_08"):
                if line.startswith("  " + key + ": "):
                    try:
                        row[key] = signed32(int(line.partition(":")[2].strip()))
                    except ValueError:
                        row[key] = None
                    break
    finish()
    return sorted(found, key=lambda row: int(row["componentFileId"]))


def checked_add(a: int, b: int) -> int:
    result = a + b
    if signed32(result) is None:
        raise OverflowError("Source polynomial step exceeds signed 32-bit precision")
    return result


def checked_multiply(a: int, b: int) -> int:
    result = a * b
    if signed32(result) is None:
        raise OverflowError("Source polynomial multiply may overflow original int32")
    return result


def sample_axis(origin: int, t: int, a: int, b: int, c: int) -> int:
    """Literal RSEG_DB.FUN_285E4 X/Z polynomial (without terrain sampling)."""
    term1 = checked_multiply(a, t) >> 12
    term2 = checked_add(b, term1)
    term3 = checked_multiply(term2, t) >> 12
    term4 = checked_add(term3, c)
    term5 = checked_multiply(term4, t) >> 8
    return checked_add(origin, term5)


def sample_road(start: dict, end: dict, flags: int,
                coeffs: dict, divisor: int) -> list[dict] | None:
    if start is None or end is None or flags is None or divisor <= 0 or \
            any(coeffs.get(key) is None for key in COEFFICIENTS):
        return None
    result = []
    for t in SAMPLE_TIMES:
        try:
            x = sample_axis(start["x"], t, coeffs["DAT_20"],
                            coeffs["DAT_28"], coeffs["DAT_30"])
            z = sample_axis(start["z"], t, coeffs["DAT_24"],
                            coeffs["DAT_2C"], coeffs["DAT_34"])
            if flags & 8:
                y = start["y"]
            elif flags & 1:
                y = checked_add(start["y"],
                                checked_multiply(t, end["y"] - start["y"]) >> 12)
            else:
                y = None  # MUST sample original VigTerrain; no guessed height.
        except OverflowError:
            return None
        fixed = {"x": x, "y": y, "z": z}
        visual = {"x": x / divisor, "y": -y / divisor if y is not None else None,
                  "z": z / divisor}
        result.append({"tFixed12": t, "sourcePosition": fixed,
                       "debugScalePosition": visual})
    return result


def link_segments(scene: str, rseg_guid: str, junc_guid: str,
                  divisor: int) -> list[dict]:
    junctions = read_source_components(scene, junc_guid)
    by_id = {}
    for j in junctions:
        identity = j["componentFileId"]
        if identity in by_id:
            raise ValueError("Original junction component fileID duplicated")
        by_id[identity] = j
    result = []
    for seg in read_road_segments(scene, rseg_guid):
        endpoints = seg["junctionComponentFileIds"]
        matched = [by_id.get(value) for value in endpoints] if len(endpoints) == 2 else []
        raws = [j["rawDat00"] for j in matched] if matched and all(matched) else []
        coords = [dict(zip(("x", "y", "z"), raw)) for raw in raws] \
            if raws and all(raw is not None for raw in raws) else []
        valid_tangents = seg["sourceTangentX"] is not None and \
                         seg["sourceTangentZ"] is not None
        valid_coeffs = all(seg["sourcePolynomial"][key] is not None for key in COEFFICIENTS)
        if len(endpoints) != 2 or any(value in (None, "0") for value in endpoints):
            status = "missing_or_invalid_source_junction_references"
        elif not matched or not all(matched):
            status = "junction_components_missing_from_scene"
        elif len(set(endpoints)) != 2:
            status = "same_junction_used_for_both_ends"
        elif len(coords) != 2:
            status = "invalid_or_missing_source_junction_coordinates"
        elif not valid_tangents or not valid_coeffs or seg["sourceFlags"] is None:
            status = "incomplete_original_segment_coefficients"
        else:
            status = "linked_original_rseg_junc_fixed_point_source"
        samples = sample_road(coords[0], coords[1], seg["sourceFlags"],
                              seg["sourcePolynomial"], divisor) if status.startswith("linked_") else None
        if status.startswith("linked_") and samples is None:
            status = "source_polynomial_arithmetic_not_safely_representable"
        result.append({
            **seg,
            "sourceTangentX": list(seg["sourceTangentX"]) if valid_tangents else None,
            "sourceTangentZ": list(seg["sourceTangentZ"]) if valid_tangents else None,
            "endpointRawDat00": coords if coords else None,
            "sourceCurveSamples": samples,
            "sourceHeightNeedsVigTerrain": bool(seg["sourceFlags"] is not None
                                                   and not (seg["sourceFlags"] & 9)),
            "referenceStatus": status,
        })
    return result


def verify_original_rseg_source(code: str) -> None:
    for text in (
        "public JUNC_DB[] DAT_00;",
        "public int[] DAT_10;",
        "public int[] DAT_14;",
        "public int DAT_20;",
        "public int DAT_24;",
        "public int DAT_28;",
        "public int DAT_2C;",
        "public int DAT_30;",
        "public int DAT_34;",
        "public void FUN_285E4(",
        "iVar3 = DAT_20 * param1 >> 12;",
        "iVar3 = DAT_24 * param1 >> 12;",
        "param2.y = iVar3;",
    ):
        if text not in code:
            raise ValueError("The original RSEG_DB curve derivation changed: " + text)


def audit(root: Path = ROOT) -> dict:
    verify_original_rseg_source((root / RSEG_SOURCE).read_text("utf-8-sig"))
    rseg_guid = meta_guid((root / RSEG_META).read_text("utf-8"))
    junc_guid = meta_guid((root / JUNC_META).read_text("utf-8"))
    manager_guid = meta_guid((root / GAMEMANAGER_META).read_text("utf-8"))
    divisor = debug_scene_translate_factor(
        (root / DEBUG_SCENE).read_text("utf-8-sig"), manager_guid)
    stages = {}
    for stage_id, (relative, expected_hash) in sorted(STAGES.items()):
        source_path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / relative
        if git_blob_sha(source_path) != expected_hash:
            raise ValueError("Original stage source hash changed: " + stage_id)
        segments = link_segments(source_path.read_text("utf-8-sig"),
                                 rseg_guid, junc_guid, divisor)
        stages[stage_id] = {
            "sourceScene": source_path.relative_to(root).as_posix(),
            "originalGitBlobSha": expected_hash,
            "sourceSegmentCount": len(segments),
            "referenceStatuses": dict(sorted(collections.Counter(
                row["referenceStatus"] for row in segments).items())),
            "segments": segments,
        }
    return {
        "schemaVersion": 1,
        "status": "original_rseg_junc_curve_evidence_no_playable_stage",
        "sceneCount": len(stages),
        "sourceRsegGuid": rseg_guid,
        "sourceJuncGuid": junc_guid,
        "debugSerializedScale": divisor,
        "sampleTimesFixed12": list(SAMPLE_TIMES),
        "roadMeshCertified": False,
        "terrainHeightsCertified": False,
        "collisionCertified": False,
        "spawnPointsCertified": False,
        "stages": stages,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    report = audit()
    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False))
    else:
        for name, data in report["stages"].items():
            print(name, "original RSEG segments:", data["sourceSegmentCount"],
                  data["referenceStatuses"])
        print("Original RSEG X/Z source only, Y may require VigTerrain. NOT playable roads.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
