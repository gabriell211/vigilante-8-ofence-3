#!/usr/bin/env python3
"""Audit original GitHub recompilation/reference snapshot, not a Unity import."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE = ROOT / "LegacySources/Vigilante8ClassicRecomp"
EXPECTED_COMMIT = "54805112186f970b05777540627ab5bdd084ddb7"
REQUIRED = (
    "README.md",
    "notes/v82_graphics_options.md",
    "reference-v8-2/LOOSE_FILES.md",
    "platform/xobf_texture.c",
    "tools/recompone-v8-2/reference-host/Vigilante82PC.csproj",
    "tools/recompone-v8-2/validate_texture_pack.py",
    "mods/v8_classic_menu/manifest.json",
    "mods/v8_classic_menu/images/main_menu.dds",
)


def verify(root: Path = ROOT):
    archive = root / "LegacySources/Vigilante8ClassicRecomp"
    if not archive.is_dir():
        raise ValueError("Missing source snapshot")
    provenance = json.loads((archive / "source.json").read_text("utf-8"))
    if provenance.get("commit") != EXPECTED_COMMIT:
        raise ValueError("Source commit not pinned")
    missing = [name for name in REQUIRED if not (archive / name).is_file()]
    if missing:
        raise ValueError("Recomp source missing: " + ", ".join(missing))
    count = sum(1 for p in archive.rglob("*") if p.is_file())
    if count < 4500:
        raise ValueError(f"Partial recomp reference snapshot: {count} files")
    return {"files": count, "commit": EXPECTED_COMMIT}


if __name__ == "__main__":
    value = verify()
    print(f"PASS recompilation reference archived: {value['files']} files @ {value['commit']}")
    print("Note: original retail data is not included in the upstream Git repo.")
