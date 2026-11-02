"""The Unity road viewer must draw exact archived RSEG XZ evidence, never roads."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VIEWER = ROOT / "Assets/Editor/OriginalRoadSegmentSourceReviewWindow.cs"
JUNCTION = ROOT / "Assets/Editor/OriginalJunctionSourceReviewBuilder.cs"
UNITY_TEST = ROOT / "Assets/Tests/Editor/OriginalRoadSegmentSourceReviewTests.cs"


class SourceRoadViewerContract(unittest.TestCase):
    def test_2d_source_viewer_and_checked_fixed_point_polynomial(self):
        viewer = VIEWER.read_text("utf-8")
        for statement in (
            "#if UNITY_EDITOR",
            "Visualizar estradas RSEG originais em planta XZ",
            "TrySampleAxis(",
            "iVar3 = DAT_20 * param1 >> 12;",
            "iVar3 = DAT_24 * param1 >> 12;",
            "OriginalJunctionSourceReviewBuilder.VerifiedOriginalScenePath(stageId)",
            "OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(RsegMeta)",
            "OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(JuncMeta)",
            "Handles.DrawAAPolyLine(",
            "NeedsVigTerrainHeight",
            "RejectedSegments",
        ):
            self.assertIn(statement, viewer)
        for forbidden in (
            "new Mesh(", "AddComponent<MeshCollider>", "AddComponent<BoxCollider>",
            "GameObject.CreatePrimitive(", "ArenaSpawnPoints",
            "AssetDatabase.CreateAsset(", "EditorSceneManager.SaveScene(",
        ):
            self.assertNotIn(forbidden, viewer)

    def test_unchanged_archived_git_hash_and_meta_proof_and_nunit_coverage(self):
        original = JUNCTION.read_text("utf-8")
        self.assertIn("ShaGit(Absolute(row.sourceScenePath)) != SceneBlobs[id]", original)
        self.assertIn("internal static string VerifiedOriginalScenePath(", original)
        self.assertIn("internal static string VerifiedScriptGuid(", original)
        tests = UNITY_TEST.read_text("utf-8")
        for case in (
            "FixedPointCurveMatchesTheArchivedRsegEquation",
            "VerifiedJunctionIdsProduceOnlyOriginalXZSamples",
            "MissingJunctionDifferentScriptAndUnsafeArithmeticAreNotRendered",
            "UnknownTerrainHeightIsNeverTreatedAsA3DPosition",
            "OriginalEightScenesStaySourceVerifiedAndUnconvertedToReleaseRoads",
        ):
            self.assertIn(case, tests)


if __name__ == "__main__":
    unittest.main()
