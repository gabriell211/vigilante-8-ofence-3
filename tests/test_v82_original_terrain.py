import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from verify_v82_original_terrain import audit


class OriginalTerrainTests(unittest.TestCase):
    def test_seven_original_meshes_and_missing_route66(self):
        data = audit()
        self.assertEqual(data["availableTerrainMeshes"], 7)
        self.assertEqual(data["missingTerrainSource"], ["v82_meteor_crater"])
        self.assertEqual(len({x["sourceGuid"] for x in data["terrains"]}), 7)

    def test_no_preview_mesh_becomes_an_official_gameplay_stage(self):
        build = (ROOT / "Assets/Editor/WindowsReleaseBuilder.cs").read_text("utf-8")
        player = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")
        self.assertIn("OriginalTerrainPreviewIdentity", build)
        self.assertIn("OriginalTerrainPreviewIdentity", player)
