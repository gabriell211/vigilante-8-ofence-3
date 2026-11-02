#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalRoadAdaptiveSourceSubdivisionTests
    {
        [Test]
        public void OriginalLittleEndianHexRetainsTwoSignedJunctionTangents()
        {
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryReadOriginalTangentPair(
                "1e000000e2ffffff", out int[] signed), Is.True);
            Assert.That(signed, Is.EqualTo(new[] { 30, -30 }));
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryReadOriginalTangentPair(
                "1e000000NOffffff", out _), Is.False);
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryReadOriginalTangentPair(
                "1e000000", out _), Is.False);
        }

        [Test]
        public void UnadjustedControlRectangleMatchesFun719CInitialSource()
        {
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryCreateOriginalUnadjustedControls(
                new Vector3Int(5000, 100, 4000), new Vector3Int(15000, 200, 4000),
                new[] { 30, -30 }, new[] { 30, -30 },
                out Vector3Int[] controls), Is.True);
            Assert.That(controls[0], Is.EqualTo(new Vector3Int(5000, 100, 4000)));
            Assert.That(controls[1], Is.EqualTo(new Vector3Int(5030, 0, 4030)));
            Assert.That(controls[2], Is.EqualTo(new Vector3Int(14970, 0, 3970)));
            Assert.That(controls[3], Is.EqualTo(new Vector3Int(15000, 200, 4000)));
        }

        [Test]
        public void SourceThresholdSplitsLongCurveAndPreservesSharedEndpoint()
        {
            var start = new Vector3Int(0, 100, 0);
            var finish = new Vector3Int(2000000, 200, 0);
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryCreateOriginalUnadjustedControls(
                start, finish, new[] { 0, 0 }, new[] { 0, 0 },
                out Vector3Int[] controls), Is.True);
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryOriginalSourceExtent(
                controls, out int extent), Is.True);
            Assert.That(extent, Is.EqualTo(2000000));
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TrySplitOriginalUnadjustedCurve(
                controls, out var leaves), Is.True);
            Assert.That(leaves.Length, Is.EqualTo(2));
            Assert.That(leaves[0].FirstParameter16, Is.Zero);
            Assert.That(leaves[0].LastParameter16, Is.EqualTo(32768));
            Assert.That(leaves[1].FirstParameter16, Is.EqualTo(32768));
            Assert.That(leaves[1].LastParameter16, Is.EqualTo(65536));
            Assert.That(leaves[0].SourceControlPoints[3],
                Is.EqualTo(leaves[1].SourceControlPoints[0]));
            Assert.That(leaves[0].SourceControlPoints[3].y, Is.EqualTo(150));
        }

        [Test]
        public void CheckedOriginalHalfSplitRejectsWraparoundAndNull()
        {
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryOriginalHalfSplit(
                null, out _, out _), Is.False);
            var controls = new[]
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(int.MaxValue, 0, 0),
                new Vector3Int(int.MaxValue, 0, 0),
                new Vector3Int(int.MaxValue, 0, 0)
            };
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryOriginalHalfSplit(
                controls, out _, out _), Is.False);
            Assert.That(OriginalRoadAdaptiveSourceSubdivision.TryCreateOriginalUnadjustedControls(
                new Vector3Int(int.MaxValue, 0, 0), Vector3Int.zero,
                new[] { 1, 0 }, new[] { 0, 0 }, out _), Is.False);
        }

        [Test]
        public void OriginalSteelMillSceneContainsAuditableAdaptiveSourceControls()
        {
            OriginalRoadAdaptiveSourceSubdivision.VerifyArchivedSource();
            var plan = OriginalRoadSegmentSourceReviewWindow.LoadVerified("v82_steel_mill");
            Assert.That(plan.TotalSegments, Is.GreaterThan(0));
            Assert.That(plan.Roads, Is.Not.Empty);
            foreach (var road in plan.Roads)
            {
                if (road.OriginalUnadjustedAdaptiveLeaves == null) continue;
                var leaves = road.OriginalUnadjustedAdaptiveLeaves;
                Assert.That(leaves[0].FirstParameter16, Is.Zero);
                Assert.That(leaves[leaves.Length - 1].LastParameter16, Is.EqualTo(65536));
            }
        }
    }
}
#endif
