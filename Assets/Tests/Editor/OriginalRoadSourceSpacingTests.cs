#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalRoadSourceSpacingTests
    {
        private static Vector3Int[] ConstantSourceDerivative()
        {
            // Original cubic with a constant x tangent (24 fixed units).
            return new[]
            {
                new Vector3Int(0, 100, 0),
                new Vector3Int(32768, 0, 0),
                new Vector3Int(65536, 0, 0),
                new Vector3Int(98304, 200, 0)
            };
        }

        [Test]
        public void OriginalFun5850Dat28StepPreservesIntegerDivisionAndFinalEndpoint()
        {
            Assert.That(OriginalRoadSourceSpacing.TrySampleOneLeaf(
                ConstantSourceDerivative(), 81920, out int[] t), Is.True);
            Assert.That(t, Is.EqualTo(new[] { 0, 3413, 4096 }));
        }

        [Test]
        public void SourceDat28CanProduceFewerSamplesThanOldFixed32PerLeafPreview()
        {
            var controls = ConstantSourceDerivative();
            var leaves = new[]
            {
                new OriginalRoadAdaptiveSourceSubdivision.Leaf
                {
                    SourceControlPoints = controls,
                    FirstParameter16 = 0, LastParameter16 = 32768
                },
                new OriginalRoadAdaptiveSourceSubdivision.Leaf
                {
                    SourceControlPoints = controls,
                    FirstParameter16 = 32768, LastParameter16 = 65536
                }
            };
            Assert.That(OriginalRoadSourceSpacing.TryBuildOriginalParameters(
                leaves, 81920, out int[] parameters), Is.True);
            Assert.That(parameters, Is.EqualTo(new[] { 0, 1706, 2048, 3754, 4096 }));

            var left = new Vector3[parameters.Length];
            var right = new Vector3[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                left[i] = new Vector3(parameters[i], 0, -100);
                right[i] = new Vector3(parameters[i], 0, 100);
            }
            Mesh preview = OriginalRoadTextureSourcePreview.BuildAdaptiveDiagnosticStrip(
                left, right, parameters, 256);
            try
            {
                Assert.That(preview.vertexCount, Is.EqualTo(10));
                Assert.That(preview.triangles.Length, Is.EqualTo(24));
                Assert.That(preview.uv[4].y, Is.EqualTo(0.5f));
                Assert.That(preview.uv[preview.vertexCount - 1], Is.EqualTo(Vector2.one));
            }
            finally { UnityEngine.Object.DestroyImmediate(preview); }
        }

        [Test]
        public void InvalidDat28StationaryTangentAndOverflowFailClosed()
        {
            Assert.That(OriginalRoadSourceSpacing.TrySampleOneLeaf(
                ConstantSourceDerivative(), 0, out _), Is.False);
            Assert.That(OriginalRoadSourceSpacing.TrySampleOneLeaf(
                ConstantSourceDerivative(), 1, out _), Is.False);
            var stationary = new[]
            {
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0),
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0)
            };
            Assert.That(OriginalRoadSourceSpacing.TrySampleOneLeaf(
                stationary, 81920, out _), Is.False);
            var overflow = new[]
            {
                new Vector3Int(int.MaxValue, 0, 0), new Vector3Int(int.MinValue, 0, 0),
                new Vector3Int(int.MaxValue, 0, 0), new Vector3Int(int.MinValue, 0, 0)
            };
            Assert.That(OriginalRoadSourceSpacing.TrySampleOneLeaf(
                overflow, 81920, out _), Is.False);
        }

        [Test]
        public void MissingOrDuplicatedLeafIntervalsAreNotPromotedToOriginalTiming()
        {
            Assert.That(OriginalRoadSourceSpacing.TryBuildOriginalParameters(
                null, 81920, out _), Is.False);
            Assert.That(OriginalRoadSourceSpacing.TryBuildOriginalParameters(
                new[] { new OriginalRoadAdaptiveSourceSubdivision.Leaf
                {
                    SourceControlPoints = ConstantSourceDerivative(),
                    FirstParameter16 = 0, LastParameter16 = 65000
                } }, 81920, out _), Is.False);
        }

        [Test]
        public void VerifyAuthenticatedSourceContainsFun5850OriginalStepAndLastPoint()
        {
            Assert.DoesNotThrow(OriginalRoadSourceSpacing.VerifySourceMethod);
        }
    }
}
#endif
