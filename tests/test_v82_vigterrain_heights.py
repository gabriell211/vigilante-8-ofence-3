"""Real source VigTerrain.FUN_1B750 algorithm; never substitute height guesses."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_vigterrain_heights as src

GUID = "68fd1e9f11866ba4480d203f972bb9ab"
OTHER = "ffffffffffffffffffffffffffffffff"


def terrain():
    vertices = [0] * 4096
    vertices[1 * 64 + 1] = 100
    vertices[2 * 64 + 1] = 200
    vertices[1 * 64 + 2] = 300
    vertices[2 * 64 + 2] = 400
    return {"fileId": "55", "zoneCount": 1,
            "chunks": tuple([0] * 1024),
            "vertices": tuple(vertices)}


def scene(v=terrain(), guid=GUID):
    chunks = b"".join(x.to_bytes(2, "little") for x in v["chunks"]).hex()
    vertices = b"".join(x.to_bytes(2, "little") for x in v["vertices"]).hex()
    return (f"%YAML 1.1\n--- !u!114 &55\nMonoBehaviour:\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n"
            f"  vertices: {vertices}\n  chunks: {chunks}\n"
            f"  zoneCount: {v['zoneCount']}\n")


class OriginalTerrainHeightsTests(unittest.TestCase):
    def test_real_original_triangle_branches_match_fixed_point_interpolation(self):
        grid = terrain()
        self.assertEqual(src.terrain_height(grid, 0x18000, 0x14000), 200 * 2048)
        self.assertEqual(src.terrain_height(grid, 0x1C000, 0x1C000), 325 * 2048)
        self.assertEqual(src.terrain_height(grid, 0, 0x14000), 0)
        self.assertEqual(src.terrain_height(grid, src.MAX_COORD, 0x14000), 0)
        self.assertIsNone(src.terrain_height(grid, float("nan"), 1))

    def test_archive_loader_reads_only_original_vigterrain_guid(self):
        parsed = src.read_original_terrain(scene(), GUID)
        self.assertEqual(parsed["zoneCount"], 1)
        self.assertEqual(len(parsed["vertices"]), 4096)
        self.assertEqual(src.terrain_height(parsed, 0x18000, 0x14000), 200 * 2048)
        self.assertIsNone(src.read_original_terrain(scene(guid=OTHER), GUID))
        with self.assertRaises(ValueError):
            src.read_original_terrain(scene() + scene()[len("%YAML 1.1\n"):], GUID)

    def test_malformed_grid_fails_closed(self):
        bad = scene().replace("  zoneCount: 1", "  zoneCount: 2")
        with self.assertRaises(ValueError):
            src.read_original_terrain(bad, GUID)
        bad = scene().replace("  chunks: ", "  chunks: xx")
        with self.assertRaises(ValueError):
            src.read_original_terrain(bad, GUID)

    def test_height_evidence_only_for_real_links_with_real_terrain(self):
        s = [{"componentFileId": "300",
              "referenceStatus": "linked_original_rseg_junc_fixed_point_source",
              "sourceCurveSamples": [
                  {"sourcePosition": {"x": 0x18000, "y": None, "z": 0x14000}},
                  {"sourcePosition": {"x": 0x1C000, "y": None, "z": 0x1C000}}],
              "sourceHeightNeedsVigTerrain": True}]
        row, counts = src.add_terrain_evidence(s, terrain(), 4096)
        self.assertEqual(row[0]["originalSourceHeights"], [409600, 665600])
        self.assertEqual(row[0]["debugScaleReviewXYZ"][0]["y"], -100)
        self.assertEqual(row[0]["heightStatus"], "original_vigterrain_source_heights_sampled")
        row, _ = src.add_terrain_evidence(s, None, 4096)
        self.assertIsNone(row[0]["originalSourceXYZ"])
        self.assertEqual(row[0]["heightStatus"], "original_vigterrain_source_missing")
        self.assertIn("original_vigterrain_source_heights_sampled", counts)

    def test_all_source_scenes_remain_hash_checked_and_not_release_certified(self):
        report = src.audit(ROOT)
        self.assertEqual(report["sourceSceneCount"], 8)
        self.assertEqual(report["originalTerrainScriptGuid"], GUID)
        self.assertFalse(report["worldPlacementCertified"])
        self.assertFalse(report["officialRoadGeometryCertified"])
        for stage, data in report["stages"].items():
            self.assertEqual(len(data["records"]), data["sourceSegments"], stage)
            if data["originalTerrainZones"] is not None:
                self.assertEqual(data["terrainVertexCount"],
                                 data["originalTerrainZones"] * 4096)
            for row in data["records"]:
                self.assertNotIn("mesh", row)
                if row["originalSourceXYZ"] is not None:
                    self.assertEqual(len(row["originalSourceXYZ"]), 5)


if __name__ == "__main__":
    unittest.main()
