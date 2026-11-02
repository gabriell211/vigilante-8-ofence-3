"""Contract checks for attaching real indexed stage WAVs to 8 source-only prefabs."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class OriginalStageAudioContracts(unittest.TestCase):
    def test_all_eight_stage_banks_are_original_unique_and_not_placeholder_audio(self):
        catalog = json.loads(
            (ROOT / "Assets/GameContent/LegacyAudioV82/catalog.json").read_text("utf-8")
        )
        roster = json.loads((ROOT / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
        expected = {a["id"] for a in roster["arenas"] if a["game"] == "v82"}
        stages = [b for b in catalog["banks"] if b["kind"] == "arena"]
        self.assertEqual(len(stages), 8)
        self.assertEqual({s["id"] for s in stages}, expected)
        self.assertEqual(len({s["id"] for s in stages}), 8)
        for bank in stages:
            self.assertGreater(len(bank["samples"]), 0)
            indices = set()
            for sample in bank["samples"]:
                self.assertNotIn(sample["originalIndex"], indices)
                indices.add(sample["originalIndex"])
                self.assertGreaterEqual(sample["originalIndex"], 0)
                self.assertTrue(
                    sample["assetPath"].startswith("Assets/GameContent/LegacyAudioV82/")
                )
                self.assertTrue((ROOT / sample["assetPath"]).is_file())
                self.assertEqual(len(sample["guid"]), 32)

    def test_editor_import_wires_real_stage_objects_to_matching_original_wav_banks(self):
        binder = (ROOT / "Assets/Editor/OriginalAudioBankBinder.cs").read_text("utf-8")
        stages = (ROOT / "Assets/Editor/OriginalStageObjectReviewBuilder.cs").read_text("utf-8")
        inspector = (ROOT / "Assets/Editor/OriginalStageAudioReviewWindow.cs").read_text("utf-8")
        for signal in (
            "AttachToStageInspectionPrefabs()",
            'b.kind == "arena"',
            "OriginalStageObjectPreviewIdentity",
            "ResolveVerifiedSamples(bank)",
            "AssetDatabase.AssetPathToGUID(sample.assetPath)",
            "PrefabUtility.SaveAsPrefabAsset(prefab, path)",
            "OfficialAssetIdentity",
            "Collider",
        ):
            self.assertIn(signal, binder)
        self.assertIn("OriginalAudioBankBinder.AttachToStageInspectionPrefabs()", stages)
        self.assertIn("OriginalStageObjectReviewBuilder.PrepareAll()", inspector)
        self.assertIn("Selection.activeObject = clip", inspector)
        self.assertIn("EditorGUIUtility.PingObject(clip)", inspector)
        self.assertNotIn("GameObject.CreatePrimitive", binder)
        self.assertNotIn("Resources/Arenas/", binder)
        self.assertNotIn("AudioSource.Play", inspector)


if __name__ == "__main__":
    unittest.main()
