#!/usr/bin/env python3
"""Verify every Unity 2020 texture GUID against its copied bitmap + metadata."""
import hashlib
import json
from pathlib import Path

from port_legacy_textures import read_guid, tim_references

ROOT = Path(__file__).resolve().parents[1]


def verify(root=ROOT):
    m = json.loads((root / "docs/legacy-v82-textures.json").read_text("utf-8"))
    sources = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    if m["vehicleCount"] != 18 or set(m["vehicles"]) != set(sources["vehicles"]):
        raise ValueError("Not all official sequel cars have a preserved texture list")
    seen = set()
    for car, refs in m["vehicles"].items():
        prefab = (root / sources["vehicles"][car]["sourcePath"]).read_text(encoding="utf-8-sig")
        original_guids = tim_references(prefab)
        if [entry["guid"] for entry in refs] != original_guids:
            raise ValueError(f"Unexpected or reordered original material list: {car}")
        for guid in original_guids:
            asset = m["textures"][guid]
            path = root / asset["path"]
            if not path.exists():
                raise ValueError(f"Missing copied texture: {guid}")
            meta = Path(str(path) + ".meta")
            if not meta.exists() or read_guid(meta.read_text(encoding="utf-8-sig")) != guid:
                raise ValueError(f"Unity metadata GUID broken: {guid}")
            if hashlib.sha256(path.read_bytes()).hexdigest() != asset["sha256"]:
                raise ValueError(f"Original texture contents changed: {guid}")
            seen.add(guid)
    if len(seen) != m["uniqueTextures"]:
        raise ValueError("Original texture asset coverage count differs")
    return m


if __name__ == "__main__":
    m = verify()
    print(f"PASS: {m['uniqueTextures']} real original bitmaps with GUID identity "
          "and order verified across 18 vehicles")
