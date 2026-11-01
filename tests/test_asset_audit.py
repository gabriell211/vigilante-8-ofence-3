import json
import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from audit_content import inspect


class AssetAuditTests(unittest.TestCase):
    def _new_repo(self, folder):
        root = pathlib.Path(folder)
        catalog = root / "Assets/Resources/Catalog"
        catalog.mkdir(parents=True)
        (catalog / "roster.json").write_text(
            json.dumps({"vehicles": [{"id": "vehicle_id", "assetId": "v8_test", "name": "Test Car"}],
                        "arenas": [{"id": "arena_id", "assetId": "v8_test_arena", "name": "Test Arena"}]}),
            encoding="utf-8")
        return root

    def test_missing_assets_are_not_claimed_as_integrated(self):
        with tempfile.TemporaryDirectory() as folder:
            result = inspect(self._new_repo(folder))
            self.assertFalse(result["complete"])
            self.assertEqual((result["expected"], result["present"], result["missing"]), (2, 0, 2))

    def test_partial_assets_remain_incomplete(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self._new_repo(folder)
            path = root / "Assets/Resources/Vehicles/v8_test.prefab"
            path.parent.mkdir(parents=True)
            path.write_text("real authored prefab would live here", encoding="utf-8")
            result = inspect(root)
            self.assertFalse(result["complete"])
            self.assertEqual((result["present"], result["missing"]), (1, 1))

    def test_full_prefab_presence_is_only_a_file_check(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self._new_repo(folder)
            for path in ("Assets/Resources/Vehicles/v8_test.prefab",
                         "Assets/Resources/Arenas/v8_test_arena.prefab"):
                destination = root / path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_text("presence only", encoding="utf-8")
            result = inspect(root)
            self.assertTrue(result["complete"])
            self.assertIn("not proof", result["note"])


if __name__ == "__main__":
    unittest.main()
