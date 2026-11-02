"""Original V82 stage object banks: topology and source provenance, not arenas."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from export_v82_stage_banks import convert


class StageObjectBankContracts(unittest.TestCase):
    def test_eight_real_source_banks_and_meshes(self):
        data = convert(check=True)
        self.assertEqual(data["stageCount"], 8)
        self.assertEqual(len({row["stageId"] for row in data["stages"]}), 8)
        self.assertGreater(sum(row["triangles"] for row in data["stages"]), 25000)
        for row in data["stages"]:
            self.assertGreater(row["tmdParts"], 50)
            self.assertGreater(row["triangles"], 0)
            for field in ("terrainReady", "materialsReady", "collisionReady", "gameplayTested"):
                self.assertFalse(row[field])

    def test_stage_previews_are_not_official_release_arenas(self):
        build = (ROOT / "Assets/Editor/WindowsReleaseBuilder.cs").read_text("utf-8")
        self.assertIn('"Assets/Resources/Arenas/" + arena.assetId + ".prefab"', build)
        self.assertIn("HasValidSpawns(4)", build)
        self.assertNotIn("ExtractedStageObjectsV82", build)


if __name__ == "__main__":
    unittest.main()
