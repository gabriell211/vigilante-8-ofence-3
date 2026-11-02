#!/usr/bin/env python3
"""Inspect actual original stage YAML for recoverable mesh source data.

A scene containing MeshFilter pointers is *not* a recovered Unity 6 map.
If serialized TMD blocks are found, their shapes can be exported by a
follow-up converter without inventing replacement scenery.
"""
from __future__ import annotations
import json
import re
from pathlib import Path
from audit_legacy_stages import ROOT

DEST = Path("docs/v82-stage-geometry-probe.json")

def analyze_scene(data: bytes) -> dict:
    return {
        "tmdListBlocks": len(re.findall(rb"(?m)^\s*tmdList:\s*$", data)),
        "tmdVertexBuffers": len(re.findall(rb"(?m)^\s*vertexStream: [a-fA-F0-9]+", data)),
        "tmdFaceBuffers": len(re.findall(rb"(?m)^\s*faceStream: [a-fA-F0-9]+", data)),
        "meshFilterReferences": len(re.findall(rb"(?m)^  m_Mesh: \{fileID:", data)),
        "serializedTerrainObjects": len(re.findall(rb"(?m)^Terrain:\s*$", data)),
        "originalSceneGameObjectNames": len(re.findall(rb"(?m)^  m_Name: .+$", data)),
        "hasRawTmdGeometry": b"vertexStream:" in data and b"faceStream:" in data,
    }

def probe(root: Path=ROOT):
    source=json.loads((root/"docs/official-asset-sources.json").read_text("utf-8"))
    result={}
    for identifier, entry in sorted(source["arenas"].items()):
        path=root/entry["sourcePath"]
        if not path.exists(): raise ValueError("Original legacy scene not found")
        data=path.read_bytes()
        result[identifier]={"sourcePath":entry["sourcePath"], **analyze_scene(data)}
    if len(result)!=8: raise ValueError("Original sequel stage inventory must include eight scenes")
    return {"schemaVersion":1, "sourceRevision":source["sourceUnityCommit"],
            "arenas":result, "status":"legacy_scene_geometry_probe_not_ported"}

if __name__=="__main__":
    data=probe()
    dest=ROOT/DEST
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(json.dumps(data,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    for key,val in data["arenas"].items():
        print(f"{key}: {val['tmdListBlocks']} TMD groups, "
              f"{val['tmdVertexBuffers']} vertex streams, {val['tmdFaceBuffers']} face streams, "
              f"{val['meshFilterReferences']} MeshFilter references")
    print("Probe only; no official stage geometry migration claimed")
