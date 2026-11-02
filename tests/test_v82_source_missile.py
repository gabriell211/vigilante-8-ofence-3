"""Preserve authenticated Missile/MissileL source rules in editor-only physics."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/Missile.cs"
LAUNCH_SOURCE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/MissileL.cs"
FLIGHT = ROOT / "Assets/Scripts/Gameplay/OriginalMissileSourceFlight.cs"
LAUNCH = ROOT / "Assets/Scripts/Gameplay/OriginalMissileSourceLauncher.cs"
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
TARGETS = ROOT / "Assets/Scripts/Gameplay/OriginalBattleTargetCoordinator.cs"
INPUT = ROOT / "Assets/Scripts/Gameplay/PlayerVehicleInput.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
EDITOR_TEST = ROOT / "Assets/Tests/Editor/OriginalMissileSourceTests.cs"
PLAYMODE = ROOT / "Assets/Tests/PlayMode/OriginalMissileSourcePhysicsTests.cs"


class OfficialMissileSourceContract(unittest.TestCase):
    def test_legitimate_original_damage_duration_and_steering_rules(self):
        source = SOURCE.read_text("utf-8")
        launcher = LAUNCH_SOURCE.read_text("utf-8")
        port = FLIGHT.read_text("utf-8")
        for signal in (
            "Shell, //FUN_441D4",
            "Projectile,  //FUN_438F4",
            "state = _MISSILE_TYPE.Projectile",
            "physics2.M2 = 480",
            "iVar2 >>= 4",
            "physics1.W += iVar2 >> 3",
            "iVar5 = -0x100",
            "iVar5 = 0x100",
        ):
            self.assertIn(signal, source)
        self.assertIn("maxHalfHealth = 12", launcher)
        self.assertIn("uVar8 = 60", launcher)
        for signal in (
            "#if UNITY_EDITOR",
            "public enum SourceState { Shell, Projectile }",
            "public const int OriginalDamage = 60;",
            "public const int OriginalLifeTicks = 480;",
            "public const int HorizontalErrorDivisor = 16;",
            "public const int VerticalErrorDivisor = 8;",
            "state = SourceState.Shell",
            "state = SourceState.Projectile",
            "ticksRemaining--",
            "body.linearVelocity = velocity + CalculateSteeringDelta(velocity, desired)",
            "lockedTarget.IsAlive",
            "IsReleaseApproved => false",
        ):
            self.assertIn(signal, port)
        self.assertNotIn("GameObject.CreatePrimitive", port)

    def test_only_genuine_source_car_test_can_fire_and_build_fails_closed(self):
        checks = {
            LAUNCH: ["GetComponent<LegacyVehiclePlaytest>() == null",
                     "vehicle.LaunchSourceMissile(lockedTarget)", "!lockedTarget.IsAlive"],
            DRIVE: ["LaunchSourceMissile(VehicleDrive target)",
                    "shot.impactDamage = OriginalMissileSourceFlight.OriginalDamage",
                    "OriginalMissileSourceFlight.OriginalLifeTicks * Time.fixedDeltaTime",
                    "AddComponent<OriginalMissileSourceFlight>()",
                    "shot.Launch(this, muzzle.forward)", "flight.ActivateProjectile()"],
            BUILDER: ["AddComponent<OriginalMissileSourceLauncher>()",
                      "GetComponent<LegacyVehiclePlaytest>()"],
            TERRAIN: ["original.GetComponent<OriginalMissileSourceLauncher>() == null"],
            TARGETS: ["playerMissile.SetTarget(SelectTarget(player, participants))",
                      "missile.SetTarget(target)"],
            INPUT: ["keyboard.qKey.wasPressedThisFrame",
                    "gamepad.dpad.up.wasPressedThisFrame"],
            RELEASE: ["prefab.GetComponent<OriginalMissileSourceLauncher>() != null",
                      "prefab.GetComponentInChildren<OriginalMissileSourceFlight>(true) != null"],
        }
        for path, terms in checks.items():
            text = path.read_text("utf-8")
            for term in terms:
                self.assertIn(term, text, str(path))
        self.assertNotIn("Resources/Vehicles/", LAUNCH.read_text("utf-8"))

    def test_unity_editor_and_playmode_scenarios_are_explicit(self):
        editor = EDITOR_TEST.read_text("utf-8")
        playmode = PLAYMODE.read_text("utf-8")
        for term in (
            "ShellTransitionsOnceToGuidedProjectileWithSourceLifetime",
            "GuidedErrorCorrectionUsesOriginalSixteenAndEightDivisors",
            "AllEighteenRealCarsCarryOnlyDevelopmentMissileLaunchers",
        ):
            self.assertIn(term, editor)
        for term in (
            "[UnityTest]",
            "RealMissileRigidbodySteersTowardLockedLivingSourceCar",
            "LostOrDestroyedTargetCausesSafeExpiration",
            "new WaitForFixedUpdate()",
        ):
            self.assertIn(term, playmode)


if __name__ == "__main__":
    unittest.main()
