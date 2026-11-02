#!/usr/bin/env python3
"""Source-only FUN_719C JUNC transition selection: genuine RSEG order/XRTP width.

Reports which endpoint requires FUN_6604, and why that decompiled routine
cannot yet be used as production road geometry (OOB original local_188).
Does not manufacture any junction mesh, UV, collision or road connectivity.
"""
from __future__ import annotations

import argparse
import collections
import json
import re
from pathlib import Path

from audit_legacy_v82_scene_layouts import STAGES, git_blob_sha
from audit_v82_road_segments import (
    ROOT, read_road_segments, RSEG_META
)
from audit_v82_xrtp_road_profiles import audit as road_profile_audit
from audit_v82_scene_mesh_references import meta_guid
from audit_v82_junction_fixed_positions import JUNC_META

LEVEL_CODE = Path("LegacySources/Vigilante2Unity/Assets/Scripts/LevelManager.cs")
MONO = re.compile(r"(?ms)^--- !u!114 &(-?\d+)\r?\n(.*?)(?=^--- !u!|\Z)")
GUID = re.compile(r"(?m)^  m_Script: \{fileID: 11500000, guid: ([a-fA-F0-9]{32}), type: 3\}")
SCALAR = re.compile(r"(?m)^  (DAT_08|DAT_0A|DAT_0C|DAT_10|DAT_11|DAT_18): ([^\r\n]+)$")
FILEID = re.compile(r"\bfileID: (-?\d+)")
REFS = re.compile(r"^  - \{fileID: (-?\d+)(?:, [^\r\n]+)?\}$")


def source_short(n):
    if not isinstance(n, int) or isinstance(n, bool) or not -32768 <= n <= 65535:
        raise ValueError("Source signed short DAT_08 invalid")
    return n if n <= 32767 else n - 65536


