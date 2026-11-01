#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    public static class OriginalAudioBankBinder
    {
        [Serializable]
        private sealed class SampleEntry
        {
            public int index;
            public string assetPath;
            public string guid;
        }

        [Serializable]
        private sealed class BankEntry
        {
            public string key;
            public SampleEntry[] samples;
        }

        // The original JSON is produced as a keyed dictionary (by vehicle ID).
        // Use a strict parser to avoid relying on JsonUtility dictionary support.
        [MenuItem("Vigilante 8/Migração/Conferir áudios originais (sem inventar efeitos)")]
        public static void ReportAudioFiles()
        {
            const string directory = "Assets/GameContent/LegacyAudioV82/SHARED/COMMON/Sounds";
            var files = AssetDatabase.FindAssets("t:AudioClip", new[] { directory });
            if (files.Length == 0)
                throw new InvalidOperationException("Original vehicle WAVs have not been imported");
            Debug.Log("Original Unity audio samples available for review: " + files.Length +
                      ". No inferred weapon/engine associations are approved.");
        }

        [MenuItem("Vigilante 8/Migração/Anexar amostras originais aos prefabs de inspeção")]
        public static void AttachToInspectionPrefabs()
        {
            const string folder = "Assets/GameContent/PreviewPrefabsV82";
            const string wavDirectory = "Assets/GameContent/LegacyAudioV82/SHARED/COMMON/Sounds";
            var catalog = CatalogRepository.Load();
            int bound = 0;
            foreach (VehicleEntry vehicle in catalog.vehicles.Where(v => v.game == "v82"))
            {
                string sourceName = SourceVehicleName(vehicle.id);
                string path = folder + "/" + vehicle.id + ".prefab";
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null)
                    throw new InvalidOperationException("Authentic inspection prefab not created: " + path);
                string[] assetPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { wavDirectory })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.StartsWith(wavDirectory + "/" + sourceName + "_", StringComparison.Ordinal))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToArray();
                if (assetPaths.Length == 0)
                    throw new InvalidOperationException("Missing original vehicle audio bank for " + vehicle.id);
                var samples = assetPaths.Select(p =>
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(p);
                    string number = name.Substring(sourceName.Length + 1);
                    if (!int.TryParse(number, out int sampleIndex))
                        throw new InvalidOperationException("Unexpected sound sample filename: " + p);
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    return new OriginalAudioBank.Sample { originalIndex = sampleIndex, clip = clip };
                }).ToArray();

                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (prefab.GetComponent<OriginalVehiclePreviewIdentity>() == null)
                        throw new InvalidOperationException("Not an original inspection model: " + path);
                    var existing = prefab.GetComponent<OriginalAudioBank>();
                    if (existing == null) existing = prefab.AddComponent<OriginalAudioBank>();
                    existing.Configure(vehicle.id, samples);
                    if (PrefabUtility.SaveAsPrefabAsset(prefab, path) == null)
                        throw new InvalidOperationException("Cannot save original audio bank: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                bound++;
            }
            AssetDatabase.SaveAssets();
            if (bound != 18) throw new InvalidOperationException("Some official sequel vehicles are missing");
            Debug.Log("Bound the preserved original audio samples to 18 real preview prefabs. " +
                      "Playback roles not assigned, not an official runtime gameplay release.");
        }

        private static string SourceVehicleName(string vehicleId)
        {
            // Derived from the actual imported source prefab file names.
            switch (vehicleId)
            {
                case "v82_wonderwagon": return "WUNDER";
                case "v82_thunderbolt": return "TBOLT";
                case "v82_dakota_stunt_cycle": return "STNTBIKE";
                case "v82_samson_tow_truck": return "TOWTRUCK";
                case "v82_livingston_truck": return "TRUCK";
                case "v82_xanadu_rv": return "CARAVLLE";
                case "v82_palomino_xiii": return "CORSAIR";
                case "v82_el_guerrero": return "ELGUERRO";
                case "v82_blue_burro_bus": return "BUS";
                case "v82_excelsior_stretch": return "EXCELSR";
                case "v82_tsunami": return "TSUNAMI";
                case "v82_marathon": return "MARATHON";
                case "v82_moon_trekker": return "LUNAR";
                case "v82_grubb_dual_loader": return "GRBLDER";
                case "v82_chrono_stinger": return "STINGER";
                case "v82_vertigo": return "VERTIGO";
                case "v82_goliath_halftrack": return "HALFTRAK";
                case "v82_wapiti_4wd": return "FRONTIER";
                default: throw new InvalidOperationException("Unknown original vehicle identity: " + vehicleId);
            }
        }
    }
}
#endif
