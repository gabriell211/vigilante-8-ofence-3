#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Independent source-only port of LevelManager.FUN_57AC/FUN_630C.
    /// This starts from unadjusted JUNC/RSEG cubic controls: FUN_719C's
    /// endpoint junction transformations, FUN_6604 and FUN_5850 sampling
    /// are not reproduced here. Leaves are diagnostic, never gameplay roads.
    /// </summary>
    public static class OriginalRoadAdaptiveSourceSubdivision
    {
        public const int OriginalExtentCutoff = 0x100000;
        public const int MaximumSourceSubdivisionDepth = 16;
        public const int MaximumInspectionLeaves = 4096;
        private const string ManagerSource =
            "LegacySources/Vigilante2Unity/Assets/Scripts/LevelManager.cs";

        public sealed class Leaf
        {
            public Vector3Int[] SourceControlPoints;
            public int FirstParameter16, LastParameter16, Depth;
        }

        private static bool SourceInt(long value) =>
            value >= int.MinValue && value <= int.MaxValue;

        /// <summary>
        /// One source DAT_10/DAT_14 field is 2 little-endian int32 values,
        /// as encoded in the original Unity YAML byte stream.
        /// </summary>
        public static bool TryReadOriginalTangentPair(string hex, out int[] pair)
        {
            pair = null;
            if (hex == null || hex.Length != 16) return false;
            var values = new int[2];
            for (int component = 0; component < 2; component++)
            {
                uint bits = 0;
                for (int byteOffset = 0; byteOffset < 4; byteOffset++)
                {
                    if (!byte.TryParse(hex.Substring(component * 8 + byteOffset * 2, 2),
                        NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte digit))
                        return false;
                    bits |= (uint)digit << (byteOffset * 8);
                }
                values[component] = unchecked((int)bits);
            }
            pair = values;
            return true;
        }

        /// <summary>
        /// Original FUN_719C initial control rectangle, prior to junction
        /// edits. Source places zero heights on the two cubic handles.
        /// </summary>
        public static bool TryCreateOriginalUnadjustedControls(
            Vector3Int start, Vector3Int finish, int[] tangentX, int[] tangentZ,
            out Vector3Int[] result)
        {
            result = null;
            if (tangentX == null || tangentZ == null ||
                tangentX.Length != 2 || tangentZ.Length != 2)
                return false;
            long p1x = (long)start.x + tangentX[0];
            long p1z = (long)start.z + tangentZ[0];
            long p2x = (long)finish.x + tangentX[1];
            long p2z = (long)finish.z + tangentZ[1];
            if (!SourceInt(p1x) || !SourceInt(p1z) ||
                !SourceInt(p2x) || !SourceInt(p2z)) return false;
            result = new[]
            {
                start,
                new Vector3Int((int)p1x, 0, (int)p1z),
                new Vector3Int((int)p2x, 0, (int)p2z),
                finish
            };
            return true;
        }

        /// <summary>
        /// FUN_57AC tests max(X range, Z range) of the original four controls.
        /// All calculations fail closed on signed 32-bit overflow.
        /// </summary>
        public static bool TryOriginalSourceExtent(Vector3Int[] controls, out int extent)
        {
            extent = 0;
            if (controls == null || controls.Length != 4) return false;
            int minX = controls[0].x, maxX = controls[0].x;
            int minZ = controls[0].z, maxZ = controls[0].z;
            for (int i = 1; i < 4; i++)
            {
                minX = Math.Min(minX, controls[i].x);
                maxX = Math.Max(maxX, controls[i].x);
                minZ = Math.Min(minZ, controls[i].z);
                maxZ = Math.Max(maxZ, controls[i].z);
            }
            long range = Math.Max((long)maxX - minX, (long)maxZ - minZ);
            if (!SourceInt(range)) return false;
            extent = (int)range;
            return true;
        }

        private static bool Midpoint(int x, int y, out int mid)
        {
            mid = 0;
            long sum = (long)x + y;
            // FUN_630C calculates (a+b)/2 using int, not 64-bit.
            // Reject an unsafe source operation rather than wrap.
            if (!SourceInt(sum)) return false;
            mid = (int)(sum / 2);
            return true;
        }

        /// <summary>
        /// Literal de Casteljau midpoint layout from FUN_630C:
        /// intermediate Y handles are reset to zero, while the shared
        /// endpoint Y equals (P0.y + P3.y) / 2, as in the original source.
        /// </summary>
        public static bool TryOriginalHalfSplit(
            Vector3Int[] source, out Vector3Int[] left, out Vector3Int[] right)
        {
            left = right = null;
            if (source == null || source.Length != 4) return false;

            if (!Midpoint(source[1].x, source[2].x, out int midX) ||
                !Midpoint(source[1].z, source[2].z, out int midZ) ||
                !Midpoint(source[0].x, source[1].x, out int aX) ||
                !Midpoint(source[0].z, source[1].z, out int aZ) ||
                !Midpoint(aX, midX, out int bX) ||
                !Midpoint(aZ, midZ, out int bZ) ||
                !Midpoint(source[2].x, source[3].x, out int eX) ||
                !Midpoint(source[2].z, source[3].z, out int eZ) ||
                !Midpoint(eX, midX, out int dX) ||
                !Midpoint(eZ, midZ, out int dZ) ||
                !Midpoint(bX, dX, out int cX) ||
                !Midpoint(bZ, dZ, out int cZ) ||
                !Midpoint(source[0].y, source[3].y, out int cY))
                return false;

            Vector3Int middle = new Vector3Int(cX, cY, cZ);
            left = new[]
            {
                source[0],
                new Vector3Int(aX, 0, aZ),
                new Vector3Int(bX, 0, bZ),
                middle
            };
            right = new[]
            {
                middle,
                new Vector3Int(dX, 0, dZ),
                new Vector3Int(eX, 0, eZ),
                source[3]
            };
            return true;
        }

        public static bool TrySplitOriginalUnadjustedCurve(
            Vector3Int[] controls, out Leaf[] leaves)
        {
            leaves = null;
            if (controls == null || controls.Length != 4) return false;
            var result = new List<Leaf>();
            bool Visit(Vector3Int[] part, int depth, int start, int end)
            {
                if (!TryOriginalSourceExtent(part, out int extent)) return false;
                if (extent < OriginalExtentCutoff)
                {
                    if (result.Count >= MaximumInspectionLeaves) return false;
                    result.Add(new Leaf
                    {
                        SourceControlPoints = part,
                        FirstParameter16 = start,
                        LastParameter16 = end,
                        Depth = depth
                    });
                    return true;
                }
                if (depth >= MaximumSourceSubdivisionDepth ||
                    end - start < 2 || result.Count >= MaximumInspectionLeaves)
                    return false;
                if (!TryOriginalHalfSplit(part, out Vector3Int[] left,
                    out Vector3Int[] right)) return false;
                int middle = start + (end - start) / 2;
                return Visit(left, depth + 1, start, middle) &&
                       Visit(right, depth + 1, middle, end);
            }
            if (!Visit(controls, 0, 0, 65536)) return false;
            leaves = result.ToArray();
            return leaves.Length > 0;
        }

        public static void VerifyArchivedSource()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", ManagerSource));
            string code = File.ReadAllText(path);
            foreach (string signature in new[]
            {
                "private int FUN_57AC(int[] param1)",
                "private void FUN_630C(int[,] param1, XRTP_DB param2, ushort param3)",
                "if (iVar3 < 0x100000)",
                "(param1[0, 3] + param1[1, 2]) / 2;",
                "(param1[1, 1] + param1[2, 0]) / 2;",
                "(param1[0, 1] + param1[2, 2]) / 2,",
                "FUN_630C(new int[,]",
                "jVar4 = FUN_5850(param1, param2, param3);"
            })
                if (!code.Contains(signature))
                    throw new InvalidDataException(
                        "Archived FUN_630C/57AC subdivision code changed: " + signature);
        }
    }
}
#endif
