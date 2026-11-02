"""Original DAT_28 tangent-based sampling remains source-only and bounded."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Editor/OriginalRoadSourceSpacing.cs"
ORIGINAL = ROOT / "LegacySources/Vigilante2Unity/Assets/Scripts/LevelManager.cs"
SAMPLER = ROOT / "Assets/Editor/OriginalRoadAdaptiveEdgeSampler.cs"
SCENE = ROOT / "Assets/Editor/OriginalRsegHeightPreviewBuilder.cs"
RENDER = ROOT / "Assets/Editor/OriginalRoadTextureSourcePreview.cs"
UNIT = ROOT / "Assets/Tests/Editor/OriginalRoadSourceSpacingTests.cs"


class OriginalRoadDat28SourceContracts(unittest.TestCase):
    def test_original_code_provides_both_dat28_phases_and_forced_endpoint(self):
        source = ORIGINAL.read_text("utf-8-sig")
        for code in (
            "private Junction FUN_5850(int[,] param1, XRTP_DB param2, ushort param3)",
            "iVar20 = iVar20 + param2.DAT_28 / iVar21;",
            "iVar21 += param2.DAT_28 / iVar15;",
            "if (local_4c == local_50 - 1)",
            "iVar21 = 4096;",
        ):
            self.assertIn(code, source)

    def test_editor_uses_checked_source_integer_steps_not_fixed_32_for_verified_profiles(self):
        src = SOURCE.read_text("utf-8")
        sampler = SAMPLER.read_text("utf-8")
        for piece in (
            "TrySampleOneLeaf(", "TryBuildOriginalParameters(",
            "sourceDat28 / norm", "if (i == count - 1)",
            "t = LocalEnd;", "TryDerivativeCoefficients(", "IntegerSqrt(",
            "OriginalRoadAdaptiveSourceSubdivision.Leaf[] leaves",
            "MaxSourcePoints - 1", "SourceMethod()",
        )[:-1]:
            self.assertIn(piece, src)
        self.assertIn("OriginalRoadSourceSpacing.TryBuildOriginalParameters(", sampler)
        self.assertIn("profile.LongitudinalStepFixed", sampler)
        self.assertIn("SourceDat28Stepping = actualDat28", sampler)
        self.assertIn("!actualDat28 && !TryBuildSourceParameterSamples(", sampler)
        self.assertNotIn("Vector3.Lerp(", sampler)

    def test_renderer_is_non_physical_original_bitmap_with_provisional_uvs(self):
        scene = SCENE.read_text("utf-8")
        renderer = RENDER.read_text("utf-8")
        self.assertIn("OriginalRoadSourceSpacing.VerifySourceMethod();", scene)
        self.assertIn("road.OriginalAdaptiveUsesDat28Stepping", scene)
        self.assertIn("sourceDat28Stepping", renderer)
        self.assertIn("BuildAdaptiveDiagnosticStrip(", renderer)
        self.assertIn("originalTimesFixed12[i] / 4096f", renderer)
        self.assertIn("PROVISIONAL UV / NO COLLISION", renderer)
        self.assertNotIn("AddComponent<MeshCollider>", renderer)
        self.assertNotIn("AddComponent<Rigidbody>", renderer)

    def test_unity_editor_nunit_covers_steps_and_rejection(self):
        src = UNIT.read_text("utf-8")
        for name in (
            "OriginalFun5850Dat28StepPreservesIntegerDivisionAndFinalEndpoint",
            "SourceDat28CanProduceFewerSamplesThanOldFixed32PerLeafPreview",
            "InvalidDat28StationaryTangentAndOverflowFailClosed",
            "MissingOrDuplicatedLeafIntervalsAreNotPromotedToOriginalTiming",
            "Is.EqualTo(new[] { 0, 3413, 4096 })",
        ):
            self.assertIn(name, src)


if __name__ == "__main__":
    unittest.main()
