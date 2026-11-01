"""Verify a genuine Unity car integration path exists, but do not claim editor tests ran."""
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class NativePlayableSourceTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.runtime = (ROOT / "Assets/Scripts/Catalog/LegacyVehiclePlaytest.cs").read_text("utf-8")
        cls.builder = (ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs").read_text("utf-8")
        cls.releaser = (ROOT / "Assets/Editor/WindowsReleaseBuilder.cs").read_text("utf-8")
        cls.integration = (ROOT / "Assets/Tests/Editor/LegacyPlayableVehicleIntegrationTests.cs").read_text("utf-8")

    def test_import_uses_real_original_prefabs_only(self):
        for signal in ("OriginalVehiclePreviewBuilder.PreparePreviewPrefabs()",
                       "OriginalAudioBankBinder.AttachToInspectionPrefabs()",
                       'Where(v => v.game == "v82")',
                       "PrepareAll", "PrefabUtility.LoadPrefabContents",
                       "PrefabUtility.SaveAsPrefabAsset"):
            self.assertIn(signal, self.builder)
        self.assertNotIn("GameObject.CreatePrimitive", self.builder)

    def test_test_projectiles_are_explicitly_not_original(self):
        self.assertIn("INVISIBLE PHYSICS TEST PROJECTILE - NOT ORIGINAL", self.builder)
        self.assertIn("PLAYTEST MUZZLE - NOT ORIGINAL", self.builder)
        self.assertIn("IsReleaseApproved => false", self.runtime)

    def test_generated_car_source_never_becomes_releasable_unchecked(self):
        self.assertIn("prefab.GetComponent<LegacyVehiclePlaytest>()", self.releaser)
        self.assertIn("enabledScenes.Length != 1", self.releaser)
        self.assertIn("public void GenerateOriginalSourcePhysicsVehicles()", self.integration)
        self.assertIn("AssetDatabase.LoadAssetAtPath<SceneAsset>", self.integration)
        self.assertIn("OfficialAssetIdentity>(), Is.Null", self.integration)

    def test_no_fake_official_track(self):
        self.assertIn("INVISIBLE TEST COLLIDER - NOT A VIGILANTE 8 ARENA", self.builder)
        self.assertIn("Only an explicit nonrelease car prototype is allowed", self.builder)
        self.assertIn("A developer test surface cannot be shipped as an arena", self.builder)


if __name__ == "__main__":
    unittest.main()
