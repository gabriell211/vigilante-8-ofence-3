"""The original-source opponent weapon AI stays safe and Editor-only."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AI = ROOT / "Assets/Scripts/Gameplay/OriginalBotSourceWeaponAI.cs"
BOT = ROOT / "Assets/Scripts/Gameplay/BotVehicleInput.cs"
SCENE = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
UNITY = ROOT / "Assets/Tests/Editor/OriginalBotSourceWeaponAITests.cs"

class OriginalSourceBotWeaponsTests(unittest.TestCase):
    def test_only_existing_authentic_source_launchers_and_live_battle_targets(self):
        source = AI.read_text("utf-8")
        for marker in (
            "#if UNITY_EDITOR", "IsReleaseApproved => false",
            "GetComponent<LegacyVehiclePlaytest>()",
            "bot.target.GetComponentInParent<VehicleDrive>()",
            "bot.CanUseSourceWeapon(intended)",
            "rocket.LockedTarget == intended",
            "missile.LockedTarget == intended",
            "rocket.TryLaunch()", "missile.TryLaunch()",
            "Time.timeScale <= 0f",
        ):
            self.assertIn(marker, source)
        for forbidden in ("CreatePrimitive(", "Resources.Load(", "Instantiate(", "InitialCharges ="):
            self.assertNotIn(forbidden, source)

    def test_existing_bot_range_aim_and_line_of_sight_remain_required(self):
        bot = BOT.read_text("utf-8")
        for marker in (
            "CanUseSourceWeapon(VehicleDrive intendedTarget)",
            "target.GetComponentInParent<VehicleDrive>() != intendedTarget",
            "!intendedTarget.IsAlive",
            "offset.sqrMagnitude >= shootingRange * shootingRange",
            "IsWithinAimCone(transform.forward, offset, 12f)",
            "return HasClearShot(intendedTarget)",
        ):
            self.assertIn(marker, bot)

    def test_only_real_editor_battle_bots_get_weapon_ai(self):
        scene = SCENE.read_text("utf-8")
        self.assertIn("opponent.AddComponent<BotVehicleInput>();\n                opponent.AddComponent<OriginalBotSourceWeaponAI>();", scene)
        self.assertNotIn("player.AddComponent<OriginalBotSourceWeaponAI>()", scene)
        release = RELEASE.read_text("utf-8")
        self.assertGreaterEqual(
            release.count("GetComponentInChildren<OriginalBotSourceWeaponAI>(true)"), 2
        )
        self.assertIn("ShootingConeIsHorizontalAndRejectsInvalidDirections", UNITY.read_text("utf-8"))

if __name__ == "__main__":
    unittest.main()
