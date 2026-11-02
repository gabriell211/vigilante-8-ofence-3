#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Browser for genuine, GUID-verified XOBF stage WAVs. Selecting a clip opens
    /// its native Unity Inspector audio preview without guessing its gameplay role.
    /// </summary>
    public sealed class OriginalStageAudioReviewWindow : EditorWindow
    {
        private int stageIndex;
        private Vector2 scroll;

        [MenuItem("Vigilante 8/Migração/Inspecionar WAVs reais das 8 fases V82")]
        public static void Open()
        {
            var window = GetWindow<OriginalStageAudioReviewWindow>("V82 original stage WAVs");
            window.minSize = new Vector2(470f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VIGILANTE 8 : 2ND OFFENSE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Amostras WAV originais por banco XOBF", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "Fonte original: WAV com índice e GUID do arquivo de origem. " +
                "Motor, cenário, explosão ou arma não são classificados nesta ferramenta. " +
                "Nenhum som é reproduzido automaticamente ou enviado para uma release.",
                MessageType.Info);
            if (GUILayout.Button("Importar objetos e anexar os 8 bancos WAV originais"))
            {
                try
                {
                    OriginalStageObjectReviewBuilder.PrepareAll();
                    AssetDatabase.Refresh();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }

            ArenaEntry[] stages;
            try
            {
                stages = CatalogRepository.Load().arenas
                    .Where(a => a.game == "v82")
                    .OrderBy(a => a.id, StringComparer.Ordinal).ToArray();
                if (stages.Length != 8) throw new InvalidOperationException("Expected eight source stages");
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
                return;
            }
            stageIndex = EditorGUILayout.Popup("Fase original",
                Mathf.Clamp(stageIndex, 0, stages.Length - 1),
                stages.Select(x => x.name).ToArray());

            ArenaEntry selected = stages[stageIndex];
            string path = "Assets/GameContent/PreviewStageObjectsV82/" + selected.id + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var identity = prefab == null ? null :
                prefab.GetComponent<OriginalStageObjectPreviewIdentity>();
            OriginalAudioBank bank = prefab == null ? null : prefab.GetComponent<OriginalAudioBank>();
            if (identity == null || identity.StageId != selected.id ||
                identity.IsReleaseApproved || bank == null || bank.SourceId != selected.id ||
                bank.Count == 0 || prefab.GetComponent<OfficialAssetIdentity>() != null)
            {
                EditorGUILayout.HelpBox(
                    "Ainda não há banco WAV validado para esta fase. Execute a importação acima. " +
                    "Isso não cria arena jogável.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("ID canônico", selected.id);
            EditorGUILayout.LabelField("Amostras do banco", bank.Count.ToString());
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var serialized = new SerializedObject(bank);
            SerializedProperty array = serialized.FindProperty("samples");
            if (array == null || array.arraySize != bank.Count)
            {
                EditorGUILayout.HelpBox("O banco original tem amostras inconsistentes.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }
            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                int originalIndex = entry.FindPropertyRelative("originalIndex").intValue;
                AudioClip clip = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Índice original " + originalIndex,
                    GUILayout.Width(125f));
                EditorGUILayout.ObjectField(clip, typeof(AudioClip), false);
                using (new EditorGUI.DisabledScope(clip == null))
                {
                    if (GUILayout.Button("Abrir WAV", GUILayout.Width(90f)))
                    {
                        Selection.activeObject = clip;
                        EditorGUIUtility.PingObject(clip);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox(
                "Use o preview de áudio do Inspector nativo do Unity depois de selecionar " +
                "a amostra. Os arquivos nunca recebem papéis de gameplay inferidos.",
                MessageType.None);
        }
    }
}
#endif
