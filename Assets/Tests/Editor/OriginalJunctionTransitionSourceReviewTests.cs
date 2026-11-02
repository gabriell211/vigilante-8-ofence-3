#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalJunctionTransitionSourceReviewTests
    {
        private static OriginalJunctionTransitionSourceReview.ConnectedRoad[] Roads() =>
            new[]
            {
                new OriginalJunctionTransitionSourceReview.ConnectedRoad
                {
                    FileId = "1", SourceOrder = 65535, WidthFixed = 131072
                },
                new OriginalJunctionTransitionSourceReview.ConnectedRoad
                {
                    FileId = "2", SourceOrder = 0, WidthFixed = 32768
                },
                new OriginalJunctionTransitionSourceReview.ConnectedRoad
                {
                    FileId = "3", SourceOrder = 4, WidthFixed = 65536
                }
            };

        [Test]
        public void OriginalEarlierRoadMaximumWidthDefinesPendingFun6604Radius()
        {
            var radius = OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 0, false, 3, 4, 0, 0, "3", Roads());
            Assert.That(radius.Status, Is.EqualTo(
                OriginalJunctionTransitionSourceReview.JunctionStatus.NeedsUnportedFun6604));
            Assert.That(radius.SourceRadiusFixed, Is.EqualTo(65536));
            Assert.That(radius.EarlierRoads, Is.EqualTo(2));
        }

        [Test]
        public void OriginalRoadOrJunctionFlagDisablesSourceTrim()
        {
            var a = OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 1, false, 3, 4, 0, 0, "3", Roads());
            var b = OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 0, false, 3, 4, 2, 0, "3", Roads());
            var c = OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 0, false, 3, 4, 4, 1, "3", Roads());
            Assert.That(a.Status, Is.EqualTo(
                OriginalJunctionTransitionSourceReview.JunctionStatus.NoSourceTrim));
            Assert.That(b.Status, Is.EqualTo(a.Status));
            Assert.That(c.Status, Is.EqualTo(a.Status));
        }

        [Test]
        public void ConfiguredXobfJunctionCannotBeSubstitutedByGenericTrimming()
        {
            var result = OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 0, true, 3, 4, 0, 0, "3", Roads());
            Assert.That(result.Status, Is.EqualTo(
                OriginalJunctionTransitionSourceReview.JunctionStatus.XobfConfigurationNotYetPorted));
            Assert.That(result.SourceRadiusFixed, Is.Zero);
        }

        [Test]
        public void MissingConnectedRoadAndUnknownWidthCannotBecomeRealJunction()
        {
            var missing = OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 0, false, 3, 4, 0, 0, "99", Roads());
            Assert.That(missing.Status, Is.EqualTo(
                OriginalJunctionTransitionSourceReview.JunctionStatus.UnverifiedSource));
            var incomplete = Roads();
            incomplete[0].WidthFixed = 0;
            Assert.That(OriginalJunctionTransitionSourceReview.Evaluate(
                "45", 0, false, 3, 4, 0, 0, "3", incomplete).Status,
                Is.EqualTo(missing.Status));
        }

        [Test]
        public void SteelMillSourceJunctionsAreAuditedInOriginalUnityScene()
        {
            OriginalJunctionTransitionSourceReview.VerifyArchivedCode();
            var profiles = OriginalXrtpSourceProfileReview.LoadVerified("v82_steel_mill");
            var result = OriginalJunctionTransitionSourceReview.ReviewVerified(
                "v82_steel_mill", profiles);
            Assert.That(result.Count, Is.GreaterThan(0));
            foreach (var endpoints in result.Values)
                Assert.That(endpoints.Length, Is.EqualTo(2));
        }
    }
}
#endif
