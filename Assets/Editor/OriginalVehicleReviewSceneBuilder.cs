#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    /// Inspection-only scene composed entirely of source-derived V82 vehicle
    /// meshes, original texture associations and archived WAV banks.
    /// Not included in a release or connected to the gameplay launch scene.
    /// </summary>
    public static class OriginalVehicleReviewSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/OriginalVehicleReview.unity";
        private const string PreviewFolder = "Assets/GameContent/PreviewPrefabsV82";
        private const string GalleryMenu = "Vigilante 8/Migração/Gerar galeria 3D dos 18 carros oficiais";
        private const int Columns = 6;

        [MenuItem(GalleryMenu)]
        public static void BuildGallery()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Saia do modo Play antes de gerar a galeria dos carros.");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            OriginalVehiclePreviewBuilder.PreparePreviewPrefabs();
            OriginalAudioBankBinder.AttachToInspectionPrefabs();

            VehicleEntry[] vehicles = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").OrderBy(v => v.id, StringComparer.Ordinal).ToArray();
            if (vehicles.Length != 18 || vehicles.Select(v => v.id).Distinct().Count() != 18)
                throw new InvalidOperationException("Official 2nd Offense vehicle roster is incomplete");

            var models = new List<GameObject>();
            var references = new HashSet<GameObject>();
            foreach (VehicleEntry vehicle in vehicles)
            {
                string path = PreviewFolder + "/" + vehicle.id + ".prefab";
                GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (original == null || original.GetComponent<OriginalVehiclePreviewIdentity>() == null)
                    throw new InvalidOperationException("Original 3D car preview is missing: " + path);
                OriginalAudioBank bank = original.GetComponent<OriginalAudioBank>();
                if (bank == null || bank.SourceId != vehicle.id || bank.Count < 1)
                    throw new InvalidOperationException("Real source audio bank not attached to " + vehicle.id);
                if (!references.Add(original))
                    throw new InvalidOperationException("One official vehicle cannot reuse another car prefab");
                models.Add(original);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("ORIGINAL V82 CAR INSPECTION - NOT FOR RELEASE");
            var spawned = new List<GameObject>();
            float maxWidth = 1f, maxDepth = 1f, maxHeight = 1f;

            foreach (GameObject original in models)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(original, scene);
                if (instance == null)
                    throw new InvalidOperationException("Failed to instantiate original car: " + original.name);
                instance.transform.SetParent(root.transform, true);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Original vehicle has no mesh: " + original.name);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.sqrMagnitude < 0.001f)
                    throw new InvalidOperationException("Imported PS1 car geometry is empty: " + original.name);
                maxWidth = Mathf.Max(maxWidth, bounds.size.x);
                maxDepth = Mathf.Max(maxDepth, bounds.size.z);
                maxHeight = Mathf.Max(maxHeight, bounds.size.y);
                spawned.Add(instance);
            }

            float spacingX = maxWidth + 4f;
            float spacingZ = maxDepth + 5f;
            for (int i = 0; i < spawned.Count; i++)
            {
                var instance = spawned[i];
                int col = i % Columns, row = i / Columns;
                Vector3 target = new Vector3((col - (Columns - 1) * 0.5f) * spacingX,
                    0f, (row - 1f) * spacingZ);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                instance.transform.position = target - bounds.center + Vector3.up * bounds.extents.y;
                instance.name = vehicles[i].officialName + " | " + vehicles[i].id + " | preview only";
            }

            var lightObject = new GameObject("Gallery Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var cameraObject = new GameObject("Original Car Gallery Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            camera.fieldOfView = 45f;
            cameraObject.AddComponent<AudioListener>();
            float extent = Mathf.Max(Columns * spacingX, 3f * spacingZ);
            cameraObject.transform.position = new Vector3(0f,
                extent * 0.85f + maxHeight, -extent * 0.95f);
            cameraObject.transform.LookAt(new Vector3(0f, maxHeight * 0.5f, 0f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.39f, 0.48f);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the original car review scene");
            if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                throw new InvalidOperationException("Inspection scene must never be part of the Windows build");
            AssetDatabase.SaveAssets();
            Debug.Log("Original vehicle review saved: " + ScenePath +
                      " (18 distinct model sources, 18 original audio banks). " +
                      "PS1 parts/UVs require visual QA; this is NOT a playable PC release.");
        }

        [MenuItem(GalleryMenu, true)]
        private static bool CanBuildGallery() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif
