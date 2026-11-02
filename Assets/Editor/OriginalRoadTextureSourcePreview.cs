#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Preview only: follows archived XRTP.timFarList[0] -> MAT._MainTex ->
    /// source BMP GUID, then uses that actual bitmap on a diagnostic strip.
    /// Neither the provisional UVs nor 33-point strip are original road meshes.
    /// </summary>
    public static class OriginalRoadTextureSourcePreview
    {
        private const string SourceLevels = "LegacySources/Vigilante2Unity/Assets/LEVELS/";
        private const string BitmapFolder = "Assets/GameContent/PreviewRoadTexturesV82";
        private const string MaterialFolder = "Assets/GameContent/PreviewRoadMaterialsV82";
        private const string MeshFolder = "Assets/GameContent/PreviewRoadMeshesV82";
        private const string Provenance = "ARCHIVED_ROAD_MAIN_TEX_PREVIEW_V1";
        private static readonly Regex Guid = new Regex(@"^guid: ([a-fA-F0-9]{32})$", RegexOptions.Multiline);
        private static readonly Regex FirstMaterial = new Regex(
            @"(?m)^  timFarList:\r?\n  - \{fileID: 2100000, guid: ([a-fA-F0-9]{32}), type: 2\}");
        private static readonly Regex MainTexture = new Regex(
            @"(?m)^    - _MainTex:\r?\n        m_Texture: \{fileID: 2800000, guid: ([a-fA-F0-9]{32}), type: 3\}");

        private static string Absolute(string path) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

        private static string Digest(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                var result = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(stream))
                    result.Append(b.ToString("x2"));
                return result.ToString();
            }
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string FindOriginalByGuid(string directory, string suffix, string guid)
        {
            string found = null;
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;
                Match id = Guid.Match(File.ReadAllText(file));
                if (!id.Success || !string.Equals(id.Groups[1].Value, guid,
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                if (found != null)
                    throw new InvalidDataException("Duplicate original road texture/material GUID");
                found = file.Substring(0, file.Length - ".meta".Length);
            }
            return found;
        }

        public sealed class OriginalBitmap
        {
            public string SourcePath, MaterialGuid, BitmapGuid, OriginalSha256;
        }

        public static OriginalBitmap ResolveOriginalBitmap(
            OriginalXrtpSourceProfileReview.SourceProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.SourcePrefabPath))
                return null;
            string sourcePath = Path.GetFullPath(profile.SourcePrefabPath);
            string sourceFolder = Absolute(SourceLevels);
            if (!sourcePath.StartsWith(sourceFolder, StringComparison.Ordinal) ||
                !sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourcePath))
                throw new InvalidDataException("Original XRTP prefab outside verified archive");
            string yaml = File.ReadAllText(sourcePath);
            Match matRef = FirstMaterial.Match(yaml);
            if (!matRef.Success) return null;
            string directory = Path.GetDirectoryName(sourcePath);
            string material = FindOriginalByGuid(directory, ".mat.meta", matRef.Groups[1].Value);
            if (material == null || !File.Exists(material)) return null;
            string matYaml = File.ReadAllText(material);
            if (!matYaml.StartsWith("%YAML 1.1\n", StringComparison.Ordinal) ||
                !Regex.IsMatch(matYaml, @"(?m)^--- !u!21 &2100000$"))
                return null;
            Match bmpRef = MainTexture.Match(matYaml);
            if (!bmpRef.Success) return null;
            string bitmap = FindOriginalByGuid(directory, ".bmp.meta", bmpRef.Groups[1].Value);
            if (bitmap == null || !File.Exists(bitmap)) return null;
            if (!bitmap.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                return null;
            return new OriginalBitmap
            {
                SourcePath = bitmap,
                MaterialGuid = matRef.Groups[1].Value.ToLowerInvariant(),
                BitmapGuid = bmpRef.Groups[1].Value.ToLowerInvariant(),
                OriginalSha256 = Digest(bitmap)
            };
        }

        public static Material PrepareDiagnosticMaterial(
            OriginalXrtpSourceProfileReview.SourceProfile profile)
        {
            OriginalBitmap verified = ResolveOriginalBitmap(profile);
            if (verified == null) return null; // never fabricate a fallback texture
            Folder(BitmapFolder);
            Folder(MaterialFolder);
            string bitmapAsset = BitmapFolder + "/" + verified.BitmapGuid + ".bmp";
            string destination = Absolute(bitmapAsset);
            if (File.Exists(destination) &&
                !string.Equals(Digest(destination), verified.OriginalSha256,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Modified original-source road preview bitmap");
            if (!File.Exists(destination))
                File.Copy(verified.SourcePath, destination);
            AssetDatabase.ImportAsset(bitmapAsset, ImportAssetOptions.ForceSynchronousImport);
            var textureImporter = AssetImporter.GetAtPath(bitmapAsset) as TextureImporter;
            if (textureImporter == null)
                throw new InvalidDataException("Original road source BMP did not import");
            string expected = Provenance + "|" + verified.BitmapGuid + "|" +
                verified.OriginalSha256;
            if (textureImporter.userData != expected ||
                !textureImporter.mipmapEnabled ||
                textureImporter.filterMode != FilterMode.Trilinear ||
                textureImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.mipmapEnabled = true;
                textureImporter.filterMode = FilterMode.Trilinear;
                textureImporter.anisoLevel = 8;
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                textureImporter.npotScale = TextureImporterNPOTScale.None;
                textureImporter.sRGBTexture = true;
                textureImporter.userData = expected;
                textureImporter.SaveAndReimport();
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(bitmapAsset);
            if (texture == null)
                throw new InvalidDataException("Original source road texture unavailable");

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("URP unlit shader required for original road preview");
            string materialPath = MaterialFolder + "/RoadSource-" +
                verified.MaterialGuid + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "ARCHIVED ROAD BITMAP / DIAGNOSTIC UV ONLY"
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            // Disable culling only for review: polygon winding is NOT original.
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 33 authentic RSEG/XRTP edge samples; 2 triangles per adjacent pair.
        /// The UVs below are explicitly provisional [0..1], NOT FUN_4F828 UVs.
        /// No collider, Rigidbody or gameplay identity may be added here.
        /// </summary>
        public static Mesh BuildDiagnosticStrip(Vector3[] left, Vector3[] right, int divisor)
        {
            const int count = OriginalRoadSegmentSourceReviewWindow.DenseCurveSampleCount;
            if (left == null || right == null || left.Length != count ||
                right.Length != count || divisor <= 0)
                throw new InvalidDataException("Original RSEG source edges are incomplete");
            return BuildSourceStrip(left, right, divisor, null);
        }

        public static Mesh BuildAdaptiveDiagnosticStrip(
            Vector3[] left, Vector3[] right, int[] originalTimesFixed12, int divisor)
        {
            int count = originalTimesFixed12 == null ? 0 : originalTimesFixed12.Length;
            if (left == null || right == null || count < 2 ||
                count > OriginalRoadAdaptiveEdgeSampler.MaximumPreviewSamples ||
                left.Length != count || right.Length != count || divisor <= 0 ||
                originalTimesFixed12[0] != 0 || originalTimesFixed12[count - 1] != 4096)
                throw new InvalidDataException("Adaptive source preview sample count invalid");
            for (int i = 1; i < count; i++)
                if (originalTimesFixed12[i] <= originalTimesFixed12[i - 1] ||
                    originalTimesFixed12[i] > 4096)
                    throw new InvalidDataException("Original adaptive source t must increase");
            return BuildSourceStrip(left, right, divisor, originalTimesFixed12);
        }

        private static Mesh BuildSourceStrip(
            Vector3[] left, Vector3[] right, int divisor, int[] originalTimesFixed12)
        {
            int count = left.Length;
            var vertices = new Vector3[count * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                vertices[2 * i] = OriginalRsegHeightPreviewBuilder
                    .ToSourceReviewPosition(left[i], divisor);
                vertices[2 * i + 1] = OriginalRsegHeightPreviewBuilder
                    .ToSourceReviewPosition(right[i], divisor);
                if (!Finite(vertices[2 * i]) || !Finite(vertices[2 * i + 1]) ||
                    (vertices[2 * i] - vertices[2 * i + 1]).sqrMagnitude < 1e-9f)
                    throw new InvalidDataException("Source road width or coordinates invalid");
                // Both modes use explicitly provisional UVs. The adaptive
                // surface uses original source t rather than row index so
                // distinct leaf sizes do not distort texture interpolation.
                float v = originalTimesFixed12 == null ? (float)i / (count - 1) :
                    originalTimesFixed12[i] / 4096f;
                uvs[2 * i] = new Vector2(0f, v);
                uvs[2 * i + 1] = new Vector2(1f, v);
            }
            for (int i = 0; i < count - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a],
                    vertices[c] - vertices[a]);
                if (normal.sqrMagnitude < 1e-12f)
                    throw new InvalidDataException("Degenerate archived road preview section");
                int j = i * 6;
                triangles[j] = a;
                triangles[j + 1] = b;
                triangles[j + 2] = c;
                triangles[j + 3] = b;
                triangles[j + 4] = d;
                triangles[j + 5] = c;
            }
            var mesh = new Mesh
            {
                name = originalTimesFixed12 == null
                    ? "RSEG/XRTP SOURCE REVIEW - PROVISIONAL UV / NO COLLISION"
                    : "RSEG/XRTP ADAPTIVE SOURCE REVIEW - PRE-JUNCTION / PROVISIONAL UV / NO COLLISION"
            };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool Finite(Vector3 point) =>
            !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
            !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);

        public static void AppendDiagnosticStrip(Transform root, string stage, string roadId,
            Vector3[] left, Vector3[] right, int divisor, Material material,
            int[] originalTimesFixed12 = null, bool sourceDat28Stepping = false)
        {
            if (root == null || material == null ||
                !Regex.IsMatch(stage ?? "", @"^v82_[a-z0-9_]+$") ||
                !Regex.IsMatch(roadId ?? "", @"^-?\d{1,20}$"))
                throw new InvalidDataException("Original road preview identity invalid");
            if (sourceDat28Stepping && originalTimesFixed12 == null)
                throw new InvalidDataException("Original DAT_28 step claim requires source parameters");
            Mesh source = originalTimesFixed12 == null
                ? BuildDiagnosticStrip(left, right, divisor)
                : BuildAdaptiveDiagnosticStrip(left, right, originalTimesFixed12, divisor);
            if (sourceDat28Stepping)
                source.name = "RSEG/XRTP FUN_5850 DAT_28 SOURCE STEP REVIEW - PROVISIONAL UV / NO COLLISION";
            Folder(MeshFolder);
            string meshPath = MeshFolder + "/" + stage + "-" + roadId + ".asset";
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(source, meshPath);
                asset = source;
            }
            else
            {
                EditorUtility.CopySerialized(source, asset);
                EditorUtility.SetDirty(asset);
                UnityEngine.Object.DestroyImmediate(source);
            }
            var obj = new GameObject(sourceDat28Stepping
                ? "SOURCE BMP ROAD DAT_28 SPACING - PROVISIONAL UV / NO COLLISION"
                : "SOURCE BMP ROAD STRIP - PROVISIONAL UV / NO COLLISION");
            obj.transform.SetParent(root, false);
            obj.AddComponent<MeshFilter>().sharedMesh = asset;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
#endif
