#!/usr/bin/env python3
"""Validate exact-source WAV bytes and audio associations, never gameplay state."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from migrate_legacy_audio import guid_from_meta

ROOT = Path(__file__).resolve().parents[1]


def verify(root: Path = ROOT) -> dict:
    data = json.loads((root / "docs/v82-audio-sources.json").read_text(encoding="utf-8"))
    mapping = json.loads((root / "docs/official-asset-sources.json").read_text(encoding="utf-8"))
    records = data["audioFiles"]
    if data["schemaVersion"] != 1 or data["count"] < 230 or len(records) != data["count"]:
        raise ValueError("Incomplete original WAV catalog")
    if set(data["vehicles"]) != set(mapping["vehicles"]) or set(data["arenas"]) != set(mapping["arenas"]):
        raise ValueError("Missing or changed official source banks")
    seen_guids, seen_paths = set(), set()
    for record in records:
        orig = root / "LegacySources/Vigilante2Unity/Assets" / record["source"]
        copied = root / record["unityPath"]
        if not orig.exists() or not copied.exists() or orig.read_bytes() != copied.read_bytes():
            raise ValueError("Original WAV bytes missing or altered: " + record["source"])
        if not str(copied.relative_to(root)).startswith("Assets/GameContent/LegacyAudioV82/"):
            raise ValueError("WAV copied to unexpected path")
        guid = guid_from_meta(Path(str(copied) + ".meta").read_text("utf-8-sig"))
        source_guid = guid_from_meta(Path(str(orig) + ".meta").read_text("utf-8-sig"))
        if guid != record["guid"] or guid != source_guid:
            raise ValueError("Original WAV GUID changed: " + record["source"])
        if hashlib.sha256(copied.read_bytes()).hexdigest() != record["sha256"]:
            raise ValueError("WAV checksum mismatch: " + record["source"])
        if guid in seen_guids or record["unityPath"] in seen_paths:
            raise ValueError("Duplicate original WAV or GUID")
        seen_guids.add(guid)
        seen_paths.add(record["unityPath"])
    by_path = {r["unityPath"]: r for r in records}
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
    print(f"PASS: {report['count']} original WAV files verified with unique GUIDs")
    print(f"PASS: {len(report['vehicles'])} original vehicle banks and "
          f"{len(report['arenas'])} stage banks")
    print("No audio role or Unity gameplay certification claimed")
