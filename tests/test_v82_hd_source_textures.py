"""Gate real source-only V82 HD texture option without generating fake pixels in CI."""
from pathlib import Path
import json
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/legacy-v82-textures.json"
BUILDER = ROOT / "Assets/Editor/OriginalV82TextureQualityBuilder.cs"
PREVIEW = ROOT / "Assets/Editor/OriginalVehiclePreviewBuilder.cs"


class ArchivedTextureQualityContractTests(unittest.TestCase):
    def test_all_790_source_paths_have_4x_bounded_geometry(self):
        manifest = json.loads(MANIFEST.read_text("utf-8"))
        self.assertEqual(manifest["vehicleCount"], 18)
        self.assertEqual(manifest["uniqueTextures"], 790)
        self.assertEqual(len(manifest["textures"]), 790)
        self.assertLessEqual(max(item["width"] for item in manifest["textures"].values()), 1024)
        self.assertLessEqual(max(item["height"] for item in manifest["textures"].values()), 1024)
        self.assertEqual(len({item["path"] for item in manifest["textures"].values()}), 790)
        for guid, item in manifest["textures"].items():
            self.assertRegex(guid, r"^[0-9a-f]{32}$")
            self.assertRegex(item["sha256"], r"^[0-9a-f]{64}$")
            self.assertTrue(item["path"].startswith("Assets/GameContent/LegacyTextureBank/"))
            self.assertTrue(item["sourcePath"].startswith("LegacySources/Vigilante2Unity/Assets/"))
            self.assertFalse(".." in item["path"] or ".." in item["sourcePath"])

    def test_lanczos_not_required_bicubic_source_pixel_and_origin_safety(self):
        code = BUILDER.read_text("utf-8")
        for term in (
            "UpscaleBicubic(Color32[] source",
            "Graphics.Blit(original, temporary);",
            "readable.GetPixels32()",
            "OriginalPath",
            "VerifySource(record);",
            "AssetDatabase.AssetPathToGUID(source.Path)",
            "Sha256(Absolute(source.OriginalPath))",
            "Sha256(Absolute(source.Path))",
            "Sha256(Absolute(target))",
            "TextureImporterCompression.Uncompressed",
            "FilterMode.Trilinear",
            "anisoLevel = 8",
            "mipmapEnabled = true",
            "SourcePrefab",  # placeholder should not appear in final
        )[:-1]:
            self.assertIn(term, code)
        self.assertNotIn("UnityEngine.Random", code)
        self.assertNotIn("AssetDatabase.DeleteAsset(source.Path)", code)
        self.assertNotIn("EditorUtility.SetDirty(original)", code)

    def test_real_vehicle_preview_materials_use_authentic_4x_source(self):
        code = PREVIEW.read_text("utf-8")
        self.assertIn(
            "OriginalV82TextureQualityBuilder.EnsureEnhanced(assetPath)", code)
        self.assertIn('material.SetTexture("_BaseMap", texture);', code)
        self.assertIn("OriginalObjMeshBuilder.CreateInspectionRoot", code)
        self.assertNotIn("new Material(Shader.Find(\"Standard\"))", code)

    def test_generated_files_live_outside_original_bank_and_release(self):
        code = BUILDER.read_text("utf-8")
        self.assertIn("Assets/GameContent/EnhancedSourceTexturesV82", code)
        self.assertNotIn("Assets/Resources/", code)
        self.assertIn("V82_SOURCE_BICUBIC_4X_V1", code)
        self.assertIn("original.SourcePath", code.replace("OriginalPath", "SourcePath"))


if __name__ == "__main__":
    unittest.main()
