#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalRoadTextureSourcePreviewTests
    {
        [Test]
        public void VerifiedSteelMillOriginalRoadProfileResolvesRealBitmap()
        {
            var profiles = OriginalXrtpSourceProfileReview.LoadVerified("v82_steel_mill");
            Assert.That(profiles.Length, Is.EqualTo(1));
            var image = OriginalRoadTextureSourcePreview.ResolveOriginalBitmap(profiles[0]);
            Assert.That(image, Is.Not.Null);
            Assert.That(image.SourcePath.Replace('\\', '/'),
                Does.EndWith("/LEVELS/STEELMIL/XRTP00_FAR00.bmp"));
            Assert.That(image.OriginalSha256.Length, Is.EqualTo(64));
            Assert.That(File.Exists(image.SourcePath), Is.True);
            Assert.That(image.BitmapGuid, Is.EqualTo("4e684e74646095d4b84fd21d129df0c1"));
        }

        [Test]
        public void MissingOriginalRoadMaterialDoesNotCreateTexture()
        {
            Assert.That(OriginalRoadTextureSourcePreview.ResolveOriginalBitmap(null), Is.Null);
            Assert.Throws<InvalidDataException>(() =>
                OriginalRoadTextureSourcePreview.ResolveOriginalBitmap(
                    new OriginalXrtpSourceProfileReview.SourceProfile
                    {
                        SourcePrefabPath = "/tmp/forged-road/XRTP00.prefab"
                    }));
        }

        [Test]
        public void ThirtyThreeOriginalEdgeSamplesMakeOnlyAnInspectionMesh()
        {
            var left = new Vector3[33];
            var right = new Vector3[33];
            for (int i = 0; i < 33; i++)
            {
                left[i] = new Vector3(100 * i, 100, 0);
                right[i] = new Vector3(100 * i, 100, 200);
            }
            Mesh mesh = OriginalRoadTextureSourcePreview.BuildDiagnosticStrip(
                left, right, 100);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(66));
                Assert.That(mesh.triangles.Length, Is.EqualTo(192));
                Assert.That(mesh.uv.Length, Is.EqualTo(66));
                Assert.That(mesh.uv[0], Is.EqualTo(Vector2.zero));
                Assert.That(mesh.uv[65], Is.EqualTo(Vector2.one));
                Assert.That(mesh.name, Does.Contain("PROVISIONAL UV"));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void SurfaceFailsClosedOnMissingZeroWidthAndMalformedSegments()
        {
            var left = new Vector3[33];
            var right = new Vector3[33];
            for (int i = 0; i < 33; i++)
            {
                left[i] = new Vector3(i * 100, 0, 0);
                right[i] = new Vector3(i * 100, 0, 200);
            }
            Assert.Throws<InvalidDataException>(() =>
                OriginalRoadTextureSourcePreview.BuildDiagnosticStrip(left, null, 100));
            Assert.Throws<InvalidDataException>(() =>
                OriginalRoadTextureSourcePreview.BuildDiagnosticStrip(left, right, 0));
            right[5] = left[5];
            Assert.Throws<InvalidDataException>(() =>
                OriginalRoadTextureSourcePreview.BuildDiagnosticStrip(left, right, 100));
            right[5] = new Vector3(500, 0, 200);
            left[3] = new Vector3(float.NaN, 0, 0);
            Assert.Throws<InvalidDataException>(() =>
                OriginalRoadTextureSourcePreview.BuildDiagnosticStrip(left, right, 100));
        }
    }
}
#endif
