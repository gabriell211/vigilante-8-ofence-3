#!/usr/bin/env python3
"""Prove provenance of seven original V82 Unity Geo.asset Mesh files."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Assets/GameContent/LegacyTerrainV82/manifest.json"


def git_blob(data: bytes) -> str:
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\0" + data).hexdigest()


def audit() -> dict:
    record = json.loads(MANIFEST.read_text("utf-8"))
    roster = json.loads((ROOT / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
    all_stages = {x["id"] for x in roster["arenas"] if x["game"] == "v82"}
    if not (record["schemaVersion"] == 1 and record["availableTerrainMeshes"] == 7
            and record["expectedV82Arenas"] == 8 and len(record["terrains"]) == 7
            and record["missingTerrainSource"] == ["v82_meteor_crater"]
            and record["status"] == "original_native_unity_terrain_meshes_inspection_only"
            and len(all_stages) == 8):
        raise ValueError("Original terrain inventory is inconsistent")
    found, guids, hashes = set(), set(), set()
    for item in record["terrains"]:
        sid = item["stageId"]
        source = Path(item["sourcePath"])
        if (sid not in all_stages or sid == "v82_meteor_crater" or sid in found
            or source.is_absolute() or ".." in source.parts
            or not source.as_posix().startswith("LegacySources/Vigilante2Unity/Assets/Levels/")
            or not source.name.endswith("Geo.asset")
            or item["importPath"] !=
                f"Assets/GameContent/ImportedOriginalTerrainV82/{sid}.asset"
            or any(item[k] for k in ("isCompleteArena", "collisionApproved", "spawnPointsApproved"))):
            raise ValueError("Invalid original terrain entry: " + sid)
        raw = (ROOT / source).read_bytes()
        if len(raw) != item["sourceBytes"] or len(raw) < 100000:
            raise ValueError("Truncated native Mesh: " + sid)
        if not raw.startswith(b"%YAML 1.1") or not all(
            b in raw for b in
            (b"Mesh:", b"m_IndexBuffer:", b"m_VertexData:", b"m_SubMeshes:")
        ):
            raise ValueError("Original asset is not a serialized Unity Mesh: " + sid)
        digest = git_blob(raw)
        if digest != item["sourceGitBlobSha"] or digest in hashes:
            raise ValueError("Original terrain Git blob hash mismatch: " + sid)
        meta = (ROOT / (str(source) + ".meta")).read_text("utf-8")
        guid = re.search(r"^guid: ([a-f0-9]{32})$", meta, re.MULTILINE)
        if (guid is None or guid.group(1) != item["sourceGuid"] or
            guid.group(1) in guids or "NativeFormatImporter:" not in meta):
            raise ValueError("Original native Mesh GUID mismatch: " + sid)
        found.add(sid)
        hashes.add(digest)
        guids.add(guid.group(1))
    if all_stages - found != {"v82_meteor_crater"}:
        raise ValueError("Unknown/missing original stage mesh")
    return record


if __name__ == "__main__":
    report = audit()
    print("PASS: 7 genuine native Unity V82 terrain mesh source files, hashes and GUIDs.")
    print("Route66 terrain missing; neither collisions nor full arena gameplay approved.")
