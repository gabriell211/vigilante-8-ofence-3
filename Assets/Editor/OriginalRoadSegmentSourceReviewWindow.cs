#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Source-verified 2D X/Z plan of RSEG_DB.FUN_285E4 segments.
    /// NEVER generates a road mesh, world height, collision or spawn.
    /// </summary>
    public sealed class OriginalRoadSegmentSourceReviewWindow : EditorWindow
    {
        private const string RsegSource = "LegacySources/Vigilante2Unity/Assets/Scripts/DB/RSEG_DB.cs";
        private const string RsegMeta = RsegSource + ".meta";
        private const string JuncMeta = "LegacySources/Vigilante2Unity/Assets/Scripts/DB/JUNC_DB.cs.meta";
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)(?: stripped)?$");
        private static readonly Regex Guid = new Regex(@"\bguid: ([a-fA-F0-9]{32})\b");
        private static readonly Regex RefId = new Regex(@"\bfileID: (-?\d+)\b");
        private static readonly Regex RawVector = new Regex(
            @"^  DAT_00: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}$");
        private static readonly string[] Coefficients =
        {
            "DAT_20", "DAT_24", "DAT_28", "DAT_2C", "DAT_30", "DAT_34"
        };
        private static readonly int[] SampleTimes = { 0, 1024, 2048, 3072, 4096 };
        // 33 exact fixed-point evaluations of RSEG_DB.FUN_285E4, not an
        // interpolated spline between the five coarse map samples.
        public const int DenseCurveSampleCount = 33;
        public const int DenseCurveStepFixed12 = 128;

        private sealed class RawMono
        {
            public string Id, Script;
            public Vector3Int? FixedPosition;
            public readonly List<string> Endpoints = new List<string>();
            public bool EndpointsPresent, TangentX, TangentZ;
            public int[] SourceTangentX, SourceTangentZ;
            public int? Flags, ProfileIndex;
            public readonly Dictionary<string, int> Coeffs =
                new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public sealed class SourceRoad
        {
            public string RsegFileId, FirstJuncFileId, SecondJuncFileId;
            public Vector2[] OriginalFixedXZ;
            public Vector2Int[] OriginalFixedXZExact;
            // Source-based 33-point geometry inspection, independently checked
            // for signed 32-bit overflow. Null if any source evaluation fails.
            public Vector2Int[] OriginalFixedXZExactDense;
            public Vector2Int[] OriginalFixedTangentXZDense;
            public Vector3[] OriginalFixedXYZDense;
            public Vector3[] OriginalLeftEdgeFixedXYZDense;
            public Vector3[] OriginalRightEdgeFixedXYZDense;
            // Original FUN_630C split of the unadjusted FUN_719C controls.
            // This deliberately excludes junction transitions and final meshes.
            public OriginalRoadAdaptiveSourceSubdivision.Leaf[] OriginalUnadjustedAdaptiveLeaves;
            public Vector3Int OriginalFirstJunctionXYZ, OriginalLastJunctionXYZ;
            public int[] OriginalPolynomialX, OriginalPolynomialZ;
            public Vector3[] OriginalAdaptiveLeftEdgeFixedXYZ;
            public Vector3[] OriginalAdaptiveRightEdgeFixedXYZ;
            public int[] OriginalAdaptiveTimesFixed12;
            public int? SourceXrtpProfileIndex;
            public int SourceRoadFlags;
            // Present only for RSEG flags which directly specify height.
            // Null when VigTerrain sampling is required or fixed-point math is unsafe.
            public Vector3[] OriginalFixedXYZ;
            public bool NeedsVigTerrainHeight;
        }

        public sealed class SourcePlan
        {
            public SourceRoad[] Roads;
            public int TotalSegments, RejectedSegments, TerrainDependentSegments;
            public int HeightKnownSegments;
        }

        private string[] ids = Array.Empty<string>();
        private string[] labels = Array.Empty<string>();
        private int selectedStage;
        private string selectedRoad;
        private SourcePlan current;
        private float zoom = 1f;

        [MenuItem("Vigilante 8/Migração/Visualizar estradas RSEG originais em planta XZ")]
        public static void Open() => GetWindow<OriginalRoadSegmentSourceReviewWindow>("RSEG / XZ ORIGINAL");

        private void OnEnable()
        {
            var stages = CatalogRepository.Load().arenas
                .Where(a => a.game == "v82").OrderBy(a => a.id, StringComparer.Ordinal).ToArray();
            ids = stages.Select(a => a.id).ToArray();
            labels = stages.Select(a => a.name).ToArray();
        }

        private static string Captured(Regex expression, string line)
        {
            Match m = expression.Match(line);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static bool ParseInt(string value, out int result) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        private static bool IsInt32(long value) => value >= int.MinValue && value <= int.MaxValue;

        /// <summary>
        /// Exact original X/Z polynomial; reject intermediate signed overflow
        /// rather than misrepresent unverified PS1 overflow semantics.
        /// </summary>
        public static bool TrySampleAxis(int origin, int t, int a, int b, int c, out int position)
        {
            position = 0;
            if (t < 0 || t > 4096) return false;
            long one = (long)a * t;
            if (!IsInt32(one)) return false;
            long two = b + (one >> 12);
            if (!IsInt32(two)) return false;
            long three = two * t;
            if (!IsInt32(three)) return false;
            long four = c + (three >> 12);
            if (!IsInt32(four)) return false;
            long five = four * t;
            if (!IsInt32(five)) return false;
            long result = origin + (five >> 8);
            if (!IsInt32(result)) return false;
            position = (int)result;
            return true;
        }

        /// <summary>
        /// Exact source RSEG_DB.FUN_285E4 tangent branch (signed fixed point).
        /// Rejects source C# int overflow instead of silently wrapping.
        /// </summary>
        public static bool TrySampleSourceTangent(int t, int a, int b, int c,
            out int derivative)
        {
            derivative = 0;
            if (t < 0 || t > 4096) return false;
            long first = (long)a * t;
            if (!IsInt32(first)) return false;
            long iVar3 = first >> 12;
            long iVar4 = b + iVar3;
            if (!IsInt32(iVar4)) return false;
            long sum = iVar3 + iVar4 * 2;
            if (!IsInt32(sum)) return false;
            long product = sum * t;
            if (!IsInt32(product)) return false;
            long value = (product >> 12) + c;
            if (!IsInt32(value)) return false;
            if (value < 0) value += 255;
            if (!IsInt32(value)) return false;
            derivative = (int)(value >> 8);
            return true;
        }

        /// <summary>
        /// RSEG_DB.FUN_285E4 original height branches: bit 8 keeps first
        /// junction's Y; bit 1 interpolates fixed Y. Otherwise VigTerrain is
        /// mandatory and we refuse a synthetic height.
        /// </summary>
        public static bool TrySampleKnownHeight(Vector3Int start, Vector3Int finish,
            int flags, int t, out int y)
        {
            y = 0;
            if (t < 0 || t > 4096) return false;
            if ((flags & 8) != 0)
            {
                y = start.y;
                return true;
            }
            if ((flags & 1) == 0) return false;
            long difference = (long)finish.y - start.y;
            if (!IsInt32(difference)) return false;
            long scaled = difference * t;
            if (!IsInt32(scaled)) return false;
            long height = start.y + (scaled >> 12);
            if (!IsInt32(height)) return false;
            y = (int)height;
            return true;
        }

        /// <summary>
        /// Only original MonoBehaviour GUIDs and scene-local JUNC fileIDs may
        /// become roads; no fallback positions or heuristic endpoints.
        /// </summary>
        public static SourcePlan ParseSource(string yaml, string rsegGuid, string juncGuid)
        {
            if (yaml == null || !yaml.StartsWith("%YAML 1.1\n", StringComparison.Ordinal) ||
                !Regex.IsMatch(rsegGuid ?? "", "^[a-fA-F0-9]{32}$") ||
                !Regex.IsMatch(juncGuid ?? "", "^[a-fA-F0-9]{32}$") ||
                string.Equals(rsegGuid, juncGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Distinct original JUNC/RSEG GUIDs and Unity YAML required");
            var junctions = new Dictionary<string, Vector3Int>(StringComparer.Ordinal);
            var segments = new List<RawMono>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            string kind = "";
            RawMono currentMono = null;
            bool readingEndpoints = false;

            void Finish()
            {
                if (kind != "114" || currentMono == null || currentMono.Script == null) return;
                if (currentMono.Script.Equals(juncGuid, StringComparison.OrdinalIgnoreCase) &&
                    currentMono.FixedPosition.HasValue)
                {
                    if (junctions.ContainsKey(currentMono.Id))
                        throw new InvalidDataException("Duplicate JUNC source component");
                    junctions.Add(currentMono.Id, currentMono.FixedPosition.Value);
                }
                else if (currentMono.Script.Equals(rsegGuid, StringComparison.OrdinalIgnoreCase))
                    segments.Add(currentMono);
            }

            using (var reader = new StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match h = Header.Match(line);
                    if (h.Success)
                    {
                        Finish();
                        kind = h.Groups[1].Value;
                        currentMono = kind == "114" ? new RawMono { Id = h.Groups[2].Value } : null;
                        if (currentMono != null && !unique.Add(currentMono.Id))
                            throw new InvalidDataException("Duplicate MonoBehaviour fileID");
                        readingEndpoints = false;
                        continue;
                    }
                    if (currentMono == null) continue;
                    if (line == "  DAT_00:")
                    {
                        currentMono.EndpointsPresent = true;
                        readingEndpoints = true;
                        continue;
                    }
                    if (readingEndpoints)
                    {
                        if (line.StartsWith("  - ", StringComparison.Ordinal))
                        {
                            currentMono.Endpoints.Add(Captured(RefId, line));
                            continue;
                        }
                        readingEndpoints = false;
                    }
                    if (line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                        currentMono.Script = Captured(Guid, line)?.ToLowerInvariant();
                    else if (line.StartsWith("  DAT_00: ", StringComparison.Ordinal))
                    {
                        Match m = RawVector.Match(line);
                        if (m.Success && ParseInt(m.Groups[1].Value, out int x) &&
                            ParseInt(m.Groups[2].Value, out int y) &&
                            ParseInt(m.Groups[3].Value, out int z))
                            currentMono.FixedPosition = new Vector3Int(x, y, z);
                    }
                    else if (line.StartsWith("  DAT_0A: ", StringComparison.Ordinal))
                    {
                        if (ParseInt(line.Substring("  DAT_0A: ".Length).Trim(), out int profile) &&
                            profile >= 0 && profile <= ushort.MaxValue)
                            currentMono.ProfileIndex = profile;
                    }
                    else if (line.StartsWith("  DAT_0C: ", StringComparison.Ordinal))
                    {
                        if (ParseInt(line.Substring("  DAT_0C: ".Length).Trim(), out int flags))
                            currentMono.Flags = flags;
                    }
                    else if (line.StartsWith("  DAT_10: ", StringComparison.Ordinal))
                        currentMono.TangentX =
                            OriginalRoadAdaptiveSourceSubdivision.TryReadOriginalTangentPair(
                                line.Substring("  DAT_10: ".Length).Trim(),
                                out currentMono.SourceTangentX);
                    else if (line.StartsWith("  DAT_14: ", StringComparison.Ordinal))
                        currentMono.TangentZ =
                            OriginalRoadAdaptiveSourceSubdivision.TryReadOriginalTangentPair(
                                line.Substring("  DAT_14: ".Length).Trim(),
                                out currentMono.SourceTangentZ);
                    else
                    {
                        foreach (string key in Coefficients)
                        {
                            string start = "  " + key + ": ";
                            if (line.StartsWith(start, StringComparison.Ordinal) &&
                                ParseInt(line.Substring(start.Length).Trim(), out int coefficient))
                                currentMono.Coeffs[key] = coefficient;
                        }
                    }
                }
                Finish();
            }

            var accepted = new List<SourceRoad>();
            foreach (RawMono segment in segments)
            {
                if (!segment.EndpointsPresent || segment.Endpoints.Count != 2 ||
                    segment.Endpoints[0] == null || segment.Endpoints[1] == null ||
                    segment.Endpoints[0] == "0" || segment.Endpoints[1] == "0" ||
                    segment.Endpoints[0] == segment.Endpoints[1] ||
                    !junctions.TryGetValue(segment.Endpoints[0], out Vector3Int start) ||
                    !junctions.TryGetValue(segment.Endpoints[1], out Vector3Int finish) ||
                    !segment.TangentX || !segment.TangentZ || !segment.Flags.HasValue ||
                    Coefficients.Any(key => !segment.Coeffs.ContainsKey(key)))
                    continue;

                var points = new List<Vector2>();
                var exactPoints = new List<Vector2Int>();
                var knownHeights = new List<Vector3>();
                bool allHeightsKnown = true;
                foreach (int t in SampleTimes)
                {
                    if (!TrySampleAxis(start.x, t, segment.Coeffs["DAT_20"],
                            segment.Coeffs["DAT_28"], segment.Coeffs["DAT_30"], out int x) ||
                        !TrySampleAxis(start.z, t, segment.Coeffs["DAT_24"],
                            segment.Coeffs["DAT_2C"], segment.Coeffs["DAT_34"], out int z))
                    {
                        points.Clear();
                        break;
                    }
                    points.Add(new Vector2(x, z));
                    exactPoints.Add(new Vector2Int(x, z));
                    if (TrySampleKnownHeight(start, finish, segment.Flags.Value, t, out int y))
                        knownHeights.Add(new Vector3(x, y, z));
                    else
                        allHeightsKnown = false;
                }
                if (points.Count != SampleTimes.Length) continue;

                // Do not invent curves by linearly interpolating the 5 coarse
                // points: re-evaluate the exact original integer polynomial.
                var denseXZ = new List<Vector2Int>(DenseCurveSampleCount);
                var denseXYZ = new List<Vector3>(DenseCurveSampleCount);
                var denseTangent = new List<Vector2Int>(DenseCurveSampleCount);
                bool denseYKnown = true;
                bool allTangentsSafe = true;
                for (int t = 0; t <= 4096; t += DenseCurveStepFixed12)
                {
                    if (!TrySampleAxis(start.x, t, segment.Coeffs["DAT_20"],
                            segment.Coeffs["DAT_28"], segment.Coeffs["DAT_30"], out int x) ||
                        !TrySampleAxis(start.z, t, segment.Coeffs["DAT_24"],
                            segment.Coeffs["DAT_2C"], segment.Coeffs["DAT_34"], out int z))
                    {
                        denseXZ.Clear();
                        denseXYZ.Clear();
                        break;
                    }
                    denseXZ.Add(new Vector2Int(x, z));
                    if (TrySampleSourceTangent(t, segment.Coeffs["DAT_20"],
                            segment.Coeffs["DAT_28"], segment.Coeffs["DAT_30"], out int dx) &&
                        TrySampleSourceTangent(t, segment.Coeffs["DAT_24"],
                            segment.Coeffs["DAT_2C"], segment.Coeffs["DAT_34"], out int dz))
                        denseTangent.Add(new Vector2Int(dx, dz));
                    else
                        allTangentsSafe = false;
                    if (TrySampleKnownHeight(start, finish, segment.Flags.Value, t, out int y))
                        denseXYZ.Add(new Vector3(x, y, z));
                    else
                        denseYKnown = false;
                }
                bool denseComplete = denseXZ.Count == DenseCurveSampleCount;
                OriginalRoadAdaptiveSourceSubdivision.Leaf[] originalLeaves = null;
                if (OriginalRoadAdaptiveSourceSubdivision.TryCreateOriginalUnadjustedControls(
                        start, finish, segment.SourceTangentX, segment.SourceTangentZ,
                        out Vector3Int[] unadjustedControls))
                    OriginalRoadAdaptiveSourceSubdivision.TrySplitOriginalUnadjustedCurve(
                        unadjustedControls, out originalLeaves);
                accepted.Add(new SourceRoad
                {
                    RsegFileId = segment.Id,
                    FirstJuncFileId = segment.Endpoints[0],
                    SecondJuncFileId = segment.Endpoints[1],
                    OriginalFixedXZ = points.ToArray(),
                    OriginalFixedXZExact = exactPoints.ToArray(),
                    OriginalFixedXZExactDense = denseComplete ? denseXZ.ToArray() : null,
                    OriginalUnadjustedAdaptiveLeaves = originalLeaves,
                    OriginalFirstJunctionXYZ = start,
                    OriginalLastJunctionXYZ = finish,
                    OriginalPolynomialX = new[]
                    {
                        segment.Coeffs["DAT_20"], segment.Coeffs["DAT_28"],
                        segment.Coeffs["DAT_30"]
                    },
                    OriginalPolynomialZ = new[]
                    {
                        segment.Coeffs["DAT_24"], segment.Coeffs["DAT_2C"],
                        segment.Coeffs["DAT_34"]
                    },
                    OriginalFixedTangentXZDense = denseComplete && allTangentsSafe &&
                        denseTangent.Count == DenseCurveSampleCount ? denseTangent.ToArray() : null,
                    SourceXrtpProfileIndex = segment.ProfileIndex,
                    SourceRoadFlags = segment.Flags.Value,
                    OriginalFixedXYZDense = denseComplete && denseYKnown &&
                        denseXYZ.Count == DenseCurveSampleCount ? denseXYZ.ToArray() : null,
                    OriginalFixedXYZ = allHeightsKnown && knownHeights.Count == SampleTimes.Length
                        ? knownHeights.ToArray() : null,
                    NeedsVigTerrainHeight = (segment.Flags.Value & 9) == 0
                });
            }
            return new SourcePlan
            {
                Roads = accepted.ToArray(),
                TotalSegments = segments.Count,
                RejectedSegments = segments.Count - accepted.Count,
                TerrainDependentSegments = accepted.Count(r => r.NeedsVigTerrainHeight),
                HeightKnownSegments = accepted.Count(r => r.OriginalFixedXYZ != null)
            };
        }

        private static void VerifyOriginalCurveCode()
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RsegSource));
            string code = File.ReadAllText(file);
            foreach (string statement in new[]
            {
                "public JUNC_DB[] DAT_00;", "public int DAT_20;", "public int DAT_24;",
                "public int DAT_28;", "public int DAT_2C;", "public int DAT_30;",
                "public int DAT_34;", "public void FUN_285E4(",
                "iVar3 = DAT_20 * param1 >> 12;",
                "iVar3 = DAT_24 * param1 >> 12;",
                "param2.y = iVar3;"
            })
                if (!code.Contains(statement))
                    throw new InvalidDataException("Archived source RSEG formula changed: " + statement);
        }

        public static SourcePlan LoadVerified(string stageId)
        {
            // Shared canonical source proof: original scene + XOBF SHA Git,
            // eight official catalog IDs, and archived script .meta GUIDs.
            string yamlFile = OriginalJunctionSourceReviewBuilder.VerifiedOriginalScenePath(stageId);
            VerifyOriginalCurveCode();
            OriginalRoadAdaptiveSourceSubdivision.VerifyArchivedSource();
            string rsegGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(RsegMeta);
            string juncGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(JuncMeta);
            return ParseSource(File.ReadAllText(yamlFile), rsegGuid, juncGuid);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("RSEG_DB / JUNC_DB — PLANTA ORIGINAL X/Z",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Traçado X/Z do código original. Não representa altura Y, " +
                "asfalto, largura da estrada, terreno, colisões ou spawns oficiais.",
                MessageType.Warning);
            if (ids.Length != 8)
            {
                EditorGUILayout.HelpBox("O catálogo das oito fases V82 está incompleto.",
                    MessageType.Error);
                return;
            }
            EditorGUI.BeginChangeCheck();
            selectedStage = EditorGUILayout.Popup("Fase", selectedStage, labels);
            if (EditorGUI.EndChangeCheck()) { current = null; selectedRoad = null; }
            zoom = EditorGUILayout.Slider("Zoom", zoom, 0.5f, 4f);
            if (GUILayout.Button("Carregar curvas RSEG originais verificadas"))
            {
                try { current = LoadVerified(ids[selectedStage]); selectedRoad = null; }
                catch (Exception error)
                {
                    current = null;
                    Debug.LogException(error);
                    EditorUtility.DisplayDialog("Fonte original inválida", error.Message, "OK");
                }
            }
            if (current == null) return;
            EditorGUILayout.LabelField(
                "RSEG: " + current.TotalSegments + " | resolvidos: " + current.Roads.Length +
                " | não resolvidos: " + current.RejectedSegments +
                " | altura via VigTerrain: " + current.TerrainDependentSegments +
                " | Y de origem conhecido: " + current.HeightKnownSegments);
            if (selectedRoad != null)
            {
                var segment = current.Roads.FirstOrDefault(r => r.RsegFileId == selectedRoad);
                EditorGUILayout.LabelField("fileID do trecho selecionado: " + selectedRoad);
                int count = segment?.OriginalUnadjustedAdaptiveLeaves?.Length ?? 0;
                EditorGUILayout.LabelField(count > 0
                    ? "FUN_630C inicial (sem ajustes JUNC): " + count +
                      " segmentos adaptativos de origem"
                    : "FUN_630C inicial: sem reconstrução segura para este trecho");
                EditorGUILayout.HelpBox(
                    "Subdivisão pré-junção, somente auditoria dos controles Bézier. " +
                    "Não substitui a malha UV e triangulação do código original.",
                    MessageType.Info);
            }
            Rect rect = GUILayoutUtility.GetRect(100f, Mathf.Max(320f, position.height - 210f),
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.08f, 0.11f));
            DrawPlan(rect);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float len = d.sqrMagnitude;
            float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, d) / len) : 0f;
            return Vector2.Distance(point, a + d * t);
        }

        private void DrawPlan(Rect area)
        {
            if (current.Roads.Length == 0)
            {
                GUI.Label(area, "Nenhum segmento da origem passou pela validação.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (SourceRoad road in current.Roads)
            foreach (Vector2 p in road.OriginalFixedXZ)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
            double centerX = ((double)minX + maxX) / 2d;
            double centerZ = ((double)minZ + maxZ) / 2d;
            float spanX = Mathf.Max(1f, maxX - minX);
            float spanZ = Mathf.Max(1f, maxZ - minZ);
            float scale = zoom * Mathf.Min((area.width - 32f) / spanX,
                                            (area.height - 32f) / spanZ);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return;
            Vector2 ToScreen(Vector2 source) => new Vector2(
                area.center.x + (float)((source.x - centerX) * scale),
                area.center.y - (float)((source.y - centerZ) * scale));
            float nearest = 8f;
            string nearestId = null;
            Handles.BeginGUI();
            foreach (SourceRoad road in current.Roads)
            {
                Vector3[] points = road.OriginalFixedXZ
                    .Select(v => (Vector3)ToScreen(v)).ToArray();
                Handles.color = road.RsegFileId == selectedRoad ? Color.white :
                    road.NeedsVigTerrainHeight
                    ? new Color(0.98f, 0.68f, 0.27f, 0.95f)
                    : new Color(0.29f, 0.82f, 0.94f, 0.95f);
                Handles.DrawAAPolyLine(road.RsegFileId == selectedRoad ? 4f : 2f, points);
                // For the selected road ONLY, draw the source unadjusted leaf
                // control polygons, not the final post-JUNC original geometry.
                if (road.RsegFileId == selectedRoad &&
                    road.OriginalUnadjustedAdaptiveLeaves != null &&
                    road.OriginalUnadjustedAdaptiveLeaves.Length <= 256)
                {
                    Handles.color = new Color(0.57f, 0.95f, 0.44f, 0.75f);
                    foreach (var leaf in road.OriginalUnadjustedAdaptiveLeaves)
                    {
                        Vector3[] originalControls = leaf.SourceControlPoints.Select(p =>
                            (Vector3)ToScreen(new Vector2(p.x, p.z))).ToArray();
                        Handles.DrawAAPolyLine(1f, originalControls);
                    }
                }
                if (Event.current.type == EventType.MouseDown &&
                    area.Contains(Event.current.mousePosition))
                {
                    for (int i = 1; i < points.Length; i++)
                    {
                        float distance = DistanceToSegment(Event.current.mousePosition,
                            points[i - 1], points[i]);
                        if (distance < nearest) { nearest = distance; nearestId = road.RsegFileId; }
                    }
                }
            }
            Handles.EndGUI();
            if (Event.current.type == EventType.MouseDown && nearestId != null)
            {
                selectedRoad = nearestId;
                Event.current.Use();
                Repaint();
            }
        }
    }
}
#endif
