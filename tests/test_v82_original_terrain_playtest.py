import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EDITOR = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
BATTLE = ROOT / "Assets/Scripts/Gameplay/OriginalBattleSession.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"


class OriginalTerrainPlaytestContracts(unittest.TestCase):
    def test_two_source_cars_on_real_mesh_are_dynamic_and_nonrelease(self):
        text = EDITOR.read_text("utf-8")
        for part in (
            "OriginalTerrainSourceImporter.ImportAll()",
            "LegacyVehiclePlaytestBuilder.PrepareAll()",
            "AssetDatabase.LoadAssetAtPath<Mesh>",
            "ground.AddComponent<MeshRenderer>()",
            "EnsureDiagnosticTerrainMaterial()",
            "ground.AddComponent<MeshCollider>()",
            "collider.sharedMesh = sourceTerrain",
            "Physics.SyncTransforms()",
            "FindGroundSpawns(collider, participants.Length)",
            "SpawnVehicle(playerPrefab",
            "SpawnVehicle(enemyPrefabs[i - 1]",
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

    def test_up_to_three_opponents_have_distinct_original_cars_and_pilots(self):
        text = EDITOR.read_text("utf-8")
        for part in (
            "public static void CreateBattle(",
            "enemyVehicleIds.Length > 3",
            "enemyVehicleIds.Distinct(StringComparer.Ordinal)",
            "participants.Select(v => v.characterId).Distinct(StringComparer.Ordinal)",
            "FindGroundSpawns(collider, participants.Length)",
            "SpawnVehicle(enemyPrefabs[i - 1]",
            "opponent.AddComponent<BotVehicleInput>()",
            "EditorGUILayout.IntSlider",
            "new[] { firstOpponent.id }.Concat(extraOpponents.Select(v => v.id))",
        ):
            self.assertIn(part, text)
        self.assertNotIn("GameObject.CreatePrimitive", text)
        self.assertNotIn("Assets/Resources/Arenas/", text)


    def test_source_battle_monitor_uses_real_health_and_is_editor_only(self):
        builder = EDITOR.read_text("utf-8")
        monitor = BATTLE.read_text("utf-8")
        for signal in (
            'new GameObject("ORIGINAL V82 BATTLE - DEVELOPMENT ONLY")',
            "AddComponent<OriginalBattleSession>()",
            "session.Configure(stageId, playerVehicleId",
            "opponentDrives.ToArray()",
        ):
            self.assertIn(signal, builder)
        for signal in (
            "#if UNITY_EDITOR",
            "OriginalBattleSession : MonoBehaviour",
            "public bool IsReleaseApproved => false",
            "public int RemainingOpponents",
            "OfficialMenuSelections.PilotForCar",
            "player.Health",
            "enemy.Health",
            "BattleOutcome.Victory",
            "BattleOutcome.Defeat",
            "Time.timeScale = paused ? 0f : 1f",
        ):
            self.assertIn(signal, monitor)
        self.assertNotIn("GameObject.CreatePrimitive", monitor)
        self.assertIn("OriginalBattleSession", RELEASE.read_text("utf-8"))

    def test_missing_route66_and_non_official_collision_are_explicit(self):
        text = EDITOR.read_text("utf-8")
        self.assertIn('stageId == "v82_meteor_crater"', text)
        self.assertIn("NOT ready", text)
        self.assertIn("hit.normal.y < 0.72f", text)
