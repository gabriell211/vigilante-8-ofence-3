"""The battle integration must be opt-in, source-only and non-colliding."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BATTLE = ROOT / "Assets/Editor/OriginalNativeTerrainPlaytestBuilder.cs"
JUNC = ROOT / "Assets/Editor/OriginalJunctionSourceReviewBuilder.cs"
RELEASE = ROOT / "Assets/Editor/WindowsReleaseBuilder.cs"
EDITOR_TEST = ROOT / "Assets/Tests/Editor/OriginalJunctionSourceReviewTests.cs"


class AuthenticJunctionBattleVisualContract(unittest.TestCase):
    def test_battle_overlay_is_off_by_default_and_uses_source_geometry_only(self):
        text = BATTLE.read_text("utf-8")
        self.assertIn("bool includeOriginalJunctionVisuals = false", text)
        self.assertIn("OriginalJunctionSourceReviewBuilder.Prepare(stageId)", text)
        self.assertIn("originalJunctions.AppendTo(sourceRoot.transform)", text)
        self.assertIn("EditorGUILayout.ToggleLeft(", text)
        self.assertIn("includeOriginalJunctionVisuals);", text)
        self.assertIn("NO COLLISION", text)
        self.assertNotIn("GameObject.CreatePrimitive(", text)

    def test_shared_prepared_visuals_preserve_authentic_data_and_reject_physics(self):
        text = JUNC.read_text("utf-8")
        for required in (
            "public sealed class PreparedVisuals",
            "public static PreparedVisuals Prepare(string stageId)",
            "public int AppendTo(Transform parent)",
            "SourceJunctionLocal(candidate.Junction.Dat00.Value, divisor)",
            "entity.AddComponent<MeshFilter>().sharedMesh = models[candidate.MeshGroup]",
            "entity.AddComponent<OriginalJunctionSourcePreviewIdentity>().Configure(",
            "parent.GetComponentsInChildren<Collider>(true).Length != 0",
            "parent.GetComponentsInChildren<Rigidbody>(true).Length != 0",
            "ShaGit(Absolute(row.sourceScenePath)) != SceneBlobs[id]",
            "mesh.Owner == junc.Owner",
        ):
            self.assertIn(required, text)
        self.assertNotIn("AddComponent<MeshCollider>", text)
        self.assertNotIn("AddComponent<BoxCollider>", text)

    def test_junctions_are_banned_from_the_real_windows_release(self):
        self.assertIn("OriginalJunctionSourcePreviewIdentity",
                      RELEASE.read_text("utf-8"))
        self.assertIn(
            "OriginalJunctionVisualsCanJoinNativeTerrainEncounterWithoutNewPhysics",
            EDITOR_TEST.read_text("utf-8"),
        )


if __name__ == "__main__":
    unittest.main()
