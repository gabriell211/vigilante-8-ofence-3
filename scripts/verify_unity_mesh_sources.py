#!/usr/bin/env python3
"""Validate all 18 original TMD OBJ exports against the strict Unity mesh importer.

Every face must be triangulated, every group must be an authentic TMD group,
and every source material must exist in the original MTL. Textures may only
reference the verified Unity Assets texture bank, never arbitrary locations.
This is static source validation, not actual Unity import/compilation.
"""
from __future__ import annotations

import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "Assets/GameContent/TexturedPreviewV82"
SAFE = re.compile(r"^[A-Za-z0-9_]+$")
SOURCE = re.compile(r"^v82_[a-z0-9_]+_tmd_\d{3}$")


def valid_float(value: str) -> float:
    parsed = float(value)
    if not math.isfinite(parsed):
        raise ValueError("Nonfinite source geometry coordinate")
    return parsed


def parse_source_obj(path: Path) -> dict:
    vertices, texcoords, groups, materials_used = 0, 0, {}, set()
    group, material = None, None
    for number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        tokens = line.split()
        kind = tokens[0]
        if kind == "v":
            if len(tokens) != 4: raise ValueError(f"{path}:{number} bad vertex")
            for part in tokens[1:]: valid_float(part)
            vertices += 1
        elif kind == "vt":
            if len(tokens) != 3: raise ValueError(f"{path}:{number} bad UV")
            for part in tokens[1:]: valid_float(part)
            texcoords += 1
        elif kind == "g":
            if len(tokens) != 2 or not SOURCE.fullmatch(tokens[1]) or tokens[1] in groups:
                raise ValueError(f"{path}:{number} bad or duplicate TMD group")
            group = tokens[1]
            groups[group] = 0
        elif kind == "usemtl":
            if len(tokens) != 2 or not SAFE.fullmatch(tokens[1]):
                raise ValueError(f"{path}:{number} bad source material")
            material = tokens[1]
            materials_used.add(material)
        elif kind == "f":
            if len(tokens) != 4 or not group or not material:
                raise ValueError(f"{path}:{number} unsupported source face")
            seen = set()
            with_uv = set()
            for token in tokens[1:]:
                parts = token.split("/")
                if len(parts) not in (1,2):
                    raise ValueError(f"{path}:{number} unsupported face token")
                ix = int(parts[0])
                if ix <= 0 or ix > vertices or ix in seen:
                    raise ValueError(f"{path}:{number} nonexistent/degenerate vertex")
                seen.add(ix)
                if len(parts) == 2:
                    t = int(parts[1])
                    if t <= 0 or t > texcoords:
                        raise ValueError(f"{path}:{number} nonexistent UV")
                    with_uv.add(True)
                else:
                    with_uv.add(False)
            if len(with_uv) != 1:
                raise ValueError(f"{path}:{number} mixed UV face")
            groups[group] += 1
        elif kind in ("mtllib","o"):
            if len(tokens) != 2:
                raise ValueError(f"{path}:{number} malformed OBJ header")
        else:
            raise ValueError(f"{path}:{number} unsupported OBJ statement {kind}")
    if not groups or not vertices or any(value == 0 for value in groups.values()):
        raise ValueError("Missing actual original group geometry")
    return {"triangles": sum(groups.values()), "groups": len(groups),
            "materials": materials_used, "vertices": vertices}


def parse_materials(path: Path, root: Path = ROOT) -> dict:
    materials = {}
    name = None
    for raw in path.read_text("utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"): continue
        if line.startswith("newmtl "):
            name = line[7:].strip()
            if not SAFE.fullmatch(name) or name in materials:
                raise ValueError("Invalid/duplicate source material: " + name)
            materials[name] = {"map": None}
        elif line.startswith("map_Kd "):
            if name is None: raise ValueError("Texture without material")
            relative = line[7:].strip()
            if not relative.startswith("../LegacyTextureBank/") or "\\" in relative:
                raise ValueError("Texture does not reference the original BMP bank")
            actual = (path.parent / relative).resolve()
            bank = (root / "Assets/GameContent/LegacyTextureBank").resolve()
            if not actual.is_relative_to(bank) or not actual.is_file():
                raise ValueError("Missing authentic referenced bitmap: " + relative)
            materials[name]["map"] = actual
        elif line.startswith("Kd "):
            if name is None: raise ValueError("RGB without material")
            rgb = line.split()
            if len(rgb) != 4 or not all(0 <= valid_float(x) <= 1 for x in rgb[1:]):
                raise ValueError("Invalid original RGB colour")
        elif line.startswith("illum "):
            pass
        else:
            raise ValueError("Unsupported source MTL directive: " + line)
    if not materials:
        raise ValueError("No original materials")
    return materials


def verify(root: Path = ROOT) -> dict:
    base = root / "Assets/GameContent/TexturedPreviewV82"
    manifest = json.loads((base / "manifest.json").read_text("utf-8"))
    geometry = json.loads(
        (root / "Assets/GameContent/ExtractedV82/manifest.json").read_text("utf-8"))
    stats = {row["vehicleId"]: row for row in geometry["vehicleModels"]}
    if len(manifest["vehicles"]) != 18 or len(stats) != 18:
        raise ValueError("Incomplete original vehicle roster")
    report = []
    for vehicle in manifest["vehicles"]:
        identifier = vehicle["vehicleId"]
        if identifier not in stats or not SAFE.fullmatch(identifier):
            raise ValueError("Unrecognized real vehicle")
        result = parse_source_obj(base / (identifier + ".obj"))
        materials = parse_materials(base / (identifier + ".mtl"), root=root)
        if result["triangles"] != vehicle["triangles"] or result["triangles"] != stats[identifier]["triangles"]:
            raise ValueError("Original triangle counts do not agree: " + identifier)
        if result["groups"] > stats[identifier]["tmdParts"] or not result["materials"] <= materials.keys():
            raise ValueError("Missing TMD parts or original MTL definitions: " + identifier)
        mapped_count = sum(bool(materials[n]["map"]) for n in result["materials"])
        if mapped_count != vehicle["mappedOriginalBitmapMaterials"]:
            raise ValueError("Original referenced bitmap count changed: " + identifier)
        report.append({"vehicle": identifier, **{
            k: value for k, value in result.items() if k != "materials"}})
    if len({row["vehicle"] for row in report}) != 18:
        raise ValueError("Duplicate original car")
    return {"vehicles": report}

if __name__ == "__main__":
    report = verify()
    print("PASS: 18 authentic PS1 TMD OBJ/MTL groups, faces, UV indices and source BMPs")
    print("Static file checks only; Unity Editor compilation, import and playable game are still pending.")
