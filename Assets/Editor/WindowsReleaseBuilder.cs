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

            GameCatalog catalog = CatalogRepository.Load();
            if (catalog.characters.Length != 23 || catalog.vehicles.Length != 31 ||
                catalog.arenas.Length != 19)
                throw new BuildFailedException("Unified V8 and V82 roster is incomplete.");

            foreach (VehicleEntry vehicle in catalog.vehicles)
            {
                string path = "Assets/Resources/Vehicles/" + vehicle.assetId + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new BuildFailedException("Missing authored vehicle: " + vehicle.assetId);

                VehicleDrive drive = prefab.GetComponent<VehicleDrive>();
                if (drive == null || prefab.GetComponent<Rigidbody>() == null ||
                    prefab.GetComponentsInChildren<Collider>(true).Length == 0 ||
                    !drive.HasWeapon)
                    throw new BuildFailedException("Vehicle physics, colliders or weapons missing: " + vehicle.assetId);

                bool mesh = prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Any(x => x.sharedMesh != null) ||
                    prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Any(x => x.sharedMesh != null);
                if (!mesh || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new BuildFailedException("Missing real authored 3D geometry: " + vehicle.assetId);
            }

            foreach (ArenaEntry arena in catalog.arenas)
            {
                string path = "Assets/Resources/Arenas/" + arena.assetId + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new BuildFailedException("Missing authored arena: " + arena.assetId);
                ArenaSpawnPoints spawns = prefab.GetComponentInChildren<ArenaSpawnPoints>(true);
                if (spawns == null || !spawns.HasValidSpawns(4) ||
                    prefab.GetComponentsInChildren<Collider>(true).Length == 0)
                    throw new BuildFailedException("Arena geometry or 4 spawn points are missing: " + arena.assetId);
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
