import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from verify_official_asset_sources import verify


class OfficialAssetSourceTests(unittest.TestCase):
    def test_every_official_sequel_vehicle_has_a_real_legacy_source(self):
        result = verify()
        self.assertEqual(result["officialCharacters"], 23)
        self.assertEqual(result["officialVehicles"], 31)
        self.assertEqual(result["sequelOriginalVehiclePrefabSources"], 18)
        self.assertEqual(result["sequelOriginalSceneSources"], 8)

    def test_n64_bonus_is_counted_but_not_claimed_as_migrated(self):
        result = verify()
        self.assertEqual(result["officialArenas"], 19)
        self.assertEqual(result["n64ExclusiveArenas"], 1)
        self.assertTrue(result["n64BonusConverterReference"])

    def test_legacy_sources_are_not_a_fake_release(self):
        result = verify()
        self.assertFalse(result["firstGameRetailMediaBundled"])
        self.assertLessEqual(result["activeUnity6VehiclePrefabs"], 31)
        self.assertLessEqual(result["activeUnity6ArenaPrefabs"], 19)
        if result["activeUnity6VehiclePrefabs"] < 31 or result["activeUnity6ArenaPrefabs"] < 19:
            self.assertFalse(result["playerReady"])


if __name__ == "__main__":
    unittest.main()
