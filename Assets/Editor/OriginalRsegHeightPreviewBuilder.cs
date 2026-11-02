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
    /// Diagnostic non-colliding lines for authentic RSEG segments whose source
    /// FUN_285E4 height branch does NOT need VigTerrain.
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

        public static PreparedSourceLines Prepare(string stageId)
        {
            // SHA verification is shared with original JUNC builder. RSEG and
            // JUNC .meta script IDs and original RSEG source are also validated.
            var plan = OriginalRoadSegmentSourceReviewWindow.LoadVerified(stageId);
            int divisor = OriginalJunctionSourceReviewBuilder.VerifiedDebugScale();
            OriginalVigTerrainSourceGrid originalGrid = plan.Roads.Any(x => x.NeedsVigTerrainHeight)
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
