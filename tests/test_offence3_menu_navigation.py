"""Non-rendered regression checks for all Offence 3 keyboard/gamepad return flows."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class Offence3NavigationContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")

    def test_contextual_back_for_cars_arenas_and_pilots(self):
        for part in ("OpenCatalog(ScreenPage.Home)", "OpenCatalog(ScreenPage.Garage)",
                     "OpenCatalog(ScreenPage.About)", "Show(catalogReturn)",
                     "catalogReturn = origin"):
            self.assertIn(part, self.text)

    def test_gamepad_back_and_pause_are_distinct(self):
        self.assertIn("Gamepad.current.buttonEast.wasPressedThisFrame", self.text)
        self.assertIn("Gamepad.current.startButton.wasPressedThisFrame", self.text)
        self.assertIn("pressedCancel && activePage != ScreenPage.Playing", self.text)
        self.assertIn("case ScreenPage.Playing: PauseGame();", self.text)
        self.assertIn("case ScreenPage.Pause: ResumeGame();", self.text)

    def test_pause_exit_and_results_do_not_leave_frozen_game(self):
        self.assertIn("Time.timeScale = 0f", self.text)
        self.assertIn("Time.timeScale = 1f", self.text)
        self.assertIn("case ScreenPage.Results: BackToMenu();", self.text)
        self.assertIn("CleanupMatch();", self.text)

    def test_no_unimplemented_buttons_for_quest_multiplayer(self):
        self.assertNotIn('CreateButton("CAMPANHA / QUEST"', self.text)
        self.assertNotIn('CreateButton("MULTIJOGADOR"', self.text)
        self.assertNotIn('CreateButton("SOBREVIVÊNCIA"', self.text)


if __name__ == "__main__":
    unittest.main()
