"""Audit source-only FUN_719C junction trim selection and unsafe source guard."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_junction_transition_selection as subject

class OriginalJunctionSelectionTests(unittest.TestCase):
    def test_earlier_road_width_and_signed_short_source_order(self):
        connected = [
            {"id": "1", "order": 65535, "width": 131072},
            {"id": "2", "order": 0, "width": 32768},
            {"id": "3", "order": 4, "width": 65536},
        ]
        self.assertEqual(subject.selected_trim_radius(
            0, False, 0, 0, 4, "3", connected, 3),
            ("original_fun6604_transition_required", 65536, 2))
        self.assertEqual(subject.selected_trim_radius(
            1, False, 0, 0, 4, "3", connected, 3)[0],
            "original_no_fun6604_trim")
        self.assertEqual(subject.selected_trim_radius(
            0, False, 2, 0, 4, "3", connected, 3)[0],
            "original_no_fun6604_trim")
        self.assertEqual(subject.selected_trim_radius(
            0, True, 0, 0, 4, "3", connected, 3)[0],
            "xobf_configured_junction_not_ported")

    def test_invalid_and_missing_junction_refs_fail_closed(self):
        candidates = [{"id": "10", "order": 1, "width": 65536}]
        self.assertEqual(subject.selected_trim_radius(
            0, False, 0, 0, 2, "100", candidates, 1)[0],
            "unverified_source_junc_links")
        self.assertEqual(subject.selected_trim_radius(
            0, False, 0, 0, 1, "10", candidates, 9)[0],
            "unverified_source_junc_links")
        self.assertEqual(subject.selected_trim_radius(
            0, False, 0, 0, 1, "10",
            [{"id": "10", "order": 1, "width": None}], 1)[0],
            "unverified_source_junc_links")

    def test_all_8_stage_endpoint_decisions_are_source_derived(self):
        result = subject.audit(ROOT)
        self.assertEqual(result["sceneCount"], 8)
        self.assertFalse(result["originalJunctionCurveCertified"])
        self.assertFalse(result["originalRoadUVsCertified"])
        self.assertFalse(result["collisionCertified"])
        self.assertIn("index 30", result["sourceFun6604ArrayBoundsIssue"])
        self.assertGreater(sum(s["sourceEndpointCount"] for s in
                               result["stages"].values()), 0)
        for stage in result["stages"].values():
            self.assertEqual(stage["sourceEndpointCount"],
                             stage["sourceRoadCount"] * 2)
            for item in stage["endpoints"]:
                self.assertIn(item["status"], {
                    "unverified_source_junc_links",
                    "original_fun6604_transition_required",
                    "original_no_fun6604_trim",
                    "xobf_configured_junction_not_ported",
                })
                if item["status"] == "original_fun6604_transition_required":
                    self.assertGreater(item["originalTrimRadiusFixed"], 0)

    def test_source_method_and_unity_contracts(self):
        c = (ROOT / "Assets/Editor/OriginalJunctionTransitionSourceReview.cs"
             ).read_text("utf-8")
        overlay = (ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
                   ).read_text("utf-8")
        for expected in (
            "ReviewVerified(",
            "Evaluate(",
            "FUN_6604(local_b0, i, iVar12);",
            "local_188[30] = local_188[5];",
            "unchecked((short)road.SourceOrder)",
            "profiles[adjacent.Profile.Value].FullWidthFixed",
            "JunctionStatus.NeedsUnportedFun6604",
        ):
            self.assertIn(expected, c)
        self.assertIn("AppendSourceTrimRadius(", overlay)
        self.assertIn("OriginalJunctionTransitionSourceReview.ReviewVerified(", overlay)
        self.assertNotIn("AddComponent<MeshCollider>", overlay)
        self.assertNotIn("AddComponent<Rigidbody>", overlay)


if __name__ == "__main__":
    unittest.main()
