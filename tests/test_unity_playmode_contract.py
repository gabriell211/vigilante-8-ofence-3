"""Ensure the real Unity Editor workflow includes actual PlayMode physics tests."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class UnityPlayModeContracts(unittest.TestCase):
    def test_real_playmode_tests_registered_and_not_shipped(self):
        path = ROOT / "Assets/Tests/PlayMode"
        assembly = json.loads((path / "Vigilante8.PlayModeTests.asmdef").read_text("utf-8"))
        self.assertIn("TestAssemblies", assembly["optionalUnityReferences"])
        self.assertIn("Vigilante8.Runtime", assembly["references"])
        self.assertFalse(assembly["autoReferenced"])
        tests = (path / "CombatPhysicsPlayModeTests.cs").read_text("utf-8")
        for signal in (
            "[UnityTest]",
            "[UnityTearDown]",
            "new WaitForFixedUpdate()",
            "Physics.SyncTransforms()",
            "projectile.Launch(attacker, Vector3.forward)",
            "victim.Health",
            "attacker.Health",
            "victim.Destroyed",
            "float.NaN",
            "float.PositiveInfinity",
            "destroyedEvents, Is.EqualTo(1)",
        ):
            self.assertIn(signal, tests)
        self.assertNotIn("Resources/Vehicles/", tests)
        self.assertNotIn("Assets/Resources/Arenas/", tests)

    def test_real_editor_workflow_has_license_gate_and_both_test_modes(self):
        workflow = (ROOT / ".github/workflows/unity-editmode.yml").read_text("utf-8")
        self.assertIn("workflow_dispatch:", workflow)
        self.assertIn("UNITY_LICENSE", workflow)
        self.assertIn("UNITY_SERIAL", workflow)
        self.assertIn("unity-test-runner@v4.4.0", workflow)
        self.assertIn("testMode: all", workflow)
        self.assertIn("unity-editor-and-playmode-artifacts", workflow)
        self.assertNotIn("build-and-release", workflow)


if __name__ == "__main__":
    unittest.main()
