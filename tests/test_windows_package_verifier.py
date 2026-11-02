"""Unit-test the ZIP verification algorithm without publishing fake game assets."""
import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

from scripts.verify_windows_package import (
    PackageVerificationError,
    verify_directory,
    verify_one,
)


class RealUnityArchiveVerifierTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.archive = self.directory / "Vigilante8Offence3-0.1.0-dev-win-x64.zip"
        self.manifest = {
            "product": "Vigilante 8 Offence 3",
            "version": "0.1.0-dev",
            "platform": "win-x64",
            "unity": "6000.6.7f1",
            "assetCatalog": {"characters": 23, "vehicles": 31, "arenas": 19},
        }

    def make_fixture(self, *, with_assembly=True, with_data=True, catalog=None):
        # Synthetic bytes are only verifier test fixtures; not playable artifacts.
        contents = {
            "Vigilante8Offence3.exe": b"MZ-TEST-ONLY",
            "UnityPlayer.dll": b"MZ-TEST-ONLY",
            "release-manifest.json": json.dumps(
                {**self.manifest, **({"assetCatalog": catalog} if catalog else {})}
            ).encode(),
        }
        if with_assembly:
            contents["GameAssembly.dll"] = b"MZ-TEST-ONLY"
        if with_data:
            contents["Vigilante8Offence3_Data/globalgamemanagers"] = b"TEST-DATA"
        with zipfile.ZipFile(self.archive, "w", zipfile.ZIP_DEFLATED) as dest:
            for name, data in contents.items():
                dest.writestr(name, data)
        digest = hashlib.sha256(self.archive.read_bytes()).hexdigest()
        Path(str(self.archive) + ".sha256").write_text(
            f"{digest}  {self.archive.name}\n", encoding="utf-8"
        )

    def test_well_formed_synthetic_fixture_checks_verifier_only(self):
        self.make_fixture()
        result = verify_directory(self.directory)
        self.assertTrue(result["verified"])
        self.assertEqual(result["version"], "0.1.0-dev")

    def test_wrong_sha256_is_rejected(self):
        self.make_fixture()
        self.archive.write_bytes(self.archive.read_bytes() + b"tampering")
        with self.assertRaisesRegex(PackageVerificationError, "SHA-256"):
            verify_one(self.archive)

    def test_uncompiled_or_missing_windows_player_is_rejected(self):
        self.make_fixture(with_assembly=False)
        with self.assertRaisesRegex(PackageVerificationError, "GameAssembly"):
            verify_one(self.archive)

    def test_missing_real_unity_data_is_rejected(self):
        self.make_fixture(with_data=False)
        with self.assertRaisesRegex(PackageVerificationError, "player data"):
            verify_one(self.archive)

    def test_incomplete_official_catalog_is_rejected(self):
        self.make_fixture(catalog={"characters": 23, "vehicles": 18, "arenas": 7})
        with self.assertRaisesRegex(PackageVerificationError, "23/31/19"):
            verify_one(self.archive)

    def test_multiple_windows_zips_are_rejected(self):
        self.make_fixture()
        self.archive.with_name("Vigilante8Offence3-second-win-x64.zip").write_bytes(
            self.archive.read_bytes()
        )
        with self.assertRaisesRegex(PackageVerificationError, "Exactly one"):
            verify_directory(self.directory)


if __name__ == "__main__":
    unittest.main()