def selected_trim_radius(junc_flags: int, has_mesh: bool, current_flags: int,
                         end: int, order: int, current_id: str,
                         neighbors: list[dict], declared: int) -> tuple[str, int, int]:
    if not current_id or end not in (0, 1) or not 0 <= junc_flags <= 255 or \
            declared != len(neighbors) or declared > 255 or \
            sum(n["id"] == current_id for n in neighbors) != 1:
        return "unverified_source_junc_links", 0, 0
    try:
        source_short(order)
        if any(n["width"] is None or n["width"] <= 0 or
               source_short(n["order"]) is None for n in neighbors):
            raise ValueError("Unverified earlier-road width")
    except (KeyError, TypeError, ValueError):
        return "unverified_source_junc_links", 0, 0
    if has_mesh:
        return "xobf_configured_junction_not_ported", 0, 0
    if junc_flags & 1 or current_flags & (2 << end):
        return "original_no_fun6604_trim", 0, 0
    earlier = [n for n in neighbors if source_short(n["order"]) < source_short(order)]
    radius = max((n["width"] // 2 for n in earlier), default=0)
    if radius > 0:
        return "original_fun6604_transition_required", radius, len(earlier)
    return "original_no_fun6604_trim", 0, len(earlier)


def parse_junctions(scene: str, script_guid: str) -> dict:
    result = {}
    for item in MONO.finditer(scene):
        body = item.group(2)
        m = GUID.search(body)
        if not m or m.group(1).lower() != script_guid:
            continue
        scalars = dict(SCALAR.findall(body))
        if len(scalars) != len(SCALAR.findall(body)):
            raise ValueError("Duplicated original JUNC scalar")
        def maybeint(key):
            try:
                return int(scalars[key])
            except (ValueError, KeyError):
                return None
        first = body.find("  DAT_1C:\n")
        attached = None
        if first >= 0:
            attached = []
            for line in body[first + len("  DAT_1C:\n"):].splitlines():
                if not line.startswith("  - "):
                    break
                match = REFS.fullmatch(line)
                if match is None:
                    attached = None
                    break
                attached.append(match.group(1))
        elif "  DAT_1C: []" in body:
            attached = []
        mesh = FILEID.search(scalars.get("DAT_18", ""))
        row = {"flags": maybeint("DAT_10"), "count": maybeint("DAT_11"),
               "mesh": mesh.group(1) if mesh else None, "attached": attached}
        if item.group(1) in result:
            raise ValueError("Duplicate source JUNC component fileID")
        result[item.group(1)] = row
    return result


def check_original_method(code: str):
    for signature in (
        "void FUN_719C(RSEG_DB param1)",
        "if (piVar7.DAT_18 == null)",
        "if ((piVar7.DAT_10 & 1) == 0)",
        "(param1.DAT_0C & 2 << (i & 31)) == 0",
        "(short)piVar7.DAT_1C[iVar5].DAT_08 < (short)param1.DAT_08",
        "xrtpList[piVar7.DAT_1C[iVar5].DAT_0A].DAT_24 / 2",
        "FUN_6604(local_b0, i, iVar12);",
        "uint[] local_188 = new uint[27];",
        "local_188[30] = local_188[5];",
    ):
        if signature not in code:
            raise ValueError("Archived FUN_719C/FUN_6604 source changed: " + signature)


def audit(root: Path = ROOT) -> dict:
    check_original_method((root / LEVEL_CODE).read_text("utf-8-sig"))
    upstream = road_profile_audit(root)
    junc_guid = meta_guid((root / JUNC_META).read_text("utf-8"))
    rseg_guid = meta_guid((root / RSEG_META).read_text("utf-8"))
    stages = {}
    for stage_id, (source, expected_hash) in sorted(STAGES.items()):
        scene_path = root / "LegacySources/Vigilante2Unity/Assets/Levels" / source
        if git_blob_sha(scene_path) != expected_hash:
            raise ValueError("Original scene hash differs: " + stage_id)
        text = scene_path.read_text("utf-8-sig")
        junctions = parse_junctions(text, junc_guid)
        roads = {
            r["componentFileId"]: r
            for r in read_road_segments(text, rseg_guid)
        }
        profiles = upstream["stages"][stage_id]["sourceProfiles"]
        decisions = []
        statuses = collections.Counter()
        for road_id, road in roads.items():
            links = road["junctionComponentFileIds"]
            for end in (0, 1):
                junction_id = links[end] if len(links) == 2 else None
                j = junctions.get(junction_id)
                status, radius, earlier = "unverified_source_junc_links", 0, 0
                if j and j["flags"] is not None and j["count"] is not None and \
                        j["mesh"] is not None and j["attached"] is not None and \
                        road["sourceRoadOrder"] is not None and road["sourceFlags"] is not None:
                    attached = []
                    for connected_id in j["attached"]:
                        neighbor = roads.get(connected_id)
                        if neighbor is None:
                            attached = None
                            break
                        index = neighbor["sourceProfileIndex"]
                        profile = (profiles[index] if isinstance(index, int) and
                                   0 <= index < len(profiles) else None)
                        attached.append({
                            "id": connected_id,
                            "order": neighbor["sourceRoadOrder"],
                            "width": profile.get("sourceWidthParameterFixed")
                                if profile and profile.get("status") ==
                                "original_source_xrtp_profile_verified" else None,
                        })
                    if attached is not None:
                        status, radius, earlier = selected_trim_radius(
                            j["flags"], j["mesh"] != "0", road["sourceFlags"],
                            end, road["sourceRoadOrder"], road_id, attached, j["count"])
                statuses[status] += 1
                decisions.append({
                    "rsegFileId": road_id,
                    "junctionFileId": junction_id,
                    "endpointIndex": end,
                    "status": status,
                    "originalEarlierRoads": earlier,
                    "originalTrimRadiusFixed": radius if status ==
                        "original_fun6604_transition_required" else None,
                })
        stages[stage_id] = {
            "sceneGitBlobSha": expected_hash,
            "sourceRoadCount": len(roads),
            "sourceJunctionCount": len(junctions),
            "sourceEndpointCount": len(decisions),
            "statuses": dict(sorted(statuses.items())),
            "endpoints": decisions,
        }
    return {
        "schemaVersion": 1,
        "status": "authentic_fun719c_trim_selection_only_not_fun6604_geometry",
        "sceneCount": len(stages),
        "sourceFun6604ArrayBoundsIssue": "local_188 declared length 27; used index 30",
        "originalJunctionCurveCertified": False,
        "originalRoadUVsCertified": False,
        "collisionCertified": False,
        "stages": stages,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    result = audit()
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
    else:
        for stage, data in result["stages"].items():
            print(stage, data["sourceEndpointCount"], data["statuses"])
        print("Source FUN_719C selection only: FUN_6604 curve mutation NOT certified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
