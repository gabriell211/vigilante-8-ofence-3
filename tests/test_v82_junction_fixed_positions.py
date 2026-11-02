"""JUNC_DB source-fixed-point positions must never become invented arena placements."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_junction_fixed_positions as audit

JUNC_GUID = "c031ef78ecc9f51459ebf05d0fb88f70"
MESH_GUID = "c9ede6c07e7b52d4fb274750ce3e096c"
GAME_GUID = "a4002230e3f441e4bb2c818028c8a835"
BANK = "99682d098e70a4647854c8897304a6ab"
OTHER = "ffffffffffffffffffffffffffffffff"
STAGE = "v82_steel_mill"


def junction(x=4096, y=8192, z=12288, mesh="101", bank=BANK,
             obj="1", fid="100"):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_GameObject: {{fileID: {obj}}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: {JUNC_GUID}, type: 3}}
  DAT_00: {{x: {x}, y: {y}, z: {z}}}
  DAT_0C: {{fileID: -5, guid: {bank}, type: 3}}
  DAT_18: {{fileID: {mesh}}}
"""


def scene_bank():
    return f"""%YAML 1.1
--- !u!1 &1
GameObject:
  m_Name: JUNC05
--- !u!4 &2
Transform:
  m_GameObject: {{fileID: 1}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Father: {{fileID: 0}}
--- !u!114 &101
MonoBehaviour:
  m_GameObject: {{fileID: 1}}
  m_Enabled: 1
  m_Script: {{fileID: 11500000, guid: {MESH_GUID}, type: 3}}
  tmdID: 7
  xobf: {{fileID: -1, guid: {BANK}, type: 3}}
"""


def map_records(raw):
    return audit.link_junctions(raw, JUNC_GUID, MESH_GUID, STAGE,
        BANK, {STAGE + "_tmd_007"}, 4096)


class SourceJunctionFixedPositionTests(unittest.TestCase):
    def test_fixed_point_axis_conversion_handles_signed_values_and_zero(self):
        self.assertEqual(audit.source_to_unity_local((4096, -8192, 0), 4096),
                         {"x": 1.0, "y": 2.0, "z": 0.0})
        self.assertEqual(audit.source_to_unity_local((0, 0, 0), 4096),
                         {"x": 0.0, "y": 0.0, "z": 0.0})
        self.assertIsNone(audit.source_to_unity_local((2 ** 31, 0, 0), 4096))
        self.assertIsNone(audit.source_to_unity_local((float("nan"), 0, 0), 4096))
        with self.assertRaises(ValueError):
            audit.source_to_unity_local((1, 2, 3), 0)

    def test_junction_fileid_links_source_mesh_and_preserves_raw_int32(self):
        rows = map_records(scene_bank() + junction())
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertEqual(row["sourceDat00Int32"], {"x": 4096, "y": 8192, "z": 12288})
        self.assertEqual(row["debugScalePreviewLocalPosition"],
                         {"x": 1.0, "y": -2.0, "z": 3.0})
        self.assertEqual(row["linkedOriginalTmdGroup"], STAGE + "_tmd_007")
        self.assertEqual(row["referenceStatus"], "linked_junction_and_original_stage_tmd")
        self.assertNotIn("gameplayWorldPosition", row)

    def test_failed_mesh_bank_and_int32_guards(self):
        source = scene_bank()
        row = map_records(source + junction(mesh="0"))[0]
        self.assertEqual(row["referenceStatus"], "source_junction_without_mesh")
        row = map_records(source + junction(bank=OTHER))[0]
        self.assertEqual(row["referenceStatus"], "junction_or_mesh_uses_different_xobf")
        row = map_records(source + junction(mesh="555"))[0]
        self.assertEqual(row["referenceStatus"], "vigmesh_component_not_found_or_not_original")
        row = map_records(source + junction(x=str(2 ** 31)))[0]
        self.assertEqual(row["referenceStatus"], "invalid_or_missing_source_dat00")
        self.assertIsNone(row["debugScalePreviewLocalPosition"])

    def test_reference_must_belong_to_same_gameobject(self):
        row = map_records(scene_bank() + junction(obj="99"))[0]
        self.assertEqual(row["referenceStatus"], "linked_vigmesh_belongs_to_another_gameobject")
        self.assertIsNone(row["gameObjectName"])

    def test_debug_scale_must_be_inspector_config_on_real_manager(self):
        scene = f"""%YAML 1.1
--- !u!114 &1
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {OTHER}, type: 3}}
  translateFactor: 32
--- !u!114 &2
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {GAME_GUID}, type: 3}}
  translateFactor: 4096
"""
        self.assertEqual(audit.debug_scene_translate_factor(scene, GAME_GUID), 4096)
        with self.assertRaises(ValueError):
            audit.debug_scene_translate_factor(scene.replace("4096", "0"), GAME_GUID)
        with self.assertRaises(ValueError):
            audit.debug_scene_translate_factor("%YAML 1.1\n", GAME_GUID)

    def test_source_eight_stages_remain_unapproved_for_release(self):
        record = audit.audit(ROOT)
        self.assertEqual(record["sceneCount"], 8)
        self.assertEqual(record["debugSceneSerializedTranslateFactor"], 4096)
        self.assertEqual(record["gameManagerSourceDefaultTranslateFactor"], 10000)
        self.assertFalse(record["activeMatchTranslateFactorVerified"])
        self.assertFalse(record["arenaGeometryPlacementCertified"])
        for stage_id, stage in record["stages"].items():
            self.assertEqual(len(stage["records"]), stage["junctionComponents"], stage_id)
            for junc in stage["records"]:
                self.assertNotIn("officialSpawn", junc)
                if junc["linkedOriginalTmdGroup"]:
                    self.assertTrue(junc["linkedOriginalTmdGroup"].startswith(stage_id + "_tmd_"))


if __name__ == "__main__":
    unittest.main()
