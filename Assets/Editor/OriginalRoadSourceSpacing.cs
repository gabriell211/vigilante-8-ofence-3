#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Source-backed step scheduler from LevelManager.FUN_5850. Uses the
    /// original DAT_28 / sqrt(derivative.x² + derivative.z²) rule and final
    /// t=4096 endpoint on each pre-junction FUN_630C leaf.
    /// Only parameter spacing is reconstructed: not FUN_6604, junction
    /// transitions, UV atlas mapping, collision or release road meshes.
    /// </summary>
    public static class OriginalRoadSourceSpacing
    {
        private const string LevelCode =
            "LegacySources/Vigilante2Unity/Assets/Scripts/LevelManager.cs";
        private const int LocalEnd = 4096;
        private const int MaxSourcePoints = 4097;

        private struct CurveDerivative
        {
            public int Cubic, Quadratic, Linear;
        }

        private static bool Int32(long value) =>
            value >= int.MinValue && value <= int.MaxValue;

        private static bool ScaleSigned(int value, int scalar, int negativeBias, out int result)
        {
            result = 0;
            long n = (long)value * scalar;
            if (!Int32(n)) return false;
            if (n < 0) n += negativeBias;
            if (!Int32(n)) return false;
            result = (int)n >> 4;
            return true;
        }

        private static bool TryDerivativeCoefficients(int p0, int p1, int p2, int p3,
            out CurveDerivative coeff)
        {
            coeff = default;
            long cubic = (long)p1 * 3 - p0 - (long)p2 * 3 + p3;
            long quadratic = (long)p0 * 3 - (long)p1 * 6 + (long)p2 * 3;
            long linear = (long)p1 * 3 - (long)p0 * 3;
            if (!Int32(cubic) || !Int32(quadratic) || !Int32(linear)) return false;
            if (!ScaleSigned((int)cubic, 1, 15, out int c) ||
                !ScaleSigned((int)quadratic, 1, 15, out int q) ||
                !ScaleSigned((int)linear, 1, 15, out int l) ||
                !Int32((long)c * 3) || !Int32((long)q * 2))
                return false;
            coeff = new CurveDerivative
            {
                Cubic = c * 3,
                Quadratic = q * 2,
                Linear = l
            };
            return true;
        }

        private static bool TrySourceTangent(int t, CurveDerivative c, out int derivative)
        {
            derivative = 0;
            if (t < 0 || t > LocalEnd) return false;
            long term = (long)c.Cubic * ((t * t) >> 12) +
                (long)c.Quadratic * t;
            if (!Int32(term)) return false;
            if (term < 0) term += 4095;
            if (!Int32(term)) return false;
            term = (term >> 12) + c.Linear;
            if (!Int32(term)) return false;
            if (term < 0) term += 255;
            if (!Int32(term)) return false;
            derivative = (int)(term >> 8);
            return true;
        }

        private static int IntegerSqrt(int value)
        {
            int sqrt = (int)Math.Sqrt(value);
            while ((long)(sqrt + 1) * (sqrt + 1) <= value) sqrt++;
            while ((long)sqrt * sqrt > value) sqrt--;
            return sqrt;
        }

        /// <summary>
        /// First FUN_5850 pass counts source steps with DAT_28 / tangent norm.
        /// Second pass samples these t values and replaces the final overshoot
        /// with exactly 4096. It rejects zero increments and unsafe overflow.
        /// </summary>
        public static bool TrySampleOneLeaf(Vector3Int[] controls, int sourceDat28,
            out int[] sourceTimes)
        {
            sourceTimes = null;
            if (controls == null || controls.Length != 4 || sourceDat28 <= 0)
                return false;
            if (!TryDerivativeCoefficients(controls[0].x, controls[1].x,
                    controls[2].x, controls[3].x, out CurveDerivative x) ||
                !TryDerivativeCoefficients(controls[0].z, controls[1].z,
                    controls[2].z, controls[3].z, out CurveDerivative z))
                return false;

            bool Step(int t, out int increment)
            {
                increment = 0;
                if (!TrySourceTangent(t, x, out int dx) ||
                    !TrySourceTangent(t, z, out int dz))
                    return false;
                long normSquared = (long)dx * dx + (long)dz * dz;
                if (!Int32(normSquared) || normSquared == 0) return false;
                int norm = IntegerSqrt((int)normSquared);
                if (norm <= 0) return false;
                increment = sourceDat28 / norm;
                return increment > 0;
            }

            int tCount = 0, count = 0;
            do
            {
                if (count >= MaxSourcePoints - 1 || !Step(tCount, out int delta) ||
                    !Int32((long)tCount + delta))
                    return false;
                tCount += delta;
                count++;
            } while (tCount < LocalEnd);

            var times = new int[count + 1];
            int t = 0;
            for (int i = 0; i <= count; i++)
            {
                times[i] = t;
                if (i == count) break;
                if (i == count - 1)
                    t = LocalEnd;
                else
                {
                    if (!Step(t, out int delta) || !Int32((long)t + delta))
                        return false;
                    t += delta;
                }
                if (t > LocalEnd || t <= times[i]) return false;
            }
            if (times[count] != LocalEnd) return false;
            sourceTimes = times;
            return true;
        }

        /// <summary>
        /// Maps actual FUN_5850 leaf-local t into the unadjusted RSEG t
        /// interval. This conversion is for the existing source-only preview
        /// surface; FUN_719C adjusted world geometry remains unsupported.
        /// </summary>
        public static bool TryBuildOriginalParameters(
            OriginalRoadAdaptiveSourceSubdivision.Leaf[] leaves,
            int sourceDat28, out int[] globalTimesFixed12)
        {
            globalTimesFixed12 = null;
            if (leaves == null || leaves.Length < 1 ||
                leaves.Length > OriginalRoadAdaptiveEdgeSampler.MaximumPreviewLeaves ||
                sourceDat28 <= 0)
                return false;
            var samples = new List<int>();
            int expectedStart = 0;
            foreach (var leaf in leaves)
            {
                if (leaf == null || leaf.SourceControlPoints == null ||
                    leaf.SourceControlPoints.Length != 4 ||
                    leaf.FirstParameter16 != expectedStart ||
                    leaf.LastParameter16 <= expectedStart ||
                    leaf.LastParameter16 > 65536 ||
                    !TrySampleOneLeaf(leaf.SourceControlPoints,
                        sourceDat28, out int[] local))
                    return false;
                int span = leaf.LastParameter16 - leaf.FirstParameter16;
                for (int j = 0; j < local.Length; j++)
                {
                    if (samples.Count > 0 && j == 0) continue;
                    long global16 = (long)leaf.FirstParameter16 +
                        ((long)span * local[j] / LocalEnd);
                    int global = (int)(global16 >> 4);
                    if (global < 0 || global > LocalEnd ||
                        (samples.Count > 0 && global <= samples[samples.Count - 1]) ||
                        samples.Count >= MaxSourcePoints)
                        return false;
                    samples.Add(global);
                }
                expectedStart = leaf.LastParameter16;
            }
            if (expectedStart != 65536 || samples[samples.Count - 1] != LocalEnd)
                return false;
            globalTimesFixed12 = samples.ToArray();
            return true;
        }

        public static void VerifySourceMethod()
        {
            string source = File.ReadAllText(Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", LevelCode)));
            foreach (string required in new[]
            {
                "private Junction FUN_5850(int[,] param1, XRTP_DB param2, ushort param3)",
                "iVar20 = iVar20 + param2.DAT_28 / iVar21;",
                "iVar21 += param2.DAT_28 / iVar15;",
                "if (local_4c == local_50 - 1)",
                "iVar21 = 4096;",
                "iVar21 = (int)Utilities.SquareRoot((iVar13 >> 8) * (iVar13 >> 8) + (iVar21 >> 8) * (iVar21 >> 8));"
            })
                if (!source.Contains(required))
                    throw new InvalidDataException(
                        "Original FUN_5850 DAT_28 parameter stepping source changed");
        }
    }
}
#endif
