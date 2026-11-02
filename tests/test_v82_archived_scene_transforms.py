"""No fabricated map coordinates: inspect authentic archived Unity YAML structures."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "scripts/audit_legacy_v82_scene_layouts.py"
spec = importlib.util.spec_from_file_location("source_scene_audit", PATH)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class OriginalUnityStageSceneAuditTests(unittest.TestCase):
    def test_parser_only_reports_explicit_local_transform_and_prefab_refs(self):
        sample = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100
GameObject:
  m_Name: Original Stage Root
--- !u!4 &200
Transform:
  m_GameObject: {fileID: 100}
  m_LocalPosition: {x: 12.5, y: -2, z: 10}
  m_Father: {fileID: 0}
--- !u!1 &101
GameObject:
  m_Name: Child
--- !u!4 &201
Transform:
  m_GameObject: {fileID: 101}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_Father: {fileID: 200}
--- !u!1001 &300
PrefabInstance:
  m_SourcePrefab: {fileID: 100100000, guid: 1234567890abcdef1234567890abcdef, type: 3}
"""
        with tempfile.TemporaryDirectory() as tmp:
            f = Path(tmp) / "original.unity"
            f.write_text(sample, "utf-8")
            audit = module.inspect_scene(f)
        self.assertEqual(audit["yamlSections"], 5)
        self.assertEqual(audit["namedGameObjects"], 2)
        self.assertEqual(audit["explicitTransforms"], 2)
        self.assertEqual(audit["namedTransforms"], 2)
        self.assertEqual(audit["nonzeroLocalTransforms"], 1)
        self.assertEqual(audit["prefabInstances"], 1)
        self.assertEqual(audit["referencedSourcePrefabs"], 1)
        self.assertEqual(audit["rootNamesSample"], ["Original Stage Root"])
        self.assertIn("LOCAL", audit["note"])

    def test_real_eight_scenes_are_exact_official_catalog_and_original_git_blobs(self):
        roster = json.loads(
            (ROOT / "Assets/Resources/Catalog/roster.json").read_text("utf-8")
        )
        expected = {a["id"] for a in roster["arenas"] if a["game"] == "v82"}
        self.assertEqual(len(expected), 8)
        self.assertEqual(set(module.STAGES), expected)
        audit = module.audit(ROOT)
        self.assertEqual(audit["sceneCount"], 8)
        self.assertEqual(set(audit["scenes"]), expected)
        self.assertTrue(audit["missingProductionAssetsAreNotSynthesized"])
        self.assertIn("not_playable_arenas", audit["status"])
        for stage, row in audit["scenes"].items():
            self.assertGreater(row["sourceBytes"], 100000, stage)
            self.assertGreater(row["yamlSections"], 1, stage)
            self.assertGreater(row["explicitTransforms"], 0, stage)
            self.assertGreaterEqual(row["namedGameObjects"], 0)
            self.assertGreaterEqual(row["prefabInstances"], 0)
            self.assertEqual(len(row["originalGitBlobSha"]), 40)
            self.assertNotIn("spawnPoints", row)
            self.assertNotIn("worldObjectPlacements", row)

    def test_rejects_missing_or_tampered_archived_scene(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            with self.assertRaises(FileNotFoundError):
                module.audit(root)
            sample = root / "wrong_scene.unity"
            sample.write_text("not Unity YAML", "utf-8")
            with self.assertRaises(ValueError):
                module.inspect_scene(sample)


if __name__ == "__main__":
    unittest.main()
