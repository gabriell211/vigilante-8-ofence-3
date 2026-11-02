import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EDITOR = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"


class OriginalTerrainPlaytestContracts(unittest.TestCase):
    def test_two_source_cars_on_real_mesh_are_dynamic_and_nonrelease(self):
        text = EDITOR.read_text("utf-8")
        for part in (
            "OriginalTerrainSourceImporter.ImportAll()",
            "LegacyVehiclePlaytestBuilder.PrepareAll()",
            "AssetDatabase.LoadAssetAtPath<Mesh>",
            "new MeshCollider",
            "collider.sharedMesh = sourceTerrain",
            "Physics.SyncTransforms()",
            "FindGroundSpawns(collider)",
            "SpawnVehicle(playerPrefab",
            "SpawnVehicle(enemyPrefab",
            "PlayerVehicleInput",
            "BotVehicleInput",
            "EditorGUILayout.Popup",
            "EditorBuildSettings.scenes.Any",
            "OriginalTerrainPreviewIdentity",
        ):
            self.assertIn(part, text)
        self.assertNotIn("GameObject.CreatePrimitive", text)
        self.assertNotIn("new BoxCollider", text)
        self.assertNotIn("Assets/Resources/Arenas/", text)

    def test_missing_route66_and_non_official_collision_are_explicit(self):
        text = EDITOR.read_text("utf-8")
        self.assertIn('stageId == "v82_meteor_crater"', text)
        self.assertIn("NOT ready", text)
        self.assertIn("hit.normal.y < 0.72f", text)
