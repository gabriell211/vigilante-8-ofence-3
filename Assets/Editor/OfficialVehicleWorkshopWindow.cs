#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Vehicle workshop for real imported V82 meshes and physics. It never
    /// publishes a generic arena or bypasses original-content release gates.
    /// </summary>
    public sealed class OfficialVehicleWorkshopWindow : EditorWindow
    {
        private VehicleEntry[] vehicles = Array.Empty<VehicleEntry>();
        private string[] labels = Array.Empty<string>();
        private int selected;
        private Vector2 scroll;

        [MenuItem("Vigilante 8/Migração/Oficina - testar os 18 veículos originais")]
        public static void OpenWorkshop()
        {
            var window = GetWindow<OfficialVehicleWorkshopWindow>("V8 - Oficina de carros");
            window.minSize = new Vector2(460f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            try
            {
                vehicles = CatalogRepository.Load().vehicles
                    .Where(x => x.game == "v82")
                    .OrderBy(x => x.officialName, StringComparer.Ordinal).ToArray();
                labels = vehicles.Select(v => v.officialName + "  /  " + v.id).ToArray();
                if (vehicles.Length != 18)
                    throw new InvalidOperationException("Expected precisely 18 original sequel vehicles");
                selected = Mathf.Clamp(selected, 0, vehicles.Length - 1);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                vehicles = Array.Empty<VehicleEntry>();
                labels = Array.Empty<string>();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(12);
            EditorGUILayout.LabelField("VIGILANTE 8 : 2ND OFFENSE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Oficina de integração — arte 3D original", EditorStyles.largeLabel);
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Somente protótipos de física: os BMP/WAV/OBJ vêm das fontes originais. " +
                "Armas especiais, propriedades físicas, cenas e aparência definitivas não estão homologadas. " +
                "Não são usados no build do jogo.", MessageType.Warning);
            if (vehicles.Length != 18)
            {
                EditorGUILayout.HelpBox("O catálogo oficial está incompleto.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            selected = EditorGUILayout.Popup("Veículo oficial", selected, labels);
            VehicleEntry vehicle = vehicles[selected];
            string path = LegacyVehiclePlaytestBuilder.PrefabDirectory +
                          "/" + vehicle.assetId + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUILayout.LabelField("Modelo PS1", vehicle.officialName);
            EditorGUILayout.LabelField("ID canônico", vehicle.id);
            EditorGUILayout.LabelField("Origem", "Vigilante2Unity / 2nd Offense");
            EditorGUILayout.LabelField("Física protótipo", prefab == null ? "Ainda não preparada" : "Asset de teste encontrado");

            GUILayout.Space(15);
            if (GUILayout.Button("1. Integrar os 18 modelos e WAVs ao Unity", GUILayout.Height(40)))
            {
                try
                {
                    LegacyVehiclePlaytestBuilder.PrepareAll();
                    AssetDatabase.Refresh();
                    Debug.Log("Integration source prefab inventory prepared for all 18 vehicles");
                }
                catch (Exception error) { Debug.LogException(error); }
            }

            using (new EditorGUI.DisabledScope(prefab == null || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("2. Criar cenário técnico do veículo selecionado",
                        GUILayout.Height(39)))
                {
                    try
                    {
                        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                        LegacyVehiclePlaytestBuilder.CreateScene(vehicle.id);
                    }
                    catch (Exception error) { Debug.LogException(error); }
                }
                if (GUILayout.Button("3. Abrir teste de direção (Play Mode)",
                        GUILayout.Height(39)))
                {
                    try
                    {
                        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                        LegacyVehiclePlaytestBuilder.CreateScene(vehicle.id);
                        EditorApplication.isPlaying = true;
                    }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            GUILayout.Space(12);
            EditorGUILayout.HelpBox(
                "O chão de teste não é uma arena oficial. Teclado: WASD/setas para dirigir, " +
                "Shift para frear, Espaço para disparo de diagnóstico.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
