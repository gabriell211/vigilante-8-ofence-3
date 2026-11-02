#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalV82TextureQualityTests
    {
        [Test]
        public void BicubicScalingPreservesConstantOriginalColourAndAlpha()
        {
            var pixels = new Color32[6];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(38, 95, 180, 255);
            Color32[] enhanced = OriginalV82TextureQualityBuilder.UpscaleBicubic(
                pixels, 3, 2);
            Assert.That(enhanced.Length, Is.EqualTo(12 * 8));
            foreach (Color32 sample in enhanced)
            {
                Assert.That(sample.r, Is.EqualTo(38));
                Assert.That(sample.g, Is.EqualTo(95));
                Assert.That(sample.b, Is.EqualTo(180));
                Assert.That(sample.a, Is.EqualTo(255));
            }
        }

        [Test]
        public void TransparentSourceDoesNotLeakRgbIntoInvisiblePixels()
        {
            Color32[] enhanced = OriginalV82TextureQualityBuilder.UpscaleBicubic(
                new[] { new Color32(255, 0, 0, 0) }, 1, 1);
            Assert.That(enhanced.Length, Is.EqualTo(16));
            foreach (Color32 sample in enhanced)
                Assert.That(sample, Is.EqualTo(new Color32(0, 0, 0, 0)));
        }

        [Test]
        public void AlphaEdgesStayBoundedWithoutCreatingNewSourceDetails()
        {
            Color32[] enhanced = OriginalV82TextureQualityBuilder.UpscaleBicubic(
                new[] { new Color32(255, 0, 0, 255),
                        new Color32(0, 0, 255, 0) }, 2, 1);
            Assert.That(enhanced.Length, Is.EqualTo(8 * 4));
            foreach (Color32 sample in enhanced)
            {
                Assert.That(sample.g, Is.Zero);
                Assert.That(sample.a, Is.InRange(0, 255));
                if (sample.a == 0)
                    Assert.That(sample, Is.EqualTo(new Color32(0, 0, 0, 0)));
            }
        }

        [Test]
        public void InvalidOrOversizedImageMustNotProduceFakeHdTexture()
        {
            Assert.Throws<ArgumentException>(() =>
                OriginalV82TextureQualityBuilder.UpscaleBicubic(null, 1, 1));
            Assert.Throws<ArgumentException>(() =>
                OriginalV82TextureQualityBuilder.UpscaleBicubic(
                    new Color32[1], 2, 1));
            Assert.Throws<ArgumentException>(() =>
                OriginalV82TextureQualityBuilder.UpscaleBicubic(
                    new Color32[1], 1, 1, 8));
            Assert.Throws<ArgumentException>(() =>
                OriginalV82TextureQualityBuilder.UpscaleBicubic(
                    new Color32[1025], 1025, 1));
        }

        [Test]
        public void VerifiedManifestRequiresAll790SourceTextureReferences()
        {
            string json = File.ReadAllText("docs/legacy-v82-textures.json");
            var manifest = OriginalV82TextureQualityBuilder.ParseManifest(json);
            Assert.That(manifest.Count, Is.EqualTo(790));
            Assert.That(manifest.Values, Is.All.Matches<OriginalV82TextureQualityBuilder.SourceTexture>(
                entry => entry.Width > 0 && entry.Height > 0));
            Assert.Throws<InvalidDataException>(() =>
                OriginalV82TextureQualityBuilder.ParseManifest("{}"));
        }
    }
}
#endif
