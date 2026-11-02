"""Source provenance and nonrelease contracts for original three special mines."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEGACY = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts"
MINE_DR = LEGACY / "MineDr.cs"
BEAR = LEGACY / "Bearhug.cs"  # Original filename, class name is Brearhug.
HOVER = LEGACY / "Hovermine.cs"
CACTUS = LEGACY / "CactusPatch.cs"
SPECIAL = ROOT / "Assets/Scripts/Gameplay/OriginalSpecialMineSourceBehavior.cs"
TRAP = ROOT / "Assets/Scripts/Gameplay/OriginalMineSourceTrap.cs"
LAUNCH = ROOT / "Assets/Scripts/Gameplay/OriginalMineSourceLauncher.cs"
INPUT = ROOT / "Assets/Scripts/Gameplay/PlayerVehicleInput.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
EDIT_TEST = ROOT / "Assets/Tests/Editor/OriginalSpecialMineSourceTests.cs"
PLAY_TEST = ROOT / "Assets/Tests/PlayMode/OriginalSpecialMineSourcePhysicsTests.cs"


class ThreeSourceSpecialMinesContracts(unittest.TestCase):
    def test_original_legacy_file_provenance_and_compatibility(self):
        source = MINE_DR.read_text("utf-8")
        for phrase in (
            "arg2 == 0x132",
            "arg2 != 0x133",
            "arg2 != 0x134",
            "maxHalfHealth < 2",
            "sVar2 = (short)(maxHalfHealth - 2)",
            "uVar8 = 6",
            "if (maxHalfHealth < 6)",
            "oVar10.maxHalfHealth = uVar8",
            "typeof(Brearhug)",
            "typeof(Hovermine)",
            "typeof(CactusPatch)",
        ):
            self.assertIn(phrase, source)
        self.assertIn("public class Brearhug : Mine", BEAR.read_text("utf-8"))
        self.assertIn("vVar2.physics1.X += local_8.x * 8", BEAR.read_text("utf-8"))
        self.assertIn("if (30 < sVar5)", HOVER.read_text("utf-8"))
        self.assertIn("if (10 < bVar4)", HOVER.read_text("utf-8"))
        self.assertIn("if (tags == 0)", CACTUS.read_text("utf-8"))
        self.assertIn("while (iVar2 < maxHalfHealth)", CACTUS.read_text("utf-8"))

    def test_charge_costs_scan_and_collision_effects_are_source_specific(self):
        special = SPECIAL.read_text("utf-8")
        launcher = LAUNCH.read_text("utf-8")
        for signal in (
            "#if UNITY_EDITOR",
            "SourceVariant { Standard, Bearhug, Hovermine, CactusPatch }",
            "HoverScanInterval = 31",
            "HoverOwnerNeutralAfterScans = 10",
            "CactusMaxCharges = 6",
            "Mathf.Min(CactusMaxCharges, remaining)",
            "remaining >= 2 ? 2 : 0",
            "case SourceVariant.Bearhug:",
            "case SourceVariant.Hovermine:",
            "case SourceVariant.CactusPatch:",
            "SpawnFragments()",
            "OriginalMineSourceTrap.SourceState.Armed",
            "Physics.OverlapSphereNonAlloc",
            "GetComponent<LegacyVehiclePlaytest>() == null",
            "fragmentsSpawned++",
            "IsReleaseApproved => false",
        ):
            self.assertIn(signal, special)
        for signal in (
            "TryDeploy() => TryDeployVariant(",
            "TryDeployVariant(OriginalSpecialMineSourceBehavior.SourceVariant variant)",
            "OriginalSpecialMineSourceBehavior.ChargeCost(variant, remainingCharges)",
            "sourceMine.Configure(vehicle)",
            "remainingCharges--",
            "remainingCharges -= cost",
            "new GameObject(\"SOURCE Mine.cs - NO ORIGINAL MESH\")",
        ):
            self.assertIn(signal, launcher)
        self.assertNotIn("TakeDamage(", special)
        self.assertNotIn("GameObject.CreatePrimitive", special)
        self.assertNotIn("Resources/Vehicles", special)

    def test_collision_integration_real_controls_and_release_gate(self):
        trap = TRAP.read_text("utf-8")
        for signal in (
            "GetComponent<OriginalSpecialMineSourceBehavior>()",
            "variant.OnVehicleContact(victim)",
            "state = SourceState.Armed",
            "active.Count >= SourceMaxRegistered",
        ):
            self.assertIn(signal, trap)
        control = INPUT.read_text("utf-8")
        for signal in ("keyboard.tKey.wasPressedThisFrame",
                       "keyboard.gKey.wasPressedThisFrame",
                       "keyboard.vKey.wasPressedThisFrame"):
            self.assertIn(signal, control)
        gate = RELEASE.read_text("utf-8")
        self.assertIn(
            "prefab.GetComponentInChildren<OriginalSpecialMineSourceBehavior>(true) != null", gate
        )
        edit = EDIT_TEST.read_text("utf-8")
        play = PLAY_TEST.read_text("utf-8")
        for signal in (
            "ChargeConsumptionFollowsMineDrSourceSwitch",
            "BearhugIsPersistentAndCannotFabricateHealthDamage",
            "HovermineUsesArchivedThirtyOneTickScanAndImpulse",
            "CactusPatchCannotScatterBeforeSourceTagsAreArmed",
            "AllEighteenOriginalCarPrototypesRetainTheSharedSixChargeLauncher",
        ):
            self.assertIn(signal, edit)
        for signal in (
            "HovermineScansForNearestAuthenticCarAfterSourceThirtyOneTicks",
            "BearhugPullsWithoutDestroyingMineOrChangingHealth",
            "ArmedCactusPatchScattersOnlyItsAuthenticPaidChargeCount",
        ):
            self.assertIn(signal, play)


if __name__ == "__main__":
    unittest.main()
