"""Unity editor must preview only real JUNC/TMD sources and never ship them."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EDITOR = ROOT / "Assets/Editor/OriginalJunctionSourceReviewBuilder.cs"
IDENTITY = ROOT / "Assets/Scripts/Catalog/OriginalJunctionSourcePreviewIdentity.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
TESTS = ROOT / "Assets/Tests/Editor/OriginalJunctionSourceReviewTests.cs"

class OriginalJunctionPreviewContract(unittest.TestCase):
    def test_source_identifiers_and_mesh_paths_are_original(self):
        source = EDITOR.read_text("utf-8")
        for text in (
            "LegacySources/Vigilante2Unity/Assets/Scripts/DB/JUNC_DB.cs",
            "LegacySources/Vigilante2Unity/Assets/DEBUG.unity",
            "Assets/GameContent/PreviewStageMeshesV82/",
            "ShaGit(Absolute(row.sourceScenePath)) != SceneBlobs[id]",
            "mesh.ScriptGuid != meshGuid",
            "mesh.Owner == junc.Owner",
            "junc.BankGuid == bankGuid && mesh.BankGuid == bankGuid",
            "SourceJunctionLocal(candidate.Junction.Dat00.Value, divisor)",
            "OriginalStageObjectReviewBuilder.PrepareAll()",
            "AddComponent<MeshFilter>()",
        ):
            self.assertIn(text, source)
        for forbidden in ("GameObject.CreatePrimitive(", "AddComponent<BoxCollider>", "AddComponent<MeshCollider>", "ArenaSpawnPoints", "OfficialAssetIdentity"):
            self.assertNotIn(forbidden, source)

    def test_editor_source_scene_not_releasable(self):
        editor = EDITOR.read_text("utf-8")
        release = RELEASE.read_text("utf-8")
        marker = IDENTITY.read_text("utf-8")
        self.assertIn("#if UNITY_EDITOR", editor)
        self.assertIn("IsReleaseApproved => false", marker)
        self.assertIn('OriginalJunctionSourcePreviewIdentity', release)
        self.assertGreaterEqual(release.count("GetComponentInChildren<OriginalJunctionSourcePreviewIdentity>(true)"), 2)
        self.assertIn("EditorBuildSettings.scenes.Any", editor)
        self.assertIn("DiagnosticsAreAlwaysRejectedAsProductionAssets", TESTS.read_text("utf-8"))

if __name__ == "__main__":
    unittest.main()
