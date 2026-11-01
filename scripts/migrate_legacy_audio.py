#!/usr/bin/env python3
"""Stage every authentic WAV from the original Unity project into Unity 6 Assets.

The original AudioImporter GUID and all bytes are preserved. Audio sample
indices are NOT guessed into engine/gun/explosion gameplay roles; metadata is
kept for original-game behaviour mapping.
"""
from __future__ import annotations

import hashlib
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path("LegacySources/Vigilante2Unity/Assets")
DEST = Path("Assets/GameContent/LegacyAudioV82")
MANIFEST = Path("docs/v82-audio-sources.json")
GUID_PATTERN = re.compile(r"(?m)^guid:\s*([a-f0-9]{32})\s*$")


def guid_from_meta(meta: str) -> str:
    found = GUID_PATTERN.search(meta)
    if not found:
        raise ValueError("Original audio importer metadata missing a 32-character GUID")
    return found.group(1)


def copy_audio(root: Path = ROOT) -> dict:
    mapping = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    if len(mapping["vehicles"]) != 18 or len(mapping["arenas"]) != 8:
        raise ValueError("Official sequel source mapping incomplete")
    src = root / SOURCE
    original = sorted(src.rglob("*.wav"))
    if len(original) < 230:
        raise ValueError(f"Original WAV source archive incomplete: {len(original)} files")
    by_guid: dict[str, str] = {}
    all_records = []
    vehicle_audio: dict[str, list[dict]] = {}
    stage_audio: dict[str, list[dict]] = {}
    for vehicle_id, entry in sorted(mapping["vehicles"].items()):
        stem = Path(entry["sourcePath"]).stem
        prefix = f"SHARED/COMMON/Sounds/{stem}_"
        files = [f for f in original if f.relative_to(src).as_posix().startswith(prefix)]
        if not files:
            raise ValueError(f"No original WAV samples for vehicle {vehicle_id} ({stem})")
        vehicle_audio[vehicle_id] = []
        for file in files:
            vehicle_audio[vehicle_id].append({"source": file.relative_to(src).as_posix()})
    stage_prefixes = {
        "v82_meteor_crater": "ROUTE66", "v82_winter_games": "OLYMPIC",
        "v82_ghastly_bayou": "BAYOU", "v82_launch_site": "LAUNCH",
        "v82_steel_mill": "STEELMIL", "v82_nuclear_plant": "NUCLEAR",
        "v82_alaskan_pipeline": "OILFIELD", "v82_pacific_harbor": "HARBOR",
    }
    for stage_id, prefix in stage_prefixes.items():
        files = [f for f in original if f.relative_to(src).as_posix()
                 .startswith("LEVELS/" + prefix + "/Sounds/")]
        if not files:
            raise ValueError(f"No original stage sound samples: {stage_id}")
        stage_audio[stage_id] = [{"source": f.relative_to(src).as_posix()} for f in files]

    for original_file in original:
        relative = original_file.relative_to(src)
        source_meta = Path(str(original_file) + ".meta")
        if not source_meta.is_file():
            raise FileNotFoundError(f"No original .meta: {source_meta}")
        guid = guid_from_meta(source_meta.read_text(encoding="utf-8-sig"))
        if guid in by_guid and by_guid[guid] != relative.as_posix():
            raise ValueError(f"Conflicting WAV GUID: {guid}")
        by_guid[guid] = relative.as_posix()
        content = original_file.read_bytes()
        if len(content) < 44 or content[:4] != b"RIFF" or content[8:12] != b"WAVE":
            raise ValueError(f"Not a real RIFF/WAVE audio file: {original_file}")
        dest = root / DEST / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.exists() and dest.read_bytes() != content:
            raise ValueError(f"Conflicting local audio: {dest}")
        shutil.copyfile(original_file, dest)
        shutil.copyfile(source_meta, Path(str(dest) + ".meta"))
        checksum = hashlib.sha256(content).hexdigest()
        all_records.append({
            "source": relative.as_posix(),
            "unityPath": (DEST / relative).as_posix(),
            "guid": guid,
            "sha256": checksum,
            "bytes": len(content),
        })

    all_by_source = {r["source"]: r for r in all_records}
    def enrich(mapping: dict[str, list[dict]]) -> dict:
        out = {}
        for key, records in mapping.items():
            result = []
            for rec in records:
                item = all_by_source[rec["source"]]
                result.append({
                    "index": int(Path(rec["source"]).stem.rsplit("_", 1)[-1]),
                    "assetPath": item["unityPath"], "guid": item["guid"]
                })
            result.sort(key=lambda x: x["index"])
            if len({x["index"] for x in result}) != len(result):
                raise ValueError(f"Duplicate original audio sample index: {key}")
            out[key] = result
        return out

    result = {
        "schemaVersion": 1,
        "sourceRevision": mapping["sourceUnityCommit"],
        "count": len(all_records),
        "audioFiles": all_records,
        "vehicles": enrich(vehicle_audio),
        "arenas": enrich(stage_audio),
        "note": "Original samples preserved, sample roles not assigned; not proof of gameplay audio",
    }
    # Unity JsonUtility does not decode dictionaries: ship a derived and
    # source-verified array catalog for the Editor (no hard-coded car mapping).
    editor_banks = []
    for kind, entries in (("vehicle", result["vehicles"]), ("arena", result["arenas"])):
        for identifier, samples in sorted(entries.items()):
            editor_banks.append({
                "id": identifier, "kind": kind,
                "samples": [
                    {"originalIndex": sample["index"], "assetPath": sample["assetPath"],
                     "guid": sample["guid"]} for sample in samples
                ],
            })
    editor_catalog = {
        "schemaVersion": 1, "sourceRevision": mapping["sourceUnityCommit"],
        "banks": editor_banks,
    }
    (root / DEST / "catalog.json").write_text(
        json.dumps(editor_catalog, indent=2, ensure_ascii=False) + chr(10),
        encoding="utf-8")

    manifest = root / MANIFEST
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    data = copy_audio()
    print(f"STAGED {data['count']} real WAV sources, "
          f"{len(data['vehicles'])} official vehicle banks, "
          f"{len(data['arenas'])} stage banks; GUIDs unchanged")
