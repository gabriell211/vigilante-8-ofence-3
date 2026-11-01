#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Bind exact original WAV sample indices to the 18 source-derived review
    /// models without hardcoding vehicle names or inventing sound roles.
    /// Audio provenance is produced by scripts/migrate_legacy_audio.py.
    /// </summary>
    public static class OriginalAudioBankBinder
    {
        private const string CatalogPath = "Assets/GameContent/LegacyAudioV82/catalog.json";
        private const string PreviewFolder = "Assets/GameContent/PreviewPrefabsV82";

        [Serializable]
        private sealed class AudioCatalog
        {
            public int schemaVersion;
            public AudioBank[] banks;
        }

        [Serializable]
        private sealed class AudioBank
        {
            public string id;
            public string kind;
            public SampleEntry[] samples;
        }

        [Serializable]
        private sealed class SampleEntry
        {
            public int originalIndex;
            public string assetPath;
            public string guid;
        }

        private static AudioCatalog LoadVerifiedCatalog()
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            if (source == null)
                throw new InvalidOperationException("Original WAV catalog not imported: " + CatalogPath);
            AudioCatalog data = JsonUtility.FromJson<AudioCatalog>(source.text);
            if (data == null || data.schemaVersion != 1 || data.banks == null ||
                data.banks.Length != 26 || data.banks.Any(x => x == null ||
                    string.IsNullOrWhiteSpace(x.id) || x.samples == null || x.samples.Length == 0))
                throw new InvalidOperationException("Invalid or incomplete original 18-car / 8-arena audio catalog");
            return data;
        }

        [MenuItem("Vigilante 8/Migração/Conferir áudios reais dos dois jogos")]
        public static void ReportOriginalAudio()
        {
            AudioCatalog catalog = LoadVerifiedCatalog();
            int count = catalog.banks.Sum(x => x.samples.Length);
            Debug.Log("Original 2nd Offense WAV references: " + count +
                      " samples across 18 car banks and 8 arena banks. " +
                      "Additional shared WAVs are archived; sound roles are not inferred.");
        }

        [MenuItem("Vigilante 8/Migração/Anexar WAVs originais aos 18 carros de inspeção")]
        public static void AttachToInspectionPrefabs()
        {
            AudioCatalog manifest = LoadVerifiedCatalog();
            var expectedIds = new HashSet<string>(
                CatalogRepository.Load().vehicles.Where(v => v.game == "v82")
                    .Select(v => v.id), StringComparer.Ordinal);
            AudioBank[] banks = manifest.banks.Where(b => b.kind == "vehicle").ToArray();
            if (expectedIds.Count != 18 || banks.Length != 18 ||
                !expectedIds.SetEquals(banks.Select(b => b.id)))
                throw new InvalidOperationException("Audio mappings do not match 18 official vehicle identities");

            // Validate every source before mutating any prefab. No partial
            // assignment if a GUID, clip, or inspection model is missing.
            var jobs = new List<(string id, string path, OriginalAudioBank.Sample[] samples)>();
            foreach (AudioBank bank in banks)
            {
                string prefabPath = PreviewFolder + "/" + bank.id + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null || prefab.GetComponent<OriginalVehiclePreviewIdentity>() == null)
                    throw new InvalidOperationException("Original model inspection prefab missing: " + prefabPath);

                var indices = new HashSet<int>();
                var samples = new List<OriginalAudioBank.Sample>();
                foreach (SampleEntry sample in bank.samples)
                {
                    if (sample.originalIndex < 0 || !indices.Add(sample.originalIndex))
                        throw new InvalidOperationException("Duplicate original audio index: " + bank.id);
                    if (!sample.assetPath.StartsWith("Assets/GameContent/LegacyAudioV82/",
                            StringComparison.Ordinal) ||
                        !string.Equals(AssetDatabase.AssetPathToGUID(sample.assetPath),
                            sample.guid, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Original audio GUID mismatch: " + sample.assetPath);
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sample.assetPath);
                    if (clip == null)
                        throw new InvalidOperationException("Original WAV did not import: " + sample.assetPath);
                    samples.Add(new OriginalAudioBank.Sample {
                        originalIndex = sample.originalIndex, clip = clip
                    });
                }
                jobs.Add((bank.id, prefabPath, samples.ToArray()));
            }

            foreach (var job in jobs)
            {
                GameObject prefab = PrefabUtility.LoadPrefabContents(job.path);
                try
                {
                    OriginalAudioBank component = prefab.GetComponent<OriginalAudioBank>();
                    if (component == null) component = prefab.AddComponent<OriginalAudioBank>();
                    component.Configure(job.id, job.samples);
                    if (PrefabUtility.SaveAsPrefabAsset(prefab, job.path) == null)
                        throw new InvalidOperationException("Cannot save original audio bank: " + job.path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Original vehicle audio indices bound to 18 inspection prefabs. " +
                      "No weapon/engine role assumed; runtime gameplay not certified.");
        }
    }
}
#endif
