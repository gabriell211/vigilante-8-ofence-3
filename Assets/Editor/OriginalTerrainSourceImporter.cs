#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Copies genuine archived Geo.asset Unity Mesh bytes and their original
    /// GUIDs into inspection-only prefabs. No fabricated terrain or colliders.
    /// </summary>
    public static class OriginalTerrainSourceImporter
    {
        private const string ManifestPath = "Assets/GameContent/LegacyTerrainV82/manifest.json";
        private const string Prefabs = "Assets/GameContent/PreviewTerrainV82";
        public const string GalleryPath = "Assets/Scenes/OriginalTerrainReview.unity";
        private const string Status = "original_native_unity_terrain_meshes_inspection_only";
        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion, availableTerrainMeshes, expectedV82Arenas;
            public string status;
            public string[] missingTerrainSource;
            public Row[] terrains;
        }
        [Serializable]
        private sealed class Row
        {
            public string stageId, sourcePath, sourceGitBlobSha, sourceGuid, importPath;
            public long sourceBytes;
            public bool isCompleteArena, collisionApproved, spawnPointsApproved;
        }
        private static string Absolute(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative[0] == '/'
                || relative.Contains("..") || relative.Contains('\\'))
                throw new InvalidOperationException("Unsafe native terrain file path");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string result = Path.GetFullPath(Path.Combine(root, relative));
            if (!result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Terrain escaped Unity project");
            return result;
        }
        private static string GitBlobSha(string file)
        {
            byte[] data = File.ReadAllBytes(file);
            byte[] header = Encoding.ASCII.GetBytes("blob " + data.Length + "\0");
            using (var sha = SHA1.Create())
            {
                sha.TransformBlock(header, 0, header.Length, header, 0);
                sha.TransformFinalBlock(data, 0, data.Length);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        private static Row[] Verified()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (text == null) throw new InvalidOperationException("Original terrain manifest missing");
            var m = JsonUtility.FromJson<Manifest>(text.text);
            if (m == null || m.schemaVersion != 1 || m.availableTerrainMeshes != 7
                || m.expectedV82Arenas != 8 || m.status != Status || m.terrains == null
                || m.terrains.Length != 7 || m.missingTerrainSource == null
                || m.missingTerrainSource.Length != 1
                || m.missingTerrainSource[0] != "v82_meteor_crater")
                throw new InvalidOperationException("Original terrain manifest is incomplete");
            var official = new HashSet<string>(CatalogRepository.Load().arenas
                .Where(x => x.game == "v82").Select(x => x.id), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Row entry in m.terrains)
            {
                if (entry == null || !official.Contains(entry.stageId) ||
                    entry.stageId == "v82_meteor_crater" || !seen.Add(entry.stageId) ||
                    entry.isCompleteArena || entry.collisionApproved || entry.spawnPointsApproved
                    || string.IsNullOrWhiteSpace(entry.sourcePath) ||
                    !entry.sourcePath.StartsWith("LegacySources/Vigilante2Unity/Assets/Levels/",
                        StringComparison.Ordinal) ||
                    !entry.sourcePath.EndsWith("Geo.asset", StringComparison.Ordinal) ||
                    entry.importPath != "Assets/GameContent/ImportedOriginalTerrainV82/" +
                        entry.stageId + ".asset" ||
                    string.IsNullOrWhiteSpace(entry.sourceGuid) || entry.sourceGuid.Length != 32 ||
                    !guids.Add(entry.sourceGuid) ||
                    string.IsNullOrWhiteSpace(entry.sourceGitBlobSha) ||
                    entry.sourceGitBlobSha.Length != 40 || entry.sourceBytes < 100000)
                    throw new InvalidOperationException("Invalid original source terrain entry");
                string path = Absolute(entry.sourcePath);
                if (!File.Exists(path) || new FileInfo(path).Length != entry.sourceBytes ||
                    GitBlobSha(path) != entry.sourceGitBlobSha)
                    throw new InvalidOperationException("Original source terrain Git SHA mismatch");
                var meta = File.ReadAllText(path + ".meta");
                if (!meta.Contains("guid: " + entry.sourceGuid) ||
                    !meta.Contains("NativeFormatImporter:"))
                    throw new InvalidOperationException("Original Unity terrain GUID mismatch");
            }
            if (official.Count != 8 || official.Except(seen).Single() != "v82_meteor_crater")
                throw new InvalidOperationException("Original V82 terrain inventory mismatch");
            return m.terrains.OrderBy(x => x.stageId, StringComparer.Ordinal).ToArray();
        }
        private static void Folder(string path)
        {
            var segments = path.Split('/');
            string parent = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string child = parent + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(child))
                    AssetDatabase.CreateFolder(parent, segments[i]);
                parent = child;
            }
        }

        [MenuItem("Vigilante 8/Migração/Importar 7 Geo Mesh originais do V82")]
        public static void ImportAll()
        {
            Row[] rows = Verified();
            Folder("Assets/GameContent/ImportedOriginalTerrainV82");
            // Preflight ALL destinations so an existing edited model cannot be overwritten.
            foreach (var row in rows)
            {
                var output = Absolute(row.importPath);
                if (File.Exists(output) && GitBlobSha(output) != row.sourceGitBlobSha)
                    throw new InvalidOperationException("Refusing to overwrite changed source mesh");
                if (File.Exists(output + ".meta") &&
                    File.ReadAllText(output + ".meta") !=
                    File.ReadAllText(Absolute(row.sourcePath) + ".meta"))
                    throw new InvalidOperationException("Refusing to overwrite changed source GUID");
            }
            foreach (var row in rows)
            {
                string source = Absolute(row.sourcePath), dest = Absolute(row.importPath);
                if (!File.Exists(dest)) File.Copy(source, dest);
                if (!File.Exists(dest + ".meta")) File.Copy(source + ".meta", dest + ".meta");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var row in rows)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(row.importPath);
                if (AssetDatabase.AssetPathToGUID(row.importPath) != row.sourceGuid ||
                    mesh == null || mesh.vertexCount < 3 || mesh.subMeshCount < 1 ||
                    mesh.bounds.size.sqrMagnitude <= 0f)
                    throw new InvalidOperationException("Unity failed to deserialize original Mesh: " + row.stageId);
                ulong indices = 0;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (mesh.GetTopology(i) != MeshTopology.Triangles)
                        throw new InvalidOperationException("Unsupported original mesh topology");
                    indices += mesh.GetIndexCount(i);
                }
                if (indices < 3 || indices % 3 != 0)
                    throw new InvalidOperationException("Invalid original terrain triangle counts");
            }
        }

        [MenuItem("Vigilante 8/Migração/Gerar galeria 3D de 7 terrenos V82 originais")]
        public static void BuildGallery()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            ImportAll();
            var records = Verified();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Missing URP preview shader");
            Folder(Prefabs);
            Folder("Assets/GameContent/PreviewTerrainMaterialsV82");
            const string matPath = "Assets/GameContent/PreviewTerrainMaterialsV82/Diagnostic.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "SOURCE TERRAIN - NOT ORIGINAL TEXTURE" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", new Color(0.73f, 0.81f, 0.91f));
            EditorUtility.SetDirty(mat);

            var prefabs = new List<GameObject>();
            foreach (var record in records)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(record.importPath);
                var go = new GameObject("ORIGINAL TERRAIN INSPECTION - " + record.stageId);
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials =
                        Enumerable.Repeat(mat, mesh.subMeshCount).ToArray();
                    ulong indices = 0;
                    for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
                    go.AddComponent<OriginalTerrainPreviewIdentity>().Configure(
                        record.stageId, record.sourceGuid, mesh.vertexCount, checked((int)(indices / 3)));
                    string path = Prefabs + "/" + record.stageId + ".prefab";
                    if (PrefabUtility.SaveAsPrefabAsset(go, path) == null)
                        throw new InvalidOperationException("Cannot save original terrain source prefab");
                    prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parent = new GameObject("SEVEN ORIGINAL TERRAIN SOURCES - NOT COMPLETE LEVELS");
            var instances = new List<GameObject>();
            float sx = 1f, sy = 1f, sz = 1f;
            foreach (var prefab in prefabs)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (go == null) throw new InvalidOperationException("Source terrain preview missing");
                go.transform.SetParent(parent.transform, true);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Bounds b = go.GetComponent<MeshRenderer>().bounds;
                sx = Mathf.Max(sx, b.size.x);
                sy = Mathf.Max(sy, b.size.y);
                sz = Mathf.Max(sz, b.size.z);
                instances.Add(go);
            }
            float dx = sx + 40f, dz = sz + 40f;
            for (int i = 0; i < instances.Count; i++)
            {
                var go = instances[i];
                var bounds = go.GetComponent<MeshRenderer>().bounds;
                var origin = new Vector3((i % 4 - 1.5f) * dx, 0f, (i / 4 - 0.5f) * dz);
                go.transform.position = origin - bounds.center + Vector3.up * bounds.extents.y;
                go.name = records[i].stageId + " - source terrain only";
            }
            var sunlight = new GameObject("Inspection Light").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.intensity = 1.2f;
            sunlight.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var eye = new GameObject("Terrain Gallery Camera");
            eye.tag = "MainCamera";
            var camera = eye.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            eye.AddComponent<AudioListener>();
            float dist = Mathf.Max(4f * dx, 2f * dz);
            eye.transform.position = new Vector3(0, dist + sy, -dist);
            eye.transform.LookAt(new Vector3(0, sy * 0.5f, 0));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.46f, 0.54f);
            Folder("Assets/Scenes");
            if (!EditorSceneManager.SaveScene(scene, GalleryPath))
                throw new InvalidOperationException("Original terrain preview gallery failed to save");
            if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == GalleryPath))
                throw new InvalidOperationException("Terrain inspection gallery cannot be in Windows build");
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
