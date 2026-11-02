"""Source-backed road shader/material-to-BMP references and failure modes."""
import sys
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import audit_v82_xrtp_surface_textures as source

GUID = "a" * 32


class RoadSourceMaterialTests(unittest.TestCase):
    def test_actual_eight_stages_report_only_source_material_evidence(self):
        data = source.audit(ROOT)
        self.assertEqual(data["sceneCount"], 8)
        self.assertFalse(data["originalRoadUVsCertified"])
        self.assertFalse(data["originalRoadSurfaceMeshCertified"])
        self.assertFalse(data["originalRoadRendererCertified"])
        self.assertFalse(data["originalRoadCollisionCertified"])
        steelmill = data["stages"]["v82_steel_mill"]
        self.assertEqual(steelmill["sourceXrtpListLength"], 1)
        self.assertEqual(steelmill["sourceProfiles"][0]["status"],
                         "original_road_source_materials_verified")
        material = steelmill["sourceProfiles"][0]["sourceMaterials"][0]
        self.assertEqual(material["status"],
                         "original_road_material_main_texture_verified")
        self.assertEqual(material["originalMainTexture"]["status"],
                         "original_stage_bmp_source_verified")
        self.assertTrue(material["materialAssetPath"].endswith(".mat"))
        self.assertTrue(material["originalMainTexture"]["textureAssetPath"].lower()
                        .endswith(".bmp"))

    def test_invalid_source_ref_never_guesses_an_asphalt_texture(self):
        for item in (
            {"type": 2, "fileId": "0", "guid": GUID},
            {"type": 3, "fileId": "2100000", "guid": GUID},
            {"type": 2, "fileId": "2100000", "guid": GUID}
        ):
            result = source.resolve_material(item, {}, {}, ROOT)
            self.assertNotEqual(result["status"],
                                "original_road_material_main_texture_verified")
            self.assertNotIn("textureAssetPath", result)

    def test_bmp_header_must_be_real_and_safely_bounded(self):
        self.assertIsNone(source.bitmap_dimensions(b"not a BMP"))
        self.assertIsNone(source.bitmap_dimensions(b"BM" + bytes(52)))
        header = bytearray(54)
        header[:2] = b"BM"
        header[14:18] = (40).to_bytes(4, "little")
        header[18:22] = (256).to_bytes(4, "little", signed=True)
        header[22:26] = (-64).to_bytes(4, "little", signed=True)
        self.assertEqual(source.bitmap_dimensions(header), (256, 64))
        header[18:22] = (0).to_bytes(4, "little", signed=True)
        self.assertIsNone(source.bitmap_dimensions(header))

    def test_missing_original_material_main_tex_is_not_promoted(self):
        self.assertEqual(source.texture_by_ref(
            ROOT, None, {}, ROOT)["status"], "material_bitmap_slot_missing")


if __name__ == "__main__":
    unittest.main()
