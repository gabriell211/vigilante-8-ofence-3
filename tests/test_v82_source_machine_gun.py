"""Verify prototype MGun state values are derived from archived V82 C#."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/MGun.cs"
BULLET = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/Bullet.cs"
PORT = ROOT / "Assets/Scripts/Gameplay/OriginalMachineGunSourceTest.cs"
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
UNITY_TEST = ROOT / "Assets/Tests/Editor/OriginalMachineGunSourceTests.cs"


class ArchivedMachineGunSourceContracts(unittest.TestCase):
    def test_constants_and_cadence_are_not_invented(self):
        src = SOURCE.read_text("utf-8")
        bullet = BULLET.read_text("utf-8")
        port = PORT.read_text("utf-8")
        for legacy in (
            "maxHalfHealth = 1280",
            "iVar6 = maxHalfHealth - 64",
            "maxHalfHealth >> 8",
            "maxHalfHealth += 32",
            "ppcVar3.maxHalfHealth = uVar4",
            "uVar4 = 7",
            "uVar4 = 14",
            "ppcVar3.physics2.M2 = 45",
        ):
            self.assertIn(legacy, src)
        self.assertIn("physics2.M2 = (short)(sVar1 - 1)", bullet)
        for canonical in (
            "#if UNITY_EDITOR",
            "public const int InitialHeat = 0x500;",
            "public const int NormalBulletDamage = 7;",
            "public const int DoubleDamageBullet = 14;",
            "public const int BulletLifeSourceTicks = 45;",
            "sourceTags = unchecked((sbyte)(sourceTags - 1))",
            "sourceTags = unchecked((sbyte)(sourceHeat >> 8))",
            "sourceHeat = unchecked((ushort)(sourceHeat + 32))",
            "sourceHeat = (ushort)Mathf.Max(InitialHeat, sourceHeat - 64)",
            "public bool IsReleaseApproved => false;",
        ):
            self.assertIn(canonical, port)
        self.assertNotIn("GameObject.CreatePrimitive", port)
        self.assertNotIn("AudioSource.Play", port)

    def test_exact_original_mgun_is_bound_only_to_authentic_car_prototypes(self):
        builder = BUILDER.read_text("utf-8")
        drive = DRIVE.read_text("utf-8")
        terrain = TERRAIN.read_text("utf-8")
        for term in (
            "instance.GetComponent<LegacyVehiclePlaytest>()",
            "AddComponent<OriginalMachineGunSourceTest>()",
            "Original special weapons",
        ):
            if term != "Original special weapons":
                self.assertIn(term, builder)
        self.assertIn("original.GetComponent<OriginalMachineGunSourceTest>() == null", terrain)
        self.assertIn("sourceMachineGun.TryFire(firing)", drive)
        self.assertIn("shot.impactDamage = OriginalMachineGunSourceTest.NormalBulletDamage", drive)
        self.assertIn("OriginalMachineGunSourceTest.BulletLifeSourceTicks * Time.fixedDeltaTime", drive)
        self.assertIn("#if UNITY_EDITOR", drive)
        self.assertNotIn("Assets/Resources/Vehicles/", builder)
        self.assertIn("prefab.GetComponent<OriginalMachineGunSourceTest>() != null",
                      RELEASE.read_text("utf-8"))
        tests = UNITY_TEST.read_text("utf-8")
        for term in ("FirstShotAndTagsFollowArchivedMGunUpdateW12Exactly",
                     "TriggerReleaseDoesNotInventASourceWeaponSwitch",
                     "EveryArchivedOriginalV82CarHasSourceMachineGunOnlyInPlaytest"):
            self.assertIn(term, tests)


if __name__ == "__main__":
    unittest.main()
