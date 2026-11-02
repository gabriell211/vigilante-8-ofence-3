#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalRoadSegmentSourceReviewTests
    {
        private const string Junc = "c031ef78ecc9f51459ebf05d0fb88f70";
        private const string Rseg = "99ef4414b53d4f94c9d186fd5df8f984";
        private const string Other = "ffffffffffffffffffffffffffffffff";

        private static string ArchivedSnippet(string rsegGuid = Rseg,
            string endpoint = "11", int flags = 1, int coefficient = 625)
        {
            return "%YAML 1.1\n" +
                "--- !u!114 &10\nMonoBehaviour:\n" +
                "  m_Script: {fileID: 11500000, guid: " + Junc + ", type: 3}\n" +
                "  DAT_00: {x: 5000, y: 100, z: 4000}\n" +
                "--- !u!114 &11\nMonoBehaviour:\n" +
                "  m_Script: {fileID: 11500000, guid: " + Junc + ", type: 3}\n" +
                "  DAT_00: {x: 15000, y: 200, z: 4000}\n" +
                "--- !u!114 &300\nMonoBehaviour:\n" +
                "  m_Script: {fileID: 11500000, guid: " + rsegGuid + ", type: 3}\n" +
                "  DAT_00:\n  - {fileID: 10}\n  - {fileID: " + endpoint + "}\n" +
                "  DAT_0A: 0\n  DAT_0C: " + flags + "\n" +
                "  DAT_10: 1e000000e2ffffff\n" +
                "  DAT_14: 1e000000e2ffffff\n" +
                "  DAT_20: 0\n  DAT_24: 0\n  DAT_28: 0\n" +
                "  DAT_2C: 0\n  DAT_30: " + coefficient + "\n  DAT_34: 0\n";
        }

        [Test]
        public void FixedPointCurveMatchesTheArchivedRsegEquation()
        {
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(
                5000, 4096, 0, 0, 625, out int end), Is.True);
            Assert.That(end, Is.EqualTo(15000));
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(
                5000, 2048, 0, 0, 625, out int middle), Is.True);
            Assert.That(middle, Is.EqualTo(10000));
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(
                0, 4096, int.MaxValue, 0, 0, out _), Is.False);
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(
                0, -1, 0, 0, 0, out _), Is.False);
        }

        [Test]
        public void VerifiedJunctionIdsProduceOnlyOriginalXZSamples()
        {
            var plan = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(), Rseg, Junc);
            Assert.That(plan.TotalSegments, Is.EqualTo(1));
            Assert.That(plan.RejectedSegments, Is.Zero);
            Assert.That(plan.Roads.Length, Is.EqualTo(1));
            Assert.That(plan.Roads[0].FirstJuncFileId, Is.EqualTo("10"));
            Assert.That(plan.Roads[0].SecondJuncFileId, Is.EqualTo("11"));
            Assert.That(plan.Roads[0].OriginalFixedXZ.Length, Is.EqualTo(5));
            Assert.That(plan.Roads[0].OriginalFixedXZ[0], Is.EqualTo(new Vector2(5000, 4000)));
            Assert.That(plan.Roads[0].OriginalFixedXZ[4], Is.EqualTo(new Vector2(15000, 4000)));
            Assert.That(plan.TerrainDependentSegments, Is.Zero);
        }

        [Test]
        public void MissingJunctionDifferentScriptAndUnsafeArithmeticAreNotRendered()
        {
            foreach (string raw in new[]
            {
                ArchivedSnippet(endpoint: "999"),
                ArchivedSnippet(endpoint: "10"),
                ArchivedSnippet(coefficient: int.MaxValue)
            })
            {
                var plan = OriginalRoadSegmentSourceReviewWindow.ParseSource(raw, Rseg, Junc);
                Assert.That(plan.TotalSegments, Is.EqualTo(1));
                Assert.That(plan.Roads, Is.Empty);
                Assert.That(plan.RejectedSegments, Is.EqualTo(1));
            }
            var wrong = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(rsegGuid: Other), Rseg, Junc);
            Assert.That(wrong.TotalSegments, Is.Zero);
            Assert.That(wrong.Roads, Is.Empty);
        }

        [Test]
        public void UnknownTerrainHeightIsNeverTreatedAsA3DPosition()
        {
            var plan = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(flags: 0), Rseg, Junc);
            Assert.That(plan.TerrainDependentSegments, Is.EqualTo(1));
            Assert.That(plan.Roads[0].NeedsVigTerrainHeight, Is.True);
            Assert.That(plan.Roads[0].OriginalFixedXZ.Length, Is.EqualTo(5));
        }

        [Test]
        public void OnlyOriginalFlagsWithExplicitHeightProduceThreeDimensionalEvidence()
        {
            var linear = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(flags: 1), Rseg, Junc);
            var xyz = linear.Roads[0].OriginalFixedXYZ;
            Assert.That(xyz, Is.Not.Null);
            Assert.That(xyz.Length, Is.EqualTo(5));
            Assert.That(xyz[0], Is.EqualTo(new Vector3(5000, 100, 4000)));
            Assert.That(xyz[2], Is.EqualTo(new Vector3(10000, 150, 4000)));
            Assert.That(xyz[4], Is.EqualTo(new Vector3(15000, 200, 4000)));
            Assert.That(linear.HeightKnownSegments, Is.EqualTo(1));

            var fixedY = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(flags: 8), Rseg, Junc);
            foreach (Vector3 sample in fixedY.Roads[0].OriginalFixedXYZ)
                Assert.That(sample.y, Is.EqualTo(100));

            var needsTerrain = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(flags: 0), Rseg, Junc);
            Assert.That(needsTerrain.Roads[0].OriginalFixedXYZ, Is.Null);
            Assert.That(needsTerrain.HeightKnownSegments, Is.Zero);

            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleKnownHeight(
                new Vector3Int(0, int.MinValue, 0),
                new Vector3Int(0, int.MaxValue, 0), 1, 4096, out _), Is.False);
        }

        [Test]
        public void DenseOriginalRsegPreviewReevaluatesEveryFixedPoint()
        {
            var plan = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(flags: 1), Rseg, Junc);
            var road = plan.Roads[0];
            Assert.That(road.OriginalFixedXZExactDense, Is.Not.Null);
            Assert.That(road.OriginalFixedXZExactDense.Length,
                Is.EqualTo(OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount));
            Assert.That(road.OriginalFixedXYZDense, Is.Not.Null);
            Assert.That(road.OriginalFixedXYZDense.Length, Is.EqualTo(33));
            Assert.That(road.OriginalFixedXYZDense[0], Is.EqualTo(new Vector3(5000, 100, 4000)));
            Assert.That(road.OriginalFixedXYZDense[4],
                Is.EqualTo(new Vector3(6250, 112, 4000)));
            Assert.That(road.OriginalFixedXYZDense[16],
                Is.EqualTo(new Vector3(10000, 150, 4000)));
            Assert.That(road.OriginalFixedXYZDense[32],
                Is.EqualTo(new Vector3(15000, 200, 4000)));
            var terrain = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(flags: 0), Rseg, Junc).Roads[0];
            Assert.That(terrain.OriginalFixedXZExactDense.Length, Is.EqualTo(33));
            Assert.That(terrain.OriginalFixedXYZDense, Is.Null,
                "Unknown VigTerrain heights may never become fabricated 3D points");
        }

        [Test]
        public void DenseTraceRejectsOverflowInsteadOfDrawingInventedInterpolations()
        {
            var rejected = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(coefficient: int.MaxValue), Rseg, Junc);
            Assert.That(rejected.Roads, Is.Empty);
            Assert.That(rejected.RejectedSegments, Is.EqualTo(1));
        }

        [Test]
        public void OriginalRsegDerivativeKeepsFixedPointArithmeticAndRejectsOverflow()
        {
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(
                0, 0, 0, 256, out int first), Is.True);
            Assert.That(first, Is.EqualTo(1));
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(
                2048, 0, 0, 256, out int middle), Is.True);
            Assert.That(middle, Is.EqualTo(1));
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(
                4096, int.MaxValue, 0, 0, out _), Is.False);
            Assert.That(OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(
                -1, 0, 0, 256, out _), Is.False);

            var road = OriginalRoadSegmentSourceReviewWindow.ParseSource(
                ArchivedSnippet(), Rseg, Junc).Roads[0];
            Assert.That(road.SourceXrtpProfileIndex, Is.EqualTo(0));
            Assert.That(road.OriginalFixedTangentXZDense.Length, Is.EqualTo(33));
            Assert.That(road.OriginalFixedTangentXZDense[0], Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void SourceDebugAxisMappingRequiresPositiveScale()
        {
            Assert.That(OriginalRsegHeightPreviewBuilder.ToSourceReviewPosition(
                new Vector3(4096, -8192, 12288), 4096),
                Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalRsegHeightPreviewBuilder.ToSourceReviewPosition(
                    Vector3.one, 0));
        }

        [Test]
        public void OriginalTerrainFixedPointInterpolationUsesBothOriginalTriangles()
        {
            var raw = new ushort[4096];
            raw[65] = 100;
            raw[129] = 200;
            raw[66] = 300;
            raw[130] = 400;
            var bytes = new byte[raw.Length * 2];
            Buffer.BlockCopy(raw, 0, bytes, 0, bytes.Length);
            string yaml = "%YAML 1.1\n--- !u!114 &55\nMonoBehaviour:\n" +
                "  m_Script: {fileID: 11500000, guid: " +
                "68fd1e9f11866ba4480d203f972bb9ab, type: 3}\n" +
                "  vertices: " + BitConverter.ToString(bytes).Replace("-", "") + "\n" +
                "  chunks: " + new string('0', 4096) + "\n  zoneCount: 1\n";
            var grid = OriginalVigTerrainSourceGrid.ParseSource(
                yaml, "68fd1e9f11866ba4480d203f972bb9ab");
            Assert.That(grid.TrySampleHeight(0x18000, 0x14000, out int a), Is.True);
            Assert.That(a, Is.EqualTo(200 * 2048));
            Assert.That(grid.TrySampleHeight(0x1C000, 0x1C000, out int b), Is.True);
            Assert.That(b, Is.EqualTo(325 * 2048));
            Assert.That(grid.TrySampleHeight(0, 0x14000, out int outer), Is.True);
            Assert.That(outer, Is.Zero);
            Assert.Throws<InvalidDataException>(() => OriginalVigTerrainSourceGrid.ParseSource(
                yaml.Replace("  zoneCount: 1", "  zoneCount: 2"),
                "68fd1e9f11866ba4480d203f972bb9ab"));
        }

        [Test]
        public void SteelMillOriginalJunctionMatchesExactSerializedVigTerrainHeight()
        {
            var original = OriginalVigTerrainSourceGrid.LoadVerified("v82_steel_mill");
            Assert.That(original.TrySampleHeight(59919356, 90376320, out int y), Is.True);
            Assert.That(y, Is.EqualTo(3006532));
        }

        [Test]
        public void OriginalEightScenesStaySourceVerifiedAndUnconvertedToReleaseRoads()
        {
            var catalog = Vigilante8.Content.CatalogRepository.Load();
            foreach (var stage in catalog.arenas)
            {
                if (stage.game != "v82") continue;
                var plan = OriginalRoadSegmentSourceReviewWindow.LoadVerified(stage.id);
                Assert.That(plan.TotalSegments, Is.GreaterThan(0), stage.id);
                Assert.That(plan.RejectedSegments, Is.GreaterThanOrEqualTo(0));
                Assert.That(plan.Roads.Length + plan.RejectedSegments,
                    Is.EqualTo(plan.TotalSegments));
            }
        }
    }
}
#endif
