#!/usr/bin/env python3
"""Reproducibly extract 18 authentic V82 VehicleData entries; do not infer game physics."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/GameManager.cs"
TARGET = ROOT / "Assets/GameContent/LegacyHandlingV82/manifest.json"
SOURCE_REVISION = "d20883df0d5da3ba517142fb4d486db1f5e673fb"
ENUM_IDS = {
    "Wonderwagon": "v82_wonderwagon",
    "Thunderbolt": "v82_thunderbolt",
    "DakotaCycle": "v82_dakota_stunt_cycle",
    "SamsonTow": "v82_samson_tow_truck",
    "Livingston": "v82_livingston_truck",
    "Xanadu": "v82_xanadu_rv",
    "Palomino": "v82_palomino_xiii",
    "ElGuerrero": "v82_el_guerrero",
    "BlueBurro": "v82_blue_burro_bus",
    "Excelsior": "v82_excelsior_stretch",
    "Tsunami": "v82_tsunami",
    "Marathon": "v82_marathon",
    "Trekker": "v82_moon_trekker",
    "Loader": "v82_grubb_dual_loader",
    "Stinger": "v82_chrono_stinger",
    "Vertigo": "v82_vertigo",
    "Goliath": "v82_goliath_halftrack",
    "Wapiti": "v82_wapiti_4wd"
}
FIELDS = {
    "maxHalfHealth": "rawHalfHealth",
    "lightness": "rawLightness",
    "DAT_0E": "dat0e",
    "DAT_0F": "dat0f",
    "DAT_10": "dat10",
    "DAT_11": "dat11",
    "DAT_12": "dat12",
    "DAT_13": "dat13",
    "DAT_15": "dat15",
    "DAT_2A": "dat2a"
}


def extract(source: str) -> dict:
    start = source.find("public static VehicleData[] vehicleConfigs")
    end = source.find("public struct TerrainJob", start)
    if start < 0 or end < 0:
        raise ValueError("Original source vehicle config table was not found")
    blocks = re.split(r"\bnew VehicleData[^\r\n]*\r?\n\s*\{", source[start:end])[1:]
    if len(blocks) != 18:
        raise ValueError(f"Expected 18 source config blocks; found {len(blocks)}")
    results = []
    for block in blocks:
        match = re.search(r"\bvehicleID\s*=\s*_VEHICLE\.(\w+)", block)
        if match is None or match.group(1) not in ENUM_IDS:
            raise ValueError("Unknown original _VEHICLE identity")
        enum = match.group(1)
        record = {"vehicleId": ENUM_IDS[enum], "sourceEnum": enum}
        for key, target in FIELDS.items():
            field = re.search(r"\b" + re.escape(key) + r"\s*=\s*([^,\r\n}]+)", block)
            if field is None:
                raise ValueError(f"{enum}: missing {key}")
            record[target] = int(field.group(1).strip(), 0)
        if record["rawHalfHealth"] <= 0 or record["rawLightness"] <= 0:
            raise ValueError(f"{enum}: invalid original health/lightness")
        results.append(record)
    if len({r["vehicleId"] for r in results}) != 18:
        raise ValueError("Source vehicle mapping duplicates")
    return {
        "schemaVersion": 1,
        "sourceCommit": SOURCE_REVISION,
        "sourcePath": SOURCE.relative_to(ROOT).as_posix(),
        "status": "authentic_raw_vehicle_config_provisional_unity_handling_not_certified",
        "vehicles": sorted(results, key=lambda r: r["vehicleId"]),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail if checked-in manifest has drifted")
    args = parser.parse_args()
    data = extract(SOURCE.read_text(encoding="utf-8-sig"))
    expected = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    if args.check:
        if not TARGET.is_file() or TARGET.read_text(encoding="utf-8") != expected:
            raise SystemExit("Source handling manifest differs from GameManager.cs")
        print("PASS: 18 original V82 vehicle configurations match the source")
    else:
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        TARGET.write_text(expected, encoding="utf-8")
        print("Wrote 18 raw original vehicle configurations; gameplay remains provisional")


if __name__ == "__main__":
    main()
