"""Dense Unity RSEG inspection retains source math and no fabricated roads."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
ROAD = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"
PREVIEW = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
UNIT = ROOT / "Assets/Tests/Editor/OriginalRoadSegmentSourceReviewTests.cs"


class DenseSourceCurveContract(unittest.TestCase):
    def test_real_polynomial_evaluations_not_spline_interpolation(self):
        source = ROAD.read_text("utf-8")
        for fragment in (
            "public const int DenseCurveSampleCount = 33;",
            "public const int DenseCurveStepFixed12 = 128;",
            "public Vector2Int[] OriginalFixedXZExactDense;",
            "public Vector3[] OriginalFixedXYZDense;",
            "t <= 4096; t += DenseCurveStepFixed12",
            'TrySampleAxis(start.x, t, segment.Coeffs["DAT_20"]',
            'TrySampleAxis(start.z, t, segment.Coeffs["DAT_24"]',
            "denseXZ.Clear();",
            "OriginalFixedXZExactDense = denseComplete ? denseXZ.ToArray() : null",
        ):
            self.assertIn(fragment, source)
        self.assertNotIn("Vector3.Lerp(", source)

    def test_original_terrain_is_resampled_at_every_dense_curve_point(self):
        source = PREVIEW.read_text("utf-8")
        for fragment in (
            "road.OriginalFixedXZExactDense[i]",
            "originalGrid.TrySampleHeight(raw.x, raw.y, out int y)",
            "if (resolved) road.OriginalFixedXYZDense = dense;",
            "line.positionCount = road.OriginalFixedXYZDense.Length;",
            "ToSourceReviewPosition(road.OriginalFixedXYZDense[i], divisor)",
            "road.OriginalFixedXYZ != null &&",
            "x.OriginalFixedXYZDense != null",
            "GetComponentsInChildren<Collider>(true)",
        ):
            self.assertIn(fragment, source)
        self.assertNotIn("Physics.Raycast(", source)
        self.assertNotIn("AddComponent<MeshCollider>", source)

    def test_original_editor_test_cases_exist(self):
        source = UNIT.read_text("utf-8")
        self.assertIn("DenseOriginalRsegPreviewReevaluatesEveryFixedPoint", source)
        self.assertIn("DenseTraceRejectsOverflowInsteadOfDrawingInventedInterpolations", source)


if __name__ == "__main__":
    unittest.main()
