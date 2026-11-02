"""Source-only checks for the Offence 3 authentic 3D garage viewer."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class AuthenticGarageViewerContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.showroom = (ROOT / "Assets/Scripts/Gameplay/Offence3VehicleShowroom.cs").read_text("utf-8")
        cls.menu = (ROOT / "Assets/Scripts/Gameplay/MatchBootstrap.cs").read_text("utf-8")

    def test_no_fake_or_placeholder_vehicle_meshes(self):
        self.assertIn("CatalogRepository.LoadVehiclePrefab(descriptor)", self.showroom)
        self.assertIn("identity.Matches(descriptor.id, descriptor.game)", self.showroom)
        self.assertIn("LegacyVehiclePlaytest", self.showroom)
        self.assertNotIn("GameObject.CreatePrimitive", self.showroom)

    def test_vehicle_is_isolated_from_live_gameplay(self):
        self.assertIn("PreviewLayer = 30", self.showroom)
        self.assertIn("collider.enabled = false", self.showroom)
        self.assertIn("body.isKinematic = true", self.showroom)
        self.assertIn("previewCamera.targetTexture", self.showroom)
        self.assertIn("previewCamera.cullingMask = 1 << PreviewLayer", self.showroom)

    def test_menu_never_claims_missing_models_are_rendered(self):
        self.assertIn("showroom.ShowOfficialVehicle(chosen)", self.menu)
        self.assertIn("showroomImage.style.display = hasRealModel ?", self.menu)
        self.assertIn("Nenhum veículo substituto será exibido", self.menu)

    def test_preview_cleanup_is_explicit(self):
        self.assertIn("previewTexture.Release()", self.showroom)
        self.assertIn("ClearVehicle()", self.showroom)
        self.assertIn("Time.unscaledDeltaTime", self.showroom)

    def test_showroom_disables_inactive_screens(self):
        self.assertIn("showroom.SetVisible(target == ScreenPage.Garage)", self.menu)
        self.assertIn("previewCamera.enabled = visible && vehicle != null", self.showroom)


if __name__ == "__main__":
    unittest.main()
