"""Ensure authenticated XRTP/MAT/BMP images drive only opt-in non-colliding previews."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
TEXTURE = ROOT / "Assets/Editor/OriginalRoadTextureSourcePreview.cs"
SCENE = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
TEST = ROOT / "Assets/Tests/Editor/OriginalRoadTextureSourcePreviewTests.cs"


class OriginalRoadTextureReviewContract(unittest.TestCase):
    def test_original_material_guid_chain_not_file_name_heuristics(self):
        source = TEXTURE.read_text("utf-8")
        for fragment in (
            "timFarList:",
            "_MainTex:",
            "FindOriginalByGuid(directory, \".mat.meta\"",
            "FindOriginalByGuid(directory, \".bmp.meta\"",
            "OriginalSha256 = Digest(bitmap)",
            "Digest(destination), verified.OriginalSha256",
            "TextureImporterCompression.Uncompressed",
            "FilterMode.Trilinear",
            "anisoLevel = 8",
            "PROVISIONAL UV",
        ):
            self.assertIn(fragment, source)
        for prohibited in ("AssetDatabase.DeleteAsset(", "GameObject.CreatePrimitive(",
                           "AddComponent<MeshCollider>", "AddComponent<Rigidbody>"):
            self.assertNotIn(prohibited, source)

    def test_inspection_strip_cannot_be_confused_with_release_roads(self):
        source = TEXTURE.read_text("utf-8")
        scene = SCENE.read_text("utf-8")
        self.assertIn("OriginalRoadSourcePreviewIdentity", scene)
        self.assertIn("OriginalRoadTextureSourcePreview.AppendDiagnosticStrip(", scene)
        self.assertIn("OriginalRoadTextureSourcePreview.PrepareDiagnosticMaterial(", scene)
        self.assertIn("if (Application.isPlaying || parent == null ||", scene)
        self.assertIn("GetComponentsInChildren<Collider>(true)", scene)
        self.assertIn("const int count = OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount", source)
        self.assertIn("(count - 1) * 6", source)
        self.assertIn("if (normal.sqrMagnitude < 1e-12f)", source)
        self.assertIn("new Vector2(0f, v)", source)
        self.assertIn("new Vector2(1f, v)", source)

    def test_original_source_and_malformed_uv_guard_covered_by_nunit(self):
        source = TEST.read_text("utf-8")
        for expected in (
            "VerifiedSteelMillOriginalRoadProfileResolvesRealBitmap",
            "ThirtyThreeOriginalEdgeSamplesMakeOnlyAnInspectionMesh",
            "SurfaceFailsClosedOnMissingZeroWidthAndMalformedSegments",
            "Does.Contain(\"PROVISIONAL UV\")",
        ):
            self.assertIn(expected, source)


if __name__ == "__main__":
    unittest.main()
