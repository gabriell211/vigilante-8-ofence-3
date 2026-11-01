#!/usr/bin/env python3
"""Verify physical coverage of the *official* V8/V82 catalog in archived GitHub sources.

Legacy source files are evidence of source availability, NEVER a Unity 6 game
build, imported playable prefab, complete weapon implementation or licensing proof.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(root: Path = ROOT) -> dict:
    from validate_catalog import validate

    roster = validate(root / "Assets/Resources/Catalog/roster.json")
    sources = json.loads((root / "docs/official-asset-sources.json").read_text("utf-8"))
    if sources.get("schemaVersion") != 1:
        raise ValueError("Unsupported source map version")

    official_vehicles = {v["id"] for v in roster["vehicles"]}
    official_arenas = {a["id"] for a in roster["arenas"]}
    sequel_vehicles = {v["id"] for v in roster["vehicles"] if v["game"] == "v82"}
    sequel_arenas = {a["id"] for a in roster["arenas"] if a["game"] == "v82"}
    if set(sources["vehicles"]) != sequel_vehicles:
        raise ValueError("Sequel source mapping has missing, extra or duplicate vehicles")
    if set(sources["arenas"]) != sequel_arenas:
        raise ValueError("Sequel source mapping has missing, extra or duplicate arenas")
    if sources["n64Bonus"]["id"] != "v8_super_dreamland_64":
        raise ValueError("N64 bonus must have its exact official identity")

    paths = []
    for group in ("vehicles", "arenas"):
        for entry in sources[group].values():
            if entry["status"] != "legacy_source_verified_not_ported":
                raise ValueError("Unverified claim about legacy source migration status")
            paths.append(entry["sourcePath"])
    paths.append(sources["n64Bonus"]["sourcePath"])
    paths.extend(sources["weaponScripts"])
    if len(paths) != len(set(paths)) or not sources["weaponScripts"]:
        raise ValueError("Duplicate sources or missing weapon scripts")
    for text in paths:
        path = Path(text)
        if path.is_absolute() or ".." in path.parts:
            raise ValueError(f"Unsafe source path: {text}")
        if not str(path).startswith("LegacySources/"):
            raise ValueError(f"Unexpected location for archived reference: {text}")
        if not (root / path).is_file():
            raise ValueError(f"Missing GitHub-tracked source material: {text}")

    prefab_vehicles = sum((root / "Assets/Resources/Vehicles" / (v["assetId"] + ".prefab")).is_file()
                          for v in roster["vehicles"])
    prefab_arenas = sum((root / "Assets/Resources/Arenas" / (a["assetId"] + ".prefab")).is_file()
                        for a in roster["arenas"])
    return {
        "officialCharacters": len(roster["characters"]),
        "officialVehicles": len(official_vehicles),
        "officialArenas": len(official_arenas),
        "playstationArenas": 18,
        "n64ExclusiveArenas": 1,
        "sequelOriginalVehiclePrefabSources": len(sources["vehicles"]),
        "sequelOriginalSceneSources": len(sources["arenas"]),
        "legacyWeaponScriptFiles": len(sources["weaponScripts"]),
        "n64BonusConverterReference": True,
        "firstGameRetailMediaBundled": False,
        "activeUnity6VehiclePrefabs": prefab_vehicles,
        "activeUnity6ArenaPrefabs": prefab_arenas,
        "playerReady": prefab_vehicles == 31 and prefab_arenas == 19,
        "scope": "source availability and canonical roster only; no Unity Editor/build proof",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strict-player", action="store_true",
                        help="fail when any official Unity 6 runtime prefab is not present")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    report = verify()
    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    else:
        print("PASS official catalog/source audit: 23 unique characters, 31 cars, 19 stages")
        print(f"Verified: {report['sequelOriginalVehiclePrefabSources']} legacy vehicle prefabs, "
              f"{report['sequelOriginalSceneSources']} legacy stages, "
              f"{report['legacyWeaponScriptFiles']} weapon scripts and N64 bonus tooling")
        print(f"Unity 6 playable prefab presence: "
              f"{report['activeUnity6VehiclePrefabs']}/31 cars, "
              f"{report['activeUnity6ArenaPrefabs']}/19 arenas")
        print("Availability does NOT certify complete PC gameplay or actual Unity compilation.")

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as out:
            out.write("### Official content coverage (not gameplay certification)\n\n")
            out.write(f"23 characters, 31 vehicles, 19 official arenas "
                      f"(18 PlayStation + Super Dreamland 64 on N64).\n\n")
            out.write(f"Legacy sources: {report['sequelOriginalVehiclePrefabSources']} vehicle prefabs, "
                      f"{report['sequelOriginalSceneSources']} scenes, "
                      f"{report['legacyWeaponScriptFiles']} weapon scripts.\n\n")
            out.write(f"**Unity 6 ready prefabs:** {report['activeUnity6VehiclePrefabs']}/31 vehicles, "
                      f"{report['activeUnity6ArenaPrefabs']}/19 arenas.\n\n")
            out.write("Legacy source assets are not proof of a working or finished PC build.\n")
    return int(args.strict_player and not report["playerReady"])


if __name__ == "__main__":
    raise SystemExit(main())
