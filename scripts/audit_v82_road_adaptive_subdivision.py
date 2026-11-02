#!/usr/bin/env python3
"""Audit archived FUN_57AC/FUN_630C pre-junction adaptive road subdivisions.

The curve controls are recovered from actual JUNC DAT_00 and RSEG DAT_10/14
before FUN_719C changes junction geometry. This does NOT certify final road
mesh, adaptive FUN_5850 sampling, UVs, physics or source gameplay.
"""
from __future__ import annotations
import argparse
import collections
import json
from pathlib import Path

from audit_v82_road_segments import ROOT, INT32_MIN, INT32_MAX, audit as road_audit

LEVEL_MANAGER = Path("LegacySources/Vigilante2Unity/Assets/Scripts/LevelManager.cs")
CUTOFF = 0x100000
MAX_DEPTH = 16
MAX_LEAVES = 4096


def check_int(value: int) -> int:
    if not INT32_MIN <= value <= INT32_MAX:
        raise OverflowError("Original FUN_630C signed 32-bit overflow")
    return value


def mean(a: int, b: int) -> int:
    # C# integer division truncates toward zero, unlike Python //.
    sum_ = check_int(a + b)
    return sum_ // 2 if sum_ >= 0 else -((-sum_) // 2)


def from_source(row: dict) -> list[tuple[int, int, int]]:
    points = row["endpointRawDat00"]
    tangent_x, tangent_z = row["sourceTangentX"], row["sourceTangentZ"]
    if not points or len(points) != 2 or not tangent_x or not tangent_z or \
            len(tangent_x) != 2 or len(tangent_z) != 2:
        raise ValueError("Missing source JUNC endpoints or RSEG tangents")
    p0, p3 = points
    return [
        (check_int(p0["x"]), check_int(p0["y"]), check_int(p0["z"])),
        (check_int(p0["x"] + tangent_x[0]), 0,
         check_int(p0["z"] + tangent_z[0])),
        (check_int(p3["x"] + tangent_x[1]), 0,
         check_int(p3["z"] + tangent_z[1])),
        (check_int(p3["x"]), check_int(p3["y"]), check_int(p3["z"])),
    ]


def extent(p: list[tuple[int, int, int]]) -> int:
    if len(p) != 4:
        raise ValueError("Expected four original cubic control points")
    return check_int(max(
        max(q[0] for q in p) - min(q[0] for q in p),
        max(q[2] for q in p) - min(q[2] for q in p),
    ))


def half(p: list[tuple[int, int, int]]) -> tuple[list, list]:
    if len(p) != 4:
        raise ValueError("Expected four controls")
    mid_x, mid_z = mean(p[1][0], p[2][0]), mean(p[1][2], p[2][2])
    ax, az = mean(p[0][0], p[1][0]), mean(p[0][2], p[1][2])
    bx, bz = mean(ax, mid_x), mean(az, mid_z)
    ex, ez = mean(p[2][0], p[3][0]), mean(p[2][2], p[3][2])
    dx, dz = mean(ex, mid_x), mean(ez, mid_z)
    shared = (mean(bx, dx), mean(p[0][1], p[3][1]), mean(bz, dz))
    return (
        [p[0], (ax, 0, az), (bx, 0, bz), shared],
        [shared, (dx, 0, dz), (ex, 0, ez), p[3]],
    )


def subdivide(p: list[tuple[int, int, int]]) -> list[dict]:
    leaves = []
    def visit(nodes, depth, first, last):
        if extent(nodes) < CUTOFF:
            if len(leaves) >= MAX_LEAVES:
                raise OverflowError("Too many original adaptive leaves")
            leaves.append({"startT16": first, "endT16": last,
                           "depth": depth, "start": nodes[0], "end": nodes[3]})
            return
        if depth >= MAX_DEPTH or last - first < 2 or len(leaves) >= MAX_LEAVES:
            raise OverflowError("Unsafe adaptive subdivision depth/leaf limit")
        left, right = half(nodes)
        middle = first + (last - first) // 2
        visit(left, depth + 1, first, middle)
        visit(right, depth + 1, middle, last)
    visit(p, 0, 0, 65536)
    return leaves


def verify_archived_source(code: str):
    for statement in (
        "private int FUN_57AC(int[] param1)",
        "private void FUN_630C(int[,] param1, XRTP_DB param2, ushort param3)",
        "if (iVar3 < 0x100000)",
        "(param1[0, 3] + param1[1, 2]) / 2;",
        "(param1[1, 1] + param1[2, 0]) / 2;",
        "(param1[0, 1] + param1[2, 2]) / 2;",
        "jVar4 = FUN_5850(param1, param2, param3);",
    ):
        if statement not in code:
            raise ValueError("Archived adaptive road source changed: " + statement)


def audit(root: Path = ROOT) -> dict:
    verify_archived_source((root / LEVEL_MANAGER).read_text(encoding="utf-8-sig"))
    roads = road_audit(root)
    stages = {}
    total_leaves = 0
    for stage_id, data in roads["stages"].items():
        segments = []
        statuses = collections.Counter()
        for row in data["segments"]:
            output = {"rsegFileId": row["componentFileId"],
                      "rsegReferenceStatus": row["referenceStatus"]}
            if row["referenceStatus"] != "linked_original_rseg_junc_fixed_point_source":
                output["status"] = "unverified_original_junction_or_curve"
            else:
                try:
                    leaves = subdivide(from_source(row))
                    output["status"] = "original_unadjusted_fun630c_leaves"
                    output["sourceAdaptiveLeafCount"] = len(leaves)
                    output["maximumDepth"] = max(x["depth"] for x in leaves)
                    output["sourceLeafParameterRangesFixed16"] = [
                        [x["startT16"], x["endT16"]] for x in leaves
                    ]
                    total_leaves += len(leaves)
                except (OverflowError, ValueError, KeyError, TypeError):
                    output["status"] = "original_unadjusted_subdivision_unsafe"
            statuses[output["status"]] += 1
            segments.append(output)
        stages[stage_id] = {
            "sourceScene": data["sourceScene"],
            "sourceSceneGitBlobSha": data["originalGitBlobSha"],
            "sourceSegmentCount": data["sourceSegmentCount"],
            "subdivisionStatuses": dict(sorted(statuses.items())),
            "segments": segments,
        }
    return {
        "schemaVersion": 1,
        "status": "original_pre_junction_fun630c_adaptive_split_only",
        "sceneCount": 8,
        "originalPreJunctionLeavesResolved": total_leaves,
        "junctionAdjustmentsCertified": False,
        "originalRoadSurfaceCertified": False,
        "originalUVsCertified": False,
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
        for stage, entry in data["stages"].items():
            print(stage, entry["subdivisionStatuses"])
        print(data["originalPreJunctionLeavesResolved"],
              "pre-junction FUN_630C source leaves; NOT final playable road meshes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
