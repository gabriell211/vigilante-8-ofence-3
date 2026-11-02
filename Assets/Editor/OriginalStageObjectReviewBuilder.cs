#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Reconstructs eight ORIGINAL TMD object banks, not terrains or complete arenas.
    /// The neutral diagnostic inspection material is NOT an original PS1 texture.
    /// No generated preview can enter official arena assets or the PC release.
    /// </summary>
    public static class OriginalStageObjectReviewBuilder
    {
        private const string SourceRoot = "Assets/GameContent/ExtractedStageObjectsV82";
        private const string PrefabRoot = "Assets/GameContent/PreviewStageObjectsV82";
        private const string MeshRoot = "Assets/GameContent/PreviewStageMeshesV82";
        private const string MaterialPath = "Assets/GameContent/PreviewStageMaterialsV82/InspectionOnly.mat";
        private const string Status = "authentic_v82_xobf_stage_object_geometry_not_terrain_or_playable_arena";
        public const string ScenePath = "Assets/Scenes/OriginalStageObjectReview.unity";

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public string sourceRevision;
            public string status;
            public int stageCount;
            public Entry[] stages;
        }

        [Serializable]
        private sealed class Entry
        {
            public string stageId;
            public string objPath;
            public string sourceGitBlobSha;
            public int emittedGroups;
            public int vertices;
            public int triangles;
            public bool terrainReady, materialsReady, collisionReady, gameplayTested;
        }

        public sealed class Part
        {
            public string Name;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Faces = new List<int>();
        }

        public sealed class SourceDocument
        {
            public readonly List<Part> Parts = new List<Part>();
            public int Vertices => Parts.Sum(p => p.Vertices.Count);
            public int Triangles => Parts.Sum(p => p.Faces.Count / 3);
        }

        public static SourceDocument Parse(string text, string id, int vertices, int triangles, int groups)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(id) ||
                !id.StartsWith("v82_", StringComparison.Ordinal) ||
                vertices <= 0 || triangles <= 0 || groups <= 0)
                throw new ArgumentException("Original stage source and nonzero counts required");

            var document = new SourceDocument();
            var names = new HashSet<string>(StringComparer.Ordinal);
            Part part = null;
            bool hasObject = false;
            int offset = 0, lineNumber = 0;
            foreach (string sourceLine in text.Split('\n'))
            {
                lineNumber++;
                string line = sourceLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (tokens[0])
                {
                    case "o":
                        if (hasObject || tokens.Length != 2 || tokens[1] != id ||
                            document.Parts.Count != 0)
                            throw Error(lineNumber, "Wrong source stage object");
                        hasObject = true;
                        break;
                    case "g":
                        string prefix = id + "_tmd_";
                        if (!hasObject || tokens.Length != 2 ||
                            !tokens[1].StartsWith(prefix, StringComparison.Ordinal) ||
                            tokens[1].Length != prefix.Length + 3 ||
                            !tokens[1].Substring(prefix.Length).All(c => c >= '0' && c <= '9') ||
                            !names.Add(tokens[1]))
                            throw Error(lineNumber, "Non-original or duplicate part");
                        if (part != null && (part.Vertices.Count < 3 || part.Faces.Count == 0))
                            throw Error(lineNumber, "Empty TMD group");
                        offset += part == null ? 0 : part.Vertices.Count;
                        part = new Part { Name = tokens[1] };
                        document.Parts.Add(part);
                        break;
                    case "v":
                        if (part == null || tokens.Length != 4 || part.Faces.Count > 0)
                            throw Error(lineNumber, "Misplaced original vertices");
                        part.Vertices.Add(new Vector3(Coordinate(tokens[1], lineNumber),
                            Coordinate(tokens[2], lineNumber),
                            Coordinate(tokens[3], lineNumber)));
                        break;
                    case "f":
                        if (part == null || part.Vertices.Count < 3 || tokens.Length != 4)
                            throw Error(lineNumber, "Invalid original triangle");
                        int a = FaceIndex(tokens[1], offset, part.Vertices.Count, lineNumber);
                        int b = FaceIndex(tokens[2], offset, part.Vertices.Count, lineNumber);
                        int c = FaceIndex(tokens[3], offset, part.Vertices.Count, lineNumber);
                        if (a == b || a == c || b == c)
                            throw Error(lineNumber, "Degenerate original triangle");
                        part.Faces.Add(a);
                        part.Faces.Add(b);
                        part.Faces.Add(c);
                        break;
                    default:
                        throw Error(lineNumber, "Unsupported TMD OBJ record: " + tokens[0]);
                }
            }
            if (!hasObject || part == null || part.Vertices.Count < 3 ||
                part.Faces.Count == 0 || document.Vertices != vertices ||
                document.Triangles != triangles || document.Parts.Count != groups)
                throw new InvalidDataException(id + ": original TMD dimensions changed");
            return document;
        }

        private static float Coordinate(string value, int line)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || float.IsNaN(x) || float.IsInfinity(x))
                throw Error(line, "Invalid source coordinate");
            return x;
        }

        private static int FaceIndex(string value, int offset, int count, int line)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index <= offset || index > offset + count)
                throw Error(line, "Face references vertex outside its original group");
            return index - offset - 1;
        }

        private static InvalidDataException Error(int line, string problem)
            => new InvalidDataException("Stage OBJ line " + line + ": " + problem);

        private static (Entry[] entries, Dictionary<string, SourceDocument> models) VerifySources()
        {
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceRoot + "/manifest.json");
            if (text == null) throw new InvalidOperationException("Missing original stage manifest");
            Manifest manifest = JsonUtility.FromJson<Manifest>(text.text);
            if (manifest == null || manifest.schemaVersion != 1 || manifest.stageCount != 8 ||
                manifest.stages == null || manifest.stages.Length != 8 ||
                manifest.status != Status ||
                manifest.sourceRevision != "d20883df0d5da3ba517142fb4d486db1f5e673fb")
                throw new InvalidOperationException("Unverified original eight-stage object bank inventory");

            var official = new HashSet<string>(CatalogRepository.Load().arenas
                .Where(x => x.game == "v82").Select(x => x.id), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var parsed = new Dictionary<string, SourceDocument>(StringComparer.Ordinal);
            foreach (Entry entry in manifest.stages)
            {
                if (entry == null || !official.Contains(entry.stageId) ||
                    !seen.Add(entry.stageId) || entry.terrainReady || entry.materialsReady ||
                    entry.collisionReady || entry.gameplayTested ||
                    string.IsNullOrWhiteSpace(entry.sourceGitBlobSha) ||
                    entry.sourceGitBlobSha.Length != 40 ||
                    entry.objPath != SourceRoot + "/" + entry.stageId + ".obj")
                    throw new InvalidOperationException("Stage object bank cannot claim gameplay certification");
                if (!File.Exists(entry.objPath))
                    throw new FileNotFoundException("Real stage source geometry missing", entry.objPath);
                parsed.Add(entry.stageId, Parse(File.ReadAllText(entry.objPath),
                    entry.stageId, entry.vertices, entry.triangles, entry.emittedGroups));
            }
            if (official.Count != 8 || !official.SetEquals(seen))
                throw new InvalidOperationException("Source banks do not cover the eight original arenas");
            return (manifest.stages, parsed);
        }

        [MenuItem("Vigilante 8/Migração/Importar objetos 3D reais das 8 fases V82")]
        public static void PrepareAll()
        {
            // Validate all sources first; never silently create partial previews.
            var (entries, models) = VerifySources();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit unavailable for inspection");
            EnsureFolder(PrefabRoot);
            EnsureFolder("Assets/GameContent/PreviewStageMaterialsV82");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "DIAGNOSTIC - SOURCE GEOMETRY ONLY" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.75f, 0.82f, 0.94f));
            EditorUtility.SetDirty(material);

            foreach (Entry row in entries)
            {
                string directory = MeshRoot + "/" + row.stageId;
                EnsureFolder(directory);
                var root = new GameObject("UNCERTIFIED XOBF BANK - " + row.stageId);
                try
                {
                    foreach (Part part in models[row.stageId].Parts)
                    {
                        var mesh = new Mesh { name = part.Name, indexFormat = IndexFormat.UInt32 };
                        mesh.SetVertices(part.Vertices);
                        mesh.SetTriangles(part.Faces, 0);
                        mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                        string path = directory + "/" + part.Name + ".asset";
                        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (saved == null)
                        {
                            AssetDatabase.CreateAsset(mesh, path);
                            saved = mesh;
                        }
                        else
                        {
                            EditorUtility.CopySerialized(mesh, saved);
                            UnityEngine.Object.DestroyImmediate(mesh);
                            EditorUtility.SetDirty(saved);
                        }
                        var piece = new GameObject(part.Name);
                        piece.transform.SetParent(root.transform, false);
                        piece.AddComponent<MeshFilter>().sharedMesh = saved;
                        piece.AddComponent<MeshRenderer>().sharedMaterial = material;
                    }

                    root.AddComponent<OriginalStageObjectPreviewIdentity>().Configure(
                        row.stageId, row.triangles, row.emittedGroups);
                    string prefab = PrefabRoot + "/" + row.stageId + ".prefab";
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefab) == null)
                        throw new InvalidOperationException("Failed to save verified preview: " + prefab);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            // The matching original XOBF WAV indices become inspectable alongside
            // genuine source geometry, without assigning any unverified sound role.
            OriginalAudioBankBinder.AttachToStageInspectionPrefabs();
            Debug.Log("8 real original TMD object banks and matching source-indexed WAVs assembled. " +
                "No original terrain, PS1 materials, collision, audio roles, spawns or placement.");
        }

        [MenuItem("Vigilante 8/Migração/Gerar galeria 3D dos bancos de objetos das 8 fases V82")]
        public static void BuildGallery()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            PrepareAll();
            ArenaEntry[] stages = CatalogRepository.Load().arenas
                .Where(x => x.game == "v82").OrderBy(x => x.id, StringComparer.Ordinal).ToArray();
            if (stages.Length != 8)
                throw new InvalidOperationException("Eight original 2nd Offense levels required");

            var prefabs = stages.Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabRoot + "/" + x.id + ".prefab")).ToArray();
            for (int i = 0; i < prefabs.Length; i++)
            {
                var identity = prefabs[i]?.GetComponent<OriginalStageObjectPreviewIdentity>();
                if (identity == null || identity.StageId != stages[i].id ||
                    identity.IsReleaseApproved || identity.SourceTriangles <= 0)
                    throw new InvalidOperationException("Incorrect original source stage prefab");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ORIGINAL STAGE OBJECT BANKS - NOT COMPLETE ARENAS");
            var instances = new List<GameObject>();
            float width = 1f, depth = 1f, height = 1f;
            foreach (GameObject prefab in prefabs)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (go == null) throw new InvalidOperationException("Unable to display genuine object bank");
                go.transform.SetParent(root.transform, true);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Renderer[] r = go.GetComponentsInChildren<Renderer>(true);
                if (r.Length == 0) throw new InvalidOperationException("Source stage has no mesh");
                Bounds bounds = r[0].bounds;
                foreach (Renderer other in r.Skip(1)) bounds.Encapsulate(other.bounds);
                if (bounds.size.sqrMagnitude < 0.001f)
                    throw new InvalidOperationException("Original stage geometry is empty");
                width = Mathf.Max(width, bounds.size.x);
                depth = Mathf.Max(depth, bounds.size.z);
                height = Mathf.Max(height, bounds.size.y);
                instances.Add(go);
            }
            const int columns = 4;
            float stepX = width + 15f, stepZ = depth + 15f;
            for (int i = 0; i < instances.Count; i++)
            {
                GameObject go = instances[i];
                Renderer[] r = go.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = r[0].bounds;
                foreach (Renderer other in r.Skip(1)) bounds.Encapsulate(other.bounds);
                Vector3 center = new Vector3((i % columns - 1.5f) * stepX,
                    0f, (i / columns - 0.5f) * stepZ);
                go.transform.position = center - bounds.center + Vector3.up * bounds.extents.y;
                go.name = stages[i].name + " - ORIGINAL OBJECT BANK ONLY";
            }

            var sun = new GameObject("Inspection Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.GetComponent<Light>().intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(45f, -32f, 0f);
            var viewer = new GameObject("Inspection Camera");
            viewer.tag = "MainCamera";
            Camera camera = viewer.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            camera.fieldOfView = 50f;
            viewer.AddComponent<AudioListener>();
            float distance = Mathf.Max(columns * stepX, 2f * stepZ);
            viewer.transform.position = new Vector3(0, distance + height, -distance * 1.1f);
            viewer.transform.LookAt(new Vector3(0, height * 0.5f, 0));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.45f, 0.53f);

            EnsureFolder("Assets/Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save real source inspection gallery");
            if (EditorBuildSettings.scenes.Any(x => x.enabled && x.path == ScenePath))
                throw new InvalidOperationException("Inspection gallery cannot be in player build");
            AssetDatabase.SaveAssets();
            Debug.Log("8 original object-bank source previews saved to " + ScenePath +
                ". NOT gameplay: terrain, positions, original materials and collisions remain missing.");
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }
}
#endif
