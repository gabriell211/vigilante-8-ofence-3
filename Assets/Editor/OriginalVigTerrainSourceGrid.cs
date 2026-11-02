#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Exact source-grid VigTerrain.FUN_1B750 height. The input is original
    /// fixed-point x/z and the grid is loaded from SHA-verified original scene.
    /// Never approximates terrain with raycasts or Unity-generated geometry.
    /// </summary>
    public sealed class OriginalVigTerrainSourceGrid
    {
        private const int ChunkCount = 1024;
        private const int ZoneVertices = 4096;
        private const int SourceBoundary = 0x7000000;
        private const string Script = "LegacySources/Vigilante2Unity/Assets/Scripts/VigTerrain.cs";
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)(?: stripped)?$");
        private static readonly Regex Guid = new Regex(@"\bguid: ([0-9a-fA-F]{32})\b");
        private readonly ushort[] chunks;
        private readonly ushort[] vertices;
        public int OriginalZoneCount { get; }
        public string OriginalComponentFileId { get; }

        private OriginalVigTerrainSourceGrid(string fileId, int zones, ushort[] c, ushort[] v)
        {
            OriginalComponentFileId = fileId;
            OriginalZoneCount = zones;
            chunks = c;
            vertices = v;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static ushort[] ReadLittleEndianUshorts(string source, int maxCount)
        {
            if (string.IsNullOrEmpty(source) || source.Length % 4 != 0 ||
                source.Length / 4 > maxCount)
                throw new InvalidDataException("Invalid original VigTerrain grid array length");
            var values = new ushort[source.Length / 4];
            for (int i = 0; i < values.Length; i++)
            {
                int at = i * 4;
                int a = HexDigit(source[at]), b = HexDigit(source[at + 1]);
                int c = HexDigit(source[at + 2]), d = HexDigit(source[at + 3]);
                if ((a | b | c | d) < 0)
                    throw new InvalidDataException("Invalid original VigTerrain source hex");
                values[i] = (ushort)((a << 4) | b | (c << 12) | (d << 8));
            }
            return values;
        }

        public static OriginalVigTerrainSourceGrid ParseSource(string yaml, string scriptGuid)
        {
            if (yaml == null || !yaml.StartsWith("%YAML 1.1\n", StringComparison.Ordinal) ||
                !Regex.IsMatch(scriptGuid ?? "", "^[a-fA-F0-9]{32}$"))
                throw new InvalidDataException("Missing verified terrain GUID or source Unity YAML");
            string kind = "", fileId = "", guid = null, rawVertices = null, rawChunks = null;
            int? zones = null;
            OriginalVigTerrainSourceGrid found = null;
            void Finish()
            {
                if (kind != "114" || !string.Equals(guid, scriptGuid,
                    StringComparison.OrdinalIgnoreCase)) return;
                if (found != null || !zones.HasValue || zones.Value < 1 || zones.Value > 256)
                    throw new InvalidDataException("Ambiguous or malformed original VigTerrain zone count");
                ushort[] c = ReadLittleEndianUshorts(rawChunks, ChunkCount);
                ushort[] v = ReadLittleEndianUshorts(rawVertices, zones.Value * ZoneVertices);
                if (c.Length != ChunkCount || v.Length != zones.Value * ZoneVertices ||
                    c.Any(id => id >= zones.Value))
                    throw new InvalidDataException("Original terrain zones or chunk mapping invalid");
                found = new OriginalVigTerrainSourceGrid(fileId, zones.Value, c, v);
            }
            using (var reader = new StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match header = Header.Match(line);
                    if (header.Success)
                    {
                        Finish();
                        kind = header.Groups[1].Value;
                        fileId = header.Groups[2].Value;
                        guid = rawVertices = rawChunks = null;
                        zones = null;
                        continue;
                    }
                    if (kind != "114") continue;
                    if (line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                    {
                        Match m = Guid.Match(line);
                        guid = m.Success ? m.Groups[1].Value : null;
                    }
                    else if (line.StartsWith("  vertices: ", StringComparison.Ordinal))
                        rawVertices = line.Substring("  vertices: ".Length);
                    else if (line.StartsWith("  chunks: ", StringComparison.Ordinal))
                        rawChunks = line.Substring("  chunks: ".Length);
                    else if (line.StartsWith("  zoneCount: ", StringComparison.Ordinal) &&
                        int.TryParse(line.Substring("  zoneCount: ".Length),
                            NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                        zones = count;
                }
                Finish();
            }
            if (found == null)
                throw new InvalidDataException("Original VigTerrain component not found");
            return found;
        }

        private bool TryVertex(int x, int z, out int result)
        {
            result = 0;
            int chunkIndex = (x >> 6) * 32 + (z >> 6);
            if (chunkIndex < 0 || chunkIndex >= chunks.Length) return false;
            int index = chunks[chunkIndex] * ZoneVertices + ((x & 63) << 6) + (z & 63);
            if (index < 0 || index >= vertices.Length) return false;
            result = vertices[index] & 0x7FF;
            return true;
        }

        /// <summary>Original split-triangle fixed-point interpolation, no synthetic heights.</summary>
        public bool TrySampleHeight(int x, int z, out int height)
        {
            height = 0;
            if (x <= 0 || z <= 0 || x >= SourceBoundary || z >= SourceBoundary)
                return true; // VigTerrain.FUN_1B750 returns 0 out-of-bounds.
            int tx = x >> 16, tz = z >> 16;
            int fx = x & 0xFFFF, fz = z & 0xFFFF;
            long raw;
            if (fx + fz < 65536)
            {
                if (!TryVertex(tx, tz, out int a) ||
                    !TryVertex(tx + 1, tz, out int b) ||
                    !TryVertex(tx, tz + 1, out int c)) return false;
                raw = ((long)a << 16) + (long)fx * (b - a) + (long)fz * (c - a);
            }
            else
            {
                if (!TryVertex(tx + 1, tz + 1, out int a) ||
                    !TryVertex(tx, tz + 1, out int b) ||
                    !TryVertex(tx + 1, tz, out int c)) return false;
                raw = ((long)a << 16) + (long)(65536 - fx) * (b - a) +
                    (long)(65536 - fz) * (c - a);
            }
            if (raw < int.MinValue || raw > int.MaxValue) return false;
            if (raw < 0) raw += 31;
            height = (int)(raw >> 5);
            return true;
        }

        private static void VerifyOriginalFormula()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Script));
            string source = File.ReadAllText(path);
            int start = source.IndexOf("public int FUN_1B750(uint x, uint z)", StringComparison.Ordinal);
            int end = source.IndexOf("public Vector3Int FUN_1BB50(", Math.Max(start, 0),
                StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidDataException("Original terrain function not available");
            string method = source.Substring(start, end - start);
            if (!method.Contains("if (iVar1 + uVar3 < 0x10000)") ||
                !method.Contains("vertices[chunkID * 4096 + vertexID / 2] & 0x7ff") ||
                method.Split(new[] { "return vertexH >> 5;" }, StringSplitOptions.None).Length != 3)
                throw new InvalidDataException("Original fixed-point interpolation changed");
        }

        public static OriginalVigTerrainSourceGrid LoadVerified(string stageId)
        {
            string sourceScene = OriginalJunctionSourceReviewBuilder.VerifiedOriginalScenePath(stageId);
            string scriptGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(Script + ".meta");
            VerifyOriginalFormula();
            return ParseSource(File.ReadAllText(sourceScene), scriptGuid);
        }
    }
}
#endif
