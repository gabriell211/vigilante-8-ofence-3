#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Connects pre-junction FUN_630C leaf intervals with the separately
    /// verified RSEG_DB.FUN_285E4 X/Z polynomial and source XRTP width.
    /// Visual inspection ONLY: FUN_719C adjustments and FUN_5850's adaptive
    /// longitudinal spacing, triangulation and road UVs are still missing.
    /// </summary>
    public static class OriginalRoadAdaptiveEdgeSampler
    {
        public const int SectionsPerLeaf = 32;
        public const int MaximumPreviewLeaves = 128;
        public const int MaximumPreviewSamples =
            MaximumPreviewLeaves * SectionsPerLeaf + 1;

        public sealed class EdgeSamples
        {
            public Vector3[] Left, Right;
            public int[] TimesFixed12;
            public bool SourceDat28Stepping;
        }

        private static bool Int32(long value) =>
            value >= int.MinValue && value <= int.MaxValue;

        /// <summary>
        /// Converts original FUN_630C fixed16 leaf intervals into monotonic
        /// t/4096 RSEG samples. No interpolation between previous 33 points.
        /// Invalid/over-dense leaves are omitted rather than silently clamped.
        /// </summary>
        public static bool TryBuildSourceParameterSamples(
            OriginalRoadAdaptiveSourceSubdivision.Leaf[] leaves, out int[] times)
        {
            times = null;
            if (leaves == null || leaves.Length < 1 ||
                leaves.Length > MaximumPreviewLeaves) return false;
            var result = new int[leaves.Length * SectionsPerLeaf + 1];
            int previousT = -1;
            for (int i = 0; i < leaves.Length; i++)
            {
                var leaf = leaves[i];
                if (leaf == null || leaf.SourceControlPoints == null ||
                    leaf.SourceControlPoints.Length != 4 ||
                    leaf.FirstParameter16 != (i == 0 ? 0 : leaves[i - 1].LastParameter16) ||
                    leaf.LastParameter16 <= leaf.FirstParameter16 ||
                    leaf.LastParameter16 > 65536 ||
                    leaf.LastParameter16 - leaf.FirstParameter16 < 16 * SectionsPerLeaf)
                    return false;
                int span = leaf.LastParameter16 - leaf.FirstParameter16;
                for (int section = 0; section <= SectionsPerLeaf; section++)
                {
                    if (i > 0 && section == 0) continue;
                    long originalT16 = (long)leaf.FirstParameter16 +
                        (long)span * section / SectionsPerLeaf;
                    int t = (int)(originalT16 >> 4);
                    if (t < 0 || t > 4096 || t <= previousT) return false;
                    int idx = i * SectionsPerLeaf + section;
                    result[idx] = t;
                    previousT = t;
                }
            }
            if (previousT != 4096) return false;
            times = result;
            return true;
        }

        /// <summary>
        /// Re-evaluates source positions AND signed tangents at each adaptive
        /// t, then samples the original VigTerrain at both side positions or
        /// uses the original RSEG height flag branch. Never guesses road Y.
        /// </summary>
        public static bool TryBuild(
            OriginalRoadSegmentSourceReviewWindow.SourceRoad road,
            OriginalXrtpSourceProfileReview.SourceProfile profile,
            OriginalVigTerrainSourceGrid terrain, out EdgeSamples samples)
        {
            samples = null;
            if (road == null || profile == null || profile.FullWidthFixed <= 0 ||
                (road.SourceRoadFlags & 0x10) != 0 ||
                road.OriginalPolynomialX == null || road.OriginalPolynomialZ == null ||
                road.OriginalPolynomialX.Length != 3 || road.OriginalPolynomialZ.Length != 3)
                return false;
            // Prefer the actual FUN_5850 DAT_28 step calculation per
            // archived FUN_630C leaf. If an original step cannot be represented
            // safely, retain clearly-labelled 32-per-leaf inspection only.
            bool actualDat28 = OriginalRoadSourceSpacing.TryBuildOriginalParameters(
                road.OriginalUnadjustedAdaptiveLeaves,
                profile.LongitudinalStepFixed, out int[] times);
            if (!actualDat28 && !TryBuildSourceParameterSamples(
                    road.OriginalUnadjustedAdaptiveLeaves, out times))
                return false;
            var left = new Vector3[times.Length];
            var right = new Vector3[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                int t = times[i];
                int[] px = road.OriginalPolynomialX;
                int[] pz = road.OriginalPolynomialZ;
                if (!OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(
                        road.OriginalFirstJunctionXYZ.x, t,
                        px[0], px[1], px[2], out int x) ||
                    !OriginalRoadSegmentSourceReviewWindow.TrySampleAxis(
                        road.OriginalFirstJunctionXYZ.z, t,
                        pz[0], pz[1], pz[2], out int z) ||
                    !OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(
                        t, px[0], px[1], px[2], out int dx) ||
                    !OriginalRoadSegmentSourceReviewWindow.TrySampleSourceTangent(
                        t, pz[0], pz[1], pz[2], out int dz) ||
                    !OriginalRsegHeightPreviewBuilder.TrySourceHalfWidthOffset(
                        dx, dz, profile.FullWidthFixed, out Vector2Int half))
                    return false;
                long lx = (long)x - half.x, rx = (long)x + half.x;
                long lz = (long)z + half.y, rz = (long)z - half.y;
                if (!Int32(lx) || !Int32(rx) || !Int32(lz) || !Int32(rz))
                    return false;

                int ly, ry;
                if (OriginalRoadSegmentSourceReviewWindow.TrySampleKnownHeight(
                    road.OriginalFirstJunctionXYZ, road.OriginalLastJunctionXYZ,
                    road.SourceRoadFlags, t, out int sourceY))
                {
                    ly = ry = sourceY;
                }
                else if ((road.SourceRoadFlags & 9) == 0 && terrain != null &&
                         terrain.TrySampleHeight((int)lx, (int)lz, out ly) &&
                         terrain.TrySampleHeight((int)rx, (int)rz, out ry))
                {
                    // Both values come independently from VigTerrain.FUN_1B750.
                }
                else return false;
                left[i] = new Vector3((int)lx, ly, (int)lz);
                right[i] = new Vector3((int)rx, ry, (int)rz);
            }
            samples = new EdgeSamples
            {
                Left = left, Right = right, TimesFixed12 = times,
                SourceDat28Stepping = actualDat28
            };
            return true;
        }
    }
}
#endif
