"""All original Quest missions must remain source-derived: only text may change."""
import copy
import hashlib
import sys
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import import_original_quest_ptbr as q


def sample_source():
    original = []
    for char in range(13):
        mission_rows = []
        for i in range(4 if char < 12 else 0):
            mission_rows.append({
                "arena": i, "target_first": 10, "target_last": 20,
                "loading_card_variant": i,
                "actors": [{"type": 0, "flags": 1, "spawn": -1, "aux": 100}],
                "briefing": f"Original V8 character {char}, mission {i}\nDESTROY: source scenery",
            })
        original.append({"source_type": char, "stable_id": f"guest.v8.{char}",
                         "missions": mission_rows})
    sequel = []
    for char in range(18):
        sequel.append({"source_type": char, "name": f"Native {char}",
                       "team": char % 3,
                       "missions": [{
                           "actors": [{"type": 1, "flags": 2, "spawn": 1, "aux": 20}],
                           "briefing": f"Native V82 character {char}, mission {i}",
                       } for i in range(8 if char < 15 else 0)]})
    routes = [{"count": 8, "records": [f"{i:040x}" for i in range(8)]}
              for _ in range(3)]
    return {
        "original_sha256": "1" * 64, "sequel_sha256": "2" * 64,
        "original": original, "sequel": sequel, "native_routes": routes,
    }


class OriginalQuestTranslationTests(unittest.TestCase):
    def test_both_original_campaigns_have_48_and_120_missions_without_new_content(self):
        source = sample_source()
        output = q.records(source)
        self.assertEqual(len(output), 27)
        self.assertEqual(sum(len(c["missions"]) for c in output if c["game"] == "v8"), 48)
        self.assertEqual(sum(len(c["missions"]) for c in output if c["game"] == "v82"), 120)
        self.assertTrue(all(c["missions"] for c in output))
        self.assertEqual(output[0]["missions"][0]["targetFirst"], 10)
        self.assertEqual(output[0]["missions"][0]["targetLast"], 20)
        self.assertEqual(output[12]["missions"][0]["sourceRouteRecordHex"], "0" * 40)
        self.assertEqual(output[0]["missions"][0]["sourceObjectiveDirective"], "DESTROY")
        self.assertEqual(output[12]["missions"][0]["sourceTeamIndex"], 0)

    def test_only_briefings_are_translated_all_source_fields_untouched(self):
        src = sample_source()
        base = q.records(src)
        translations = q.worksheet(src)
        self.assertEqual(len(translations["translations"]), 168)
        for item in translations["translations"]:
            item["ptBr"] = "Texto PT-BR fiel para: " + item["source"]
        localized = q.apply_ptbr(src, translations)
        self.assertEqual(localized["locale"], "pt-BR")
        self.assertEqual(localized["provenance"], q.PROVENANCE)
        self.assertEqual(len(localized["campaigns"]), 27)
        for before, after in zip(base, localized["campaigns"]):
            self.assertEqual(before["id"], after["id"])
            for original, translated in zip(before["missions"], after["missions"]):
                self.assertEqual({k: v for k, v in original.items() if k != "briefingPtBr"},
                                 {k: v for k, v in translated.items() if k != "briefingPtBr"})
                self.assertNotEqual(original["briefingPtBr"], translated["briefingPtBr"])
                self.assertEqual(original["sourceRecordSha256"],
                                 translated["sourceRecordSha256"])

    def test_no_source_binary_or_untranslated_text_must_never_ship_as_quest(self):
        src = sample_source()
        translations = q.worksheet(src)
        with self.assertRaisesRegex(ValueError, "Missing, unchanged"):
            q.apply_ptbr(src, translations)
        translations["translations"][0]["ptBr"] = "Tradução validada"
        with self.assertRaisesRegex(ValueError, "Missing, unchanged"):
            q.apply_ptbr(src, translations)
        broken = copy.deepcopy(src)
        del broken["sequel"][0]["missions"][0]["actors"]
        with self.assertRaises(ValueError):
            q.records(broken)
        broken = copy.deepcopy(src)
        broken["original"][0]["missions"].pop()
        with self.assertRaises(ValueError):
            q.records(broken)

    def test_reject_malicious_translation_record_and_quest_objectives(self):
        src = sample_source()
        translated = q.worksheet(src)
        for item in translated["translations"]:
            item["ptBr"] = "Missão traduzida " + item["sha256"]
        translated["translations"][0]["source"] = "Texto alterado"
        with self.assertRaises(ValueError):
            q.apply_ptbr(src, translated)
        modified = sample_source()
        modified["original"][0]["missions"][0]["target_last"] = 2
        with self.assertRaises(ValueError):
            q.records(modified)

    def test_catalog_and_ui_contract_require_original_source_before_showing_missions(self):
        runtime = (ROOT / "Assets/Scripts/Gameplay/OriginalQuestArchive.cs").read_text("utf-8")
        menu = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")
        self.assertIn("ExpectedV8Missions = 48", runtime)
        self.assertIn("ExpectedV82Missions = 120", runtime)
        self.assertIn("OriginalQuestArchive.TryLoad()", menu)
        self.assertIn("MISSÕES ORIGINAIS AINDA NÃO IMPORTADAS", menu)
        self.assertIn("mission.briefingPtBr", menu)
        self.assertNotIn("CreateButton(\"JOGAR MISSÃO", menu)
        self.assertNotIn("new MissionDefinition", menu)


if __name__ == "__main__":
    unittest.main()
