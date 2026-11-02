#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalRoadAdaptiveEdgeSamplerTests
    {
        private static OriginalRoadSegmentSourceReviewWindow.SourceRoad LongSyntheticRoad()
        {
            var start = new Vector3Int(0, 100, 0);
            var finish = new Vector3Int(2000000, 200, 0);
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryCreateOriginalUnadjustedControls(
                start, finish, new[] { 0, 0 }, new[] { 0, 0 },
                out Vector3Int[] original), Is.True);
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TrySplitOriginalUnadjustedCurve(
                original, out var leaves), Is.True);
            Assert.That(leaves.Length, Is.EqualTo(2));
            return new OriginalRoadSegmentSourceReviewWindow.SourceRoad
            {
                RsegFileId = "300",
                OriginalFirstJunctionXYZ = start,
                OriginalLastJunctionXYZ = finish,
                OriginalUnadjustedAdaptiveLeaves = leaves,
                OriginalPolynomialX = new[] { 0, 0, 125000 },
                OriginalPolynomialZ = new[] { 0, 0, 0 },
                SourceRoadFlags = 1
            };
        }

        [Test]
        public void AdaptiveSubdividedOriginalParameterGridIsContinuous()
        {
            var road = LongSyntheticRoad();
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuildSourceParameterSamples(
                road.OriginalUnadjustedAdaptiveLeaves, out int[] times), Is.True);
            Assert.That(times.Length, Is.EqualTo(65));
            Assert.That(times[0], Is.Zero);
            Assert.That(times[16], Is.EqualTo(1024));
            Assert.That(times[32], Is.EqualTo(2048));
            Assert.That(times[48], Is.EqualTo(3072));
            Assert.That(times[64], Is.EqualTo(4096));
            for (int i = 1; i < times.Length; i++)
                Assert.That(times[i], Is.GreaterThan(times[i - 1]));
        }

        [Test]
        public void OriginalAdaptiveRoadEdgesProduceSourceHeightAndWidthWithoutTerrain()
        {
            var road = LongSyntheticRoad();
            var profile = new OriginalXrtpSourceProfileReview.SourceProfile
            {
                FullWidthFixed = 65536,
                LongitudinalStepFixed = 81920
            };
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuild(
                road, profile, null, out var sample), Is.True);
            Assert.That(sample.Left.Length, Is.EqualTo(65));
            Assert.That(sample.Right.Length, Is.EqualTo(65));
            Assert.That(sample.Left[32].x, Is.EqualTo(1000000f));
            Assert.That(sample.Left[32].y, Is.EqualTo(150f));
            Assert.That(sample.Left[32].z, Is.EqualTo(32768f));
            Assert.That(sample.Right[32].z, Is.EqualTo(-32768f));
            var mesh = OriginalRoadTextureSourcePreview.BuildAdaptiveDiagnosticStrip(
                sample.Left, sample.Right, sample.TimesFixed12, 4096);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(130));
                Assert.That(mesh.triangles.Length, Is.EqualTo(384));
                Assert.That(mesh.uv[64].y, Is.EqualTo(0.5f));
                Assert.That(mesh.uv[mesh.vertexCount - 1], Is.EqualTo(Vector2.one));
                Assert.That(mesh.name, Does.Contain("PRE-JUNCTION"));
                Assert.That(mesh.name, Does.Contain("PROVISIONAL UV"));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void UnsupportedOrDuplicatedAdaptiveIntervalsNeverGetAFullMesh()
        {
            var road = LongSyntheticRoad();
            var leaves = road.OriginalUnadjustedAdaptiveLeaves;
            leaves[1].FirstParameter16 += 128;
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuildSourceParameterSamples(
                leaves, out _), Is.False);
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuild(
                road, new OriginalXrtpSourceProfileReview.SourceProfile
                {
                    FullWidthFixed = 65536
                }, null, out _), Is.False);

            var positions = new Vector3[65];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3(i, 0, 100);
            var times = new int[65];
            for (int i = 0; i < times.Length; i++)
                times[i] = i * 64;
            times[30] = times[29];
            Assert.Throws<InvalidDataException>(() =>
                OriginalRoadTextureSourcePreview.BuildAdaptiveDiagnosticStrip(
                    positions, positions, times, 4096));
        }

        [Test]
        public void ZeroDerivativeOverflowOrMissingOriginalCurveIsRejected()
        {
            var road = LongSyntheticRoad();
            var profile = new OriginalXrtpSourceProfileReview.SourceProfile
            {
                FullWidthFixed = 65536
            };
            road.OriginalPolynomialX = new[] { 0, 0, 0 };
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuild(
                road, profile, null, out _), Is.False);
            road.OriginalPolynomialX = new[] { int.MaxValue, 0, 125000 };
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuild(
                road, profile, null, out _), Is.False);
            road.OriginalPolynomialX = null;
            Assert.That(OriginalRoadAdaptiveEdgeSampler.TryBuild(
                road, profile, null, out _), Is.False);
        }
    }
}
#endif
