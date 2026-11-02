#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Visualizes ORIGINAL JUNC_DB scene source references using archived TMD meshes.
    /// Only root JUNC objects with exact same-GameObject VigMesh references are
    /// shown. The meshes are NOT terrain-deformed as in the original executable.
    /// No physics, invented meshes, official spawn or release content is created.
    /// </summary>
    public sealed class OriginalJunctionSourceReviewBuilder : EditorWindow
    {
        public const string ScenePath = "Assets/Scenes/OriginalJunctionSourceReview.unity";
        private const string ManifestPath = "Assets/GameContent/ExtractedStageObjectsV82/manifest.json";
        private const string MeshFolder = "Assets/GameContent/PreviewStageMeshesV82/";
        private const string MaterialPath = "Assets/GameContent/PreviewStageMaterialsV82/InspectionOnly.mat";
        private const string JuncSource = "LegacySources/Vigilante2Unity/Assets/Scripts/DB/JUNC_DB.cs";
        private const string JuncMeta = JuncSource + ".meta";
        private const string MeshMeta = "LegacySources/Vigilante2Unity/Assets/Scripts/VigMesh.cs.meta";
        private const string ManagerMeta = "LegacySources/Vigilante2Unity/Assets/Scripts/GameManager.cs.meta";
        private const string DebugSource = "LegacySources/Vigilante2Unity/Assets/DEBUG.unity";
        private const string BankStatus = "authentic_v82_xobf_stage_object_geometry_not_terrain_or_playable_arena";

        private static readonly Dictionary<string, string> SceneBlobs =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "v82_alaskan_pipeline", "eeabc7174370d1482f7c8aa2c61df60739b13925" },
                { "v82_ghastly_bayou", "391c654649366c665d3ecfb283b87d3a04182045" },
                { "v82_launch_site", "df6ac4f87de0e7d7a50087bfe2c0b7902a234334" },
                { "v82_meteor_crater", "3669c932afbcc1548cdb7635f7a139793bfaadbf" },
                { "v82_nuclear_plant", "1327a7cad46cba445d2a286c4edccb23f20912eb" },
                { "v82_pacific_harbor", "b2ad393114f437357f092f3f4f17f17cac430f0b" },
                { "v82_steel_mill", "2d39a1a504c443605a85ab205c78715287d72116" },
                { "v82_winter_games", "3e1f9d9e94b5c43330555a69b21fe5a8f9669796" }
            };
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)(?: stripped)?$");
        private static readonly Regex FileId = new Regex(@"fileID: (-?\d+)");
        private static readonly Regex Guid = new Regex(@"guid: ([a-fA-F0-9]{32})");
        private static readonly Regex Position = new Regex(@"^  DAT_00: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}$");
        private static readonly Regex MetaGuid = new Regex(@"^guid: ([a-fA-F0-9]{32})$", RegexOptions.Multiline);

        [Serializable] private sealed class Manifest
        {
            public int schemaVersion, stageCount;
            public string status;
            public Row[] stages;
        }
        [Serializable] private sealed class Row
        {
            public string stageId, sourcePath, sourceScenePath, sourceGitBlobSha;
            public string objPath;
        }

        internal sealed class Mono
        {
            public string FileId, Owner, ScriptGuid, BankGuid, LinkedMeshId;
            public Vector3Int? Dat00;
            public int? TmdId;
            public int? TranslateFactor;
        }
        private sealed class SourceScene
        {
            public readonly Dictionary<string, string> Names = new Dictionary<string, string>();
            public readonly Dictionary<string, List<string>> ParentsByOwner =
                new Dictionary<string, List<string>>();
            public readonly Dictionary<string, Mono> Components =
                new Dictionary<string, Mono>();
        }
        internal sealed class Placement
        {
            public Mono Junction;
            public Mono Mesh;
            public string MeshGroup;
            public string OriginalName;
        }

        /// <summary>
        /// A verified set of archived JUNC/TMD visuals ready to append to an
        /// Editor-only diagnostic scene. Import and source-hash checks happen
        /// before an existing scene is replaced. Never adds physical colliders.
        /// </summary>
        public sealed class PreparedVisuals
        {
            private readonly string stageId;
            private readonly List<Placement> candidates;
            private readonly Dictionary<string, Mesh> models;
            private readonly Material diagnostic;
            private readonly int divisor;

            internal PreparedVisuals(string stageId, List<Placement> candidates,
                Dictionary<string, Mesh> models, Material diagnostic, int divisor)
            {
                this.stageId = stageId;
                this.candidates = candidates;
                this.models = models;
                this.diagnostic = diagnostic;
                this.divisor = divisor;
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
                        "JUNC inspection requires a new empty origin-aligned scene root");

                foreach (Placement candidate in candidates)
                {
                    var entity = new GameObject(candidate.OriginalName +
                        " / JUNC " + candidate.Junction.FileId + " / " + candidate.MeshGroup);
                    entity.transform.SetParent(parent, false);
                    entity.transform.localPosition =
                        SourceJunctionLocal(candidate.Junction.Dat00.Value, divisor);
                    entity.AddComponent<MeshFilter>().sharedMesh = models[candidate.MeshGroup];
                    entity.AddComponent<MeshRenderer>().sharedMaterial = diagnostic;
                    entity.AddComponent<OriginalJunctionSourcePreviewIdentity>().Configure(
                        stageId, candidate.Junction.FileId, candidate.MeshGroup,
                        candidate.Junction.Dat00.Value, divisor);
                }

                if (parent.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    parent.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException(
                        "JUNC source-only preview must not add gameplay physics");
                return candidates.Count;
            }
        }

        private string[] stageIds = Array.Empty<string>();
        private string[] stageLabels = Array.Empty<string>();
        private int selected;

        [MenuItem("Vigilante 8/Migração/Inspecionar posições JUNC originais com malhas TMD")]
        public static void OpenReview()
        {
            GetWindow<OriginalJunctionSourceReviewBuilder>("JUNC / ORIGINAL V82");
        }

        private void OnEnable()
        {
            var stages = CatalogRepository.Load().arenas
                .Where(a => a.game == "v82").OrderBy(a => a.id, StringComparer.Ordinal).ToArray();
            stageIds = stages.Select(a => a.id).ToArray();
            stageLabels = stages.Select(a => a.name).ToArray();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("JUNC_DB / TMD — INSPEÇÃO DE ORIGEM", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "As peças são modelos verdadeiros do XOBF. A posição usa DAT_00 e a escala " +
                "do DEBUG.unity preservado. A geometria ainda NÃO recebeu deformação de terreno, " +
                "texturas PS1, colisões ou spawns oficiais. Nunca é uma arena de release.",
                MessageType.Warning);
            if (stageIds.Length != 8)
            {
                EditorGUILayout.HelpBox("O catálogo original de oito arenas está incompleto.", MessageType.Error);
                return;
            }
            selected = EditorGUILayout.Popup("Arena original", selected, stageLabels);
            if (GUILayout.Button("Gerar cena de inspeção das malhas e coordenadas reais"))
            {
                try { Build(stageIds[selected]); }
                catch (Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("Fonte original inválida", error.Message, "OK"); }
            }
        }

        public static Vector3 SourceJunctionLocal(Vector3Int raw, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
            // Exact JUNC_DB.Update axis mapping. Negate AFTER converting to float
            // so int.MinValue never overflows in signed integer arithmetic.
            return new Vector3(raw.x / (float)divisor, -(float)raw.y / divisor,
                raw.z / (float)divisor);
        }

        public static bool IsRootSourceMatch(bool sameObject, bool correctBank,
            bool rootTransform, string stageId, string meshGroup)
        {
            if (!sameObject || !correctBank || !rootTransform ||
                string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(meshGroup))
                return false;
            string prefix = stageId + "_tmd_";
            if (!meshGroup.StartsWith(prefix, StringComparison.Ordinal) ||
                meshGroup.Length != prefix.Length + 3)
                return false;
            return meshGroup.Substring(prefix.Length).All(char.IsDigit);
        }

        private static string Absolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith("/", StringComparison.Ordinal) ||
                path.Contains("..") || path.Contains('\\'))
                throw new InvalidDataException("Unsafe source path: " + path);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(Path.Combine(root, path));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Source path escaped original Unity project");
            return full;
        }

        private static string SourceGuid(string path)
        {
            Match match = MetaGuid.Match(File.ReadAllText(Absolute(path)));
            if (!match.Success) throw new InvalidDataException("Original GUID missing: " + path);
            return match.Groups[1].Value.ToLowerInvariant();
        }

        private static string MatchFileId(string line)
        {
            Match match = FileId.Match(line);
            return match.Success ? match.Groups[1].Value : null;
        }
        private static string MatchGuid(string line)
        {
            Match match = Guid.Match(line);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        private static SourceScene Scan(string path)
        {
            var result = new SourceScene();
            string kind = "", fileId = "", name = null, owner = null, parent = null;
            Mono mono = null;
            void Finish()
            {
                if (kind == "1" && name != null)
                    result.Names.Add(fileId, name);
                else if (kind == "4" && owner != null)
                {
                    if (!result.ParentsByOwner.TryGetValue(owner, out List<string> parents))
                    {
                        parents = new List<string>();
                        result.ParentsByOwner.Add(owner, parents);
                    }
                    parents.Add(parent);
                }
                else if (kind == "114" && mono != null && mono.ScriptGuid != null)
                    result.Components.Add(fileId, mono);
            }
            foreach (string line in File.ReadLines(Absolute(path), Encoding.UTF8))
            {
                Match h = Header.Match(line);
                if (h.Success)
                {
                    Finish();
                    kind = h.Groups[1].Value;
                    fileId = h.Groups[2].Value;
                    name = owner = parent = null;
                    mono = kind == "114" ? new Mono { FileId = fileId } : null;
                    continue;
                }
                if (kind == "1" && line.StartsWith("  m_Name: ", StringComparison.Ordinal))
                    name = line.Substring(10);
                else if (kind == "4")
                {
                    if (line.StartsWith("  m_GameObject: ", StringComparison.Ordinal)) owner = MatchFileId(line);
                    else if (line.StartsWith("  m_Father: ", StringComparison.Ordinal)) parent = MatchFileId(line);
                }
                else if (kind == "114" && mono != null)
                {
                    if (line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                        mono.ScriptGuid = MatchGuid(line);
                    else if (line.StartsWith("  m_GameObject: ", StringComparison.Ordinal))
                        mono.Owner = MatchFileId(line);
                    else if (line.StartsWith("  DAT_0C: ", StringComparison.Ordinal) ||
                             line.StartsWith("  xobf: ", StringComparison.Ordinal))
                        mono.BankGuid = MatchGuid(line);
                    else if (line.StartsWith("  DAT_18: ", StringComparison.Ordinal))
                        mono.LinkedMeshId = MatchFileId(line);
                    else if (line.StartsWith("  tmdID: ", StringComparison.Ordinal) &&
                             int.TryParse(line.Substring(9).Trim(), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out int tmd))
                        mono.TmdId = tmd;
                    else if (line.StartsWith("  translateFactor: ", StringComparison.Ordinal) &&
                             int.TryParse(line.Substring(19).Trim(), NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out int factor))
                        mono.TranslateFactor = factor;
                    else if (line.StartsWith("  DAT_00: ", StringComparison.Ordinal))
                    {
                        Match coords = Position.Match(line);
                        if (coords.Success &&
                            int.TryParse(coords.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
                            int.TryParse(coords.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) &&
                            int.TryParse(coords.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                            mono.Dat00 = new Vector3Int(x, y, z);
                    }
                }
            }
            Finish();
            return result;
        }

        private static int ReadDebugScale()
        {
            string expectedGuid = SourceGuid(ManagerMeta);
            Mono[] matches = Scan(DebugSource).Components.Values
                .Where(x => x.ScriptGuid == expectedGuid).ToArray();
            if (matches.Length != 1 || matches[0].TranslateFactor.GetValueOrDefault() <= 0)
                throw new InvalidDataException("DEBUG GameManager source scale not proven");
            return matches[0].TranslateFactor.Value;
        }

        private static void VerifyJunctionSource()
        {
            string text = File.ReadAllText(Absolute(JuncSource));
            foreach (string statement in new[]
            {
                "public Vector3Int DAT_00;",
                "(float)DAT_00.x / GameManager.instance.translateFactor,",
                "(float)-DAT_00.y / GameManager.instance.translateFactor,",
                "(float)DAT_00.z / GameManager.instance.translateFactor);"
            })
                if (!text.Contains(statement))
                    throw new InvalidDataException("JUNC_DB.Update source conversion changed");
        }

        internal static int VerifiedDebugScale() => ReadDebugScale();

        internal static string VerifiedOriginalScenePath(string id) =>
            Absolute(VerifiedStage(id).sourceScenePath);

        internal static string VerifiedScriptGuid(string metaPath) =>
            SourceGuid(metaPath);

        private static Row VerifiedStage(string id)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (asset == null) throw new InvalidDataException("Missing archived XOBF stage bank manifest");
            Manifest manifest = JsonUtility.FromJson<Manifest>(asset.text);
            if (manifest == null || manifest.schemaVersion != 1 ||
                manifest.stageCount != 8 || manifest.status != BankStatus ||
                manifest.stages == null || manifest.stages.Length != 8 ||
                !SceneBlobs.Keys.OrderBy(x => x).SequenceEqual(
                    manifest.stages.Select(x => x.stageId).OrderBy(x => x)))
                throw new InvalidDataException("The original 8-stage source manifest changed");
            Row row = manifest.stages.SingleOrDefault(x => x.stageId == id);
            if (row == null || !row.sourceScenePath.StartsWith(
                    "LegacySources/Vigilante2Unity/Assets/Levels/", StringComparison.Ordinal) ||
                row.sourceGitBlobSha?.Length != 40 ||
                row.objPath != "Assets/GameContent/ExtractedStageObjectsV82/" + id + ".obj" ||
                row.sourcePath.IndexOf("XOBF00.prefab", StringComparison.Ordinal) < 0 ||
                !OriginalSourceGitBlob.Matches(Absolute(row.sourceScenePath), SceneBlobs[id]) ||
                !OriginalSourceGitBlob.Matches(Absolute(row.sourcePath), row.sourceGitBlobSha))
                throw new InvalidDataException("Stage original source/bank hash or path changed");
            return row;
        }

        private static List<Placement> Resolve(string id, Row source, SourceScene scene)
        {
            string junctionGuid = SourceGuid(JuncMeta);
            string meshGuid = SourceGuid(MeshMeta);
            string bankGuid = SourceGuid(source.sourcePath + ".meta");
            var linked = new List<Placement>();
            foreach (Mono junc in scene.Components.Values.Where(x => x.ScriptGuid == junctionGuid))
            {
                if (junc.Dat00 == null || junc.LinkedMeshId == null ||
                    !scene.Components.TryGetValue(junc.LinkedMeshId, out Mono mesh) ||
                    mesh.ScriptGuid != meshGuid || mesh.TmdId == null ||
                    mesh.TmdId < 0 || mesh.TmdId > 999)
                    continue;
                if (!scene.ParentsByOwner.TryGetValue(junc.Owner ?? "", out List<string> parents))
                    continue;
                bool root = parents.Count == 1 && parents[0] == "0";
                string group = id + "_tmd_" + mesh.TmdId.Value.ToString("D3", CultureInfo.InvariantCulture);
                if (!IsRootSourceMatch(mesh.Owner == junc.Owner,
                    junc.BankGuid == bankGuid && mesh.BankGuid == bankGuid,
                    root, id, group))
                    continue;
                if (!scene.Names.TryGetValue(junc.Owner, out string name))
                    continue;
                linked.Add(new Placement { Junction = junc, Mesh = mesh,
                    MeshGroup = group, OriginalName = name });
            }
            if (linked.Count == 0)
                throw new InvalidDataException("No root JUNC with verified XOBF/TMD source linkage");
            return linked.OrderBy(x => long.Parse(x.Junction.FileId, CultureInfo.InvariantCulture)).ToList();
        }

        public static PreparedVisuals Prepare(string stageId)
        {
            if (!SceneBlobs.ContainsKey(stageId) ||
                !CatalogRepository.Load().arenas.Any(x => x.game == "v82" && x.id == stageId))
                throw new ArgumentException("Only one of the 8 authentic V82 stages is allowed");
            if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                throw new InvalidOperationException("JUNC source inspection scene cannot enter Windows build");
            VerifyJunctionSource();
            Row stage = VerifiedStage(stageId);
            int divisor = ReadDebugScale();
            SourceScene archived = Scan(stage.sourceScenePath);
            List<Placement> candidates = Resolve(stageId, stage, archived);
            OriginalStageObjectReviewBuilder.PrepareAll();
            Material diagnostic = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (diagnostic == null) throw new InvalidOperationException("Original stage inspection material missing");

            // Preflight ALL genuine TMD Mesh assets before opening a new scene.
            var models = new Dictionary<string, Mesh>();
            foreach (Placement candidate in candidates)
            {
                if (models.ContainsKey(candidate.MeshGroup)) continue;
                string path = MeshFolder + stageId + "/" + candidate.MeshGroup + ".asset";
                Mesh original = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (original == null || original.vertexCount < 3 || original.subMeshCount < 1)
                    throw new InvalidDataException("Genuine extracted TMD mesh missing: " + path);
                models.Add(candidate.MeshGroup, original);
            }
            return new PreparedVisuals(stageId, candidates, models, diagnostic, divisor);
        }

        public static void Build(string stageId)
        {
            PreparedVisuals preview = Prepare(stageId);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parent = new GameObject("SOURCE JUNC INSPECTION - NO ORIGINAL TERRAIN DEFORMATION");
            int junctionCount = preview.AppendTo(parent.transform);
            Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("No original junction visuals for inspection");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            var sunlight = new GameObject("Inspection-only sunlight").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.intensity = 1.1f;
            sunlight.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            var cameraObject = new GameObject("Source Junction Review Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.045f);
            camera.farClipPlane = Mathf.Max(1000f, bounds.size.magnitude * 4f);
            cameraObject.AddComponent<AudioListener>();
            float distance = Mathf.Max(25f, bounds.size.magnitude * 1.4f);
            cameraObject.transform.position = bounds.center + new Vector3(distance, distance, -distance);
            cameraObject.transform.LookAt(bounds.center);
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Original source diagnostic scene failed to save");
            if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                throw new InvalidOperationException("Source diagnostic scene cannot ship");
            Debug.Log("JUNC source inspection created: " + stageId + " / " +
                junctionCount + " root JUNC references to original TMD pieces. " +
                "The original terrain deformation, fixed-point runtime context, physics, " +
                "materials and spawn points are NOT ported. No release output.");
        }
    }
}
#endif
