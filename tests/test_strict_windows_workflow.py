"""The manual Windows pipeline cannot bypass the official asset and Unity gates."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/unity-windows-strict.yml"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"


class StrictWindowsBuildContracts(unittest.TestCase):
    def test_manual_real_editor_build_cannot_fake_release(self):
        source = WORKFLOW.read_text("utf-8")
        for signal in (
            "workflow_dispatch:",
            "scripts/audit_content.py --strict",
            "UNITY_LICENSE",
            "UNITY_SERIAL",
            "game-ci/unity-test-runner@v4.4.0",
            "testMode: all",
            "game-ci/unity-builder@v4",
            "targetPlatform: StandaloneWindows64",
            "buildMethod: Vigilante8.Editor.WindowsReleaseBuilder.BuildFromCommandLine",
            "scripts/verify_windows_package.py --builds-dir Builds",
            "Vigilante8Offence3-*-win-x64.zip.sha256",
            "if-no-files-found: error",
        ):
            self.assertIn(signal, source)
        self.assertNotIn("softprops/action-gh-release", source)
        self.assertNotIn("gh release create", source)

    def test_real_source_gate_still_required_by_windows_build(self):
        source = RELEASE.read_text("utf-8")
        for signal in (
            "ValidateContent();",
            "BuildOptions.StrictMode",
            "IL2CPP",
            "BuildPipeline.BuildPlayer",
            "BuildResult.Succeeded",
            "File.WriteAllText(zip + \".sha256\"",
            "catalog.vehicles.Length != 31",
            "catalog.arenas.Length != 19",
        ):
            self.assertIn(signal, source)


if __name__ == "__main__":
    unittest.main()
