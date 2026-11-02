#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Vigilante8.Content;
using Vigilante8.Gameplay;

namespace Vigilante8.Editor
{
    public static class WindowsReleaseBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string PanelPath = "Assets/UI/GamePanel.asset";

        [MenuItem("Vigilante 8/Preparar cena PC nativa")]
        public static void GenerateStartupScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/UI");
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Gameplay Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.transform.position = new Vector3(0f, 9f, -18f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ChaseCamera>();

            var lightObject = new GameObject("World Sun");
            var sunlight = lightObject.AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var runtime = new GameObject("Vigilante 8 Game");
            UIDocument document = runtime.AddComponent<UIDocument>();
            document.panelSettings = panel;
            runtime.AddComponent<MatchBootstrap>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new BuildFailedException("Unity failed to save startup scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PC startup scene saved: " + ScenePath);
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }

        [MenuItem("Vigilante 8/Validar conteúdo real e pronto para build")]
        public static void ValidateContent()
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
                throw new BuildFailedException("Configure Universal Render Pipeline under Project Settings > Graphics.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new BuildFailedException("Bootstrap scene not generated.");
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) == null)
                throw new BuildFailedException("Saved UI Toolkit PanelSettings asset missing.");

            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled).Select(s => s.path).ToArray();
            if (enabledScenes.Length != 1 || enabledScenes[0] != ScenePath)
                throw new BuildFailedException("Only the official Bootstrap scene may enter the Windows build. " +
                    "Remove vehicle test scenes and other developer previews from Build Settings.");

            GameCatalog catalog = CatalogRepository.Load();
            if (catalog.characters.Length != 23 || catalog.vehicles.Length != 31 ||
                catalog.arenas.Length != 19)
                throw new BuildFailedException("Unified V8 and V82 roster is incomplete.");

            var seenVehicleBodyMeshes = new System.Collections.Generic.HashSet<int>();
            foreach (VehicleEntry vehicle in catalog.vehicles)
            {
                string path = "Assets/Resources/Vehicles/" + vehicle.assetId + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new BuildFailedException("Missing official vehicle: " + vehicle.assetId);

                if (prefab.GetComponentInChildren<OriginalJunctionSourcePreviewIdentity>(true) != null)
                    throw new BuildFailedException("JUNC source-only preview cannot ship as vehicle: " + vehicle.id);
                if (prefab.GetComponent<LegacyVehiclePlaytest>() != null)
                    throw new BuildFailedException("Development-only TMD physics prototype cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalMachineGunSourceTest>() != null)
                    throw new BuildFailedException("Source-only MGun prototype cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalBastionRocketLauncher>() != null ||
                    prefab.GetComponentInChildren<OriginalBastionRocketFlight>(true) != null)
                    throw new BuildFailedException("Source-only Rocket/Bastion test cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalMissileSourceLauncher>() != null ||
                    prefab.GetComponentInChildren<OriginalMissileSourceFlight>(true) != null)
                    throw new BuildFailedException("Source-only Missile flight test cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalMineSourceLauncher>() != null ||
                    prefab.GetComponentInChildren<OriginalMineSourceTrap>(true) != null ||
                    prefab.GetComponentInChildren<OriginalSpecialMineSourceBehavior>(true) != null)
                    throw new BuildFailedException("Source-only Mine test cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalRoadRunnerSourceLauncher>() != null ||
                    prefab.GetComponentInChildren<OriginalRoadRunnerSourceCharge>(true) != null)
                    throw new BuildFailedException("Source-only RoadRunner rocket cannot be released: " + vehicle.id);
                if (prefab.GetComponentInChildren<OriginalBotSourceWeaponAI>(true) != null)
                    throw new BuildFailedException("Source-only AI weapons test cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalRocketSourceInventory>() != null)
                    throw new BuildFailedException("Source-only RocketL ammunition cannot be released: " + vehicle.id);
                if (prefab.GetComponent<OriginalMissileSourceInventory>() != null)
                    throw new BuildFailedException("Source-only MissileL ammunition cannot be released: " + vehicle.id);

                OfficialAssetIdentity identity = prefab.GetComponent<OfficialAssetIdentity>();
                if (identity == null || !identity.Matches(vehicle.id, vehicle.game))
                    throw new BuildFailedException("Wrong or missing official vehicle identity: " + vehicle.id);

                VehicleDrive drive = prefab.GetComponent<VehicleDrive>();
                if (drive == null || prefab.GetComponent<Rigidbody>() == null ||
                    prefab.GetComponentsInChildren<Collider>(true).Length == 0 ||
                    !drive.HasWeapon)
                    throw new BuildFailedException("Original vehicle lacks configured physics or weapon: " + vehicle.id);

                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0 || renderers.Any(r =>
                    r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m == null)))
                    throw new BuildFailedException("Vehicle materials are missing: " + vehicle.id);

                Mesh primaryMesh = prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null).Select(f => f.sharedMesh)
                    .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(f => f.sharedMesh != null).Select(f => f.sharedMesh))
                    .OrderByDescending(mesh => mesh.vertexCount).FirstOrDefault();
                if (primaryMesh == null || primaryMesh.vertexCount < 3 ||
                    string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(primaryMesh)))
                    throw new BuildFailedException("Imported authored vehicle geometry missing: " + vehicle.id);
                if (!seenVehicleBodyMeshes.Add(primaryMesh.GetInstanceID()))
                    throw new BuildFailedException("Repeated vehicle body mesh used as another official car: " + vehicle.id);
            }

            var seenArenaMeshes = new System.Collections.Generic.HashSet<int>();
            foreach (ArenaEntry arena in catalog.arenas)
            {
                string path = "Assets/Resources/Arenas/" + arena.assetId + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new BuildFailedException("Missing official arena: " + arena.assetId);

                if (prefab.GetComponentInChildren<LegacyVehiclePlaytest>(true) != null ||
                    prefab.GetComponentInChildren<OriginalBattleSession>(true) != null ||
                    prefab.GetComponentInChildren<OriginalBattleTargetCoordinator>(true) != null ||
                    prefab.GetComponentInChildren<OriginalBotSourceWeaponAI>(true) != null ||
                    prefab.GetComponentInChildren<OriginalJunctionSourcePreviewIdentity>(true) != null ||
                    prefab.GetComponentInChildren<OriginalStageObjectPreviewIdentity>(true) != null ||
                    prefab.GetComponentInChildren<OriginalTerrainPreviewIdentity>(true) != null)
                    throw new BuildFailedException("Source-only preview embedded in an official arena: " + arena.id);

                OfficialAssetIdentity identity = prefab.GetComponent<OfficialAssetIdentity>();
                if (identity == null || !identity.Matches(arena.id, arena.game))
                    throw new BuildFailedException("Wrong or missing arena identity: " + arena.id);

                ArenaSpawnPoints spawns = prefab.GetComponentInChildren<ArenaSpawnPoints>(true);
                if (spawns == null || !spawns.HasValidSpawns(4) ||
                    prefab.GetComponentsInChildren<Collider>(true).Length == 0)
                    throw new BuildFailedException("Arena collision or four spawn points missing: " + arena.id);

                Mesh primaryMesh = prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null).Select(f => f.sharedMesh)
                    .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(f => f.sharedMesh != null).Select(f => f.sharedMesh))
                    .OrderByDescending(mesh => mesh.vertexCount).FirstOrDefault();
                if (primaryMesh == null || primaryMesh.vertexCount < 3 ||
                    string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(primaryMesh)))
                    throw new BuildFailedException("Actual arena mesh not imported: " + arena.id);
                if (!seenArenaMeshes.Add(primaryMesh.GetInstanceID()))
                    throw new BuildFailedException("Same arena geometry reused for different official stages: " + arena.id);
            }
            Debug.Log("Release asset gate passed: 23 drivers, 31 vehicles, 19 arenas.");
        }

        [MenuItem("Vigilante 8/Build Windows x64 IL2CPP com assets")]
        public static void BuildWindows()
        {
            // The scene is reproducibly generated in Editor, never at player startup.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                GenerateStartupScene();
            ValidateContent();

            PlayerSettings.companyName = "gabriell211";
            PlayerSettings.productName = "Vigilante 8 Offence 3";
            PlayerSettings.bundleVersion = "0.1.0-dev";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,
                ScriptingImplementation.IL2CPP);

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string buildRoot = Path.Combine(root, "Builds");
            string outputDir = Path.Combine(buildRoot, "Windows-x64");
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);
            string executable = Path.Combine(outputDir, "Vigilante8Offence3.exe");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Unity build failed: " + report.summary.result);
            if (!File.Exists(executable) ||
                !Directory.Exists(Path.Combine(outputDir, "Vigilante8Offence3_Data")))
                throw new BuildFailedException("Unity player exe or associated data files are missing.");

            string manifest = "{\n" +
                "  \"product\": \"Vigilante 8 Offence 3\",\n" +
                "  \"version\": \"" + PlayerSettings.bundleVersion + "\",\n" +
                "  \"platform\": \"win-x64\",\n" +
                "  \"unity\": \"" + Application.unityVersion + "\",\n" +
                "  \"assetCatalog\": {\"characters\": 23, \"vehicles\": 31, \"arenas\": 19}\n" +
                "}\n";
            File.WriteAllText(Path.Combine(outputDir, "release-manifest.json"), manifest);
            string zip = Path.Combine(buildRoot,
                "Vigilante8Offence3-" + PlayerSettings.bundleVersion + "-win-x64.zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(outputDir, zip, CompressionLevel.Optimal, false);
            using (var stream = File.OpenRead(zip))
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", "").ToLowerInvariant();
                File.WriteAllText(zip + ".sha256", hash + "  " + Path.GetFileName(zip) + "\n");
            }
            Debug.Log("Windows native package created and verified: " + zip);
        }

        public static void BuildFromCommandLine() => BuildWindows();
    }

    public sealed class RequiredWindowsAssetsGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => WindowsReleaseBuilder.ValidateContent();
    }
}
#endif
