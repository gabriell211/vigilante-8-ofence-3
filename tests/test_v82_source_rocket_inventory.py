"""Verify actual RocketL source ammunition and damage, not unlimited prototypes."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ORIGINAL = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/RocketL.cs"
INVENTORY = ROOT / "Assets/Scripts/Gameplay/OriginalRocketSourceInventory.cs"
BASTION = ROOT / "Assets/Scripts/Gameplay/OriginalBastionRocketLauncher.cs"
ROADRUNNER = ROOT / "Assets/Scripts/Gameplay/OriginalRoadRunnerSourceLauncher.cs"
CHARGE = ROOT / "Assets/Scripts/Gameplay/OriginalRoadRunnerSourceCharge.cs"
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
HUD = ROOT / "Assets/Scripts/Gameplay/OriginalBattleSession.cs"
EDIT_TEST = ROOT / "Assets/Tests/Editor/OriginalRocketInventorySourceTests.cs"


class VerifiedRocketLSourceContracts(unittest.TestCase):
    def test_legacy_rocket_ammo_damage_and_lifetime_are_proven(self):
        source = ORIGINAL.read_text("utf-8")
        for term in (
            "maxHalfHealth = 10;",
            "case 12:",
            "maxHalfHealth - 1",
            "arg2 != 0x422",
            "maxHalfHealth < 2",
            "maxHalfHealth - 2",
            "uVar4 = 80",
            "ppcVar1.physics2.M2 = 240;",
        ):
            self.assertIn(term, source)
        current = INVENTORY.read_text("utf-8")
        for term in (
            "#if UNITY_EDITOR",
            "InitialCharges = 10",
            "StandardRocketCost = 1",
            "RoadRunnerCost = 2",
            "RocketDamage = 80",
            "OriginalRocketLifeTicks = 240",
            "remainingCharges -= cost",
            "remainingCharges >= cost",
            "IsReleaseApproved => false",
        ):
            self.assertIn(term, current)

    def test_launcher_modes_share_real_ten_charge_inventory(self):
        for path, cost in (
            (BASTION, "OriginalRocketSourceInventory.StandardRocketCost"),
            (ROADRUNNER, "OriginalRocketSourceInventory.RoadRunnerCost"),
        ):
            text = path.read_text("utf-8")
            for term in ("GetComponent<OriginalRocketSourceInventory>()",
                         "inventory.CanSpend(", "inventory.TrySpend(", cost):
                self.assertIn(term, text, str(path))
        drive = DRIVE.read_text("utf-8")
        self.assertIn("shot.impactDamage = OriginalRocketSourceInventory.RocketDamage", drive)
        self.assertIn("OriginalRocketSourceInventory.OriginalRocketLifeTicks", drive)
        self.assertIn("flight.Configure(target, ticks)", drive)
        self.assertIn("OriginalRoadRunnerSourceCharge.AttachedSourceTicks", drive)
        charge = CHARGE.read_text("utf-8")
        for term in (
            "rocketFlightTicks = OriginalRocketSourceInventory.OriginalRocketLifeTicks",
            "rocketFlightTicks--",
            "if (rocketFlightTicks <= 0) Expire()",
            "remainingTicks--",
            "attachedTarget.TakeDamage(OriginalDamageMagnitude, owner)",
        ):
            self.assertIn(term, charge)

    def test_all_eighteen_originals_and_shipping_gate_are_enforced(self):
        builder = BUILDER.read_text("utf-8")
        self.assertIn("AddComponent<OriginalRocketSourceInventory>()", builder)
        self.assertIn("original.GetComponent<OriginalRocketSourceInventory>() == null",
                      TERRAIN.read_text("utf-8"))
        self.assertIn("prefab.GetComponent<OriginalRocketSourceInventory>() != null",
                      RELEASE.read_text("utf-8"))
        self.assertIn("rockets.RemainingCharges.ToString()", HUD.read_text("utf-8"))
        test = EDIT_TEST.read_text("utf-8")
        for name in (
            "RealRocketLStartsAtTenAndSpendsOneOrTwoCharges",
            "DestroyedCarCannotConsumeSourceRockets",
            "AllEighteenAuthenticCarPrototypesShareOneRocketLInventory",
        ):
            self.assertIn(name, test)


if __name__ == "__main__":
    unittest.main()
