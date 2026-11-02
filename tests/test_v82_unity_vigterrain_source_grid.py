"""Original VigTerrain source grid must drive RSEG visuals without raycast heights."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
GRID = ROOT / "Assets/Editor/OriginalVigTerrainSourceGrid.cs"
PLAN = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"
OVERLAY = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
EDITOR_TEST = ROOT / "Assets/Tests/Editor/OriginalRoadSegmentSourceReviewTests.cs"

class OriginalTerrainUnityContract(unittest.TestCase):
    def test_original_grid_source_and_math(self):
        code = GRID.read_text("utf-8")
        for fragment in (
            "LegacySources/Vigilante2Unity/Assets/Scripts/VigTerrain.cs",
            "VerifiedOriginalScenePath(stageId)",
            "VerifiedScriptGuid(Script + \".meta\")",
            "public bool TrySampleHeight(int x, int z, out int height)",
            "fx + fz < 65536",
            "height = (int)(raw >> 5);",
            "c.Length != ChunkCount",
            "v.Length != zones.Value * ZoneVertices",
        ):
            self.assertIn(fragment, code)
        for forbidden in ("Physics.Raycast(", "AddComponent<MeshCollider>", "GameObject.CreatePrimitive("):
            self.assertNotIn(forbidden, code)

    def test_rseg_uses_exact_int32_positions_to_recover_height(self):
        source = PLAN.read_text("utf-8")
        preview = OVERLAY.read_text("utf-8")
        for required in ("public Vector2Int[] OriginalFixedXZExact;",
                         "exactPoints.Add(new Vector2Int(x, z))",
                         "OriginalFixedXZExact = exactPoints.ToArray()"):
            self.assertIn(required, source)
        for required in ("OriginalVigTerrainSourceGrid.LoadVerified(stageId)",
                         "road.OriginalFixedXZExact[i]",
                         "originalGrid.TrySampleHeight(original.x, original.y, out int y)",
                         "road.OriginalFixedXYZ = samples",
                         "AddComponent<LineRenderer>()"):
            self.assertIn(required, preview)

    def test_original_junction_regression_exists(self):
        s = EDITOR_TEST.read_text("utf-8")
        self.assertIn("SteelMillOriginalJunctionMatchesExactSerializedVigTerrainHeight", s)
        self.assertIn("59919356, 90376320, out int y", s)
        self.assertIn("Is.EqualTo(3006532)", s)


if __name__ == "__main__":
    unittest.main()
