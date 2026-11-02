"""Ensure the raw V82 car handling manifest is authentic and reproducible."""
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from extract_v82_handling import extract, SOURCE, TARGET


class OriginalHandlingContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = json.loads(TARGET.read_text("utf-8"))
        cls.official = json.loads((ROOT / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
        cls.editor = (ROOT / "Assets/Editor/LegacyVehiclePlaytestBuilder.cs").read_text("utf-8")
        cls.profile = (ROOT / "Assets/Scripts/Catalog/LegacyVehicleHandlingProfile.cs").read_text("utf-8")

    def test_source_manifest_is_exactly_reproducible(self):
        self.assertEqual(extract(SOURCE.read_text("utf-8-sig")), self.document)

    def test_all_eighteen_source_cars_have_unique_original_stats(self):
        cars = self.document["vehicles"]
        self.assertEqual(len(cars), 18)
        self.assertEqual(len({c["vehicleId"] for c in cars}), 18)
        self.assertEqual({c["vehicleId"] for c in cars},
                         {v["id"] for v in self.official["vehicles"] if v["game"] == "v82"})
        self.assertEqual(len({(c["rawHalfHealth"], c["rawLightness"], c["dat10"], c["dat12"],
                               c["dat13"]) for c in cars}), 18)
        for car in cars:
            self.assertGreater(car["rawHalfHealth"], 0)
            self.assertGreater(car["rawLightness"], 0)

    def test_editor_wires_source_metadata_into_each_real_mesh(self):
        for fragment in ("LoadSourceHandling(vehicles)", "LegacyVehicleHandlingProfile",
                         "stats.Configure(", "stats.ApplyProvisionalHandling(drive)",
                         "rigidbody.mass = drive.massKg"):
            self.assertIn(fragment, self.editor)
        self.assertIn("authentic GameManager stats", self.editor)
        self.assertIn("PROTOTYPE calibration", self.profile)
        self.assertNotIn("GameObject.CreatePrimitive", self.editor)

    def test_release_remains_blocked_for_dev_prototypes(self):
        release = (ROOT / "Assets/Editor/WindowsReleaseBuilder.cs").read_text("utf-8")
        self.assertIn("LegacyVehiclePlaytest", release)
        self.assertIn("Development-only TMD physics prototype cannot be released", release)


if __name__ == "__main__":
    unittest.main()
