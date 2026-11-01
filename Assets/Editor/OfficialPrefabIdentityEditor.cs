#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    public static class OfficialPrefabIdentityEditor
    {
        [MenuItem("Vigilante 8/Vincular identidade oficial ao prefab selecionado", true)]
        private static bool CanBind()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return path != null && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                   (path.StartsWith("Assets/Resources/Vehicles/", StringComparison.Ordinal) ||
                    path.StartsWith("Assets/Resources/Arenas/", StringComparison.Ordinal));
        }

        [MenuItem("Vigilante 8/Vincular identidade oficial ao prefab selecionado")]
        private static void Bind()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Select an imported official vehicle/arena prefab first");

            GameCatalog catalog = CatalogRepository.Load();
            string id = System.IO.Path.GetFileNameWithoutExtension(path);
            string game = null;

            if (path.StartsWith("Assets/Resources/Vehicles/", StringComparison.Ordinal))
            {
                foreach (var vehicle in catalog.vehicles)
                    if (vehicle.assetId == id) { game = vehicle.game; break; }
            }
            else if (path.StartsWith("Assets/Resources/Arenas/", StringComparison.Ordinal))
            {
                foreach (var arena in catalog.arenas)
                    if (arena.assetId == id) { game = arena.game; break; }
            }

            if (game == null)
                throw new InvalidOperationException("The selected filename is not in the official game catalog: " + id);

            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                OfficialAssetIdentity identity = prefab.GetComponent<OfficialAssetIdentity>();
                if (identity == null) identity = prefab.AddComponent<OfficialAssetIdentity>();
                identity.Configure(id, game);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            Debug.Log("Official identity linked: " + id + " (" + game + ") — still run full build content validation.");
        }
    }
}
#endif
