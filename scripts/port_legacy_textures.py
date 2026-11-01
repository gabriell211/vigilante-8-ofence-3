#!/usr/bin/env python3
"""Import exact Vigilante2Unity vehicle textures by their Unity 2020 GUID references.

Uses original timList in XOBF prefab and matches the GUID in the corresponding
TextureImporter .meta. Copies each unique source bitmap *once*, preserving its
GUID and bytes, to avoid Unity's duplicate-GUID errors. NEVER substitutes
fake images or assumes texture order matches filenames.
"""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_ASSETS = Path("LegacySources/Vigilante2Unity/Assets")
DESTINATION = Path("Assets/GameContent/LegacyTextureBank")
MANIFEST = Path("docs/legacy-v82-textures.json")
GUID = re.compile(r"(?m)^guid:\s*([a-f0-9]{32})\s*$")
REF = re.compile(r"^  - \{fileID: 2800000, guid: ([a-f0-9]{32}), type: 3\}")


def read_guid(meta: str) -> str:
    match = GUID.search(meta)
    if not match:
        raise ValueError("TextureImporter .meta missing a valid GUID")
    return match.group(1)


def tim_references(prefab: str) -> list[str]:
    references = []
    inside = False
    for line in prefab.splitlines():
        if line == "  timList:":
            inside = True
            continue
        if not inside:
            continue
        match = REF.fullmatch(line)
        if match:
            references.append(match.group(1))
        elif line.strip():
            break
    if not references:
        raise ValueError("No original Unity Texture2D links in XOBF prefab")
    return references


def build_index(root: Path) -> dict[str, Path]:
    assets = root / SOURCE_ASSETS
    if not assets.is_dir():
        raise ValueError("Upstream project files have not been imported")
    by_guid = {}
    for meta in sorted(assets.rglob("*.bmp.meta")):
        original = meta.with_suffix("")
        if not original.is_file():
            raise ValueError(f"Texture metadata without actual original BMP: {meta}")
        guid = read_guid(meta.read_text(encoding="utf-8-sig"))
        previous = by_guid.get(guid)
        if previous and previous != original:
            raise ValueError(f"Conflicting Unity source GUID {guid}")
        by_guid[guid] = original
    if not by_guid:
        raise ValueError("No real Unity texture assets indexed")
    return by_guid


def migrate(root: Path = ROOT) -> dict:
    provenance = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    vehicles = provenance["vehicles"]
    if len(vehicles) != 18:
        raise ValueError("Expected 18 official sequel vehicles")
    by_guid = build_index(root)
    output = root / DESTINATION
    output.mkdir(parents=True, exist_ok=True)
    catalog = {}
    resources = {}
    for vehicle_id, entry in sorted(vehicles.items()):
        prefab = (root / entry["sourcePath"]).read_text(encoding="utf-8-sig")
        refs = tim_references(prefab)
        linked = []
        for texture_id, guid in enumerate(refs):
            if guid not in by_guid:
                raise ValueError(f"{vehicle_id}: missing authentic texture GUID {guid}")
            source = by_guid[guid]
            blob = source.read_bytes()
            if len(blob) < 54 or blob[:2] != b"BM":
                raise ValueError(f"{vehicle_id}: original BMP is not a valid Windows bitmap")
            width, height = struct.unpack_from("<ii", blob, 18)
            if width == 0 or height == 0 or abs(width) > 16384 or abs(height) > 16384:
                raise ValueError(f"{vehicle_id}: unreasonable original BMP dimensions")
            relative = source.relative_to(root / SOURCE_ASSETS)
            destination = output / relative
            if not destination.exists():
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, destination)
                shutil.copyfile(Path(str(source) + ".meta"),
                                Path(str(destination) + ".meta"))
            elif destination.read_bytes() != blob:
                raise ValueError(f"Active original bitmap changed: {destination}")

            destination_meta = Path(str(destination) + ".meta")
            if read_guid(destination_meta.read_text(encoding="utf-8-sig")) != guid:
                raise ValueError(f"Texture's original Unity GUID changed: {destination_meta}")
            checksum = hashlib.sha256(blob).hexdigest()
            resources[guid] = {
                "path": destination.relative_to(root).as_posix(),
                "sourcePath": source.relative_to(root).as_posix(),
                "sha256": checksum,
                "width": abs(width),
                "height": abs(height),
            }
            linked.append({"index": texture_id, "guid": guid})
        catalog[vehicle_id] = linked

    result = {
        "schemaVersion": 1,
        "sourceCommit": provenance["sourceUnityCommit"],
        "vehicleCount": len(vehicles),
        "uniqueTextures": len(resources),
        "vehicles": catalog,
        "textures": dict(sorted(resources.items())),
        "state": "authentic_Unity20_texture_files_imported_not_URP_material_migration",
    }
    (root / MANIFEST).parent.mkdir(parents=True, exist_ok=True)
    (root / MANIFEST).write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    result = migrate()
    print(f"EXPORTED {result['uniqueTextures']} authentic BMP textures for "
          f"{result['vehicleCount']} original sequel vehicles, all GUIDs intact.")
    print("URP materials and UV atlas reconstruction remain unverified.")
