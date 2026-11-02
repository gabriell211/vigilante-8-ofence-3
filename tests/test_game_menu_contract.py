"""Static wiring regressions for Unity native main menu; not a rendered PlayMode test."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class InGameMenuWiringTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.menu = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")
        cls.settings = (ROOT / "Assets/Scripts/Gameplay/GameMenuSettings.cs").read_text("utf-8")

    def test_main_selection_and_match_pages_exist(self):
        for name in ("Home", "Garage", "Options", "Controls", "About", "Playing",
                     "Pause", "Results"):
            self.assertIn(name, self.menu)

    def test_actual_official_catalog_is_used_instead_of_demo_vehicle_lists(self):
        self.assertIn("catalog.characters.OrderBy", self.menu)
        self.assertIn("catalog.arenas.OrderBy", self.menu)
        self.assertIn("catalog.vehicles.Where(v => v.characterId", self.menu)
        self.assertIn("identity.Matches(vehicle.id, vehicle.game)", self.menu)
        self.assertIn("identity.Matches(arena.id, arena.game)", self.menu)
        self.assertIn("startCombat.SetEnabled(available)", self.menu)

    def test_actions_are_real_and_persistent(self):
        for action in ("PauseGame", "ResumeGame", "Time.timeScale = 0f",
                       "Time.timeScale = 1f", "AudioListener.volume = MasterVolume",
                       "QualitySettings.SetQualityLevel", "Screen.fullScreen = Fullscreen",
                       "PlayerPrefs.Save()", "Return"):
            if action == "Return":
                continue
            self.assertIn(action, self.menu + self.settings)

    def test_no_placeholder_3d_model_creation(self):
        self.assertNotIn("GameObject.CreatePrimitive", self.menu)
        self.assertNotIn("new Mesh()", self.menu)

    def test_settings_ui_rejects_missing_real_arena_or_vehicle(self):
        self.assertIn("IsVehicleReady", self.menu)
        self.assertIn("IsArenaReady", self.menu)
        self.assertIn("spawns.HasValidSpawns(4)", self.menu)
        self.assertIn("prefab.GetComponent<LegacyVehiclePlaytest>()", self.menu)
        self.assertIn("renderers.All", self.menu)
        self.assertIn("hasRealMesh", self.menu)

    def test_dropdown_restore_never_overwrites_saved_choices(self):
        self.assertIn("restoringSelections = true", self.menu)
        self.assertIn("if (restoringSelections) return;", self.menu)
        self.assertIn("RestoreLastOfficialSelections()", self.menu)
        self.assertIn("StoreVehicleForCharacter", self.menu)
        self.assertIn("RestoreVehicleForCharacter", self.menu)

    def test_menu_focus_on_navigation(self):
        self.assertIn("focusTarget.Focus();", self.menu)
        self.assertIn("focusTarget.panel != null", self.menu)

    def test_each_match_validates_camera_and_cleans_up(self):
        self.assertIn('BackToMenu();', self.menu)
        self.assertIn('Camera has no ChaseCamera component.', self.menu)
        self.assertIn('CleanupMatch();', self.menu)

if __name__ == "__main__":
    unittest.main()
