#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Non-colliding source RSEG centerline and optional authentic-XRTP-width
    /// edge guides. Edge guides are a diagnostic cross-section only and DO NOT
    /// reproduce original adaptive splitting or junction-adjusted road meshes.
    /// Source coefficients and all fileIDs come from the SHA-checked scene.
    /// </summary>
    public static class OriginalRsegHeightPreviewBuilder
    {
        private const string MaterialFolder = "Assets/GameContent/PreviewRoadMaterialsV82";
        private const string MaterialPath = MaterialFolder + "/SourceOnly.mat";

        public sealed class PreparedSourceLines
        {
            private readonly string stage;
            private readonly int divisor;
            private readonly OriginalRoadSegmentSourceReviewWindow.SourceRoad[] lines;
            private readonly Material material;

            internal PreparedSourceLines(string stageId, int factor,
                OriginalRoadSegmentSourceReviewWindow.SourceRoad[] sourceRoads,
                Material sourceMaterial)
            {
                stage = stageId;
                divisor = factor;
                lines = sourceRoads;
                material = sourceMaterial;
            }

            public int AppendTo(Transform parent)
            {
                if (Application.isPlaying || parent == null ||
                    parent.gameObject.scene != SceneManager.GetActiveScene() ||
                    parent.childCount != 0 ||
                    parent.position != Vector3.zero ||
                    parent.rotation != Quaternion.identity ||
                    parent.lossyScale != Vector3.one)
                    throw new InvalidOperationException(
                        "RSEG visual overlay needs a new empty origin-aligned scene root");

                foreach (var road in lines)
                {
                    if (road.OriginalFixedXYZDense == null ||
                        road.OriginalFixedXYZDense.Length !=
                        OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount)
                        throw new InvalidDataException("RSEG source has incomplete original 33-sample Y data");
                    var item = new GameObject("RSEG / " + road.RsegFileId +
                        " / JUNC " + road.FirstJuncFileId + " > " + road.SecondJuncFileId);
                    item.transform.SetParent(parent, false);
                    var line = item.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.sharedMaterial = material;
                    line.widthMultiplier = 0.08f; // visual guide only, NOT road width
                    line.numCapVertices = 0;
                    line.numCornerVertices = 0;
                    line.receiveShadows = false;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.positionCount = road.OriginalFixedXYZDense.Length;
                    for (int i = 0; i < road.OriginalFixedXYZDense.Length; i++)
                        line.SetPosition(i, ToSourceReviewPosition(road.OriginalFixedXYZDense[i], divisor));
                    // Optional sides use the authentic XRTP width and source
                    // tangent. Their source Y is independently resolved per edge.
                    // Parent identity excludes ALL these guides from release.
                    if (road.OriginalLeftEdgeFixedXYZDense != null &&
                        road.OriginalRightEdgeFixedXYZDense != null)
                    {
                        AppendGuide(item.transform, "SOURCE LEFT EDGE (NOT A ROAD MESH)",
                            road.OriginalLeftEdgeFixedXYZDense, divisor, material);
                        AppendGuide(item.transform, "SOURCE RIGHT EDGE (NOT A ROAD MESH)",
                            road.OriginalRightEdgeFixedXYZDense, divisor, material);
                    }
                    item.AddComponent<OriginalRoadSourcePreviewIdentity>().Configure(
                        stage, road.RsegFileId, road.FirstJuncFileId,
                        road.SecondJuncFileId, divisor);
                }

                if (parent.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    parent.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException(
                        "Original RSEG source lines must NEVER add physics");
                return lines.Length;
            }
        }

        public static Vector3 ToSourceReviewPosition(Vector3 fixedPoint, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
            return new Vector3(fixedPoint.x / divisor,
                -fixedPoint.y / divisor, fixedPoint.z / divisor);
        }

        /// <summary>
        /// Translates FUN_5850's unadjusted perpendicular offset using its
        /// source signed integer division order. The junction transition,
        /// adaptive t-step and triangulation have not yet been ported.
        /// </summary>
        public static bool TrySourceHalfWidthOffset(int derivativeX, int derivativeZ,
            int fullWidth, out Vector2Int offset)
        {
            offset = Vector2Int.zero;
            if (fullWidth <= 0) return false;
            long squared = (long)derivativeX * derivativeX +
                (long)derivativeZ * derivativeZ;
            if (squared < 1 || squared > int.MaxValue) return false;
            int magnitude = (int)Math.Sqrt(squared);
            // Integer sqrt correction avoids platform floating-point uncertainty.
            while ((long)(magnitude + 1) * (magnitude + 1) <= squared) magnitude++;
            while ((long)magnitude * magnitude > squared) magnitude--;
            if (magnitude <= 0) return false;
            long horizontal = (long)derivativeZ * fullWidth;
            long vertical = (long)derivativeX * fullWidth;
            if (horizontal < int.MinValue || horizontal > int.MaxValue ||
                vertical < int.MinValue || vertical > int.MaxValue) return false;
            offset = new Vector2Int((int)((horizontal / 2) / magnitude),
                (int)((vertical / 2) / magnitude));
            return true;
        }

        private static bool IsInt32(long value) =>
            value >= int.MinValue && value <= int.MaxValue;

        private static void AppendGuide(Transform parent, string name, Vector3[] positions,
            int divisor, Material shared)
        {
            if (positions.Length != OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount)
                throw new InvalidDataException("Partial source edge guide rejected");
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var line = item.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = shared;
            line.widthMultiplier = 0.04f; // diagnostic marker, not physical curb width
            line.receiveShadows = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.positionCount = positions.Length;
            for (int i = 0; i < positions.Length; i++)
                line.SetPosition(i, ToSourceReviewPosition(positions[i], divisor));
        }

        private static void TryAddSourceEdges(
            OriginalRoadSegmentSourceReviewWindow.SourceRoad road,
            OriginalXrtpSourceProfileReview.SourceProfile profile,
            OriginalVigTerrainSourceGrid originalGrid)
        {
            if (profile == null || (road.SourceRoadFlags & 0x10) != 0 ||
                road.OriginalFixedXYZDense == null ||
                road.OriginalFixedTangentXZDense == null ||
                road.OriginalFixedTangentXZDense.Length !=
                    OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount)
                return;
            var left = new Vector3[road.OriginalFixedXYZDense.Length];
            var right = new Vector3[left.Length];
            for (int i = 0; i < left.Length; i++)
            {
                Vector2Int tangent = road.OriginalFixedTangentXZDense[i];
                if (!TrySourceHalfWidthOffset(tangent.x, tangent.y,
                        profile.FullWidthFixed, out Vector2Int delta))
                    return;
                // Keep integer XZ source precision: Unity Vector3 is a float
                // and rounds large original fixed-point coordinates.
                if (road.OriginalFixedXZExactDense == null ||
                    road.OriginalFixedXZExactDense.Length != left.Length)
                    return;
                Vector2Int originalXZ = road.OriginalFixedXZExactDense[i];
                long lx = (long)originalXZ.x - delta.x;
                long rx = (long)originalXZ.x + delta.x;
                long lz = (long)originalXZ.y + delta.y;
                long rz = (long)originalXZ.y - delta.y;
                if (!IsInt32(lx) || !IsInt32(rx) || !IsInt32(lz) || !IsInt32(rz))
                    return;

                int ly, ry;
                if ((road.SourceRoadFlags & 1) != 0)
                    ly = ry = (int)road.OriginalFixedXYZDense[i].y; // FUN_5850 bridge/non-terrain height branch
                else if (originalGrid != null &&
                    originalGrid.TrySampleHeight((int)lx, (int)lz, out ly) &&
                    originalGrid.TrySampleHeight((int)rx, (int)rz, out ry))
                {
                    // FUN_5850 actually samples each terrain-side Y independently.
                }
                else
                    return;
                left[i] = new Vector3((int)lx, ly, (int)lz);
                right[i] = new Vector3((int)rx, ry, (int)rz);
            }
            road.OriginalLeftEdgeFixedXYZDense = left;
            road.OriginalRightEdgeFixedXYZDense = right;
        }

        public static PreparedSourceLines Prepare(string stageId)
        {
            // SHA verification is shared with original JUNC builder. RSEG and
            // JUNC .meta script IDs and original RSEG source are also validated.
            var plan = OriginalRoadSegmentSourceReviewWindow.LoadVerified(stageId);
            int divisor = OriginalJunctionSourceReviewBuilder.VerifiedDebugScale();
            OriginalXrtpSourceProfileReview.SourceProfile[] originalProfiles =
                OriginalXrtpSourceProfileReview.LoadVerified(stageId);
            OriginalVigTerrainSourceGrid originalGrid = plan.Roads.Any(x =>
                x.NeedsVigTerrainHeight ||
                (x.SourceXrtpProfileIndex.HasValue && (x.SourceRoadFlags & 1) == 0))
                ? OriginalVigTerrainSourceGrid.LoadVerified(stageId) : null;
            foreach (var road in plan.Roads)
            {
                if (road.OriginalFixedXYZ != null || !road.NeedsVigTerrainHeight ||
                    road.OriginalFixedXZExact == null || road.OriginalFixedXZExact.Length != 5)
                    continue;
                var samples = new Vector3[road.OriginalFixedXZExact.Length];
                bool resolved = true;
                for (int i = 0; i < samples.Length; i++)
                {
                    Vector2Int original = road.OriginalFixedXZExact[i];
                    if (originalGrid == null ||
                        !originalGrid.TrySampleHeight(original.x, original.y, out int y))
                    {
                        resolved = false;
                        break;
                    }
                    samples[i] = new Vector3(original.x, y, original.y);
                }
                if (resolved) road.OriginalFixedXYZ = samples;

                // The terrain grid is the original signed-height source,
                // queried independently at every genuine curve position.
                // We never interpolate heights between the 5 coarse points.
                if (!resolved || road.OriginalFixedXZExactDense == null ||
                    road.OriginalFixedXZExactDense.Length !=
                    OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount)
                    continue;
                var dense = new Vector3[road.OriginalFixedXZExactDense.Length];
                for (int i = 0; i < dense.Length; i++)
                {
                    Vector2Int raw = road.OriginalFixedXZExactDense[i];
                    if (!originalGrid.TrySampleHeight(raw.x, raw.y, out int y))
                    {
                        resolved = false;
                        break;
                    }
                    dense[i] = new Vector3(raw.x, y, raw.y);
                }
                if (resolved) road.OriginalFixedXYZDense = dense;
            }
            foreach (var road in plan.Roads)
            {
                int? originalIndex = road.SourceXrtpProfileIndex;
                if (!originalIndex.HasValue || originalIndex.Value < 0 ||
                    originalIndex.Value >= originalProfiles.Length)
                    continue;
                TryAddSourceEdges(road, originalProfiles[originalIndex.Value], originalGrid);
            }
            var onlyKnownHeight = plan.Roads.Where(x => x.OriginalFixedXYZ != null &&
                x.OriginalFixedXYZDense != null).ToArray();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    throw new InvalidOperationException("URP original-source line shader not available");
                if (!AssetDatabase.IsValidFolder(MaterialFolder))
                    AssetDatabase.CreateFolder("Assets/GameContent", "PreviewRoadMaterialsV82");
                material = new Material(shader) { name = "RSEG TRACE - INSPECTION ONLY" };
                material.SetColor("_BaseColor", new Color(0.96f, 0.55f, 0.19f, 1f));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            return new PreparedSourceLines(stageId, divisor, onlyKnownHeight, material);
        }
    }
}
#endif
