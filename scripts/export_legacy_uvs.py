#!/usr/bin/env python3
"""Create fidelity-focused textured previews from real PS1 TMD + linked BMPs.

This is a separate preview directory, NOT Resources/Vehicles, and does not
mark any vehicle as game-ready. No invented textures, no fallback stock art.
Only direct TIM material-id modes (0x04,0x14,0x24,0x34) are associated with
their original GUID-backed bitmaps; all other modes keep source RGB colour.
"""
from __future__ import annotations

import hashlib
import json
import os
import struct
from collections import Counter
from pathlib import Path

from export_legacy_geometry import ROOT, STRIDES, REVERSED, read_tmds
from port_legacy_textures import tim_references

DEST = Path("Assets/GameContent/TexturedPreviewV82")
SOURCE_MODE = {0x04: (22, 12), 0x14: (22, 12),
               0x24: (26, 16), 0x34: (22, 12)}


def iter_records(tmd):
    pos = 0
    for index in range(tmd.faces):
        if pos + 12 > len(tmd.polygons):
            raise ValueError(f"Truncated polygon stream at {index}")
        mode = tmd.polygons[pos + 3] & 0x3C
        if mode == 0x38:
            return
        if mode == 0x28:
            stride = 12 + struct.unpack_from("<H", tmd.polygons, pos + 10)[0] * 8
        else:
            stride = STRIDES.get(mode)
            if stride is None:
                raise ValueError(f"Unsupported original opcode {mode}")
        if pos + stride > len(tmd.polygons):
            raise ValueError(f"Truncated source TMD polygon {index}")
        yield mode, tmd.polygons[pos:pos + stride]
        pos += stride


def mapped_uvs(record, offset, width, height, reversed_winding):
    if min(width, height) <= 0:
        raise ValueError("Original texture dimensions must be positive")
    uv = []
    for i in range(3):
        px = record[offset + 4*i]
        py = record[offset + 4*i + 1]
        u = min(1.0, px / max(1, width - 1))
        v = 1.0 - min(1.0, py / max(1, height - 1))
        uv.append((u, v))
    return list(reversed(uv)) if reversed_winding else uv


