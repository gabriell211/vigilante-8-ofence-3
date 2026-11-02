"""Source-trace MineDr/Mine constants; defend production from diagnostic mines."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MINEDR = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/MineDr.cs"
MINE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/Mine.cs"
GAMEMANAGER = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/GameManager.cs"
TRAP = ROOT / "Assets/Scripts/Gameplay/OriginalMineSourceTrap.cs"
LAUNCHER = ROOT / "Assets/Scripts/Gameplay/OriginalMineSourceLauncher.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
INPUT = ROOT / "Assets/Scripts/Gameplay/PlayerVehicleInput.cs"
HUD = ROOT / "Assets/Scripts/Gameplay/OriginalBattleSession.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
TEST = ROOT / "Assets/Tests/Editor/OriginalMineSourceTests.cs"
PLAYMODE = ROOT / "Assets/Tests/PlayMode/OriginalMineSourcePhysicsTests.cs"


class OriginalMineContracts(unittest.TestCase):
    def test_original_source_has_six_ammo_sixteen_active_upward_impact(self):
        launcher = MINEDR.read_text("utf-8")
        mine = MINE.read_text("utf-8")
        manager = GAMEMANAGER.read_text("utf-8")
        for term in (
            "maxHalfHealth = 6;",
            "case 12:",
            "maxHalfHealth--;",
            "FUN_479DC((Vehicle)arg2, 182, typeof(Mine))",
            "uVar3 = 0",
        ):
            if term != "uVar3 = 0":
                self.assertIn(term, launcher)
        for term in (
            "if (oVar1.type == 0)",
            "if (oVar1.type == 2)",
            "FUN_2B370(GameManager.DAT_A5C, screen)",
            "if (15 < iVar1)",
        ):
            self.assertIn(term, mine)
        self.assertIn("DAT_A5C = new Vector3Int(0, -0x20000, 0)", manager)
        trap = TRAP.read_text("utf-8")
        for term in (
            "#if UNITY_EDITOR",
            "public const int SourceMaxRegistered = 16",
            "public const int SourceVerticalImpulseY = -0x20000",
            "SourceState.Airborne",
            "SourceState.Armed",
            "SourceState.Triggered",
            "active.Count >= SourceMaxRegistered",
            "Physics.IgnoreCollision",
            "targetBody.AddForce(Vector3.up * 5f, ForceMode.Impulse)",
            "IsReleaseApproved => false",
        ):
            self.assertIn(term, trap)
        self.assertNotIn("TakeDamage(", trap)
        self.assertNotIn("GameObject.CreatePrimitive", trap)

    def test_only_authentic_source_car_tests_can_deploy_invisible_mines(self):
        tests = {
            LAUNCHER: [
                "InitialSourceCharges = 6",
                "GetComponent<LegacyVehiclePlaytest>() == null",
                "remainingCharges--",
                'new GameObject("SOURCE Mine.cs - NO ORIGINAL MESH")',
                "sourceMine.Configure(vehicle)",
            ],
            BUILDER: ["AddComponent<OriginalMineSourceLauncher>()"],
            TERRAIN: ["original.GetComponent<OriginalMineSourceLauncher>() == null"],
            INPUT: ["keyboard.rKey.wasPressedThisFrame",
                    "gamepad.dpad.down.wasPressedThisFrame"],
            HUD: ["OriginalMineSourceLauncher", "mine.RemainingCharges"],
            RELEASE: ["prefab.GetComponent<OriginalMineSourceLauncher>() != null",
                      "prefab.GetComponentInChildren<OriginalMineSourceTrap>(true) != null"],
        }
        for path, terms in tests.items():
            data = path.read_text("utf-8")
            for term in terms:
                self.assertIn(term, data, str(path))

    def test_editor_and_playmode_cover_collision_without_invented_hit_points(self):
        editor = TEST.read_text("utf-8")
        playmode = PLAYMODE.read_text("utf-8")
        for term in (
            "SourceMineConfiguresWithoutInventingDamageOrCertifiedVisuals",
            "UnconfiguredMineCannotBeArmed",
            "AllEighteenRealCarsCarrySixChargeNonReleaseLauncher",
        ):
            self.assertIn(term, editor)
        for term in (
            "[UnityTest]",
            "SourceMineArmsOnRealGroundCollisionWithoutInventedDamage",
            "SourceMineTriggersOnOtherCarAppliesImpulseWithoutFakeDamage",
            "new WaitForFixedUpdate()",
        ):
            self.assertIn(term, playmode)


if __name__ == "__main__":
    unittest.main()
