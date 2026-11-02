"""Prevent fabricated normal-missile ammo costs; trace only real 0x442 Halo."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEGACY = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts"
INV = ROOT / "Assets/Scripts/Gameplay/OriginalMissileSourceInventory.cs"
FLIGHT = ROOT / "Assets/Scripts/Gameplay/OriginalMissileSourceFlight.cs"
LAUNCHER = ROOT / "Assets/Scripts/Gameplay/OriginalMissileSourceLauncher.cs"
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
PROJECTILE = ROOT / "Assets/Scripts/Gameplay/Projectile.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
INPUT = ROOT / "Assets/Scripts/Gameplay/PlayerVehicleInput.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
EDITOR = ROOT / "Assets/Tests/Editor/OriginalMissileHaloSourceTests.cs"
PLAYMODE = ROOT / "Assets/Tests/PlayMode/OriginalMissileHaloPhysicsTests.cs"


class MissileHaloSourceContracts(unittest.TestCase):
    def test_legitimate_missilel_special_branch_does_not_invent_normal_ammo_cost(self):
        source = (LEGACY / "MissileL.cs").read_text("utf-8")
        missile = (LEGACY / "Missile.cs").read_text("utf-8")
        for s in (
            "maxHalfHealth = 12",
            "arg2 == 0x442",
            "if (1 < maxHalfHealth)",
            "maxHalfHealth--;",
            "mVar6.flags |= 0x41000000",
        ):
            self.assertIn(s, source)
        normal_case = source.split("case 12:", 1)[1].split("case 13:", 1)[0]
        self.assertNotIn("maxHalfHealth--", normal_case,
                         "Never claim a normal missile charge decrement from UpdateW(12)")
        for s in (
            "_MISSILE_TYPE.Halo",
            "state = _MISSILE_TYPE.Halo",
            "(flags & 0x1000000) != 0",
            "hit.self.type != 8",
            "FUN_24700(0, 0x100, 0)",
            "physics2.M2 = 480",
        ):
            self.assertIn(s, missile)
        ammo = INV.read_text("utf-8")
        for s in (
            "#if UNITY_EDITOR",
            "InitialSourceCharges = 12",
            "HaloSpecialCost = 1",
            "remainingCharges > HaloSpecialCost",
            "remainingCharges -= HaloSpecialCost",
            "IsReleaseApproved => false",
        ):
            self.assertIn(s, ammo)

    def test_halo_state_is_distinct_from_existing_guided_missile(self):
        f = FLIGHT.read_text("utf-8")
        for term in (
            "SourceState { Shell, Projectile, Halo }",
            "HaloYawSourceAngle = 0x100",
            "HaloYawDegreesPerTick",
            "public void ActivateHalo()",
            "IgnoreDiagnosticProjectileContact",
            "state == SourceState.Halo",
            "Quaternion.AngleAxis(",
            "body.linearVelocity = rotated",
        ):
            self.assertIn(term, f)
        c = PROJECTILE.read_text("utf-8")
        self.assertIn("halo.IgnoreDiagnosticProjectileContact(collision.collider)", c)
        self.assertIn("Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider)", c)
        self.assertLess(c.index("halo.IgnoreDiagnosticProjectileContact"),
                        c.index("impacted = true;"))
        self.assertNotIn("GameObject.CreatePrimitive", f)

    def test_source_mode_is_wired_into_real_car_only_and_blocked_from_release(self):
        checks = {
            LAUNCHER: ["TryLaunchHalo()", "GetComponent<OriginalMissileSourceInventory>()",
                       "!ammo.CanFireHalo", "vehicle.LaunchHaloSourceMissile()"],
            DRIVE: ["LaunchHaloSourceMissile()", "shot.Launch(this, muzzle.forward)",
                    "flight.ActivateHalo()", "flight.Configure(this)"],
            BUILDER: ["AddComponent<OriginalMissileSourceInventory>()"],
            TERRAIN: ["original.GetComponent<OriginalMissileSourceInventory>() == null"],
            INPUT: ["keyboard.hKey.wasPressedThisFrame",
                    "gamepad.dpad.right.wasPressedThisFrame"],
            RELEASE: ["prefab.GetComponent<OriginalMissileSourceInventory>() != null"],
        }
        for p, terms in checks.items():
            text = p.read_text("utf-8")
            for term in terms:
                self.assertIn(term, text, str(p))

    def test_real_unity_tests_cover_state_health_ammo_and_trajectory(self):
        edit = EDITOR.read_text("utf-8")
        play = PLAYMODE.read_text("utf-8")
        for phrase in (
            "TwelveOriginalSourceChargesKeepLastChargeForHalo",
            "DestroyedVehicleCannotSpendHaloCharges",
            "HaloIsNotAGuidedProjectileAndChangesModeOnce",
            "AllEighteenAuthenticV82CarsGetMissileLSourceInventoryOnlyInEditor",
        ):
            self.assertIn(phrase, edit)
        for phrase in (
            "HaloMovesActualRigidbodyWithSourceYawNotGuidedTarget",
            "HaloIgnoresOtherDiagnosticProjectileClassOnly",
            "new WaitForFixedUpdate()",
        ):
            self.assertIn(phrase, play)


if __name__ == "__main__":
    unittest.main()
