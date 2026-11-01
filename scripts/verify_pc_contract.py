#!/usr/bin/env python3
"""Static Windows contract checks. Does NOT claim to compile or run Unity."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_GAMEPLAY = (
    "VehicleDrive.cs",
    "Projectile.cs",
    "PlayerVehicleInput.cs",
    "BotVehicleInput.cs",
    "ChaseCamera.cs",
    "ArenaSpawnPoints.cs",
    "MatchBootstrap.cs",
)


def verify(root: Path = ROOT) -> None:
    package = json.loads((root / "Packages/manifest.json").read_text(encoding="utf-8"))
    dependencies = package["dependencies"]
    expected = {
        "com.unity.inputsystem": "1.20.1",
        "com.unity.render-pipelines.universal": "17.6.0",
        "com.unity.test-framework": "1.8.0",
    }
    for name, version in expected.items():
        if dependencies.get(name) != version:
            raise ValueError(f"Unexpected Unity version: {name}={dependencies.get(name)}")

    version = (root / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    if "m_EditorVersion: 6000.6.7f1" not in version:
        raise ValueError("Unity Editor version not pinned to 6000.6.7f1")

    runtime = root / "Assets/Scripts/Gameplay"
    for filename in REQUIRED_GAMEPLAY:
        if not (runtime / filename).is_file():
            raise ValueError(f"Missing runtime source: {filename}")

    game_code = "\n".join((runtime / p).read_text(encoding="utf-8")
                          for p in REQUIRED_GAMEPLAY)
    forbidden = ("GameObject.CreatePrimitive", "AssetDatabase.", "System.IO.File.Read",
                 "File.ReadAllBytes", "Process.Start(", ".bin", ".cue")
    for fragment in forbidden:
        if fragment in game_code:
            raise ValueError(f"Prohibited temporary/runtime dependency: {fragment}")

    if not (root / "Assets/Scripts/Catalog/OfficialAssetIdentity.cs").is_file():
        raise ValueError("Missing official per-prefab identity component")
    if not (root / "Assets/Editor/OfficialPrefabIdentityEditor.cs").is_file():
        raise ValueError("Missing official identity binding editor")

    editor = (root / "Assets/Editor/WindowsReleaseBuilder.cs").read_text(encoding="utf-8")
    tokens = ("RequiredWindowsAssetsGate", "BuildPipeline.BuildPlayer",
              "BuildTarget.StandaloneWindows64", "ScriptingImplementation.IL2CPP",
              "ZipFile.CreateFromDirectory", "SHA256.Create",
              "foreach (VehicleEntry vehicle in catalog.vehicles)",
              "foreach (ArenaEntry arena in catalog.arenas)",
              "HasValidSpawns(4)", "!drive.HasWeapon", "identity.Matches(vehicle.id",
              "identity.Matches(arena.id", "seenVehicleBodyMeshes", "seenArenaMeshes")
    for token in tokens:
        if token not in editor:
            raise ValueError(f"Missing build gate contract: {token}")

    from validate_catalog import validate
    catalog = validate(root / "Assets/Resources/Catalog/roster.json")
    if len(catalog["characters"]) != 23 or len(catalog["vehicles"]) != 31:
        raise ValueError("Unexpected unified game content counts")


if __name__ == "__main__":
    verify()
    print("PASS: Native Windows packaging contract; static checks only, not a Unity build")
