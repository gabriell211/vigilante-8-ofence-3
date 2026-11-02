"""Verify source-bounded adaptive FUN_57AC/FUN_630C port and its limits."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_road_adaptive_subdivision as module

C = ROOT / "Assets/Editor/OriginalRoadAdaptiveSourceSubdivision.cs"
WINDOW = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"


class ArchivedAdaptiveRoadSubdivisionTests(unittest.TestCase):
    def test_straight_long_source_road_subdivides_at_original_extent(self):
        controls = [(0, 100, 0), (0, 0, 0),
                    (2_000_000, 0, 0), (2_000_000, 200, 0)]
        self.assertEqual(module.extent(controls), 2_000_000)
        leaves = module.subdivide(controls)
        self.assertEqual(len(leaves), 2)
        self.assertEqual([r["startT16"] for r in leaves], [0, 32768])
        self.assertEqual([r["endT16"] for r in leaves], [32768, 65536])
        self.assertEqual(leaves[0]["end"], leaves[1]["start"])
        self.assertEqual(leaves[0]["end"][1], 150)

    def test_original_half_split_truncates_negative_csharp_midpoints(self):
        self.assertEqual(module.mean(-1, 0), 0)
        self.assertEqual(module.mean(-3, -2), -2)
        self.assertEqual(module.mean(3, 2), 2)
        self.assertEqual(module.mean(0, -3), -1)

    def test_unsafe_source_coordinates_never_wrap(self):
        original = [(0, 0, 0), (0, 0, 0),
                    (2_147_483_647, 0, 0), (2_147_483_647, 0, 0)]
        with self.assertRaises(OverflowError):
            module.subdivide(original)
        with self.assertRaises(OverflowError):
            module.mean(2_147_483_647, 2_147_483_647)
        with self.assertRaises(ValueError):
            module.extent([(0, 0, 0)])

    def test_all_eight_original_scenes_and_tangents_are_accounted_for(self):
        result = module.audit(ROOT)
        self.assertEqual(result["sceneCount"], 8)
        self.assertFalse(result["junctionAdjustmentsCertified"])
        self.assertFalse(result["originalRoadSurfaceCertified"])
        self.assertFalse(result["originalUVsCertified"])
        self.assertFalse(result["originalRoadCollisionCertified"])
        self.assertGreater(result["originalPreJunctionLeavesResolved"], 0)
        for stage in result["stages"].values():
            self.assertEqual(len(stage["segments"]), stage["sourceSegmentCount"])
            for seg in stage["segments"]:
                self.assertIn(seg["status"], (
                    "original_unadjusted_fun630c_leaves",
                    "original_unadjusted_subdivision_unsafe",
                    "unverified_original_junction_or_curve",
                ))
                if seg["status"] == "original_unadjusted_fun630c_leaves":
                    ranges = seg["sourceLeafParameterRangesFixed16"]
                    self.assertEqual(ranges[0][0], 0)
                    self.assertEqual(ranges[-1][-1], 65536)
                    self.assertTrue(all(a[1] == b[0]
                                        for a, b in zip(ranges, ranges[1:])))

    def test_unity_editor_reuses_exact_source_tangent_and_split(self):
        source = C.read_text("utf-8")
        window = WINDOW.read_text("utf-8")
        for piece in ("TryOriginalSourceExtent(", "TryOriginalHalfSplit(",
                      "TryReadOriginalTangentPair(", "TryCreateOriginalUnadjustedControls(",
                      "OriginalExtentCutoff = 0x100000",
                      "TrySplitOriginalUnadjustedCurve("):
            self.assertIn(piece, source)
        for piece in ("OriginalRoadAdaptiveSourceSubdivision.VerifyArchivedSource();",
                      "OriginalUnadjustedAdaptiveLeaves = originalLeaves",
                      "OriginalRoadAdaptiveSourceSubdivision.TrySplitOriginalUnadjustedCurve(",
                      "FUN_630C inicial (sem ajustes JUNC)"):
            self.assertIn(piece, window)
        self.assertNotIn("GameObject.CreatePrimitive(", source)
        self.assertNotIn("AddComponent<MeshCollider>", source)


if __name__ == "__main__":
    unittest.main()
