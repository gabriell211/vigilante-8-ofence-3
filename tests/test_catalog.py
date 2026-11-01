import copy
import json
import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from validate_catalog import validate


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.data = validate()

    def check_rejects(self, change):
        modified = copy.deepcopy(self.data)
        change(modified)
        with tempfile.TemporaryDirectory() as tmp:
            path = pathlib.Path(tmp) / "catalog.json"
            path.write_text(json.dumps(modified), encoding="utf-8")
            with self.assertRaises(ValueError):
                validate(path)

    def test_catalog_is_valid(self):
        self.assertEqual(len(self.data["characters"]), 23)
        self.assertEqual(len(self.data["vehicles"]), 31)
        self.assertEqual(len(self.data["arenas"]), 19)

    def test_rejects_duplicate_vehicle(self):
        self.check_rejects(lambda d: d["vehicles"][1].update(
            name=d["vehicles"][0]["name"]))

    def test_rejects_missing_character(self):
        self.check_rejects(lambda d: d["vehicles"][0].update(
            characterId="does_not_exist"))

    def test_rejects_reused_alias(self):
        self.check_rejects(lambda d: d["characters"][1]["aliases"].append(
            d["characters"][0]["name"]))

    def test_rejects_path_traversal(self):
        self.check_rejects(lambda d: d["arenas"][0].update(assetId="../secret"))

    def test_shared_character_has_distinct_vehicles(self):
        owner = next(c for c in self.data["characters"] if c["id"] == "sheila")
        cars = [v["id"] for v in self.data["vehicles"] if v["characterId"] == owner["id"]]
        self.assertEqual(owner["appearances"], ["v8", "v82"])
        self.assertEqual(len(cars), 2)
        self.assertEqual(len(set(cars)), 2)

    def test_dave_and_cultsmen_are_distinct(self):
        ids = {c["id"] for c in self.data["characters"]}
        self.assertIn("dave", ids)
        self.assertIn("dave_s_cultsmen", ids)

    def test_lord_clyde_is_slick_clyde(self):
        clyde = next(c for c in self.data["characters"] if c["id"] == "slick_clyde")
        self.assertIn("Lord Clyde", clyde["aliases"])


    def test_n64_bonus_is_exclusive_and_not_duplicated(self):
        bonuses = [a for a in self.data["arenas"] if a["bonus"]]
        self.assertEqual(len(bonuses), 1)
        self.assertEqual(bonuses[0]["id"], "v8_super_dreamland_64")
        self.assertEqual(bonuses[0]["platforms"], ["n64"])

    def test_rejects_invented_stage_even_when_count_is_correct(self):
        self.check_rejects(lambda d: d["arenas"][0].update(name="Nonexistent Stage"))

    def test_rejects_changed_official_vehicle_owner(self):
        self.check_rejects(lambda d: d["vehicles"][0].update(characterId="sheila"))

    def test_rejects_wrong_bonus_platform(self):
        self.check_rejects(lambda d: d["arenas"][-1].update(platforms=["ps1"]))

if __name__ == "__main__":
    unittest.main()
