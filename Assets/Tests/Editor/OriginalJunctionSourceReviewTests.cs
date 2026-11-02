using System;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Content;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalJunctionSourceReviewTests
    {
        [Test]
        public void OriginalFixedPointMappingInvertsYAndAvoidsSignedMinOverflow()
        {
            Assert.That(OriginalJunctionSourceReviewBuilder.SourceJunctionLocal(
                new Vector3Int(4096, -8192, 12288), 4096),
                Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Vector3 limit = OriginalJunctionSourceReviewBuilder.SourceJunctionLocal(
                new Vector3Int(0, int.MinValue, 0), 4096);
            Assert.That(limit.y, Is.GreaterThan(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OriginalJunctionSourceReviewBuilder.SourceJunctionLocal(Vector3Int.one, 0));
        }

        [Test]
        public void OnlyVerifiedRootJunctionMeshesBecomeInspectionCandidates()
        {
            Assert.That(OriginalJunctionSourceReviewBuilder.IsRootSourceMatch(true, true,
                true, "v82_steel_mill", "v82_steel_mill_tmd_164"), Is.True);
            Assert.That(OriginalJunctionSourceReviewBuilder.IsRootSourceMatch(true, true,
                false, "v82_steel_mill", "v82_steel_mill_tmd_164"), Is.False);
            Assert.That(OriginalJunctionSourceReviewBuilder.IsRootSourceMatch(true, false,
                true, "v82_steel_mill", "v82_steel_mill_tmd_164"), Is.False);
            Assert.That(OriginalJunctionSourceReviewBuilder.IsRootSourceMatch(false, true,
                true, "v82_steel_mill", "v82_steel_mill_tmd_164"), Is.False);
            Assert.That(OriginalJunctionSourceReviewBuilder.IsRootSourceMatch(true, true,
                true, "v82_steel_mill", "v82_launch_site_tmd_164"), Is.False);
        }

        [Test]
        public void OriginalJunctionVisualsCanJoinNativeTerrainEncounterWithoutNewPhysics()
        {
            var preview = OriginalJunctionSourceReviewBuilder.Prepare("v82_steel_mill");
            var root = new GameObject("original scene JUNC inspection only");
            try
            {
                int count = preview.AppendTo(root.transform);
                Assert.That(count, Is.GreaterThan(0));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                var sources = root.GetComponentsInChildren<OriginalJunctionSourcePreviewIdentity>(true);
                Assert.That(sources.Length, Is.EqualTo(count));
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Length,
                    Is.EqualTo(count));
                foreach (var source in sources)
                {
                    Assert.That(source.StageId, Is.EqualTo("v82_steel_mill"));
                    Assert.That(source.OriginalTmdGroup, Does.StartWith("v82_steel_mill_tmd_"));
                    Assert.That(source.IsReleaseApproved, Is.False);
                }
                Assert.Throws<InvalidOperationException>(() => preview.AppendTo(root.transform));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DiagnosticsAreAlwaysRejectedAsProductionAssets()
        {
            var obj = new GameObject("source-only");
            try
            {
                var identity = obj.AddComponent<OriginalJunctionSourcePreviewIdentity>();
                identity.Configure("v82_steel_mill", "4152953",
                    "v82_steel_mill_tmd_164",
                    new Vector3Int(59919356, 3006532, 90376320), 4096);
                Assert.That(identity.IsReleaseApproved, Is.False);
                Assert.That(identity.SourcePreviewScale, Is.EqualTo(4096));
                Assert.That(identity.JunctionFileId, Is.EqualTo("4152953"));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
    }
}
