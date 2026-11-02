"""Contract tests for the original Vigilante 8 Offence 3 menu redesign."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class Offence3DesignTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.theme = (ROOT / "Assets/Scripts/Gameplay/Offence3MenuTheme.cs").read_text("utf-8")
        cls.menu = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")

    def test_own_title_and_new_third_installment_identity(self):
        self.assertIn('new Label("OFFENCE 3")', self.theme)
        self.assertIn("NOVA GERAÇÃO", self.theme)
        self.assertIn("Offence3MenuTheme.AddIdentity", self.menu)

    def test_classic_roster_navigation_is_a_real_interface(self):
        for snippet in ("SELECIONE SEU CARRO", "OfficialMenuSelections.Cars",
                        "OfficialMenuSelections.PilotForCar", "OfficialMenuSelections.Arenas",
                        "SelectVehicleFromCatalog", "SelectArenaFromCatalog"):
            self.assertIn(snippet, self.menu)

    def test_keyboard_and_gamepad_have_visual_focus_states(self):
        for snippet in ("RegisterCallback<FocusInEvent>", "RegisterCallback<FocusOutEvent>",
                        "RegisterCallback<PointerEnterEvent>", "RegisterCallback<PointerLeaveEvent>"):
            self.assertIn(snippet, self.theme)
        self.assertIn("focusTarget.Focus();", self.menu)

    def test_classic_unimplemented_modes_not_claimed_as_playable(self):
        self.assertIn("CAMPANHA / QUEST", self.menu)
        self.assertIn("SOBREVIVÊNCIA", self.menu)
        self.assertIn("MULTIJOGADOR", self.menu)
        self.assertIn("A consulta às missões não desbloqueia combate", self.menu)
        self.assertIn("sobrevivência e multiplayer seguem pendentes", self.menu)
        self.assertIn("MISSÕES ORIGINAIS AINDA NÃO IMPORTADAS", self.menu)
        self.assertNotIn("GameObject.CreatePrimitive", self.menu)

    def test_neo_arcade_panels_have_stripes_and_readiness_states(self):
        for snippet in ("AddHazardStripe", "AddIdentity", "ContentPanel", "Chip",
                        "Emphasize", "ElectricBlue", "Gold"):
            self.assertIn(snippet, self.theme)
        self.assertIn("Offence3MenuTheme.Emphasize(readinessLabel, available)", self.menu)

    def test_main_menu_remains_functionally_connected(self):
        for snippet in ('"UM JOGADOR  /  MODO ARCADE"',
                        '"OPÇÕES GRÁFICAS E ÁUDIO"', '"CONTROLES"',
                        '"SAIR DO JOGO"', "StartMatch", "IsMatchAvailable",
                        "Time.timeScale = 0f", "Time.timeScale = 1f"):
            self.assertIn(snippet, self.menu)


if __name__ == "__main__":
    unittest.main()
