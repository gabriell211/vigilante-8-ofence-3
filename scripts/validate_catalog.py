#!/usr/bin/env python3
"""Validate unique V8 + V8:2 metadata without importing proprietary assets."""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
CATALOG = ROOT / "Assets/Resources/Catalog/roster.json"
SAFE_ID = re.compile(r"^[a-z0-9_]+$")


def validate(path=CATALOG):
    data = json.loads(pathlib.Path(path).read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1:
        raise ValueError("Unsupported schema")
    expected = {"characters": 23, "vehicles": 31, "arenas": 19}
    for entity, count in expected.items():
        records = data.get(entity)
        if not isinstance(records, list) or len(records) != count:
            raise ValueError(f"Expected {count} {entity}")
        ids = [item.get("id") for item in records]
        if len(ids) != len(set(ids)) or any(not isinstance(i, str) or not SAFE_ID.fullmatch(i) for i in ids):
            raise ValueError(f"Duplicate or unsafe ID in {entity}")

    games = {g["id"] for g in data["games"]}
    if games != {"v8", "v82"}:
        raise ValueError("Unexpected game IDs")
    characters = {c["id"]: c for c in data["characters"]}
    known_names = set()
    for character in data["characters"]:
        appearances = character.get("appearances", [])
        if not appearances or len(appearances) != len(set(appearances)) or not set(appearances) <= games:
            raise ValueError("Invalid character appearances")
        for name in [character["name"], *character.get("aliases", [])]:
            key = name.casefold().strip()
            if not key or key in known_names:
                raise ValueError(f"Duplicate identity or alias: {name}")
            known_names.add(key)

    vehicle_names = set()
    for vehicle in data["vehicles"]:
        driver = characters.get(vehicle["characterId"])
        if driver is None or vehicle["game"] not in driver["appearances"]:
            raise ValueError(f"Vehicle has missing or out-of-game driver: {vehicle['id']}")
        if not SAFE_ID.fullmatch(vehicle["assetId"]):
            raise ValueError("Unsafe vehicle asset ID")
        name = vehicle["name"].casefold().strip()
        if not name or name in vehicle_names:
            raise ValueError(f"Repeated vehicle: {name}")
        vehicle_names.add(name)

    for arena in data["arenas"]:
        if arena["game"] not in games or not SAFE_ID.fullmatch(arena["assetId"]):
            raise ValueError(f"Invalid arena: {arena['id']}")

    for game, expected_drivers, expected_cars, expected_arenas in [
        ("v8", 13, 13, 11), ("v82", 18, 18, 8)
    ]:
        if sum(game in c["appearances"] for c in data["characters"]) != expected_drivers:
            raise ValueError(f"Wrong driver count: {game}")
        if sum(v["game"] == game for v in data["vehicles"]) != expected_cars:
            raise ValueError(f"Wrong vehicle count: {game}")
        if sum(a["game"] == game for a in data["arenas"]) != expected_arenas:
            raise ValueError(f"Wrong arena count: {game}")

    official = json.loads((ROOT / "tests/fixtures/official_content.json").read_text("utf-8"))
    expected_characters = official["characters"]
    actual_characters = {c["id"]: {"name": c["name"], "appearances": c["appearances"]}
                         for c in data["characters"]}
    if actual_characters != expected_characters:
        raise ValueError("Official character roster mismatch")
    actual_vehicles = {v["id"]: {k: v[k] for k in ("name", "characterId", "game", "officialName")}
                       for v in data["vehicles"]}
    if actual_vehicles != official["vehicles"]:
        raise ValueError("Official vehicles or driver associations mismatch")
    actual_arenas = {a["id"]: {k: a[k] for k in ("name", "game", "bonus", "platforms")}
                     for a in data["arenas"]}
    if actual_arenas != official["arenas"]:
        raise ValueError("Official stages, platforms or N64 bonus mismatch")
    if sum(a["bonus"] for a in data["arenas"]) != 1:
        raise ValueError("Expected exactly one N64 exclusive bonus stage")
    return data


if __name__ == "__main__":
    content = validate()
    print(f"PASS: {len(content['characters'])} unique characters, "
          f"{len(content['vehicles'])} unique vehicles, "
          f"{len(content['arenas'])} arenas, no repeated identities")
