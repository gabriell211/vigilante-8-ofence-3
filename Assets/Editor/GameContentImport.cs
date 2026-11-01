#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Only imports from a folder that the user deliberately selects.
    /// Never downloads, commits or distributes proprietary game assets.
    /// </summary>
    public static class GameContentImport
    {
        [MenuItem("Vigilante 8/Importar conteúdo local (FBX)")]
        public static void ImportUserContent()
        {
            var root = EditorUtility.OpenFolderPanel("Select locally licensed content", "", "");
            if (string.IsNullOrWhiteSpace(root)) return;
            var catalog = CatalogRepository.Load();

            // Fail before any writes: never silently ship an incomplete roster.
            foreach (var vehicle in catalog.vehicles)
                EnsureSource(root, "Vehicles", vehicle.assetId);
            foreach (var arena in catalog.arenas)
                EnsureSource(root, "Arenas", arena.assetId);

            try
            {
                AssetDatabase.StartAssetEditing();
                CopyTextures(root);
                foreach (var vehicle in catalog.vehicles)
                    CopySource(root, "Vehicles", vehicle.assetId);
                foreach (var arena in catalog.arenas)
                    CopySource(root, "Arenas", arena.assetId);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            foreach (var vehicle in catalog.vehicles)
                CreatePrefab("Vehicles", vehicle.assetId);
            foreach (var arena in catalog.arenas)
                CreatePrefab("Arenas", arena.assetId);
            AssetDatabase.SaveAssets();
            Debug.Log("Vigilante8: user-owned content imported. Check materials, colliders and scale.");
        }

        private static void EnsureSource(string root, string kind, string id)
        {
            var path = Path.Combine(root, kind, id + ".fbx");
            if (!File.Exists(path)) throw new FileNotFoundException("Missing required model: " + path);
        }

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            var parent = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }

        private static void CopySource(string root, string kind, string id)
        {
            var destination = "Assets/GameContent/Private/" + kind;
            EnsureAssetFolder(destination);
            File.Copy(Path.Combine(root, kind, id + ".fbx"), destination + "/" + id + ".fbx", true);
        }

        private static void CopyTextures(string root)
        {
            var textures = Path.Combine(root, "Textures");
            if (!Directory.Exists(textures)) return;
            var destination = "Assets/GameContent/Private/Textures";
            EnsureAssetFolder(destination);
            foreach (var file in Directory.EnumerateFiles(textures, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".png" && ext != ".tga" && ext != ".jpg" && ext != ".exr") continue;
                var relative = Path.GetRelativePath(textures, file).Replace('\\', '/');
                var target = destination + "/" + relative;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void CreatePrefab(string kind, string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/Private/" + kind + "/" + id + ".fbx");
            if (asset == null) throw new InvalidOperationException("FBX cannot be imported: " + id);
            var destination = "Assets/Resources/" + kind;
            EnsureAssetFolder(destination);
            var instance = UnityEngine.Object.Instantiate(asset);
            try
            {
                instance.name = id;
                PrefabUtility.SaveAsPrefabAsset(instance, destination + "/" + id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }

}
#endif
