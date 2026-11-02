"""Source-only CI verifies free-for-all uses real source cars, never placeholder models."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COORDINATOR = ROOT / "Assets/Scripts/Gameplay/OriginalBattleTargetCoordinator.cs"
BUILDER = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
TESTS = ROOT / "Assets/Tests/Editor/OriginalBattleTargetCoordinatorTests.cs"


class OriginalFreeForAllContract(unittest.TestCase):
    def test_dynamic_nearest_living_targets_and_real_car_identity(self):
        source = COORDINATOR.read_text("utf-8")
        for signal in (
            "#if UNITY_EDITOR",
            "public bool IsReleaseApproved => false",
            "IReadOnlyList<VehicleDrive>",
            "candidate == seeker || !candidate.IsAlive",
            "distance >= bestDistance",
            "catalog.vehicles.SingleOrDefault",
            "official.Select(v => v.characterId).Distinct(StringComparer.Ordinal)",
            "GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId",
            "bot.target = target == null ? null : target.transform",
        ):
            self.assertIn(signal, source)
        self.assertNotIn("GameObject.CreatePrimitive", source)
        self.assertNotIn("Resources.Load<GameObject>", source)

    def test_real_scene_wiring_not_published_in_windows(self):
        builder = BUILDER.read_text("utf-8")
        self.assertIn("new GameObject(\"ORIGINAL V82 BATTLE - DEVELOPMENT ONLY\")", builder)
        self.assertIn("AddComponent<OriginalBattleTargetCoordinator>()", builder)
        self.assertIn("targeting.Configure(player.GetComponent<VehicleDrive>()", builder)
        self.assertIn("EditorBuildSettings.scenes.Any", builder)
        self.assertIn("OriginalBattleTargetCoordinator", RELEASE.read_text("utf-8"))
        tests = TESTS.read_text("utf-8")
        for signal in ("BotTargetsNearestLivingOpponentNotAlwaysPlayer",
                       "DeadTargetIsSkippedAndNextLiveCarIsSelected",
                       "TiesRespectOfficialParticipantOrder",
                       "InvalidCandidatesAndDeadSeekerCannotReceiveTargets"):
            self.assertIn(signal, tests)


if __name__ == "__main__":
    unittest.main()
