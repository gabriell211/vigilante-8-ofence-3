import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AI = ROOT / "Assets/Scripts/Gameplay/BotVehicleInput.cs"
UNITY_TEST = ROOT / "Assets/Tests/Editor/BotVehicleSourceTerrainTests.cs"


class OriginalSourceTerrainBotContracts(unittest.TestCase):
    def test_ground_is_not_an_obstacle_or_a_spurious_shot_target(self):
        source = AI.read_text("utf-8")
        for signal in (
            "Physics.SphereCastNonAlloc",
            "IsDriveableSurface(hit.normal)",
            "Physics.RaycastNonAlloc",
            "hit.transform.IsChildOf(targetDrive.transform)",
            "targetDrive == null || !targetDrive.IsAlive",
            "vehicle.SetControls(0f, 0f, 1f, false)",
            "QueryTriggerInteraction.Ignore",
        ):
            self.assertIn(signal, source)
        self.assertNotIn("Physics.SphereCast(", source)
        self.assertNotIn("Physics.RaycastAll(", source)
        self.assertIn("IsDriveableSurface(Vector3.up)", UNITY_TEST.read_text("utf-8"))
        self.assertIn("IsDriveableSurface(Vector3.right)", UNITY_TEST.read_text("utf-8"))


if __name__ == "__main__":
    unittest.main()
