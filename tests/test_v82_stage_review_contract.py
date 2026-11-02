"""Static regression only; Unity Editor tests are separate and license-gated."""
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]

class ReviewStageContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.editor = (ROOT / "Assets/Editor/OriginalStageObjectReviewBuilder.cs").read_text("utf-8")
        cls.identity = (ROOT / "Assets/Scripts/Catalog/OriginalStageObjectPreviewIdentity.cs").read_text("utf-8")
        cls.player = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")
        cls.release = (ROOT / "Assets/Editor/WindowsReleaseBuilder.cs").read_text("utf-8")

    def test_original_meshes_import_into_editor_only_preview(self):
        for token in ("manifest.stageCount != 8", "VerifySources()", "Parse(File.ReadAllText",
                      "new Mesh", "mesh.SetTriangles(part.Faces, 0)",
                      "PrefabUtility.SaveAsPrefabAsset", "EditorSceneManager.SaveScene",
                      "OriginalStageObjectReview.unity"):
            self.assertIn(token, self.editor)
        self.assertNotIn("GameObject.CreatePrimitive", self.editor)
        self.assertNotIn("Resources/Arenas", self.editor)

    def test_inspection_only_assets_cannot_unlock_windows_or_gameplay(self):
        self.assertIn("OriginalStageObjectPreviewIdentity", self.player)
        self.assertIn("OriginalStageObjectPreviewIdentity", self.release)
        self.assertIn("IsReleaseApproved => false", self.identity)
        self.assertIn("EditorBuildSettings.scenes.Any", self.editor)

if __name__ == "__main__":
    unittest.main()
