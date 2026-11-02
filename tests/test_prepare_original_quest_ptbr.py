"""Synthetic binary fixtures verify importer behavior; they are never shipped."""
import copy
import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import prepare_original_quest_ptbr as p


def append_cstring(data: bytearray, value: str) -> int:
    offset = len(data)
    data.extend(value.encode("latin1") + b"\0")
    return offset


def native_fixture():
    original = bytearray(256)
    struct.pack_into("<I", original, 0, 13)
    for character in range(12):
        offset = len(original)
        original.extend(b"\0" * (4 * 16))
        struct.pack_into("<II", original, 4 + character * 8, 4, offset)
        for mission in range(4):
            actors = len(original)
            original.extend(struct.pack("<BBhH", 2, 4, -1, 17))
            text = append_cstring(
                original, f"Original {character} mission {mission}\\nDESTROY: source scenery")
            struct.pack_into("<BBHHHII", original, offset + mission * 16,
                             mission, 1, 10, 20, 1, actors, text)
    # Character slot 12 has no original campaign.
    sequel = bytearray(256)
    struct.pack_into("<I", sequel, 0, 18)
    for team in range(3):
        offset = len(sequel)
        sequel.extend(struct.pack("<I", 8))
        for mission in range(8):
            sequel.extend(bytes([team, mission]) + b"\0" * 18)
        struct.pack_into("<I", sequel, 4 + team * 4, offset)
    for character in range(15):
        offset = len(sequel)
        sequel.extend(b"\0" * (8 * 12))
        struct.pack_into("<I", sequel, 16 + character * 4, offset)
        for mission in range(8):
            actors = len(sequel)
            sequel.extend(struct.pack("<BBhH", 1, 0, 2, 36))
            text = append_cstring(
                sequel, f"Sequel {character} mission {mission}")
            struct.pack_into("<III", sequel, offset + mission * 12, 1, actors, text)
    executable = bytearray(p.V82_TEAM_OFFSET + p.V82_CHARACTER_COUNT)
    for character in range(p.V82_CHARACTER_COUNT):
        executable[p.V82_TEAM_OFFSET + character] = character % 3
    return bytes(original), bytes(sequel), bytes(executable)


class OriginalQuestBinaryPreparationTests(unittest.TestCase):
    def test_real_decoder_on_synthetic_binary_structures_and_source_counts(self):
        v8, v82, exe = native_fixture()
        audit = p.source_report(v8, v82, exe)
        self.assertEqual(sum(len(c["missions"]) for c in audit["original"]), 48)
        self.assertEqual(sum(len(c["missions"]) for c in audit["sequel"]), 120)
        self.assertEqual(len(audit["native_routes"]), 3)
        self.assertEqual(len(audit["native_routes"][0]["records"]), 8)
        self.assertEqual(audit["original"][0]["missions"][0]["target_first"], 10)
        self.assertEqual(audit["sequel"][0]["team"], 0)
        self.assertEqual(len(p.importer.worksheet(audit)["translations"]), 168)

    def test_bad_originals_or_missing_team_executable_are_rejected(self):
        v8, v82, exe = native_fixture()
        with self.assertRaisesRegex(ValueError, "SLUS_008.68"):
            p.source_report(v8, v82, exe[:-2])
        with self.assertRaisesRegex(ValueError, "Tabela de equipes"):
            p.source_report(v8, v82, exe[:-1] + b"\x08")
        with self.assertRaises(ValueError):
            p.source_report(bytes(256), v82, exe)
        with self.assertRaises(ValueError):
            p.source_report(v8, bytes(256), exe)

    def test_previous_translation_reuse_requires_exact_original_hash(self):
        v8, v82, exe = native_fixture()
        worksheet = p.importer.worksheet(p.source_report(v8, v82, exe))
        previous = copy.deepcopy(worksheet)
        previous["translations"][0]["ptBr"] = "Tradução revisada da primeira missão"
        self.assertEqual(p.merge_translations(worksheet, previous), 1)
        self.assertEqual(worksheet["translations"][0]["ptBr"],
                         "Tradução revisada da primeira missão")
        previous["translations"][0]["source"] = "FORGED ORIGINAL"
        with self.assertRaises(ValueError):
            p.merge_translations(worksheet, previous)

    def test_cli_writes_only_json_and_never_overwrites_reviewed_worksheet(self):
        v8, v82, exe = native_fixture()
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            first, second, executable = (
                root / "v8.bin", root / "v82.bin", root / "SLUS_008.68")
            first.write_bytes(v8)
            second.write_bytes(v82)
            executable.write_bytes(exe)
            workdir = root / "work"
            argv = ["--v8-quest-bin", str(first),
                    "--v82-quest-bin", str(second),
                    "--v82-executable", str(executable),
                    "--work-dir", str(workdir)]
            self.assertEqual(p.main(argv), 0)
            audit = json.loads((workdir / "quest-source-audit.json").read_text("utf-8"))
            worksheet_path = workdir / "quest-ptbr-template.json"
            worksheet = json.loads(worksheet_path.read_text("utf-8"))
            self.assertEqual(len(worksheet["translations"]), 168)
            self.assertFalse(any(workdir.rglob("*.bin")))
            for row in worksheet["translations"]:
                row["ptBr"] = "Missão traduzida para português: " + row["source"]
            worksheet_path.write_text(json.dumps(worksheet), "utf-8")
            catalog = root / "Assets/Resources/Missions/original_quests_ptbr.json"
            self.assertEqual(p.main(argv + ["--translations", str(worksheet_path),
                                            "--catalog", str(catalog)]), 0)
            compiled = json.loads(catalog.read_text("utf-8"))
            self.assertEqual(sum(len(c["missions"]) for c in compiled["campaigns"]), 168)
            self.assertEqual(len(json.loads(worksheet_path.read_text("utf-8"))[
                "translations"][0]["ptBr"]), len(worksheet["translations"][0]["ptBr"]))
            self.assertFalse(any((root / "Assets").rglob("*.bin")))

    def test_incomplete_ptbr_cannot_produce_a_fake_catalog(self):
        v8, v82, exe = native_fixture()
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            first, second, executable = (root / "v8.bin", root / "v82.bin",
                                          root / "SLUS_008.68")
            first.write_bytes(v8)
            second.write_bytes(v82)
            executable.write_bytes(exe)
            worksheet = p.importer.worksheet(p.source_report(v8, v82, exe))
            source = root / "translations.json"
            source.write_text(json.dumps(worksheet), "utf-8")
            catalog = root / "game.json"
            with self.assertRaisesRegex(ValueError, "Missing, unchanged"):
                p.main(["--v8-quest-bin", str(first),
                        "--v82-quest-bin", str(second),
                        "--v82-executable", str(executable),
                        "--work-dir", str(root / "work"),
                        "--translations", str(source), "--catalog", str(catalog)])
            self.assertFalse(catalog.exists())


if __name__ == "__main__":
    unittest.main()
