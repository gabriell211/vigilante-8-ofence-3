"""Validate the complete official source roster and menu selection filters."""
from pathlib import Path
import json
import unittest

ROOT = Path(__file__).resolve().parents[1]


class CompleteMenuSelectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads((ROOT / "Assets/Resources/Catalog/roster.json").read_text("utf-8"))
        cls.menu = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")
        cls.rules = (ROOT / "Assets/Scripts/Gameplay/OfficialMenuSelections.cs").read_text("utf-8")

    def test_official_roster_has_no_missing_menu_entries(self):
        self.assertEqual(len(self.data["characters"]), 23)
        self.assertEqual(len(self.data["vehicles"]), 31)
        self.assertEqual(len(self.data["arenas"]), 19)
        self.assertEqual(len({v["id"] for v in self.data["vehicles"]}), 31)
        self.assertEqual(len({a["id"] for a in self.data["arenas"]}), 19)
        self.assertEqual(len({c["id"] for c in self.data["characters"]}), 23)

    def test_every_car_can_be_selected_only_by_its_pilot(self):
        pilots = {c["id"] for c in self.data["characters"]}
        owners = {id: [] for id in pilots}
        for car in self.data["vehicles"]:
            self.assertIn(car["characterId"], pilots)
            owners[car["characterId"]].append(car["id"])
        self.assertTrue(all(len(cars) >= 1 for cars in owners.values()))
        self.assertIn("OfficialMenuSelections.OwnsVehicle", self.menu)

    def test_filter_counts_match_every_game_and_bonus(self):
        for game, vehicles, arenas in (("v8", 13, 11), ("v82", 18, 8)):
            self.assertEqual(sum(v["game"] == game for v in self.data["vehicles"]), vehicles)
            self.assertEqual(sum(a["game"] == game for a in self.data["arenas"]), arenas)
            self.assertIn('gameId == null || v.game == gameId', self.rules)
        bonus = [a for a in self.data["arenas"] if a.get("bonus")]
        self.assertEqual([x["id"] for x in bonus], ["v8_super_dreamland_64"])
        self.assertEqual(bonus[0]["platforms"], ["n64"])

    def test_menu_browser_does_not_invent_playable_content(self):
        self.assertIn('MIGRAÇÃO PENDENTE', self.menu)
        self.assertIn('IsVehicleReady', self.menu)
        self.assertIn('IsArenaReady', self.menu)


if __name__ == "__main__":
    unittest.main()
