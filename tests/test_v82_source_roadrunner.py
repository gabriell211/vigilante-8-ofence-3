"""Archived Rocket.RoadRunner must attach, wait 360 ticks and apply -40 once."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEGACY_ROCKET = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/Rocket.cs"
LEGACY_VEHICLE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/Vehicle.cs"
CHARGE = ROOT / "Assets/Scripts/Gameplay/OriginalRoadRunnerSourceCharge.cs"
LAUNCHER = ROOT / "Assets/Scripts/Gameplay/OriginalRoadRunnerSourceLauncher.cs"
PROJECTILE = ROOT / "Assets/Scripts/Gameplay/Projectile.cs"
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
INPUT = ROOT / "Assets/Scripts/Gameplay/PlayerVehicleInput.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
EDIT_TEST = ROOT / "Assets/Tests/Editor/OriginalRoadRunnerSourceTests.cs"
PLAY_TEST = ROOT / "Assets/Tests/PlayMode/OriginalRoadRunnerSourcePhysicsTests.cs"


class SourceRoadRunnerContract(unittest.TestCase):
    def test_authentic_source_uses_sticky_vehicle_mode_and_delayed_40(self):
        rocket = LEGACY_ROCKET.read_text("utf-8")
        vehicle = LEGACY_VEHICLE.read_text("utf-8")
        for term in (
            "case _ROCKET_TYPE.RoadRunner:",
            "(flags & 0x1000000) != 0",
            "state = _ROCKET_TYPE.RoadRunner",
            "physics2.M2 = 360",
            "transform.parent = oVar2.transform",
            "vVar2.FUN_3A020(-40, vTransform.position, true)",
        ):
            self.assertIn(term, rocket)
        self.assertIn("public bool FUN_3A020(int param1, Vector3Int param2, bool param3)",
                      vehicle)
        self.assertIn("bVar1 = FUN_39DCC(param1, param2, param3)", vehicle)

    def test_projectile_intercepts_sticky_hit_without_immediate_damage(self):
        sticky = CHARGE.read_text("utf-8")
        projectile = PROJECTILE.read_text("utf-8")
        for term in (
            "#if UNITY_EDITOR",
            "SourceState { Rocket, RoadRunner, Expired }",
            "AttachedSourceTicks = 360",
            "OriginalDamageMagnitude = 40",
            "TryAttach(VehicleDrive victim)",
            "collider.enabled = false",
            "body.isKinematic = true",
            "transform.SetParent(victim.transform, true)",
            "attachedTarget.TakeDamage(OriginalDamageMagnitude, owner)",
            "remainingTicks--",
            "IsReleaseApproved => false",
        ):
            self.assertIn(term, sticky)
        self.assertIn("sticky.TryAttach(collision.collider.GetComponentInParent<VehicleDrive>())",
                      projectile)
        self.assertLess(projectile.index("sticky.TryAttach("),
                        projectile.index("impacted = true;"),
                        "Sticky state must suppress normal projectile collision")
        self.assertNotIn("GameObject.CreatePrimitive", sticky)
        self.assertNotIn("Resources/Vehicles/", sticky)

    def test_only_real_editor_car_can_use_the_source_sticky_rocket(self):
        checks = {
            LAUNCHER: [
                "GetComponent<LegacyVehiclePlaytest>() == null",
                "vehicle.LaunchSourceRoadRunner()",
                "IsReleaseApproved => false",
            ],
            DRIVE: [
                "LaunchSourceRoadRunner()",
                "GetComponent<OriginalRoadRunnerSourceLauncher>() == null",
                "AddComponent<OriginalRoadRunnerSourceCharge>().Configure(this)",
                "shot.Launch(this, muzzle.forward)",
            ],
            BUILDER: ["AddComponent<OriginalRoadRunnerSourceLauncher>()"],
            TERRAIN: ["original.GetComponent<OriginalRoadRunnerSourceLauncher>() == null"],
            INPUT: [
                "keyboard.yKey.wasPressedThisFrame",
                "gamepad.buttonWest.wasPressedThisFrame",
            ],
            RELEASE: [
                "prefab.GetComponent<OriginalRoadRunnerSourceLauncher>() != null",
                "prefab.GetComponentInChildren<OriginalRoadRunnerSourceCharge>(true) != null",
            ],
        }
        for path, terms in checks.items():
            data = path.read_text("utf-8")
            for term in terms:
                self.assertIn(term, data, str(path))

    def test_real_editor_and_playmode_fixtures_are_authorized(self):
        edit = EDIT_TEST.read_text("utf-8")
        play = PLAY_TEST.read_text("utf-8")
        for marker in (
            "SourceRocketAttachesOnceAndSuppressesImmediateDamage",
            "SourceRocketNeverSticksToItselfOrDeadTarget",
            "AllEighteenRealCarMeshesHaveNonreleaseRoadRunnerSourceLauncher",
        ):
            self.assertIn(marker, edit)
        for marker in (
            "[UnityTest]",
            "PhysicalRocketCollisionAttachesAndDamagesOnlyAfter360Ticks",
            "EliminatedHostCannotReceiveSecondDelayedHit",
            "new WaitForFixedUpdate()",
        ):
            self.assertIn(marker, play)


if __name__ == "__main__":
    unittest.main()
