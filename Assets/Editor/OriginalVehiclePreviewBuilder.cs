#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Converts only authentic TMD-derived OBJ/MTL previews into Unity 6 URP
    /// inspection prefabs. Intentionally not stored in Resources/Vehicles and
    /// never identified as a releasable or gameplay-tested official car.
    /// </summary>
    public static class OriginalVehiclePreviewBuilder
    {
        private const string SourceDir = "Assets/GameContent/TexturedPreviewV82";
        private const string MaterialDir = "Assets/GameContent/PreviewMaterialsV82";
        private const string PrefabDir = "Assets/GameContent/PreviewPrefabsV82";

        [Serializable]
        private sealed class SourceManifest
        {
            public int schemaVersion;
            public PreviewRecord[] vehicles;
        }

        [Serializable]
        private sealed class PreviewRecord
        {
            public string vehicleId;
            public int triangles;
            public string status;
        }

        private sealed class MaterialSpec
        {
            public string name;
            public Color color = Color.white;
            public string originalTexture;
        }

        [MenuItem("Vigilante 8/Migração/Preparar 18 carros originais para inspeção URP")]
        public static void PreparePreviewPrefabs()
        {
            var manifestFile = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceDir + "/manifest.json");
            if (manifestFile == null)
                throw new InvalidOperationException("Missing source-derived preview manifest");
            SourceManifest manifest = JsonUtility.FromJson<SourceManifest>(manifestFile.text);
            if (manifest == null || manifest.schemaVersion != 1 ||
                manifest.vehicles == null || manifest.vehicles.Length != 18 ||
                manifest.vehicles.Select(v => v.vehicleId).Distinct().Count() != 18)
                throw new InvalidOperationException("Original 18-car preview inventory is incomplete");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP/Lit shader unavailable; install and activate URP");

            EnsureFolder(MaterialDir);
            EnsureFolder(PrefabDir);
            foreach (PreviewRecord record in manifest.vehicles.OrderBy(r => r.vehicleId))
            {
                if (record.triangles < 1 ||
                    record.status != "textured_preview_not_verified_game_asset" ||
                    !record.vehicleId.StartsWith("v82_", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unapproved source preview: " + record.vehicleId);

                var sourceAsset = SourceDir + "/" + record.vehicleId + ".obj";
                var originalObj = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAsset);
                if (originalObj == null)
                    throw new InvalidOperationException("Unity has not imported the original OBJ: " + sourceAsset);
                string mtlAsset = SourceDir + "/" + record.vehicleId + ".mtl";
                if (!File.Exists(mtlAsset))
                    throw new FileNotFoundException("Missing original material association", mtlAsset);

                Dictionary<string, Material> materials = ImportMaterialReferences(
                    record.vehicleId, mtlAsset, shader);
                GameObject instance = UnityEngine.Object.Instantiate(originalObj);
                try
                {
                    instance.name = "ORIGINAL INSPECTION ONLY - " + record.vehicleId;
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0)
                        throw new InvalidOperationException("No original 3D renderer: " + record.vehicleId);
                    int assigned = 0;
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] sourceMaterials = renderer.sharedMaterials;
                        if (sourceMaterials.Length == 0)
                            throw new InvalidOperationException("No imported material slots in " + record.vehicleId);
                        var replacements = new Material[sourceMaterials.Length];
                        for (int i = 0; i < sourceMaterials.Length; i++)
                        {
                            Material old = sourceMaterials[i];
                            if (old == null || !materials.TryGetValue(old.name, out Material replacement))
                                throw new InvalidOperationException("Unmapped original material " +
                                    (old == null ? "<null>" : old.name) + " on " + record.vehicleId +
                                    ". Inspect Unity OBJ material importer, do not apply generic replacements.");
                            replacements[i] = replacement;
                            assigned++;
                        }
                        renderer.sharedMaterials = replacements;
                    }

                    if (assigned == 0)
                        throw new InvalidOperationException("No original materials applied on " + record.vehicleId);

                    var marker = instance.AddComponent<OriginalVehiclePreviewIdentity>();
                    marker.Configure(record.vehicleId, record.triangles);
                    string prefab = PrefabDir + "/" + record.vehicleId + ".prefab";
                    if (PrefabUtility.SaveAsPrefabAsset(instance, prefab) == null)
                        throw new InvalidOperationException("Failed to save original preview: " + prefab);
                    Debug.Log("Prepared original model inspection: " + record.vehicleId +
                              " (" + assigned + " material slots, no release certification)");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("18 source-derived vehicle inspection prefabs created outside Resources.");
        }

        private static Dictionary<string, Material> ImportMaterialReferences(
            string id, string mtlFile, Shader shader)
        {
            var output = new Dictionary<string, Material>(StringComparer.Ordinal);
            MaterialSpec current = null;

            void SaveMaterial()
            {
                if (current == null) return;
                string folder = MaterialDir + "/" + id;
                EnsureFolder(folder);
                string target = folder + "/" + current.name + ".mat";
                if (current.name.Contains('/') || current.name.Contains('\\') ||
                    current.name.Contains(".."))
                    throw new InvalidOperationException("Unsafe original material name");

                Material material = AssetDatabase.LoadAssetAtPath<Material>(target);
                if (material == null)
                {
                    material = new Material(shader);
                    material.name = current.name;
                    AssetDatabase.CreateAsset(material, target);
                }
                material.shader = shader;
                material.SetColor("_BaseColor", current.color);

                if (!string.IsNullOrEmpty(current.originalTexture))
                {
                    string absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mtlFile),
                        current.originalTexture));
                    string root = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
                    absolute = absolute.Replace('\\', '/');
                    if (!absolute.StartsWith(root + "/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Material texture must remain inside Unity Assets");

                    string assetPath = "Assets/" + absolute.Substring(root.Length + 1);
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture == null)
                        throw new InvalidOperationException("Missing actual original BMP: " + assetPath);
                    material.SetTexture("_BaseMap", texture);
                }
                else
                {
                    material.SetTexture("_BaseMap", null);
                }
                EditorUtility.SetDirty(material);
                if (!output.TryAdd(current.name, material))
                    throw new InvalidOperationException("Duplicate source material: " + current.name);
            }

            foreach (string raw in File.ReadAllLines(mtlFile))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("newmtl ", StringComparison.Ordinal))
                {
                    SaveMaterial();
                    current = new MaterialSpec { name = line.Substring(7).Trim() };
                }
                else if (line.StartsWith("Kd ", StringComparison.Ordinal) && current != null)
                {
                    string[] values = line.Substring(3)
                        .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length != 3)
                        throw new InvalidOperationException("Invalid original RGB colour");
                    float r = float.Parse(values[0], CultureInfo.InvariantCulture);
                    float g = float.Parse(values[1], CultureInfo.InvariantCulture);
                    float b = float.Parse(values[2], CultureInfo.InvariantCulture);
                    current.color = new Color(r, g, b);
                }
                else if (line.StartsWith("map_Kd ", StringComparison.Ordinal) && current != null)
                {
                    current.originalTexture = line.Substring(7).Trim();
                }
            }
            SaveMaterial();
            if (output.Count == 0)
                throw new InvalidOperationException("No source-derived material definitions in " + mtlFile);
            return output;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string folder = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = folder;
            }
        }
    }
}
#endif
