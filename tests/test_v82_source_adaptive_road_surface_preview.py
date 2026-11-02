"""Source-only adaptive FUN_630C sample-to-texture preview regression gates."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SAMPLE = ROOT / "Assets/Editor/OriginalRoadAdaptiveEdgeSampler.cs"
ROAD = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
PARSER = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"
MESH = ROOT / "Assets/Editor/OriginalRoadTextureSourcePreview.cs"
UNIT = ROOT / "Assets/Tests/Editor/OriginalRoadAdaptiveEdgeSamplerTests.cs"


class AdaptiveSourceRoadPreviewContracts(unittest.TestCase):
    def test_source_parameter_sampling_not_interpolated_prior_33_points(self):
        code = SAMPLE.read_text("utf-8")
        for value in (
            "TryBuildSourceParameterSamples(",
            "OriginalRoadAdaptiveSourceSubdivision.Leaf[] leaves",
            "OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(",
            "OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(",
            "OriginalRoadSegmentSourceReviewWindow.TrySampleKnownHeight(",
            "terrain.TrySampleHeight((int)lx, (int)lz, out ly)",
            "terrain.TrySampleHeight((int)rx, (int)rz, out ry)",
            "OriginalRoadAdaptiveEdgeSampler", # defined in this source
            "MaximumPreviewLeaves = 128",
            "SectionsPerLeaf = 32",
            "if (t < 0 || t > 4096 || t <= previousT) return false",
        ):
            self.assertIn(value, code)
        self.assertNotIn("Vector3.Lerp(", code)
        self.assertNotIn("Physics.Raycast(", code)
        self.assertNotIn("AddComponent<MeshCollider>", code)

    def test_source_parser_retains_real_original_polynomial_data(self):
        src = PARSER.read_text("utf-8")
        for term in (
            "OriginalFirstJunctionXYZ = start",
            "OriginalLastJunctionXYZ = finish",
            "OriginalPolynomialX = new[]",
            'segment.Coeffs["DAT_20"], segment.Coeffs["DAT_28"]',
            'segment.Coeffs["DAT_24"], segment.Coeffs["DAT_2C"]',
            "OriginalAdaptiveTimesFixed12",
        ):
            self.assertIn(term, src)

    def test_preview_falls_back_to_verified_original_33_points(self):
        s = ROAD.read_text("utf-8")
        m = MESH.read_text("utf-8")
        for term in (
            "OriginalRoadAdaptiveEdgeSampler.TryBuild(road",
            "road.OriginalAdaptiveTimesFixed12 = adaptive.TimesFixed12;",
            "OriginalRoadTextureSourcePreview.AppendDiagnosticStrip(",
            "adaptive ? road.OriginalAdaptiveLeftEdgeFixedXYZ",
            "GetComponentsInChildren<Collider>(true)",
            "OriginalRoadSourcePreviewIdentity",
        ):
            self.assertIn(term, s)
        for term in (
            "BuildDiagnosticStrip(Vector3[] left, Vector3[] right, int divisor)",
            "BuildAdaptiveDiagnosticStrip(",
            "originalTimesFixed12[i] / 4096f",
            "(count - 1) * 6",
            "new Vector2(0f, v)",
            "new Vector2(1f, v)",
            "PROVISIONAL UV / NO COLLISION",
        ):
            self.assertIn(term, m)
        self.assertNotIn("AddComponent<Rigidbody>", m)
        self.assertNotIn("AddComponent<MeshCollider>", m)

    def test_unity_editor_includes_long_curve_and_invalid_boundary_tests(self):
        s = UNIT.read_text("utf-8")
        for name in (
            "AdaptiveSubdividedOriginalParameterGridIsContinuous",
            "OriginalAdaptiveRoadEdgesProduceSourceHeightAndWidthWithoutTerrain",
            "UnsupportedOrDuplicatedAdaptiveIntervalsNeverGetAFullMesh",
            "ZeroDerivativeOverflowOrMissingOriginalCurveIsRejected",
            "Is.EqualTo(384)",
        ):
            self.assertIn(name, s)


if __name__ == "__main__":
    unittest.main()
