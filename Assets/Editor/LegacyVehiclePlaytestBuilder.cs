#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vigilante8.Content;
using Vigilante8.Gameplay;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Builds real TMD-derived 2nd Offense meshes into *development-only*
    /// physics and combat prototypes. Original weapon geometry and stage
    /// collisions still need to be ported before any production release.
    /// </summary>
    public static class LegacyVehiclePlaytestBuilder
    {
        public const string PrefabDirectory = "Assets/GameContent/PlaytestVehiclesV82";
        public const string ScenePath = "Assets/Scenes/LegacyVehiclePhysicsTest.unity";
        private const string ProjectilePath = "Assets/GameContent/PlaytestWeaponsV82/PhysicsTestProjectile.prefab";
        private const string UpstreamRevision = "d20883df0d5da3ba517142fb4d486db1f5e673fb";
        private const string HandlingPath = "Assets/GameContent/LegacyHandlingV82/manifest.json";

        [Serializable]
        private sealed class RawHandlingManifest
        {
            public int schemaVersion;
            public string sourceCommit;
            public string status;
            public RawCar[] vehicles;
        }

        [Serializable]
        private sealed class RawCar
        {
            public string vehicleId;
            public string sourceEnum;
            public int rawHalfHealth, rawLightness, dat0e, dat0f, dat10, dat11;
            public int dat12, dat13, dat15, dat2a;
        }

        private static Dictionary<string, RawCar> LoadSourceHandling(VehicleEntry[] expected)
        {
            TextAsset file = AssetDatabase.LoadAssetAtPath<TextAsset>(HandlingPath);
            if (file == null) throw new InvalidOperationException("Missing source vehicle handling data");
            var manifest = JsonUtility.FromJson<RawHandlingManifest>(file.text);
            if (manifest == null || manifest.schemaVersion != 1 ||
                manifest.sourceCommit != UpstreamRevision ||
                manifest.status != "authentic_raw_vehicle_config_provisional_unity_handling_not_certified" ||
                manifest.vehicles == null || manifest.vehicles.Length != 18)
                throw new InvalidOperationException("Unverified original GameManager vehicle table");
            var ids = new HashSet<string>(expected.Select(v => v.id), StringComparer.Ordinal);
            var profiles = new Dictionary<string, RawCar>(StringComparer.Ordinal);
            foreach (RawCar entry in manifest.vehicles)
            {
                if (entry == null || !ids.Contains(entry.vehicleId) ||
                    string.IsNullOrWhiteSpace(entry.sourceEnum) ||
                    entry.rawHalfHealth <= 0 || entry.rawLightness <= 0 ||
                    !profiles.TryAdd(entry.vehicleId, entry))
                    throw new InvalidOperationException("Duplicated or invalid original vehicle handling data");
            }
            if (profiles.Count != expected.Length)
                throw new InvalidOperationException("Missing official source vehicle handling data");
            return profiles;
        }

        [MenuItem("Vigilante 8/Migração/Preparar todos os 18 carros para teste de física")]
        public static void PrepareAll()
        {
            OriginalVehiclePreviewBuilder.PreparePreviewPrefabs();
            OriginalAudioBankBinder.AttachToInspectionPrefabs();
            Projectile testProjectile = EnsureProjectile();
            VehicleEntry[] vehicles = CatalogRepository.Load().vehicles
                .Where(v => v.game == "v82").OrderBy(v => v.id, StringComparer.Ordinal).ToArray();
            if (vehicles.Length != 18 || vehicles.Select(v => v.id).Distinct().Count() != 18)
                throw new InvalidOperationException("The original 18-vehicle sequel roster is missing.");

            Dictionary<string, RawCar> handling = LoadSourceHandling(vehicles);
            EnsureFolder(PrefabDirectory);
            foreach (VehicleEntry vehicle in vehicles)
            {
                string source = "Assets/GameContent/PreviewPrefabsV82/" + vehicle.assetId + ".prefab";
                GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if (preview == null ||
                    preview.GetComponent<OriginalVehiclePreviewIdentity>()?.VehicleId != vehicle.id ||
                    preview.GetComponent<OriginalAudioBank>()?.SourceId != vehicle.id)
                    throw new InvalidOperationException("Missing original geometry or indexed WAV bank: " + vehicle.id);
            }

            foreach (VehicleEntry vehicle in vehicles)
            {
                string source = "Assets/GameContent/PreviewPrefabsV82/" + vehicle.assetId + ".prefab";
                string destination = PrefabDirectory + "/" + vehicle.assetId + ".prefab";
                GameObject instance = PrefabUtility.LoadPrefabContents(source);
                try
                {
                    if (instance.GetComponent<OfficialAssetIdentity>() != null)
                        throw new InvalidOperationException("Cannot turn a certified asset into a prototype");
                    if (instance.GetComponent<LegacyVehiclePlaytest>() == null)
                        instance.AddComponent<LegacyVehiclePlaytest>().Configure(vehicle.id, UpstreamRevision);

                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0 || renderers.Any(r => r.sharedMaterials.Length == 0 ||
                        r.sharedMaterials.Any(m => m == null)))
                        throw new InvalidOperationException("Real original materials missing: " + vehicle.id);

                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers.Skip(1))
                        bounds.Encapsulate(renderer.bounds);
                    if (bounds.size.x < 0.1f || bounds.size.z < 0.1f)
                        throw new InvalidOperationException("Empty source body geometry: " + vehicle.id);

                    var box = instance.GetComponent<BoxCollider>() ??
                              instance.AddComponent<BoxCollider>();
                    box.center = instance.transform.InverseTransformPoint(bounds.center);
                    box.size = new Vector3(
                        Mathf.Max(0.25f, bounds.size.x),
                        Mathf.Max(0.25f, bounds.size.y),
                        Mathf.Max(0.25f, bounds.size.z));

                    Rigidbody rigidbody = instance.GetComponent<Rigidbody>() ??
                        instance.AddComponent<Rigidbody>();
                    rigidbody.useGravity = true;
                    RawCar original = handling[vehicle.id];
                    LegacyVehicleHandlingProfile stats = instance.GetComponent<LegacyVehicleHandlingProfile>() ??
                        instance.AddComponent<LegacyVehicleHandlingProfile>();
                    stats.Configure(vehicle.id, original.sourceEnum, original.rawHalfHealth,
                        original.rawLightness, original.dat0e, original.dat0f,
                        original.dat10, original.dat11, original.dat12,
                        original.dat13, original.dat15, original.dat2a);
                    rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                    var drive = instance.GetComponent<VehicleDrive>() ??
                        instance.AddComponent<VehicleDrive>();
                    stats.ApplyProvisionalHandling(drive);
                    rigidbody.mass = drive.massKg;
                    var muzzle = instance.transform.Find("PLAYTEST MUZZLE - NOT ORIGINAL");
                    if (muzzle == null)
                    {
                        var mount = new GameObject("PLAYTEST MUZZLE - NOT ORIGINAL");
                        muzzle = mount.transform;
                        muzzle.SetParent(instance.transform, false);
                    }
                    muzzle.localPosition = box.center + new Vector3(0f,
                        Mathf.Max(0.05f, box.size.y * 0.1f), box.size.z * 0.53f);
                    muzzle.localRotation = Quaternion.identity;
                    drive.ConfigurePlaytestLoadout(muzzle, testProjectile);
                    // The preserved MGun.UpdateW state machine is available only
                    // on authentic, nonrelease 2nd Offense vehicle prefabs.
                    if (instance.GetComponent<OriginalMachineGunSourceTest>() == null)
                        instance.AddComponent<OriginalMachineGunSourceTest>();
                    if (instance.GetComponent<OriginalBastionRocketLauncher>() == null)
                        instance.AddComponent<OriginalBastionRocketLauncher>();
                    if (instance.GetComponent<OriginalMissileSourceLauncher>() == null)
                        instance.AddComponent<OriginalMissileSourceLauncher>();
                    if (instance.GetComponent<OriginalMineSourceLauncher>() == null)
                        instance.AddComponent<OriginalMineSourceLauncher>();
                    if (instance.GetComponent<OriginalRoadRunnerSourceLauncher>() == null)
                        instance.AddComponent<OriginalRoadRunnerSourceLauncher>();
                    if (!drive.HasWeapon)
                        throw new InvalidOperationException("Test drive loadout invalid: " + vehicle.id);

                    if (PrefabUtility.SaveAsPrefabAsset(instance, destination) == null)
                        throw new InvalidOperationException("Could not save imported physics prototype: " + vehicle.id);
                }
                finally { PrefabUtility.UnloadPrefabContents(instance); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("18 real source-derived vehicle *physics prototypes* generated outside Resources. " +
                      "Each uses its own authentic GameManager stats and provisional Unity tuning. " +
                      "Source MGun cadence and Rocket/Bastion homing phases are under inspection; exact PS1 physics, rocket damage/mesh, remaining specials and final handling NOT certified.");
        }

        private static Projectile EnsureProjectile()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            if (original != null)
            {
                var component = original.GetComponent<Projectile>();
                if (component == null || original.GetComponent<Rigidbody>() == null ||
                    original.GetComponent<Collider>() == null)
                    throw new InvalidOperationException("Corrupt development projectile prefab");
                return component;
            }
            EnsureFolder("Assets/GameContent/PlaytestWeaponsV82");
            var projectile = new GameObject("INVISIBLE PHYSICS TEST PROJECTILE - NOT ORIGINAL");
            try
            {
                var body = projectile.AddComponent<Rigidbody>();
                body.mass = 0.15f;
                body.useGravity = false;
                var collider = projectile.AddComponent<SphereCollider>();
                collider.radius = 0.14f;
                Projectile component = projectile.AddComponent<Projectile>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePath);
                if (saved == null)
                    throw new InvalidOperationException("Unable to save development-only physics projectile");
                return saved.GetComponent<Projectile>();
            }
            finally { UnityEngine.Object.DestroyImmediate(projectile); }
        }

        [MenuItem("Vigilante 8/Migração/Criar cenário técnico de física do carro selecionado")]
        public static void CreateSelectedPhysicsTest()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            string id = "v82_tsunami";
            GameObject selected = Selection.activeObject as GameObject;
            if (selected != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selected);
                if (selectedPath.StartsWith(PrefabDirectory + "/", StringComparison.Ordinal) &&
                    selectedPath.EndsWith(".prefab", StringComparison.Ordinal))
                    id = Path.GetFileNameWithoutExtension(selectedPath);
            }
            CreateScene(id);
        }

        public static void CreateScene(string officialId)
        {
            if (string.IsNullOrWhiteSpace(officialId) ||
                !CatalogRepository.Load().vehicles.Any(v => v.id == officialId && v.game == "v82"))
                throw new ArgumentException("Only one of the 18 official 2nd Offense vehicles may be tested");

            string path = PrefabDirectory + "/" + officialId + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new InvalidOperationException("Run PrepareAll before creating the test scene");
            LegacyVehiclePlaytest source = asset.GetComponent<LegacyVehiclePlaytest>();
            if (source == null || source.OriginalVehicleId != officialId ||
                asset.GetComponent<OfficialAssetIdentity>() != null)
                throw new InvalidOperationException("Only an explicit nonrelease car prototype is allowed");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = new GameObject("INVISIBLE TEST COLLIDER - NOT A VIGILANTE 8 ARENA");
            var collision = floor.AddComponent<BoxCollider>();
            collision.center = new Vector3(0f, -0.5f, 0f);
            collision.size = new Vector3(200f, 1f, 200f);

            var car = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            if (car == null)
                throw new InvalidOperationException("Could not instantiate authentic source car mesh");
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (first) throw new InvalidOperationException("No source body geometry for driving");
            car.transform.position = Vector3.up * (-bounds.min.y + 0.1f);
            car.AddComponent<PlayerVehicleInput>();

            var cameraObject = new GameObject("Source vehicle chase camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.04f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ChaseCamera>().target = car.transform;
            cameraObject.transform.SetPositionAndRotation(
                car.transform.position + new Vector3(0f, 4f, -10f), Quaternion.identity);

            var sun = new GameObject("Inspection lighting");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(46f, -28f, 0f);
            EnsureFolder("Assets/Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save development-only driving scene");

            // Do not add this test scene to the PC player build.
            if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                throw new InvalidOperationException("A developer test surface cannot be shipped as an arena");
            Debug.Log("Created development physics test: " + officialId +
                      ". No official arena is shown and special weapons are not ported.");
        }

        private static void EnsureFolder(string folder)
        {
            string[] elements = folder.Split('/');
            string parent = elements[0];
            for (int i = 1; i < elements.Length; i++)
            {
                string next = parent + "/" + elements[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, elements[i]);
                parent = next;
            }
        }
    }
}
#endif
