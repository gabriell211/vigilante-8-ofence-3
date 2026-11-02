#!/usr/bin/env python3
"""Validate exact-source WAV bytes and audio associations, never gameplay state."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from migrate_legacy_audio import EMPTY_SOURCE, EMPTY_SOURCE_SHA256, guid_from_meta

ROOT = Path(__file__).resolve().parents[1]


def verify(root: Path = ROOT) -> dict:
    data = json.loads((root / "docs/v82-audio-sources.json").read_text(encoding="utf-8"))
    mapping = json.loads((root / "docs/official-asset-sources.json").read_text(encoding="utf-8"))
    records = data["audioFiles"]
    if data["schemaVersion"] != 1 or data["count"] < 230 or len(records) != data["count"]:
        raise ValueError("Incomplete original WAV catalog")
    if data.get("importableCount") != sum(r["unityPath"] is not None for r in records):
        raise ValueError("Imported WAV count mismatch")
    if set(data["vehicles"]) != set(mapping["vehicles"]) or set(data["arenas"]) != set(mapping["arenas"]):
        raise ValueError("Missing or changed official source banks")
    seen_guids, seen_paths = set(), set()
    for record in records:
        orig = root / "LegacySources/Vigilante2Unity/Assets" / record["source"]
        if not orig.exists():
            raise ValueError("Original WAV source missing: " + record["source"])
        source_guid = guid_from_meta(Path(str(orig) + ".meta").read_text("utf-8-sig"))
        if source_guid != record["guid"]:
            raise ValueError("Original WAV GUID changed: " + record["source"])
        if hashlib.sha256(orig.read_bytes()).hexdigest() != record["sha256"]:
            raise ValueError("WAV checksum mismatch: " + record["source"])
        if record["unityPath"] is None:
            if record["source"] != EMPTY_SOURCE or record["sha256"] != EMPTY_SOURCE_SHA256 \
                    or record.get("importStatus") != "empty_source_not_importable":
                raise ValueError("Unknown unimportable WAV: " + record["source"])
            invalid_copy = root / "Assets/GameContent/LegacyAudioV82" / record["source"]
            if invalid_copy.exists() or Path(str(invalid_copy) + ".meta").exists():
                raise ValueError("Empty WAV must stay outside Unity Assets")
        else:
            copied = root / record["unityPath"]
            if not copied.exists() or orig.read_bytes() != copied.read_bytes():
                raise ValueError("Original WAV bytes missing or altered: " + record["source"])
            if not str(copied.relative_to(root)).startswith("Assets/GameContent/LegacyAudioV82/"):
                raise ValueError("WAV copied to unexpected path")
            guid = guid_from_meta(Path(str(copied) + ".meta").read_text("utf-8-sig"))
            if guid != source_guid:
                raise ValueError("Copied WAV GUID changed: " + record["source"])
            if record["unityPath"] in seen_paths:
                raise ValueError("Duplicate original WAV path")
            seen_paths.add(record["unityPath"])
        if source_guid in seen_guids:
            raise ValueError("Duplicate original WAV or GUID")
        seen_guids.add(source_guid)
    by_path = {r["unityPath"]: r for r in records if r["unityPath"] is not None}
    for group in ("vehicles", "arenas"):
        for identifier, samples in data[group].items():
            if not samples or len({s["index"] for s in samples}) != len(samples):
                raise ValueError("Missing or duplicated WAV sample index: " + identifier)
            if samples != sorted(samples, key=lambda s: s["index"]):
                raise ValueError("Sample order changed: " + identifier)
            for sample in samples:
                if sample["assetPath"] not in by_path:
                    raise ValueError("Audio bank references unknown WAV: " + identifier)
                if by_path[sample["assetPath"]]["guid"] != sample["guid"]:
                    raise ValueError("Audio bank references mismatched GUID: " + identifier)
    editor_manifest = json.loads(
        (root / "Assets/GameContent/LegacyAudioV82/catalog.json").read_text("utf-8"))
    banks = editor_manifest.get("banks", [])
    if editor_manifest.get("schemaVersion") != 1 or len(banks) != 26:
        raise ValueError("Unity Editor audio source manifest is incomplete")
    expected_banks = {("vehicle", key): val for key, val in data["vehicles"].items()}
    expected_banks.update({("arena", key): val for key, val in data["arenas"].items()})
    observed = {}
    for bank in banks:
        key = (bank["kind"], bank["id"])
        if key in observed or key not in expected_banks:
            raise ValueError("Unknown or duplicate original Unity audio bank")
        observed[key] = [{
            "index": sample["originalIndex"], "assetPath": sample["assetPath"],
            "guid": sample["guid"]
        } for sample in bank["samples"]]
    if observed != expected_banks:
        raise ValueError("Unity Editor source audio banks differ from the original")
    return data


if __name__ == "__main__":
    report = verify()
    print(f"PASS: {report['count']} archived WAV sources verified; "
          f"{report['importableCount']} imported into Unity with unique GUIDs")
    print(f"PASS: {len(report['vehicles'])} original vehicle banks and "
          f"{len(report['arenas'])} stage banks")
    print("No audio role or Unity gameplay certification claimed")
