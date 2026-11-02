"""Verify scene XOBF/TMD crosswalk relies exclusively on authentic serialized IDs."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_scene_mesh_references as original

MESH_SCRIPT = "c9ede6c07e7b52d4fb274750ce3e096c"
BANK = "99682d098e70a4647854c8897304a6ab"
OTHER = "ffffffffffffffffffffffffffffffff"
STAGE = "v82_steel_mill"


def go(obj_id="10", trans_id="11", parent="0"):
    return f"""--- !u!1 &{obj_id}
GameObject:
  m_Name: JUNC05
--- !u!4 &{trans_id}
Transform:
  m_GameObject: {{fileID: {obj_id}}}
  m_LocalPosition: {{x: 1, y: 2, z: 3}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Father: {{fileID: {parent}}}
"""


def mesh(comp_id="20", obj_id="10", script=MESH_SCRIPT,
         xobf=BANK, tmd="7", enabled="1"):
    return f"""--- !u!114 &{comp_id}
MonoBehaviour:
  m_GameObject: {{fileID: {obj_id}}}
  m_Enabled: {enabled}
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  tmdID: {tmd}
  xobf: {{fileID: -1, guid: {xobf}, type: 3}}
"""


def crosswalk(raw):
    return original.map_components(raw, STAGE, BANK,
        {STAGE + "_tmd_007"}, MESH_SCRIPT)


class GenuineSourceMeshCrosswalkTests(unittest.TestCase):
    def test_matches_only_original_bank_guid_and_existing_tmd_group(self):
        raw = ("%YAML 1.1\n" + go() + mesh())
        rows = crosswalk(raw)
        self.assertEqual(len(rows), 1)
        entry = rows[0]
        self.assertEqual(entry["gameObjectName"], "JUNC05")
        self.assertEqual(entry["tmdId"], 7)
        self.assertEqual(entry["originalTmdGroup"], STAGE + "_tmd_007")
        self.assertEqual(entry["serializedWorldPosition"],
                         {"x": 1.0, "y": 2.0, "z": 3.0})
        self.assertEqual(entry["referenceStatus"],
                         "matched_original_bank_and_tmd_with_serialized_transform")

    def test_fails_closed_on_foreign_bank_missing_group_or_parent(self):
        raw = ("%YAML 1.1\n" + go(parent="999") +
            mesh(comp_id="20", xobf=OTHER) +
            mesh(comp_id="21", tmd="8") + mesh(comp_id="22"))
        rows = crosswalk(raw)
        self.assertEqual([r["referenceStatus"] for r in rows],
            ["different_xobf_source_bank",
             "tmd_not_extracted_from_source_bank",
             "unresolved_serialized_transform_hierarchy"])
        self.assertTrue(all(r["serializedWorldPosition"] is None for r in rows))

    def test_does_not_confuse_other_monobehaviours_or_missing_transform(self):
        raw = ("%YAML 1.1\n" +
            mesh(comp_id="19", script=OTHER) +
            mesh(comp_id="20") +
            mesh(comp_id="21", obj_id="66", xobf=OTHER))
        rows = crosswalk(raw)
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0]["referenceStatus"], "missing_or_ambiguous_serialized_transform")
        self.assertEqual(rows[1]["referenceStatus"], "different_xobf_source_bank")

    def test_missing_source_guid_and_negative_tmd_are_not_invented(self):
        raw = ("%YAML 1.1\n" + go() +
            mesh(comp_id="20", xobf=OTHER, tmd="-1") +
            mesh(comp_id="21").replace("  xobf: {fileID: -1, guid: " + BANK + ", type: 3}", "  xobf: {fileID: 0}"))
        rows = crosswalk(raw)
        self.assertIsNone(rows[0]["originalTmdGroup"])
        self.assertEqual(rows[1]["referenceStatus"], "missing_xobf_reference")
        self.assertIsNone(rows[1]["referencedXobfGuid"])

    def test_real_eight_source_scenes_and_banks_are_hash_checked(self):
        report = original.audit(ROOT)
        self.assertEqual(report["sceneCount"], 8)
        self.assertFalse(report["inGamePositionVerified"])
        self.assertFalse(report["collisionVerified"])
        self.assertFalse(report["spawnPointsVerified"])
        for stage_id, stage in report["stages"].items():
            self.assertGreater(stage["originalTmdGroups"], 0, stage_id)
            self.assertEqual(len(stage["records"]), stage["vigMeshComponents"])
            self.assertNotIn("officialSpawnPoints", stage)
            for row in stage["records"]:
                self.assertNotIn("gameplayWorldPosition", row)
                if row["originalTmdGroup"]:
                    self.assertTrue(row["originalTmdGroup"].startswith(stage_id + "_tmd_"))


if __name__ == "__main__":
    unittest.main()
