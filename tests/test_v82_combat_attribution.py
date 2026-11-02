"""Killer attribution is wired to real physics and source-car battle scoreboard."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
PROJECTILE = ROOT / "Assets/Scripts/Gameplay/Projectile.cs"
BATTLE = ROOT / "Assets/Scripts/Gameplay/OriginalBattleSession.cs"
EDITOR_TEST = ROOT / "Assets/Tests/Editor/OriginalBattleSessionTests.cs"
PLAY_TEST = ROOT / "Assets/Tests/PlayMode/CombatEliminationAttributionTests.cs"


class CombatEliminationContracts(unittest.TestCase):
    def test_lethal_damage_tracks_real_projectile_owner_while_preserving_api(self):
        drive = DRIVE.read_text("utf-8")
        projectile = PROJECTILE.read_text("utf-8")
        for signal in (
            "public event Action<VehicleDrive, VehicleDrive> Eliminated;",
            "public void TakeDamage(float damage) => TakeDamage(damage, null);",
            "public void TakeDamage(float damage, VehicleDrive attacker)",
            "float.IsNaN(damage)",
            "float.IsInfinity(damage)",
            "if (!IsAlive",
            "Eliminated?.Invoke(this, attacker != this ? attacker : null)",
        ):
            self.assertIn(signal, drive)
        self.assertIn("victim.TakeDamage(impactDamage, owner)", projectile)
        self.assertIn("victim.TakeDamage(impactDamage * falloff, owner)", projectile)

    def test_source_car_scoreboard_never_invents_credit(self):
        battle = BATTLE.read_text("utf-8")
        for signal in (
            "#if UNITY_EDITOR",
            "CreditedKillerSlot",
            "Array.IndexOf(botDrives, victim) < 0",
            "int slot = CreditedKillerSlot(victim, killer, player, opponents)",
            "playerEliminations++;",
            "opponentEliminations[slot - 1]++;",
            "LastElimination => lastElimination",
            "player.Eliminated += OnVehicleEliminated",
            "opponent.Eliminated += OnVehicleEliminated",
            "player.Eliminated -= OnVehicleEliminated",
        ):
            self.assertIn(signal, battle)
        self.assertNotIn("GameObject.CreatePrimitive", battle)

    def test_real_editor_and_playmode_cover_fatal_killer_and_edge_cases(self):
        edit = EDITOR_TEST.read_text("utf-8")
        play = PLAY_TEST.read_text("utf-8")
        for signal in (
            "OnlyRealParticipantsReceiveKillsAndSelfHitsGiveNoPoints",
            "CreditedKillerSlot",
        ):
            self.assertIn(signal, edit)
        for signal in (
            "ProjectilePhysicsCreditsActualFiringVehicleOnce",
            "EnvironmentDamageAndInvalidHitsNeverInventKiller",
            "SplashAndDirectHitsCanCreditDifferentCarsWithoutDuplicateDeaths",
            "new WaitForFixedUpdate()",
            "projectile.Launch(attacker, Vector3.forward)",
            "Assert.That(actualKiller, Is.SameAs(attacker))",
        ):
            self.assertIn(signal, play)


if __name__ == "__main__":
    unittest.main()
