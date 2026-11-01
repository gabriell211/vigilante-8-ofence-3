import json
import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from port_legacy_textures import read_guid, tim_references, build_index


class LegacyTextureTests(unittest.TestCase):
    def test_read_original_guid(self):
        self.assertEqual(read_guid("fileFormatVersion: 2\nguid: " + "a" * 32 + "\n"), "a" * 32)

    def test_missing_guid_is_rejected(self):
        with self.assertRaises(ValueError):
            read_guid("fileFormatVersion: 2")

    def test_original_tim_order_is_preserved(self):
        prefab = ("  timList:\n"
                  "  - {fileID: 2800000, guid: " + "a"*32 + ", type: 3}\n"
                  "  - {fileID: 2800000, guid: " + "b"*32 + ", type: 3}\n"
                  "  materialList: 00000000\n")
        self.assertEqual(tim_references(prefab), ["a"*32, "b"*32])

    def test_empty_tim_list_is_an_error(self):
        with self.assertRaises(ValueError):
            tim_references("  timList:\n  materialList: 00\n")

    def test_duplicate_source_guid_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            for name in ("A", "B"):
                folder = root / "LegacySources/Vigilante2Unity/Assets"
                folder.mkdir(parents=True, exist_ok=True)
                (folder / (name+".bmp")).write_bytes(b"BM" + bytes(58))
                (folder / (name+".bmp.meta")).write_text("guid: " + "a"*32 + "\n")
            with self.assertRaises(ValueError):
                build_index(root)


if __name__ == "__main__":
    unittest.main()
