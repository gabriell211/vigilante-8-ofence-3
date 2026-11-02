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
    /// Generates optional 4x source-derived texture previews from the 790
    /// checksum-verified original V82 BMPs. The source art and its GUIDs are
    /// NEVER modified. Bicubic filtering improves screen-space presentation,
    /// but cannot invent lost original texture detail.
    /// </summary>
    public static class OriginalV82TextureQualityBuilder
    {
        private const string ManifestPath = "docs/legacy-v82-textures.json";
        private const string Destination = "Assets/GameContent/EnhancedSourceTexturesV82";
        private const string Version = "V82_SOURCE_BICUBIC_4X_V1";
        private const int ExpectedCount = 790;
        public const int Scale = 4;
        private const int MaximumSide = 4096;

        private static readonly Regex Entry = new Regex(
            @"""(?<guid>[0-9a-f]{32})""\s*:\s*\{\s*" +
            @"""path""\s*:\s*""(?<path>[^""]+)"",\s*" +
            @"""sourcePath""\s*:\s*""(?<original>[^""]+)"",\s*" +
            @"""sha256""\s*:\s*""(?<sha>[0-9a-f]{64})"",\s*" +
            @"""width""\s*:\s*(?<width>\d+),\s*" +
            @"""height""\s*:\s*(?<height>\d+)\s*\}",
            RegexOptions.Compiled);

        public sealed class SourceTexture
        {
            public string Guid, Path, OriginalPath, Sha256;
            public int Width, Height;
        }

        private static Dictionary<string, SourceTexture> sourceIndex;

        private static string Absolute(string path) =>
            System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath, "..", path));

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] result = sha.ComputeHash(stream);
                var hex = new StringBuilder(result.Length * 2);
                foreach (byte part in result) hex.Append(part.ToString("x2"));
                return hex.ToString();
            }
        }

        public static Dictionary<string, SourceTexture> ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json) ||
                !Regex.IsMatch(json, @"""uniqueTextures""\s*:\s*790\b"))
                throw new InvalidDataException("Original V82 texture manifest absent or changed");

            var byPath = new Dictionary<string, SourceTexture>(StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Entry.Matches(json))
            {
                var source = new SourceTexture
                {
                    Guid = match.Groups["guid"].Value,
                    Path = match.Groups["path"].Value,
                    OriginalPath = match.Groups["original"].Value,
                    Sha256 = match.Groups["sha"].Value,
                    Width = int.Parse(match.Groups["width"].Value),
                    Height = int.Parse(match.Groups["height"].Value)
                };
                if (source.Width < 1 || source.Height < 1 ||
                    source.Width > MaximumSide / Scale ||
                    source.Height > MaximumSide / Scale ||
                    !source.Path.StartsWith("Assets/GameContent/LegacyTextureBank/",
                        StringComparison.Ordinal) ||
                    !source.OriginalPath.StartsWith("LegacySources/Vigilante2Unity/Assets/",
                        StringComparison.Ordinal) ||
                    !source.Path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                    !source.OriginalPath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                    source.Path.Contains("..") || source.OriginalPath.Contains("..") ||
                    !guids.Add(source.Guid) || !byPath.TryAdd(source.Path, source))
                    throw new InvalidDataException("Invalid, duplicated or unbounded original texture");
            }
            if (byPath.Count != ExpectedCount)
                throw new InvalidDataException("Original 790 texture records are incomplete");
            return byPath;
        }

        private static Dictionary<string, SourceTexture> GetIndex()
        {
            if (sourceIndex != null) return sourceIndex;
            sourceIndex = ParseManifest(File.ReadAllText(Absolute(ManifestPath)));
            return sourceIndex;
        }

        private static void VerifySource(SourceTexture source)
        {
            if (!File.Exists(Absolute(source.Path)) ||
                !File.Exists(Absolute(source.OriginalPath)) ||
                !string.Equals(AssetDatabase.AssetPathToGUID(source.Path),
                    source.Guid, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Sha256(Absolute(source.Path)), source.Sha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Sha256(Absolute(source.OriginalPath)), source.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Original V82 source bitmap or GUID differs: " + source.Path);
        }

        public static Color32[] UpscaleBicubic(Color32[] source, int width, int height,
            int scale = Scale)
        {
            if (source == null || width < 1 || height < 1 || scale < 2 || scale > 4 ||
                width > MaximumSide / scale || height > MaximumSide / scale ||
                source.Length != (long)width * height)
                throw new ArgumentException("Invalid original bitmap size or scale");

            int outWidth = checked(width * scale);
            int outHeight = checked(height * scale);
            var output = new Color32[checked(outWidth * outHeight)];

            for (int y = 0; y < outHeight; y++)
            {
                float sy = (y + 0.5f) / scale - 0.5f;
                int iy = Mathf.FloorToInt(sy);
                for (int x = 0; x < outWidth; x++)
                {
                    float sx = (x + 0.5f) / scale - 0.5f;
                    int ix = Mathf.FloorToInt(sx);
                    double r = 0, g = 0, b = 0, a = 0, total = 0;
                    for (int yy = -1; yy <= 2; yy++)
                    {
                        double wy = Weight(sy - (iy + yy));
                        int row = Mathf.Clamp(iy + yy, 0, height - 1) * width;
                        for (int xx = -1; xx <= 2; xx++)
                        {
                            double weight = wy * Weight(sx - (ix + xx));
                            Color32 c = source[row + Mathf.Clamp(ix + xx, 0, width - 1)];
                            double alpha = c.a / 255.0;
                            total += weight;
                            a += alpha * weight;
                            r += c.r * alpha * weight;
                            g += c.g * alpha * weight;
                            b += c.b * alpha * weight;
                        }
                    }
                    // Premultiplied RGB prevents halos at transparent pixels.
                    double opacity = total > 0 ? Math.Max(0, Math.Min(1, a / total)) : 0;
                    output[y * outWidth + x] = opacity < 1e-6
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(Channel(r / a), Channel(g / a),
                            Channel(b / a), Channel(opacity * 255));
                }
            }
            return output;
        }

        private static byte Channel(double value) =>
            (byte)Math.Max(0, Math.Min(255, Math.Round(value)));

        private static double Weight(double distance)
        {
            double x = Math.Abs(distance);
            if (x < 1) return 1.5 * x * x * x - 2.5 * x * x + 1;
            if (x < 2) return -0.5 * x * x * x + 2.5 * x * x - 4 * x + 2;
            return 0;
        }

        private static Color32[] ReadOriginalPixels(Texture2D original)
        {
            RenderTexture backup = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(original.width, original.height,
                0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var readable = new Texture2D(original.width, original.height,
                TextureFormat.RGBA32, false, false);
            try
            {
                Graphics.Blit(original, temporary);
                RenderTexture.active = temporary;
                readable.ReadPixels(new Rect(0, 0, original.width, original.height), 0, 0);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = backup;
                RenderTexture.ReleaseTemporary(temporary);
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static string Target(SourceTexture record) =>
            Destination + "/" + record.Guid + ".png";

        private static bool ProvenanceMatches(SourceTexture source, string target)
        {
            if (!File.Exists(Absolute(target))) return false;
            var importer = AssetImporter.GetAtPath(target) as TextureImporter;
            if (importer == null) return false;
            string match = Version + "|" + source.Guid + "|" + source.Sha256 + "|";
            return importer.userData != null &&
                string.Equals(importer.userData,
                    match + Sha256(Absolute(target)), StringComparison.Ordinal);
        }

        public static Texture2D EnsureEnhanced(string sourcePath)
        {
            if (!GetIndex().TryGetValue(sourcePath, out SourceTexture record))
                throw new InvalidDataException("Texture outside verified 790-BMP catalog: " +
                    sourcePath);
            VerifySource(record);
            string target = Target(record);
            if (!ProvenanceMatches(record, target))
            {
                Texture2D original = AssetDatabase.LoadAssetAtPath<Texture2D>(record.Path);
                if (original == null ||
                    original.width != record.Width || original.height != record.Height)
                    throw new InvalidDataException(
                        "Original BMP not imported with authenticated dimensions");

                Color32[] output = UpscaleBicubic(ReadOriginalPixels(original),
                    record.Width, record.Height);
                var generated = new Texture2D(record.Width * Scale, record.Height * Scale,
                    TextureFormat.RGBA32, false, false);
                try
                {
                    generated.SetPixels32(output);
                    generated.Apply(false, false);
                    if (!AssetDatabase.IsValidFolder(Destination))
                        AssetDatabase.CreateFolder("Assets/GameContent",
                            "EnhancedSourceTexturesV82");
                    File.WriteAllBytes(Absolute(target), generated.EncodeToPNG());
                    AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(target) as TextureImporter;
                    if (importer == null)
                        throw new InvalidOperationException("Enhanced PNG importer missing");
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 8;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = MaximumSide;
                    importer.userData = Version + "|" + record.Guid + "|" +
                        record.Sha256 + "|" + Sha256(Absolute(target));
                    importer.SaveAndReimport();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(generated);
                }
                if (!ProvenanceMatches(record, target))
                    throw new InvalidDataException("Enhanced preview texture provenance mismatch");
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (texture == null || texture.width != record.Width * Scale ||
                texture.height != record.Height * Scale)
                throw new InvalidOperationException("Enhanced source preview invalid: " + target);
            return texture;
        }

        [MenuItem("Vigilante 8/Migração/Melhorar texturas originais V82 (790 BMP / 4x)")]
        public static void BuildCompleteSourceBank()
        {
            var originals = GetIndex().Values.OrderBy(x => x.Guid,
                StringComparer.Ordinal).ToArray();
            try
            {
                for (int i = 0; i < originals.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("V82 source HD previews",
                        originals[i].Path, (float)i / originals.Length);
                    EnsureEnhanced(originals[i].Path);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("790 source-derived 4x preview PNGs are ready. " +
                    "Original archived bitmaps remain unchanged; no new image detail invented.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