def build_preview(source: str, vehicle_id: str,
                  texture_guids: list[str], textures: dict) -> tuple[str, str, dict]:
    parts = read_tmds(source)
    obj = [
        "# Original PS1 geometry and direct TIM texture links, preview only",
        "# UV atlases, transforms and light modes still need native Unity QA",
        f"mtllib {vehicle_id}.mtl",
        f"o {vehicle_id}",
    ]
    materials = {}
    face_counts = Counter()
    vertex_index = 1
    uv_index = 1
    triangle_count = 0
    for part_num, part in enumerate(parts):
        records = list(iter_records(part))
        if not records:
            continue
        obj.append(f"g {vehicle_id}_tmd_{part_num:03d}")
        for i in range(part.count):
            x, y, z = struct.unpack_from("<hhh", part.vertices, i * 8)
            obj.append(f"v {x/256:.6f} {-y/256:.6f} {z/256:.6f}")
        for mode, face in records:
            if mode in (0x28, 0x38):
                continue
            a, b, c = struct.unpack_from("<HHH", face, 4)
            if any(i % 8 or i // 8 >= part.count for i in (a, b, c)):
                raise ValueError(f"{vehicle_id}: incorrect original polygon references")
            local = [a//8, b//8, c//8]
            if len(set(local)) < 3:
                continue
            reversed_winding = mode in REVERSED
            if reversed_winding:
                local.reverse()
            face_counts[f"0x{mode:02x}"] += 1
            material = None
            uv = None
            if mode in SOURCE_MODE:
                material_offset, uv_offset = SOURCE_MODE[mode]
                index = face[material_offset]
                if index < len(texture_guids):
                    guid = texture_guids[index]
                    image = textures.get(guid)
                    if image is None:
                        raise ValueError(f"{vehicle_id}: missing original texture GUID {guid}")
                    material = f"texture_{index:03d}"
                    uv = mapped_uvs(face, uv_offset,
                                    image["width"], image["height"], reversed_winding)
                    materials[material] = {
                        "texturePath": image["path"],
                        "color": None,
                    }
            if material is None:
                rgb = face[:3]
                material = f"rgb_{rgb[0]:02x}{rgb[1]:02x}{rgb[2]:02x}"
                materials[material] = {
                    "texturePath": None,
                    "color": [round(x / 255, 6) for x in rgb],
                }
            obj.append("usemtl " + material)
            if uv is not None:
                for u, v in uv:
                    obj.append(f"vt {u:.6f} {v:.6f}")
                obj.append("f " + " ".join(
                    f"{vertex_index + n}/{uv_index + k}" for k, n in enumerate(local)))
                uv_index += 3
            else:
                obj.append("f " + " ".join(str(vertex_index + n) for n in local))
            triangle_count += 1
        vertex_index += part.count

    if triangle_count < 1:
        raise ValueError("No real source polygon triangles in " + vehicle_id)

    mtl = ["# Original PS1 source bitmap links and RGB colours"]
    textured = 0
    for name, options in sorted(materials.items()):
        mtl.append("newmtl " + name)
        if options["texturePath"]:
            source_path = Path(options["texturePath"])
            # Paths are resolved from destination directory inside Assets.
            relative = os.path.relpath(source_path, DEST).replace(os.sep, "/")
            mtl.extend(("Kd 1.000000 1.000000 1.000000",
                        "map_Kd " + relative))
            textured += 1
        else:
            color = options["color"]
            mtl.append("Kd " + " ".join(f"{value:.6f}" for value in color))
        mtl.append("illum 1")

    report = {
        "vehicleId": vehicle_id, "tmdParts": len(parts),
        "triangles": triangle_count, "materials": len(materials),
        "mappedOriginalBitmapMaterials": textured,
        "polygonModes": dict(sorted(face_counts.items())),
        "status": "textured_preview_not_verified_game_asset",
    }
    return "\n".join(obj) + "\n", "\n".join(mtl) + "\n", report


def build_all(root: Path = ROOT) -> dict:
    provenance = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    texbank = json.loads((root / "docs/legacy-v82-textures.json").read_text("utf-8"))
    geometry = json.loads((root / "Assets/GameContent/ExtractedV82/manifest.json").read_text("utf-8"))
    if len(provenance["vehicles"]) != 18 or set(texbank["vehicles"]) != set(provenance["vehicles"]):
        raise ValueError("Original source/texture vehicle coverage incomplete")
    stats = []
    target = root / DEST
    target.mkdir(parents=True, exist_ok=True)
    for vehicle_id, meta in sorted(provenance["vehicles"].items()):
        source_path = root / meta["sourcePath"]
        source_text = source_path.read_text(encoding="utf-8-sig")
        original_guid_order = tim_references(source_text)
        backed_guids = [v["guid"] for v in texbank["vehicles"][vehicle_id]]
        if backed_guids != original_guid_order:
            raise ValueError(vehicle_id + ": original texture order changed")
        obj, mtl, record = build_preview(source_text, vehicle_id, original_guid_order,
                                         texbank["textures"])
        (target / (vehicle_id + ".obj")).write_text(obj, encoding="utf-8")
        (target / (vehicle_id + ".mtl")).write_text(mtl, encoding="utf-8")
        original_geometry = next(
            x for x in geometry["vehicleModels"] if x["vehicleId"] == vehicle_id)
        if record["triangles"] != original_geometry["triangles"]:
            raise ValueError(vehicle_id + ": new preview changed original polygon count")
        record["sourceSha256"] = hashlib.sha256(source_path.read_bytes()).hexdigest()
        record["obj"] = (DEST / (vehicle_id + ".obj")).as_posix()
        record["mtl"] = (DEST / (vehicle_id + ".mtl")).as_posix()
        stats.append(record)
    manifest = {
        "schemaVersion": 1,
        "sourceCommit": provenance["sourceUnityCommit"],
        "vehicles": stats,
        "completeOriginalGeometryPreview": len(stats) == 18,
        "notYetCertified": ["legacy part transforms", "opaque PSX polygon modes",
                            "vertex lighting", "rendered Unity 6.6 appearance",
                            "damage animation", "weapon attachment", "player physics"],
    }
    (target / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


if __name__ == "__main__":
    report = build_all()
    print(f"GENERATED {len(report['vehicles'])} original textured vehicle previews")
    for model in report["vehicles"]:
        print(f"{model['vehicleId']}: {model['triangles']} original triangles / "
              f"{model['mappedOriginalBitmapMaterials']} bitmap materials")
    print("These remain a preview, not working ready-to-release vehicle prefabs.")
