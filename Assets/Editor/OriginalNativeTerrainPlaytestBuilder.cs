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
using Vigilante8.Gameplay;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Development-only: two real TMD car prototypes driving and firing test
    /// projectiles on one original V82 native Geo.asset terrain mesh.
    /// Terrain collisions, procedural safe spots and weapon timings are NOT
    /// official PS1 gameplay. No generated scene enters the shipping build.
    /// </summary>
    public static class OriginalNativeTerrainPlaytestBuilder
    {
        public const string ScenePath = "Assets/Scenes/OriginalNativeTerrainDriveTest.unity";
        private const string MeshDir = "Assets/GameContent/ImportedOriginalTerrainV82/";
        private const string VehicleDir = "Assets/GameContent/PlaytestVehiclesV82/";

        public static void Create(string stageId, string playerVehicleId, string enemyVehicleId)
        {
            CreateBattle(stageId, playerVehicleId, new[] { enemyVehicleId });
        }

        /// <summary>
        /// Builds a development-only 1-v-1 to 1-v-3 encounter using only original
        /// 2nd Offense vehicle meshes and original source terrain. Never certifies
        /// original spawn points, weapon effects or stage gameplay.
        /// </summary>
        public static void CreateBattle(string stageId, string playerVehicleId,
            IEnumerable<string> opponentVehicleIds)
        {
            if (string.IsNullOrWhiteSpace(stageId) ||
                string.IsNullOrWhiteSpace(playerVehicleId) || opponentVehicleIds == null)
                throw new ArgumentException("A preserved terrain and source cars are required");
            string[] enemyVehicleIds = opponentVehicleIds.ToArray();
            if (enemyVehicleIds.Length < 1 || enemyVehicleIds.Length > 3 ||
                enemyVehicleIds.Any(string.IsNullOrWhiteSpace) ||
                enemyVehicleIds.Contains(playerVehicleId) ||
                enemyVehicleIds.Distinct(StringComparer.Ordinal).Count() != enemyVehicleIds.Length)
                throw new ArgumentException("Choose one to three different original opponents");

            GameCatalog catalog = CatalogRepository.Load();
            if (!catalog.arenas.Any(a => a.game == "v82" && a.id == stageId) ||
                stageId == "v82_meteor_crater")
                throw new ArgumentException("Only the 7 preserved original terrains are allowed");
            string[] selectedIds = new[] { playerVehicleId }.Concat(enemyVehicleIds).ToArray();
            VehicleEntry[] participants = selectedIds.Select(id =>
                catalog.vehicles.SingleOrDefault(v => v.game == "v82" && v.id == id)).ToArray();
            if (participants.Any(v => v == null) ||
                participants.Select(v => v.characterId).Distinct(StringComparer.Ordinal).Count()
                    != participants.Length)
                throw new ArgumentException("All vehicles must be original and have distinct official pilots");

            OriginalTerrainSourceImporter.ImportAll();
            LegacyVehiclePlaytestBuilder.PrepareAll();
            Mesh sourceTerrain = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + stageId + ".asset");
            if (sourceTerrain == null || sourceTerrain.vertexCount < 3 ||
                sourceTerrain.subMeshCount < 1)
                throw new InvalidOperationException("Authentic native source terrain not imported");

            GameObject playerPrefab = VerifiedCar(playerVehicleId);
            GameObject[] enemyPrefabs = enemyVehicleIds.Select(VerifiedCar).ToArray();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = new GameObject("GENUINE V82 TERRAIN MESH - UNVERIFIED COLLISION");
            ground.AddComponent<MeshFilter>().sharedMesh = sourceTerrain;
            // The original geometry is visible with a clearly labeled *diagnostic*
            // material. This is not a port of the stage's original PS1 textures.
            var terrainRenderer = ground.AddComponent<MeshRenderer>();
            Material diagnostic = EnsureDiagnosticTerrainMaterial();
            terrainRenderer.sharedMaterials = Enumerable.Repeat(
                diagnostic, sourceTerrain.subMeshCount).ToArray();
            // This is collision against the actual archived native mesh, not a
            // synthetic floor, but it is NOT original VigTerrain collision behavior.
            var collider = ground.AddComponent<MeshCollider>();
            collider.sharedMesh = sourceTerrain;
            collider.convex = false;
            ground.isStatic = true;
            var originalIdentity = new GameObject("Development scene warning");
            originalIdentity.transform.SetParent(ground.transform, false);
            originalIdentity.AddComponent<OriginalTerrainPreviewIdentity>().Configure(
                stageId, AssetDatabase.AssetPathToGUID(MeshDir + stageId + ".asset"),
                sourceTerrain.vertexCount, CountTriangles(sourceTerrain));

            Physics.SyncTransforms();
            Vector3[] points = FindGroundSpawns(collider, participants.Length);
            GameObject player = SpawnVehicle(playerPrefab, points[0], points[1], scene);
            player.AddComponent<PlayerVehicleInput>();
            var opponentDrives = new List<VehicleDrive>(enemyVehicleIds.Length);
            for (int i = 1; i < points.Length; i++)
            {
                GameObject opponent = SpawnVehicle(enemyPrefabs[i - 1], points[i], points[0], scene);
                opponent.AddComponent<BotVehicleInput>().target = player.transform;
                opponentDrives.Add(opponent.GetComponent<VehicleDrive>());
            }

            var sessionObject = new GameObject("ORIGINAL V82 BATTLE - DEVELOPMENT ONLY");
            var session = sessionObject.AddComponent<OriginalBattleSession>();
            session.Configure(stageId, playerVehicleId, player.GetComponent<VehicleDrive>(),
                enemyVehicleIds, opponentDrives.ToArray());

            var cameraObject = new GameObject("Original terrain chase camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            camera.farClipPlane = Mathf.Max(2000f, sourceTerrain.bounds.size.magnitude * 2f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ChaseCamera>().target = player.transform;
            cameraObject.transform.position = player.transform.position +
                new Vector3(0f, 5f, -12f);
            var light = new GameObject("Level inspection sunlight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.43f, 0.52f);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save genuine terrain physics test");
            if (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath))
                throw new InvalidOperationException("Development terrain physics test must not ship");
            Debug.Log("Source-derived 1-v-" + enemyVehicleIds.Length +
                " terrain battle ready: " + stageId + " / " + playerVehicleId +
                " vs " + string.Join(", ", enemyVehicleIds) +
                ". Original special weapons, exact collision and official spawn points NOT ready.");
        }

        private static Material EnsureDiagnosticTerrainMaterial()
        {
            const string materialFolder = "Assets/GameContent/PreviewTerrainMaterialsV82";
            const string materialPath = materialFolder + "/Diagnostic.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("URP inspection shader is missing");
            if (!AssetDatabase.IsValidFolder(materialFolder))
                AssetDatabase.CreateFolder("Assets/GameContent", "PreviewTerrainMaterialsV82");
            var material = new Material(shader) { name = "SOURCE TERRAIN - NOT ORIGINAL TEXTURE" };
            material.SetColor("_BaseColor", new Color(0.73f, 0.81f, 0.91f));
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static int CountTriangles(Mesh mesh)
        {
            ulong indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Unsupported original terrain topology");
                indices += mesh.GetIndexCount(i);
            }
            if (indices < 3 || indices % 3 != 0)
                throw new InvalidOperationException("Invalid authentic source terrain mesh");
            return checked((int)(indices / 3));
        }

        private static GameObject VerifiedCar(string id)
        {
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(
                VehicleDir + id + ".prefab");
            if (original == null || original.GetComponent<LegacyVehiclePlaytest>() == null ||
                original.GetComponent<LegacyVehiclePlaytest>().OriginalVehicleId != id ||
                original.GetComponent<OfficialAssetIdentity>() != null ||
                original.GetComponent<LegacyVehicleHandlingProfile>() == null ||
                original.GetComponent<OriginalAudioBank>() == null ||
                original.GetComponent<Rigidbody>() == null ||
                original.GetComponent<VehicleDrive>()?.HasWeapon != true ||
                !original.GetComponentsInChildren<MeshFilter>(true)
                    .Any(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 2))
                throw new InvalidOperationException("Original source car prototype is not valid: " + id);
            return original;
        }

        private static Vector3[] FindGroundSpawns(MeshCollider source, int count)
        {
            // Locations are inferred from the genuine triangles, never claimed
            // as official stage spawns. Fail closed if no safe surface is found.
            Bounds area = source.bounds;
            if (area.size.x < 3f || area.size.z < 3f)
                throw new InvalidOperationException("Imported source mesh has no suitable ground area");
            if (count < 2 || count > 4)
                throw new ArgumentOutOfRangeException(nameof(count));
            var chosen = new List<Vector3>(count);
            float required = Mathf.Max(8f, Mathf.Min(area.size.x, area.size.z) * 0.1f);
            for (int zi = 1; zi <= 11; zi++)
            for (int xi = 1; xi <= 11; xi++)
            {
                float x = Mathf.Lerp(area.min.x, area.max.x, xi / 12f);
                float z = Mathf.Lerp(area.min.z, area.max.z, zi / 12f);
                Ray ray = new Ray(new Vector3(x, area.max.y + 25f, z), Vector3.down);
                if (!source.Raycast(ray, out RaycastHit hit, area.size.y + 50f) ||
                    hit.normal.y < 0.72f ||
                    chosen.Any(p => Vector3.Distance(p, hit.point) < required))
                    continue;
                chosen.Add(hit.point);
                if (chosen.Count == count) return chosen.ToArray();
            }
            throw new InvalidOperationException(
                "Original terrain could not provide two safe source-derived driving positions");
        }

        private static GameObject SpawnVehicle(GameObject prefab, Vector3 spawn,
            Vector3 toward, Scene scene)
        {
            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (car == null) throw new InvalidOperationException("Source vehicle failed to instantiate");
            var collider = car.GetComponentInChildren<Collider>(true);
            if (collider == null) throw new InvalidOperationException("Imported vehicle has no collider");
            float bottomOffset = car.transform.position.y - collider.bounds.min.y;
            car.transform.position = spawn + Vector3.up * (Mathf.Max(0.2f, bottomOffset) + 0.5f);
            Vector3 heading = toward - spawn;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
                car.transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
            return car;
        }
    }

    public sealed class OriginalNativeTerrainPlaytestWindow : EditorWindow
    {
        private int stageIndex, carIndex, opponentIndex = 1, opponentCount = 1;
        [MenuItem("Vigilante 8/Migração/Dirigir em terreno real V82 (teste, não release)")]
        public static void ShowWindow() =>
            GetWindow<OriginalNativeTerrainPlaytestWindow>("V8 original terrain test");

        private void OnGUI()
        {
            var catalog = CatalogRepository.Load();
            var terrains = catalog.arenas.Where(a => a.game == "v82" &&
                a.id != "v82_meteor_crater").OrderBy(a => a.id, StringComparer.Ordinal).ToArray();
            var vehicles = catalog.vehicles.Where(v => v.game == "v82")
                .OrderBy(v => v.id, StringComparer.Ordinal).ToArray();
            if (terrains.Length != 7 || vehicles.Length != 18)
            {
                EditorGUILayout.HelpBox("Original seven-stage / 18-car catalog is incomplete", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                "Uses authentic V82 terrain and TMD car meshes. Terrain collider, spawn " +
                "points and projectile are DEVELOPMENT approximations, not PS1-certified.", MessageType.Warning);
            stageIndex = EditorGUILayout.Popup("Terreno original",
                Mathf.Clamp(stageIndex, 0, terrains.Length - 1),
                terrains.Select(a => a.name).ToArray());
            carIndex = EditorGUILayout.Popup("Seu carro / piloto original",
                Mathf.Clamp(carIndex, 0, vehicles.Length - 1),
                vehicles.Select(v => v.officialName).ToArray());
            opponentIndex = EditorGUILayout.Popup("Carro adversário",
                Mathf.Clamp(opponentIndex, 0, vehicles.Length - 1),
                vehicles.Select(v => v.officialName).ToArray());
            opponentCount = EditorGUILayout.IntSlider("Adversários (1 a 3)", opponentCount, 1, 3);
            VehicleEntry selected = vehicles[carIndex];
            VehicleEntry firstOpponent = vehicles[opponentIndex];
            VehicleEntry[] extraOpponents = vehicles.Where(v =>
                v.characterId != selected.characterId &&
                v.characterId != firstOpponent.characterId)
                .OrderBy(v => v.id, StringComparer.Ordinal)
                .Take(opponentCount - 1).ToArray();
            bool validSelection = selected.characterId != firstOpponent.characterId &&
                extraOpponents.Length == opponentCount - 1;
            if (!validSelection)
                EditorGUILayout.HelpBox("Choose a different official vehicle/pilot for the opponent.",
                    MessageType.Error);
            using (new EditorGUI.DisabledScope(!validSelection))
            {
                if (GUILayout.Button("Criar combate real de teste (não release)"))
                    OriginalNativeTerrainPlaytestBuilder.CreateBattle(
                        terrains[stageIndex].id, selected.id,
                        new[] { firstOpponent.id }.Concat(extraOpponents.Select(v => v.id)));
            }
            EditorGUILayout.LabelField(
                "1 a 3 adversários com carros/pilotos próprios. Cena excluída do build Windows.");
        }
    }
}
#endif
