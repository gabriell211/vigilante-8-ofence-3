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
    /// Reads the actual serialized LevelManager.xrtpList order and its original
    /// XRTP prefabs; never guesses a road width from a road name or appearance.
    /// This is source evidence for visual-only inspection, not a mesh importer.
    /// </summary>
    public static class OriginalXrtpSourceProfileReview
    {
        private const string AssetRoot = "LegacySources/Vigilante2Unity/Assets/";
        private const string LevelMeta = AssetRoot + "Scripts/LevelManager.cs.meta";
        private const string XrtpMeta = AssetRoot + "Scripts/DB/XRTP_DB.cs.meta";
        private const string LevelCode = AssetRoot + "Scripts/LevelManager.cs";

        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)(?: stripped)?$");
        private static readonly Regex ScriptGuid = new Regex(@"\bguid: ([a-fA-F0-9]{32})\b");
        private static readonly Regex MetaGuid = new Regex(@"^guid: ([a-fA-F0-9]{32})$", RegexOptions.Multiline);
        private static readonly Regex External = new Regex(
            @"^\s*- \{fileID: (-?\d+), guid: ([a-fA-F0-9]{32}), type: 3\}$");
        private static readonly Regex PrefabMono = new Regex(
            @"^--- !u!114 &(-?\d+)\r?\n(.*?)(?=^--- !u!|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        private static readonly Regex Width = new Regex(@"^  DAT_24: (-?\d+)$", RegexOptions.Multiline);
        private static readonly Regex Step = new Regex(@"^  DAT_28: (-?\d+)$", RegexOptions.Multiline);

        public sealed class SourceProfile
        {
            public int FullWidthFixed;
            public int LongitudinalStepFixed;
            public string SourcePrefabPath;
        }

        private struct ExternalRef
        {
            public string FileId, Guid;
        }

        private static string Absolute(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        private static void VerifyOriginalRoadWidthCode()
        {
            string code = File.ReadAllText(Absolute(LevelCode));
            foreach (string required in new[]
            {
                "xrtpList[dbVar1.DAT_0A].timFarList.Count > 0",
                "FUN_719C(dbVar1);",
                "iVar20 = iVar20 + param2.DAT_28 / iVar21;",
                "iVar23 = ((iVar13 * param2.DAT_24) / 2) / iVar15;",
                "iVar24 = ((iVar17 * param2.DAT_24) / 2) / iVar15;"
            })
                if (!code.Contains(required))
                    throw new InvalidDataException("Original LevelManager road width code changed");
        }

        private static List<ExternalRef> OriginalOrderedRefs(string scene, string managerGuid)
        {
            var found = new List<List<ExternalRef>>();
            string kind = "";
            string guid = null;
            bool list = false;
            List<ExternalRef> refs = null;
            void Finish()
            {
                if (kind == "114" && string.Equals(guid, managerGuid,
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (refs == null)
                        throw new InvalidDataException("Original LevelManager.xrtpList missing");
                    found.Add(refs);
                }
            }

            using (var reader = new StringReader(scene))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match header = Header.Match(line);
                    if (header.Success)
                    {
                        Finish();
                        kind = header.Groups[1].Value;
                        guid = null;
                        refs = null;
                        list = false;
                        continue;
                    }
                    if (kind != "114") continue;
                    if (line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                    {
                        Match m = ScriptGuid.Match(line);
                        guid = m.Success ? m.Groups[1].Value : null;
                    }
                    else if (line == "  xrtpList:")
                    {
                        if (refs != null)
                            throw new InvalidDataException("Duplicate source XRTP list");
                        refs = new List<ExternalRef>();
                        list = true;
                    }
                    else if (list)
                    {
                        if (!line.StartsWith("  - ", StringComparison.Ordinal))
                        {
                            list = false;
                            continue;
                        }
                        Match m = External.Match(line);
                        if (!m.Success)
                            throw new InvalidDataException("Original XRTP external ref malformed");
                        refs.Add(new ExternalRef
                        {
                            FileId = m.Groups[1].Value,
                            Guid = m.Groups[2].Value.ToLowerInvariant()
                        });
                    }
                }
                Finish();
            }
            if (found.Count != 1)
                throw new InvalidDataException("Original LevelManager missing or ambiguous");
            return found[0];
        }

        private static SourceProfile TryReadPrefab(string prefabPath, string fileId,
            string xrtpScriptGuid)
        {
            string yaml = File.ReadAllText(prefabPath);
            if (!yaml.StartsWith("%YAML 1.1\n", StringComparison.Ordinal)) return null;
            foreach (Match m in PrefabMono.Matches(yaml))
            {
                if (m.Groups[1].Value != fileId) continue;
                string body = m.Groups[2].Value;
                Match script = Regex.Match(body, @"^  m_Script: ([^\r\n]+)", RegexOptions.Multiline);
                Match guid = ScriptGuid.Match(script.Value);
                if (!guid.Success || !string.Equals(guid.Groups[1].Value,
                    xrtpScriptGuid, StringComparison.OrdinalIgnoreCase)) return null;
                Match width = Width.Match(body), step = Step.Match(body);
                if (!width.Success || !step.Success ||
                    !int.TryParse(width.Groups[1].Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int full) ||
                    !int.TryParse(step.Groups[1].Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int longitudinal) ||
                    full <= 0 || longitudinal <= 0 ||
                    !Regex.IsMatch(body, @"(?m)^  timFarList:\r?\n  - \{fileID: (?!0\})-?\d+, guid: [0-9a-fA-F]{32}, type: 2\}"))
                    return null;
                return new SourceProfile
                {
                    FullWidthFixed = full,
                    LongitudinalStepFixed = longitudinal,
                    SourcePrefabPath = prefabPath
                };
            }
            return null;
        }

        public static SourceProfile[] LoadVerified(string stageId)
        {
            string scenePath = OriginalJunctionSourceReviewBuilder.VerifiedOriginalScenePath(stageId);
            VerifyOriginalRoadWidthCode();
            string managerGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(LevelMeta);
            string xrtpGuid = OriginalJunctionSourceReviewBuilder.VerifiedScriptGuid(XrtpMeta);
            List<ExternalRef> refs = OriginalOrderedRefs(
                File.ReadAllText(scenePath), managerGuid);
            string stageName = Path.GetFileName(Path.GetDirectoryName(scenePath));
            string levels = Absolute(AssetRoot + "LEVELS");
            string[] stageDirs = Directory.GetDirectories(levels)
                .Where(p => string.Equals(Path.GetFileName(p), stageName,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            if (stageDirs.Length != 1)
                throw new InvalidDataException("Ambiguous or missing archived XRTP stage folder");

            var byGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string meta in Directory.GetFiles(stageDirs[0], "XRTP??.prefab.meta"))
            {
                if (!Regex.IsMatch(Path.GetFileName(meta),
                    @"^XRTP[0-9]{2}\.prefab\.meta$")) continue;
                Match id = MetaGuid.Match(File.ReadAllText(meta));
                if (!id.Success || !byGuid.TryAdd(id.Groups[1].Value,
                    meta.Substring(0, meta.Length - ".meta".Length)))
                    throw new InvalidDataException("Ambiguous or invalid archived XRTP prefab GUID");
            }
            var profiles = new SourceProfile[refs.Count];
            for (int i = 0; i < refs.Count; i++)
            {
                if (refs[i].FileId == "0" || !byGuid.TryGetValue(refs[i].Guid, out string path))
                    continue; // missing authentic profile is NOT a license for a generic width
                profiles[i] = TryReadPrefab(path, refs[i].FileId, xrtpGuid);
            }
            return profiles;
        }
    }
}
#endif
