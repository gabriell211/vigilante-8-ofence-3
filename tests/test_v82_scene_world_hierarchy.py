"""Verify world translations use complete original parent TRS, never guessed positions."""
import math
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import resolve_v82_scene_hierarchy as original


def node(name, object_id, transform_id, parent, pos, rot="0, 0, 0, 1",
         scale="1, 1, 1", kind="4"):
    px, py, pz = pos.split(", ")
    rx, ry, rz, rw = rot.split(", ")
    sx, sy, sz = scale.split(", ")
    return f"""--- !u!1 &{object_id}
GameObject:
  m_Name: {name}
--- !u!{kind} &{transform_id}
Transform:
  m_GameObject: {{fileID: {object_id}}}
  m_LocalRotation: {{x: {rx}, y: {ry}, z: {rz}, w: {rw}}}
  m_LocalPosition: {{x: {px}, y: {py}, z: {pz}}}
  m_LocalScale: {{x: {sx}, y: {sy}, z: {sz}}}
  m_Father: {{fileID: {parent}}}
"""


class WorldTransformEvidenceTests(unittest.TestCase):
    def test_source_parent_rotation_and_nonuniform_scale_affect_world_position(self):
        half = math.sqrt(0.5)
        data = ("%YAML 1.1\n" +
                node("parent", 10, 100, 0, "10, 0, 0",
                     f"0, 0, {half}, {half}", "2, 3, 1") +
                node("child", 11, 101, 100, "1, 0, 0") +
                node("grandchild", 12, 102, 101, "0, 1, 0"))
        rows = {r["transformFileId"]: r for r in original.resolve_hierarchy(
            original.read_explicit_transforms(data))}
        self.assertEqual(len(rows), 3)
        self.assertEqual(rows["101"]["gameObjectName"], "child")
        self.assertEqual(rows["101"]["resolutionStatus"], "resolved_explicit_hierarchy")
        self.assertAlmostEqual(rows["101"]["worldPosition"]["x"], 10, places=6)
        self.assertAlmostEqual(rows["101"]["worldPosition"]["y"], 2, places=6)
        self.assertAlmostEqual(rows["102"]["worldPosition"]["x"], 7, places=6)
        self.assertAlmostEqual(rows["102"]["worldPosition"]["y"], 2, places=6)

    def test_no_parent_origin_fallback_and_no_cycles(self):
        data = ("%YAML 1.1\n" +
                node("orphan", 1, 10, 999, "1, 2, 3") +
                node("cycleA", 2, 20, 21, "4, 5, 6") +
                node("cycleB", 3, 21, 20, "7, 8, 9"))
        rows = original.resolve_hierarchy(original.read_explicit_transforms(data))
        self.assertEqual(rows[0]["resolutionStatus"], "unresolved_parent_not_serialized")
        self.assertEqual(rows[1]["resolutionStatus"], "cycle_in_source_hierarchy")
        self.assertEqual(rows[2]["resolutionStatus"], "cycle_in_source_hierarchy")
        self.assertTrue(all(r["worldPosition"] is None for r in rows))

    def test_incomplete_and_nonfinite_transforms_are_not_resolved(self):
        raw = original.read_explicit_transforms("%YAML 1.1\n" +
            node("bad", 1, 10, 0, "NaN, 0, 0") +
            node("good", 2, 11, 0, "0, 0, 0", kind="224"))
        rows = original.resolve_hierarchy(raw)
        self.assertIsNone(rows[0]["worldPosition"])
        self.assertEqual(rows[1]["componentType"], "RectTransform")
        self.assertEqual(rows[1]["worldPosition"], {"x": 0.0, "y": 0.0, "z": 0.0})
        with self.assertRaises(ValueError):
            original.read_explicit_transforms("not yaml")

    def test_eight_authentic_scenes_are_hash_checked_and_no_spawns_invented(self):
        report = original.audit(ROOT)
        self.assertEqual(report["sceneCount"], 8)
        self.assertFalse(report["originalSpawnsCertified"])
        self.assertFalse(report["stageObjectBankMappingCertified"])
        self.assertFalse(report["prefabOverridesResolved"])
        for stage, record in report["scenes"].items():
            self.assertEqual(len(record["originalGitBlobSha"]), 40, stage)
            self.assertGreater(record["serializedTransforms"], 0, stage)
            self.assertEqual(record["resolvedWorldTranslations"] +
                             record["unresolvedTransforms"],
                             record["serializedTransforms"], stage)
            self.assertNotIn("spawnPoints", record)
            for row in record["records"]:
                self.assertNotIn("spawn", row)
                self.assertEqual(row["worldPosition"] is not None,
                                 row["resolutionStatus"] == "resolved_explicit_hierarchy")


if __name__ == "__main__":
    unittest.main()
