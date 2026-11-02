"""Source-traced Rocket/Bastion prototype contracts; no fake commercial weapon."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/Rocket.cs"
FLIGHT = ROOT / "Assets/Scripts/Gameplay/OriginalBastionRocketFlight.cs"
LAUNCHER = ROOT / "Assets/Scripts/Gameplay/OriginalBastionRocketLauncher.cs"
DRIVE = ROOT / "Assets/Scripts/Gameplay/VehicleDrive.cs"
INPUT = ROOT / "Assets/Scripts/Gameplay/PlayerVehicleInput.cs"
BUILDER = ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs"
TERRAIN = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
TARGETS = ROOT / "Assets/Scripts/Gameplay/OriginalBattleTargetCoordinator.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
EDITOR_TEST = ROOT / "Assets/Tests/Editor/OriginalBastionRocketTests.cs"
PLAYMODE = ROOT / "Assets/Tests/PlayMode/OriginalBastionRocketFlightPhysicsTests.cs"


class OriginalBastionRocketContracts(unittest.TestCase):
    def test_bastion_source_state_is_preserved_in_separate_editor_only_flight(self):
        original = SOURCE.read_text("utf-8")
        stage = FLIGHT.read_text("utf-8")
        for signal in (
            "case _ROCKET_TYPE.Rocket:",
            "case _ROCKET_TYPE.Bastion:",
            "state = _ROCKET_TYPE.Bastion",
            "physics2.M2 < 211",
            "(uVar2 & 15) == 0",
            "DAT_84 != null",
        ):
            self.assertIn(signal, original)
        for signal in (
            "#if UNITY_EDITOR",
            "public enum SourceState { Rocket, Bastion }",
            "StrongSteeringThreshold = 211",
            "TargetValidationInterval = 16",
            "SourceState.Rocket",
            "sourceState = SourceState.Bastion",
            "remainingSourceTicks % TargetValidationInterval",
            "body.linearVelocity = Vector3.Lerp",
            "lockedTarget.IsAlive",
            "IsReleaseApproved => false",
        ):
            self.assertIn(signal, stage)
        self.assertNotIn("GameObject.CreatePrimitive", stage)
        self.assertNotIn("Assets/Resources/", stage)

    def test_original_18_vehicles_can_fire_a_lock_on_rocket_only_in_editor(self):
        expected = {
            BUILDER: [
                "instance.GetComponent<LegacyVehiclePlaytest>()",
                "AddComponent<OriginalBastionRocketLauncher>()",
            ],
            DRIVE: [
                "#if UNITY_EDITOR",
                "LaunchBastionSourceRocket(VehicleDrive target)",
                "AddComponent<OriginalBastionRocketFlight>()",
                "flight.Configure(target, ticks)",
                "shot.Launch(this, muzzle.forward)",
                "flight.EnterBastion()",
            ],
            INPUT: ["keyboard.eKey.wasPressedThisFrame", "gamepad.leftShoulder.wasPressedThisFrame"],
            TARGETS: [
                "playerLauncher.SetTarget(SelectTarget(player, participants))",
                "launcher.SetTarget(target)",
            ],
            TERRAIN: ["original.GetComponent<OriginalBastionRocketLauncher>() == null"],
            RELEASE: [
                "prefab.GetComponent<LegacyVehiclePlaytest>() != null",
                "prefab.GetComponent<OriginalBastionRocketLauncher>() != null",
                "prefab.GetComponentInChildren<OriginalBastionRocketFlight>(true) != null",
            ],
        }
        for path, signals in expected.items():
            source = path.read_text("utf-8")
            for signal in signals:
                self.assertIn(signal, source, str(path))
        launcher = LAUNCHER.read_text("utf-8")
        self.assertIn("GetComponent<LegacyVehiclePlaytest>() == null", launcher)
        self.assertIn("vehicle.LaunchBastionSourceRocket(lockedTarget)", launcher)
        self.assertIn("target == vehicle", launcher)
        self.assertIn("!lockedTarget.IsAlive", launcher)
        self.assertIn("IsReleaseApproved => false", launcher)
        self.assertNotIn("GameObject.CreatePrimitive", launcher)

    def test_real_unity_editmode_and_playmode_tests_are_authored(self):
        editor = EDITOR_TEST.read_text("utf-8")
        play = PLAYMODE.read_text("utf-8")
        for marker in (
            "ArchivedRocketToBastionTransitionAndTwoGuidancePhases",
            "CannotUseMissingTargetOrInvalidLifetime",
            "AllEighteenOriginalVehiclePrefabsHaveTheNonreleaseRocketLauncher",
        ):
            self.assertIn(marker, editor)
        for marker in (
            "[UnityTest]",
            "BastionActuallySteersRigidbodyTowardLivingCar",
            "ArchivedTargetValidationTerminatesLostTarget",
            "new WaitForFixedUpdate()",
            "pair.shot.Launch(source, Vector3.forward)",
        ):
            self.assertIn(marker, play)


if __name__ == "__main__":
    unittest.main()
