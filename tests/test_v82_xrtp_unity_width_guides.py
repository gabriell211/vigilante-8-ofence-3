"""Original XRTP half-width visual guides must be source-derived and fail closed."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
RSEG = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"
PROFILE = ROOT / "Assets/Editor/OriginalXrtpSourceProfileReview.cs"
GUIDES = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
EDITOR_TESTS = ROOT / "Assets/Tests/Editor/OriginalRoadSegmentSourceReviewTests.cs"


class OriginalWidthGuideContract(unittest.TestCase):
    def test_source_derivative_not_approximate_five_point_differences(self):
        code = RSEG.read_text("utf-8")
        for fragment in (
            "public static bool TrySampleSourceTangent(",
            "if (value < 0) value += 255;",
            "OriginalFixedTangentXZDense",
            "SourceXrtpProfileIndex = segment.ProfileIndex",
            'TrySampleSourceTangent(t, segment.Coeffs["DAT_20"]',
        ):
            self.assertIn(fragment, code)
        self.assertNotIn("Vector2.Lerp(", code)

    def test_guid_source_profiles_not_generic_width(self):
        code = PROFILE.read_text("utf-8")
        for fragment in (
            "OriginalJunctionSourceReviewBuilder.VerifiedOriginalScenePath(stageId)",
            "OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(LevelMeta)",
            "OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(XrtpMeta)",
            "OriginalOrderedRefs(",
            "TryReadPrefab(",
            "full <= 0 || longitudinal <= 0",
        ):
            self.assertIn(fragment, code)
        self.assertNotIn("GameObject.CreatePrimitive(", code)

    def test_edge_height_is_source_terrain_or_original_bridge_y(self):
        code = GUIDES.read_text("utf-8")
        for fragment in (
            "TrySourceHalfWidthOffset(",
            "profile.FullWidthFixed",
            "OriginalXrtpSourceProfileReview.LoadVerified(stageId)",
            "originalGrid.TrySampleHeight((int)lx, (int)lz, out ly)",
            "originalGrid.TrySampleHeight((int)rx, (int)rz, out ry)",
            "OriginalLeftEdgeFixedXYZDense",
            "OriginalRightEdgeFixedXYZDense",
            "road.SourceRoadFlags & 0x10",
            "GetComponentsInChildren<Collider>(true)",
        ):
            self.assertIn(fragment, code)
        for forbidden in ("Physics.Raycast(", "AddComponent<Rigidbody>", "AddComponent<MeshCollider>"):
            self.assertNotIn(forbidden, code)

    def test_nunit_source_width_regression_exists(self):
        tests = EDITOR_TESTS.read_text("utf-8")
        self.assertIn("VerifiedSteelMillOriginalXrtpWidthMatchesSerializedPrefab", tests)
        self.assertIn("OriginalWidthOffsetIsNotInventedAndFollowsUnadjustedSourceSigns", tests)


if __name__ == "__main__":
    unittest.main()
