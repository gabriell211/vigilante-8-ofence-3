"""Canonical archived XRTP road-profile join and negative provenance tests."""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_xrtp_road_profiles as origin

XRTP = "e45072bfca89e3e47966d4fee255579b"
MANAGER = "f" * 32
PREFAB = "a" * 32
MATERIAL = "b" * 32


def synthetic_prefab(width=65536, step=81920):
    return f"""%YAML 1.1
--- !u!114 &-321
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {XRTP}, type: 3}}
  timFarList:
  - {{fileID: 2100000, guid: {MATERIAL}, type: 2}}
  DAT_1C: 48
  DAT_24: {width}
  DAT_28: {step}
  DAT_2C: 294
  DAT_2E: 640
  DAT_30: 281
"""


def synthetic_scene(guid=PREFAB, index=0):
    return f"""%YAML 1.1
--- !u!114 &35
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {MANAGER}, type: 3}}
  xrtpList:
  - {{fileID: -321, guid: {guid}, type: 3}}
  juncList: []
--- !u!114 &90
MonoBehaviour:
  m_Script: {{fileID: 11500000, guid: {XRTP}, type: 3}}
  DAT_24: {index}
"""


class ArchivedRoadProfileTests(unittest.TestCase):
    def test_original_order_is_level_manager_list_not_any_prefab_name(self):
        refs = origin.scene_xrtp_references(synthetic_scene(), MANAGER)
        self.assertEqual(refs, [{"fileId": "-321", "guid": PREFAB, "type": 3}])
        with self.assertRaises(ValueError):
            origin.scene_xrtp_references(synthetic_scene() +
                "--- !u!114 &400\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " +
                MANAGER + ", type: 3}\n  xrtpList: []\n", MANAGER)

    def test_original_full_width_and_step_and_material_ref(self):
        row = origin.original_xrtp_profile(synthetic_prefab(), XRTP, "-321")
        self.assertEqual(row["sourceWidthParameterFixed"], 65536)
        self.assertEqual(row["sourceLongitudinalStepFixed"], 81920)
        self.assertEqual(row["sourceTextureRefs"][0]["guid"], MATERIAL)
        self.assertEqual(row["sourceTextureFlags"], 294)
        self.assertIsNone(origin.original_xrtp_profile(synthetic_prefab(), XRTP, "999"))
        self.assertIsNone(origin.original_xrtp_profile(synthetic_prefab(), MANAGER, "-321"))

    def test_source_profile_malformed_or_missing_is_never_guessed(self):
        for raw in (
            synthetic_prefab(width=0),
            synthetic_prefab(step=0),
            synthetic_prefab().replace("  DAT_24: 65536\n", ""),
            synthetic_prefab().replace(f"guid: {MATERIAL}", "guid: bad"),
        ):
            self.assertIsNone(origin.original_xrtp_profile(raw, XRTP, "-321"))

    def test_original_reference_missing_guid_does_not_resolve(self):
        ref = {"guid": "c" * 32, "fileId": "-321", "type": 3}
        self.assertEqual(origin.resolved_profile(ref, "STEELMIL", {}, ROOT, XRTP)["status"],
                         "xrtp_source_prefab_guid_unresolved")
        ref = {"guid": None, "fileId": "-321", "type": 3}
        self.assertEqual(origin.resolved_profile(ref, "STEELMIL", {}, ROOT, XRTP)["status"],
                         "invalid_external_xrtp_reference")

    def test_all_eight_preserved_scenes_and_rseg_source_links(self):
        result = origin.audit(ROOT)
        self.assertEqual(result["sceneCount"], 8)
        self.assertFalse(result["originalRoadSurfaceMeshCertified"])
        self.assertFalse(result["originalRoadUVsCertified"])
        self.assertFalse(result["originalRoadCollisionCertified"])
        saw_verified = False
        for stage in result["stages"].values():
            self.assertEqual(stage["sourceSegmentCount"], len(stage["segments"]))
            self.assertEqual(stage["sourceXrtpListLength"], len(stage["sourceProfiles"]))
            for profile in stage["sourceProfiles"]:
                if profile["status"] == "original_source_xrtp_profile_verified":
                    saw_verified = True
                    self.assertGreater(profile["sourceWidthParameterFixed"], 0)
                    self.assertTrue(profile["sourceTextureRefs"])
            for segment in stage["segments"]:
                self.assertNotIn("officialRoadMesh", segment)
                self.assertNotIn("officialRoadMaterial", segment)
                if segment["originalXRTPProfileStatus"] == "original_source_xrtp_profile_verified":
                    self.assertGreater(segment["sourceWidthParameterFixed"], 0)
        self.assertTrue(saw_verified)


if __name__ == "__main__":
    unittest.main()
