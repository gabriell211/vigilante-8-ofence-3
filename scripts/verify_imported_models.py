#!/usr/bin/env python3
"""Verify imported FBX models byte-for-byte against their pinned upstream Git blobs.

These are real *source meshes*, not game-ready prefabs. Exit nonzero on
missing/corrupt input; a valid mesh does NOT assert a complete Unity build.
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Assets/GameContent/imported-sources.json"


def git_blob_sha(data: bytes) -> str:
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\0" + data).hexdigest()


def validate(root: pathlib.Path = ROOT) -> dict:
    manifest = json.loads((root / "Assets/GameContent/imported-sources.json").read_text("utf-8"))
    if manifest.get("schemaVersion") != 1:
        raise ValueError("Unknown import manifest schema")
    catalog = json.loads((root / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
    expected = {entry["assetId"] for entry in catalog["vehicles"]}
    entries = manifest.get("models", [])
    ids = [entry["assetId"] for entry in entries]
    if len(ids) != len(set(ids)) or not set(ids) <= expected:
        raise ValueError("Unknown or repeated model asset ID")
    results = []
    for entry in entries:
        path = pathlib.Path(entry["local"])
        if path.is_absolute() or ".." in path.parts or path.suffix.lower() != ".fbx":
            raise ValueError("Unsafe or invalid imported path")
        absolute = root / path
        data = absolute.read_bytes()
        if not data.startswith(b"; FBX 7.4.0 project file"):
            raise ValueError("Missing real ASCII FBX header: " + str(path))
        if git_blob_sha(data) != entry["gitBlobSha"]:
            raise ValueError("Imported model differs from pinned upstream blob: " + str(path))
        geometry_count = len(re.findall(rb"Geometry: \d+, \"Geometry::", data))
        texture_count = len(re.findall(rb"Texture: \d+, \"Texture::", data))
        if geometry_count < 1 or geometry_count != entry["geometryMeshes"]:
            raise ValueError("No geometry or unexpected FBX geometry count: " + str(path))
        if texture_count != entry["referencedTextures"]:
            raise ValueError("Unexpected FBX texture-reference count: " + str(path))
        results.append({
            "vehicleId": entry["assetId"],
            "bytes": len(data),
            "geometryMeshes": geometry_count,
            "linkedTexturesInFbx": texture_count,
            "readyAsGamePrefab": False,
        })
    return {"importedSourceModels": len(results), "totalCatalogVehicles": len(expected),
            "verified": results}


if __name__ == "__main__":
    report = validate()
    print(f"PASS: {report['importedSourceModels']}/{report['totalCatalogVehicles']} "
          "source vehicle FBX meshes verified byte-for-byte")
    for model in report["verified"]:
        print(f"  {model['vehicleId']}: {model['geometryMeshes']} geometries, "
              f"{model['linkedTexturesInFbx']} texture references, "
              f"{model['bytes']} bytes; gameplay prefab NOT yet built")
