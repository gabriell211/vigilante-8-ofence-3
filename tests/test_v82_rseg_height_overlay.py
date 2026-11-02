"""Only source-height-known RSEG links may be added to Unity battle previews."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
SOURCE = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"
PREVIEW = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
IDENTITY = ROOT / "Assets/Scripts/Catalog/OriginalRoadSourcePreviewIdentity.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
UNIT = ROOT / "Assets/Tests/Editor/OriginalRoadSegmentSourceReviewTests.cs"


class OriginalHeightKnownRoadContract(unittest.TestCase):
    def test_rseg_original_heights_never_assume_terrain(self):
        source = SOURCE.read_text("utf-8")
        for required in (
            "public Vector3[] OriginalFixedXYZ;",
            "public static bool TrySampleKnownHeight(",
            "if ((flags & 8) != 0)",
            "if ((flags & 1) == 0) return false;",
            "if (!IsInt32(scaled)) return false;",
            "OriginalFixedXYZ = allHeightsKnown",
            "HeightKnownSegments",
        ):
            self.assertIn(required, source)

    def test_preflight_before_scene_replacement_and_opt_in(self):
        source = BATTLE.read_text("utf-8")
        self.assertIn("bool includeHeightKnownRoadReview = false", source)
        self.assertIn("OriginalRsegHeightPreviewBuilder.Prepare(stageId)", source)
        self.assertIn("originalRoadLines.AppendTo(roadRoot.transform)", source)
        self.assertIn("includeOriginalJunctionVisuals, includeHeightKnownRoadReview", source)
        self.assertLess(source.index("OriginalRsegHeightPreviewBuilder.Prepare(stageId)"),
                        source.index("EditorSceneManager.NewScene("))
        self.assertNotIn("GameObject.CreatePrimitive(", source)

    def test_line_guides_cannot_modify_physics_or_ship(self):
        code = PREVIEW.read_text("utf-8")
        for required in (
            "OriginalRoadSegmentSourceReviewWindow.LoadVerified(stageId)",
            "OriginalJunctionSourceReviewBuilder.VerifiedDebugScale()",
            "OriginalFixedXYZ != null",
            "AddComponent<LineRenderer>()",
            "AddComponent<OriginalRoadSourcePreviewIdentity>().Configure(",
            "GetComponentsInChildren<Collider>(true)",
            "GetComponentsInChildren<Rigidbody>(true)",
        ):
            self.assertIn(required, code)
        for forbidden in ("AddComponent<MeshCollider>", "AddComponent<BoxCollider>",
                          "AddComponent<Rigidbody>", "GameObject.CreatePrimitive("):
            self.assertNotIn(forbidden, code)
        self.assertIn("IsReleaseApproved => false", IDENTITY.read_text("utf-8"))
        self.assertGreaterEqual(RELEASE.read_text("utf-8").count(
            "GetComponentInChildren<OriginalRoadSourcePreviewIdentity>(true)"), 2)
        self.assertIn("OnlyOriginalFlagsWithExplicitHeightProduceThreeDimensionalEvidence",
                      UNIT.read_text("utf-8"))


if __name__ == "__main__":
    unittest.main()
