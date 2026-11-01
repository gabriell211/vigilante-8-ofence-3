#!/usr/bin/env python3
"""Extract authentic PS1 TMD triangle geometry from Vigilante2Unity XOBF prefabs.

Source: the serialized tmdList.vertexStream / faceStream in the original Unity
2020 prefabs already vendored under LegacySources. No substituted geometry,
cube stand-ins, fictional vehicles, copied meshes or downloaded retail dumps.

This converts geometry ONLY: TMD part configuration, texel atlases, normals,
UVs, animation and damage states require a separate faithful migration pass.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import struct
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAPPING = ROOT / "docs/official-asset-sources.json"
OUTPUT = ROOT / "Assets/GameContent/ExtractedV82"
# Exact record sizes read from the original VigMesh.FUN_21F70 dispatch table.
STRIDES = {
    0x00: 12, 0x04: 32, 0x08: 20, 0x0C: 32,
    0x10: 12, 0x14: 24, 0x18: 12, 0x1C: 24,
    0x20: 16, 0x24: 28, 0x2C: 28, 0x30: 24,
    0x34: 24, 0x3C: 24,
}
# Original VigMesh follows reversed winding on these textured primitives.
REVERSED = {0x04, 0x0C, 0x24, 0x2C, 0x34, 0x3C}
FIELDS = ("vertices", "faces", "vertexStream", "faceStream")


@dataclass(frozen=True)
class Tmd:
    count: int
    faces: int
    vertices: bytes
    polygons: bytes


def read_tmds(source: str) -> list[Tmd]:
    blocks: list[dict[str, str]] = []
    current: dict[str, str] | None = None
    inside_tmd = False
    for line in source.splitlines():
        if line.strip() == "tmdList:":
            inside_tmd = True
            continue
        if not inside_tmd:
            continue
        match = re.match(r"^  - vertices: (\d+)\s*$", line)
        if match:
            current = {"vertices": match.group(1)}
            blocks.append(current)
            continue
        if not line.startswith("    ") and not line.startswith("  - "):
            break
        if current is None:
            continue
        match = re.match(r"^    (faces|vertexStream|faceStream):\s*(.*?)\s*$", line)
        if match:
            current[match.group(1)] = match.group(2)
    parsed = []
    for index, block in enumerate(blocks):
        if not all(key in block for key in FIELDS):
            raise ValueError(f"TMD {index}: incomplete serialized data")
        count = int(block["vertices"])
        faces = int(block["faces"])
        if count < 0 or faces < 0 or count > 65535 or faces > 65535:
            raise ValueError(f"TMD {index}: unreasonable lengths")
        try:
            vertices = bytes.fromhex(block["vertexStream"])
            polygons = bytes.fromhex(block["faceStream"])
        except ValueError as exc:
            raise ValueError(f"TMD {index}: invalid hexadecimal stream") from exc
        if len(vertices) < count * 8:
            raise ValueError(f"TMD {index}: shortened vertex buffer")
        parsed.append(Tmd(count, faces, vertices, polygons))
    if not parsed:
        raise ValueError("No original TMD geometry found in prefab")
    return parsed


def decode_faces(tmd: Tmd) -> tuple[list[tuple[int, int, int]], int, int]:
    cursor = 0
    triangles: list[tuple[int, int, int]] = []
    skipped = 0
    for face in range(tmd.faces):
        if cursor + 12 > len(tmd.polygons):
            raise ValueError(f"Face {face}: truncated PS1 polygon header")
        mode = tmd.polygons[cursor + 3] & 0x3C
        if mode == 0x38:  # End marker in original renderer, not geometry.
            skipped += tmd.faces - face
            break
        if mode == 0x28:  # Occlusion block, not an actual polygon.
            occlusion_count = struct.unpack_from("<H", tmd.polygons, cursor + 10)[0]
            stride = 12 + occlusion_count * 8
            skipped += 1
        else:
            stride = STRIDES.get(mode)
            if stride is None:
                raise ValueError(f"Face {face}: unsupported PS1 polygon mode 0x{mode:02x}")
            a, b, c = struct.unpack_from("<HHH", tmd.polygons, cursor + 4)
            if (a | b | c) % 8 != 0 or any(
                offset + 6 > len(tmd.vertices) for offset in (a, b, c)
            ):
                raise ValueError(f"Face {face}: polygon references invalid vertex offset")
            indices = (a // 8, b // 8, c // 8)
            if len(set(indices)) == 3:
                triangles.append((indices[2], indices[1], indices[0])
                                 if mode in REVERSED else indices)
            else:
                skipped += 1
        if cursor + stride > len(tmd.polygons):
            raise ValueError(f"Face {face}: truncated PS1 polygon record")
        cursor += stride
    return triangles, skipped, cursor


def export_model(source: str, vehicle_id: str) -> tuple[str, dict]:
    parts = read_tmds(source)
    lines = [
        "# Authentic PlayStation triangle data decoded from Vigilante2Unity.",
        "# No synthetic geometry. Raw coordinates scaled 1/256 for preview only.",
        "# Materials, PS1 texture atlas, part transforms and UVs NOT yet migrated.",
        f"o {vehicle_id}",
    ]
    global_index = 1
    triangles_total = 0
    vertex_total = 0
    skipped_total = 0
    for part_id, part in enumerate(parts):
        triangles, skipped, _ = decode_faces(part)
        skipped_total += skipped
        if not triangles:
            continue
        lines.append(f"g {vehicle_id}_tmd_{part_id:03d}")
        for i in range(part.count):
            x, y, z = struct.unpack_from("<hhh", part.vertices, i * 8)
            lines.append(f"v {x / 256:.6f} {-y / 256:.6f} {z / 256:.6f}")
        for a, b, c in triangles:
            lines.append(f"f {global_index + a} {global_index + b} {global_index + c}")
        global_index += part.count
        vertex_total += part.count
        triangles_total += len(triangles)
    if vertex_total < 3 or triangles_total < 1:
        raise ValueError(f"No valid non-placeholder triangles exported: {vehicle_id}")
    return "\n".join(lines) + "\n", {
        "vehicleId": vehicle_id, "tmdParts": len(parts),
        "vertices": vertex_total, "triangles": triangles_total,
        "skippedRecords": skipped_total,
        "origin": "Vigilante2Unity serialized TMD",
        "materialsReady": False, "prefabReady": False, "gameplayTested": False,
    }


def convert(root: Path = ROOT, output: Path = OUTPUT) -> dict:
    provenance = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    catalog = json.loads((root / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
    sequel_ids = {car["id"] for car in catalog["vehicles"] if car["game"] == "v82"}
    if len(sequel_ids) != 18 or set(provenance["vehicles"]) != sequel_ids:
        raise ValueError("Official sequel catalog and source list do not match")
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for vehicle_id, entry in sorted(provenance["vehicles"].items()):
        source_path = root / entry["sourcePath"]
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        source_data = source_path.read_bytes()
        source = source_data.decode("utf-8-sig")
        obj, stats = export_model(source, vehicle_id)
        target = output / f"{vehicle_id}.obj"
        target.write_text(obj, encoding="utf-8", newline="\n")
        stats["sourcePath"] = entry["sourcePath"]
        stats["sha256"] = hashlib.sha256(source_data).hexdigest()
        stats["objPath"] = target.relative_to(root).as_posix()
        records.append(stats)
    report = {
        "schemaVersion": 1, "sourceCommit": provenance["sourceUnityCommit"],
        "vehicleModels": records, "completeRosterModels": len(records) == 18,
        "warning": "Actual original TMD triangle geometry only; not an approved rendered playable 3D vehicle.",
    }
    (output / "manifest.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    report = convert()
    if args.json:
        print(json.dumps(report, indent=2))
    else:
        for item in report["vehicleModels"]:
            print(f"{item['vehicleId']}: {item['vertices']} verts / "
                  f"{item['triangles']} triangles / {item['tmdParts']} TMDs "
                  f"({item['skippedRecords']} nontriangles)")
        print(f"EXPORTED {len(report['vehicleModels'])} authentic source geometry OBJ files.")
        print("UVs, materials, part transforms, gameplay and URP require separate migration.")


if __name__ == "__main__":
    main()
