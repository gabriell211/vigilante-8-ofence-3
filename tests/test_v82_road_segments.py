"""Source RSEG topology and deterministic fixed-point curve safeguards."""
from pathlib import Path
import struct
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_road_segments as source

JUNC = "c031ef78ecc9f51459ebf05d0fb88f70"
RSEG = "99ef4414b53d4f94c9d186fd5df8f984"
OTHER = "ffffffffffffffffffffffffffffffff"


def junction(fid, x, y, z):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_GameObject: {{fileID: 55}}
  m_Script: {{fileID: 11500000, guid: {JUNC}, type: 3}}
  DAT_00: {{x: {x}, y: {y}, z: {z}}}
"""


def segment(start="10", end="11", flags="1", coeff=None, guid=RSEG):
    values = coeff or (0, 0, 0, 0, 625, 0)
    definition = "\n".join(f"  {name}: {n}" for name, n in
                           zip(source.COEFFICIENTS, values))
    tangent = struct.pack("<ii", 30, -30).hex()
    return f"""--- !u!114 &300
MonoBehaviour:
  m_GameObject: {{fileID: 88}}
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  DAT_00:
  - {{fileID: {start}}}
  - {{fileID: {end}}}
  DAT_0C: {flags}
  DAT_10: {tangent}
  DAT_14: {tangent}
{definition}
"""


class RoadGraphTests(unittest.TestCase):
    def setUp(self):
        self.raw = ("%YAML 1.1\n" + junction(10, 5000, 100, 4000) +
                    junction(11, 15000, 200, 4000))

    def test_canonical_source_endpoints_and_polynomial_sample(self):
        rows = source.link_segments(self.raw + segment(), RSEG, JUNC, 4096)
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertEqual(row["referenceStatus"],
                         "linked_original_rseg_junc_fixed_point_source")
        self.assertEqual(row["sourceTangentX"], [30, -30])
        self.assertEqual(row["junctionComponentFileIds"], ["10", "11"])
        self.assertEqual(row["sourceCurveSamples"][0]["sourcePosition"],
                         {"x": 5000, "y": 100, "z": 4000})
        self.assertEqual(row["sourceCurveSamples"][-1]["sourcePosition"],
                         {"x": 15000, "y": 200, "z": 4000})

    def test_terrain_height_is_never_made_up(self):
        rows = source.link_segments(self.raw + segment(flags="0"), RSEG, JUNC, 4096)
        self.assertTrue(rows[0]["sourceHeightNeedsVigTerrain"])
        self.assertTrue(all(p["sourcePosition"]["y"] is None for p in
                            rows[0]["sourceCurveSamples"]))
        rows = source.link_segments(self.raw + segment(flags="8"), RSEG, JUNC, 4096)
        self.assertTrue(all(p["sourcePosition"]["y"] == 100 for p in
                            rows[0]["sourceCurveSamples"]))

    def test_missing_junction_and_bad_coefficients_fail_closed(self):
        for raw, expected in (
            (segment(end="666"), "junction_components_missing_from_scene"),
            (segment(end="10"), "same_junction_used_for_both_ends"),
            (segment(coeff=(0, 0, 0, 0, 2 ** 31, 0)),
             "incomplete_original_segment_coefficients"),
        ):
            rows = source.link_segments(self.raw + raw, RSEG, JUNC, 4096)
            self.assertEqual(rows[0]["referenceStatus"], expected)
            self.assertIsNone(rows[0]["sourceCurveSamples"])

    def test_unsigned_source_cannot_invent_curve_after_int_overflow(self):
        row = source.link_segments(self.raw +
            segment(coeff=(0, 0, 0, 0, 1000000000, 0)),
            RSEG, JUNC, 4096)[0]
        self.assertEqual(row["referenceStatus"],
                         "source_polynomial_arithmetic_not_safely_representable")
        self.assertIsNone(row["sourceCurveSamples"])

    def test_does_not_include_other_scripts(self):
        self.assertEqual(source.link_segments(self.raw + segment(guid=OTHER),
                                              RSEG, JUNC, 4096), [])

    def test_archive_all_eight_scenes_with_original_hashes(self):
        result = source.audit(ROOT)
        self.assertEqual(result["sceneCount"], 8)
        self.assertEqual(result["debugSerializedScale"], 4096)
        self.assertFalse(result["roadMeshCertified"])
        self.assertFalse(result["terrainHeightsCertified"])
        self.assertFalse(result["collisionCertified"])
        self.assertGreater(sum(s["sourceSegmentCount"]
                               for s in result["stages"].values()), 0)
        for stage in result["stages"].values():
            self.assertEqual(stage["sourceSegmentCount"], len(stage["segments"]))
            for seg in stage["segments"]:
                self.assertNotIn("officialRoadMesh", seg)
                self.assertNotIn("officialSpawnPoints", seg)


if __name__ == "__main__":
    unittest.main()
