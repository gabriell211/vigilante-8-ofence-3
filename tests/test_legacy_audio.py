import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / "scripts"))
from migrate_legacy_audio import guid_from_meta


class OriginalAudioTests(unittest.TestCase):
    def test_valid_guid(self):
        self.assertEqual(guid_from_meta("guid: " + "a"*32 + "\n"), "a"*32)

    def test_missing_guid_rejected(self):
        with self.assertRaises(ValueError):
            guid_from_meta("AudioImporter:\n  enabled: true")

    def test_truncated_guid_rejected(self):
        with self.assertRaises(ValueError):
            guid_from_meta("guid: abc\n")
