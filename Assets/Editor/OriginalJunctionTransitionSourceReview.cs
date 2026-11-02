#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Port of FUN_719C's junction trim SELECTION, not FUN_6604's mutation.
    /// The archived FUN_6604 translation indexes local_188[30] despite
    /// declaring only 27 elements; copying it into production is unsafe.
    /// Source-only diagnostic information: NO road deformation or collision.
    /// </summary>
    public static class OriginalJunctionTransitionSourceReview
    {
        private const string Source = "LegacySources/Vigilante2Unity/Assets/Scripts/LevelManager.cs";
        private const string JunctionMeta =
            "LegacySources/Vigilante2Unity/Assets/Scripts/DB/JUNC_DB.cs.meta";
        private const string RoadMeta =
            "LegacySources/Vigilante2Unity/Assets/Scripts/DB/RSEG_DB.cs.meta";

        private static readonly Regex Mono = new Regex(
            @"(?ms)^--- !u!114 &(-?\d+)\r?\n(.*?)(?=^--- !u!|\z)");
        private static readonly Regex Script = new Regex(
            @"(?m)^  m_Script: \{fileID: 11500000, guid: ([0-9a-fA-F]{32}), type: 3\}");

        public enum JunctionStatus
        {
            NoSourceTrim,
            NeedsUnportedFun6604,
            XobfConfigurationNotYetPorted,
            UnverifiedSource
        }

        public sealed class ConnectedRoad
        {
            public string FileId;
            public int SourceOrder, WidthFixed;
        }

        public sealed class Endpoint
        {
            public string JunctionFileId;
            public JunctionStatus Status;
            public int SourceRadiusFixed, EarlierRoads;
        }

        private sealed class Junc
        {
            public string Id, MeshRef;
            public int? Flags, Count;
            public List<string> Attached;
        }

        private sealed class Road
        {
            public string Id;
            public int? Order, Profile, Flags;
            public List<string> Endpoints;
        }

        private static string Absolute(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        private static string LineValue(string body, string key)
        {
            Match m = Regex.Match(body, @"(?m)^  " + Regex.Escape(key) +
                @": ([^\r\n]+)$");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static int? Number(string value) =>
            int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int n) ? n : (int?)null;

        private static string Ref(string value)
        {
            Match match = Regex.Match(value ?? "", @"\bfileID: (-?\d+)\b");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static List<string> References(string body, string key)
        {
            var result = new List<string>();
            bool inside = false, found = false;
            using (var reader = new StringReader(body))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == "  " + key + ":")
                    {
                        if (found) return null;
                        inside = found = true;
                        continue;
                    }
                    if (!inside) continue;
                    if (!line.StartsWith("  - ", StringComparison.Ordinal)) break;
                    string id = Ref(line);
                    if (id == null) return null;
                    result.Add(id);
                }
            }
            return found || body.Contains("  " + key + ": []") ? result : null;
        }

        public static Endpoint Evaluate(string juncId, int flags, bool hasXobfMesh,
            int declaredConnections, int currentOrder, int currentFlags, int endpoint,
            string currentRoadId, IReadOnlyList<ConnectedRoad> connected)
        {
            Endpoint Result(JunctionStatus status, int radius = 0, int earlier = 0) =>
                new Endpoint
                {
                    JunctionFileId = juncId, Status = status,
                    SourceRadiusFixed = radius, EarlierRoads = earlier
                };

            if (string.IsNullOrEmpty(juncId) || string.IsNullOrEmpty(currentRoadId) ||
                endpoint < 0 || endpoint > 1 || flags < 0 || flags > 255 ||
                declaredConnections < 0 || declaredConnections > 255 ||
                currentOrder < short.MinValue || currentOrder > ushort.MaxValue ||
                connected == null || declaredConnections != connected.Count ||
                connected.Count(r => r != null && r.FileId == currentRoadId) != 1 ||
                connected.Any(r => r == null || string.IsNullOrEmpty(r.FileId) ||
                    r.SourceOrder < short.MinValue ||
                    r.SourceOrder > ushort.MaxValue || r.WidthFixed <= 0))
                return Result(JunctionStatus.UnverifiedSource);

            if (hasXobfMesh)
                return Result(JunctionStatus.XobfConfigurationNotYetPorted);
            if ((flags & 1) != 0 || (currentFlags & (2 << endpoint)) != 0)
                return Result(JunctionStatus.NoSourceTrim);

            int radius = 0, previous = 0;
            foreach (ConnectedRoad road in connected)
            {
                // FUN_719C casts both DAT_08 values to signed short.
                if (unchecked((short)road.SourceOrder) >=
                    unchecked((short)currentOrder)) continue;
                previous++;
                radius = Math.Max(radius, road.WidthFixed / 2);
            }
            return radius > 0
                ? Result(JunctionStatus.NeedsUnportedFun6604, radius, previous)
                : Result(JunctionStatus.NoSourceTrim, 0, previous);
        }

        public static void VerifyArchivedCode()
        {
            string code = File.ReadAllText(Absolute(Source));
            foreach (string statement in new[]
            {
                "void FUN_719C(RSEG_DB param1)",
                "if (piVar7.DAT_18 == null)",
                "if ((piVar7.DAT_10 & 1) == 0)",
                "(param1.DAT_0C & 2 << (i & 31)) == 0",
                "(short)piVar7.DAT_1C[iVar5].DAT_08 < (short)param1.DAT_08",
                "xrtpList[piVar7.DAT_1C[iVar5].DAT_0A].DAT_24 / 2",
                "if (iVar12 < iVar13)",
                "FUN_6604(local_b0, i, iVar12);",
                "uint[] local_188 = new uint[27];",
                "local_188[30] = local_188[5];"
            })
                if (!code.Contains(statement))
                    throw new InvalidDataException(
                        "Original source FUN_719C/FUN_6604 changed: " + statement);
        }

        /// <summary>
        /// SHA-verified scene and genuine JUNC/RSEG component GUIDs only.
        /// Returns two decisions per original road (one per junction endpoint).
        /// Unknown references always become UnverifiedSource, never guessed.
        /// </summary>
        public static Dictionary<string, Endpoint[]> ReviewVerified(
            string stageId, OriginalXrtpSourceProfileReview.SourceProfile[] profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            string scene = File.ReadAllText(
                OriginalJunctionSourceReviewBuilder.VerifiedOriginalScenePath(stageId));
            VerifyArchivedCode();
            string juncGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(JunctionMeta);
            string roadGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(RoadMeta);
            var junctions = new Dictionary<string, Junc>(StringComparer.Ordinal);
            var roads = new Dictionary<string, Road>(StringComparer.Ordinal);
            foreach (Match match in Mono.Matches(scene))
            {
                string id = match.Groups[1].Value, body = match.Groups[2].Value;
                Match script = Script.Match(body);
                if (!script.Success) continue;
                string guid = script.Groups[1].Value;
                if (string.Equals(guid, juncGuid, StringComparison.OrdinalIgnoreCase))
                {
                    if (!junctions.TryAdd(id, new Junc
                    {
                        Id = id,
                        Flags = Number(LineValue(body, "DAT_10")),
                        Count = Number(LineValue(body, "DAT_11")),
                        MeshRef = Ref(LineValue(body, "DAT_18")),
                        Attached = References(body, "DAT_1C")
                    }))
                        throw new InvalidDataException("Duplicate original JUNC fileID");
                }
                else if (string.Equals(guid, roadGuid, StringComparison.OrdinalIgnoreCase))
                {
                    if (!roads.TryAdd(id, new Road
                    {
                        Id = id,
                        Order = Number(LineValue(body, "DAT_08")),
                        Profile = Number(LineValue(body, "DAT_0A")),
                        Flags = Number(LineValue(body, "DAT_0C")),
                        Endpoints = References(body, "DAT_00")
                    }))
                        throw new InvalidDataException("Duplicate original RSEG fileID");
                }
            }
            var output = new Dictionary<string, Endpoint[]>(StringComparer.Ordinal);
            foreach (var pair in roads)
            {
                var current = pair.Value;
                var ends = new Endpoint[2];
                for (int endpoint = 0; endpoint < 2; endpoint++)
                {
                    string id = current.Endpoints != null &&
                        current.Endpoints.Count == 2 ? current.Endpoints[endpoint] : null;
                    if (id == null || !junctions.TryGetValue(id, out Junc junction) ||
                        !junction.Flags.HasValue || !junction.Count.HasValue ||
                        junction.MeshRef == null || !current.Order.HasValue ||
                        !current.Flags.HasValue || junction.Attached == null)
                    {
                        ends[endpoint] = new Endpoint
                        {
                            JunctionFileId = id, Status = JunctionStatus.UnverifiedSource
                        };
                        continue;
                    }
                    var connected = new List<ConnectedRoad>();
                    bool valid = true;
                    foreach (string attached in junction.Attached)
                    {
                        if (!roads.TryGetValue(attached, out Road adjacent) ||
                            !adjacent.Order.HasValue || !adjacent.Profile.HasValue ||
                            adjacent.Profile.Value < 0 ||
                            adjacent.Profile.Value >= profiles.Length ||
                            profiles[adjacent.Profile.Value] == null)
                        {
                            valid = false;
                            break;
                        }
                        connected.Add(new ConnectedRoad
                        {
                            FileId = attached,
                            SourceOrder = adjacent.Order.Value,
                            WidthFixed = profiles[adjacent.Profile.Value].FullWidthFixed
                        });
                    }
                    ends[endpoint] = valid
                        ? Evaluate(id, junction.Flags.Value, junction.MeshRef != "0",
                            junction.Count.Value, current.Order.Value,
                            current.Flags.Value, endpoint, current.Id, connected)
                        : new Endpoint
                        {
                            JunctionFileId = id, Status = JunctionStatus.UnverifiedSource
                        };
                }
                output[pair.Key] = ends;
            }
            return output;
        }
    }
}
#endif
