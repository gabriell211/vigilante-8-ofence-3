#!/usr/bin/env python3
"""Audit the eight genuine Unity-2020 2nd Offense scenes without inventing arenas.

Stage scene YAML remains under LegacySources because old VigMesh/LevelManager
components are not compatible with the current Unity 6 runtime. This produces
an evidence manifest, not built/ported scene geometry.
"""
from __future__ import annotations

import hashlib
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = Path("Assets/GameContent/LegacyStageManifests/scene-audit.json")
DOCUMENT_RE = re.compile(rb"(?m)^--- !u!(\d+) &")
GUID_RE = re.compile(rb"guid: ([a-f0-9]{32})")


def inspect_unity_scene(data: bytes) -> dict:
    if not data.startswith(b"%YAML 1.1") or len(data) < 100:
        raise ValueError("Source is not a serialized original Unity scene")
    documents = Counter(int(value) for value in DOCUMENT_RE.findall(data))
    if not documents or documents[1] < 1 or documents[4] < 1:
        raise ValueError("Scene lacks original GameObjects or Transforms")
    ids = sorted(set(x.decode("ascii") for x in GUID_RE.findall(data)))
    return {
        "sizeBytes": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "unityDocuments": sum(documents.values()),
        "gameObjects": documents[1],
        "transforms": documents[4],
        "monoBehaviours": documents[114],
        "prefabInstances": documents[1001],
        "meshFilters": documents[33],
        "meshRenderers": documents[23],
        "sceneReferencedAssetGuids": len(ids),
        "notes": "Legacy Unity scene metadata, NOT migrated collision/geometry or certified gameplay",
    }


def audit(root: Path = ROOT) -> dict:
    source = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    catalog = json.loads((root / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
    arenas = {a["id"] for a in catalog["arenas"] if a["game"] == "v82"}
    if len(arenas) != 8 or set(source["arenas"]) != arenas:
        raise ValueError("Not all eight legacy arenas are mapped")
    entries = {}
    seen_paths = set()
    for identifier, row in sorted(source["arenas"].items()):
        relative = Path(row["sourcePath"])
        if relative.is_absolute() or ".." in relative.parts or relative.suffix != ".unity":
            raise ValueError(f"Unsafe or missing source stage path: {identifier}")
        if not str(relative).startswith("LegacySources/Vigilante2Unity/Assets/Levels/"):
            raise ValueError("Stage source must come from original public Unity repo")
        if relative in seen_paths:
            raise ValueError("Same original scene aliased to multiple official arenas")
        seen_paths.add(relative)
        scene_path = root / relative
        if not scene_path.is_file():
            raise ValueError("Original stage scene is missing: " + str(relative))
        info = inspect_unity_scene(scene_path.read_bytes())
        if info["sizeBytes"] < 10000:
            raise ValueError("This source scene looks truncated: " + str(relative))
        entries[identifier] = {"sourcePath": relative.as_posix(),
                               "migrationStatus": "original_scene_source_not_unity6_compatible",
                               **info}
    return {
        "schemaVersion": 1, "sourceRevision": source["sourceUnityCommit"],
        "stageCount": len(entries), "scenes": entries,
        "nextStep": "Port actual LevelManager/VigMesh, original materials, collision and spawn points in Unity Editor",
        "certifiedUnity6Arenas": 0,
    }


def main():
    result = audit()
    destination = ROOT / OUT
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n",
                           encoding="utf-8")
    print(f"PASS: {result['stageCount']} real legacy scene files audited and source hashes pinned.")
    for id, row in result["scenes"].items():
        print(f"  {id}: {row['sizeBytes']} bytes / "
              f"{row['gameObjects']} objects / {row['monoBehaviours']} scripts")
    print("This is a source archive audit, NOT playable Unity 6 scenes.")


if __name__ == "__main__":
    main()
