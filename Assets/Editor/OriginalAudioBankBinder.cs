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

                OriginalAudioBank.Sample[] samples = ResolveVerifiedSamples(bank);
                jobs.Add((bank.id, prefabPath, samples));
            }

            foreach (var job in jobs)
                WriteBankToPrefab(job.id, job.path, job.samples);
            AssetDatabase.SaveAssets();
            Debug.Log("Original vehicle audio indices bound to 18 inspection prefabs. " +
                      "No weapon/engine role assumed; runtime gameplay not certified.");
        }
        /// <summary>
        /// Attaches archived indexed WAV samples to all eight REAL XOBF stage-object
        /// inspection prefabs. Those objects are not complete level prefabs, and
        /// these clips do not have approved gameplay roles.
        /// </summary>
        [MenuItem("Vigilante 8/Migração/Anexar WAVs originais aos 8 bancos das fases")]
        public static void AttachToStageInspectionPrefabs()
        {
            AudioCatalog manifest = LoadVerifiedCatalog();
            var expected = new HashSet<string>(
                CatalogRepository.Load().arenas.Where(a => a.game == "v82")
                    .Select(a => a.id), StringComparer.Ordinal);
            AudioBank[] banks = manifest.banks.Where(b => b.kind == "arena").ToArray();
            if (expected.Count != 8 || banks.Length != 8 ||
                banks.Select(b => b.id).Distinct(StringComparer.Ordinal).Count() != 8 ||
                !expected.SetEquals(banks.Select(b => b.id)))
                throw new InvalidOperationException("Source WAVs must cover precisely eight unique V82 arenas");

            const string directory = "Assets/GameContent/PreviewStageObjectsV82";
            var jobs = new List<(string id, string path, OriginalAudioBank.Sample[] samples)>();
            foreach (AudioBank bank in banks)
            {
                string path = directory + "/" + bank.id + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null ||
                    prefab.GetComponent<OriginalStageObjectPreviewIdentity>()?.StageId != bank.id ||
                    prefab.GetComponent<OfficialAssetIdentity>() != null ||
                    prefab.GetComponentInChildren<Collider>(true) != null)
                    throw new InvalidOperationException("The authentic stage inspection prefab is missing: " + path);
                jobs.Add((bank.id, path, ResolveVerifiedSamples(bank)));
            }

            // All stage WAVs and GUIDs are verified before any prefab is written.
            foreach (var job in jobs)
                WriteBankToPrefab(job.id, job.path, job.samples);
            AssetDatabase.SaveAssets();
            Debug.Log("Original indexed WAV clips attached to eight source XOBF stage-object banks. " +
                "No original audio role, full arena, collision, or gameplay certification claimed.");
        }

        private static OriginalAudioBank.Sample[] ResolveVerifiedSamples(AudioBank bank)
        {
            if (bank == null || string.IsNullOrWhiteSpace(bank.id) ||
                bank.samples == null || bank.samples.Length == 0)
                throw new InvalidOperationException("Missing original WAV source bank");

            var indices = new HashSet<int>();
            var samples = new List<OriginalAudioBank.Sample>(bank.samples.Length);
            foreach (SampleEntry sample in bank.samples)
            {
                if (sample == null || sample.originalIndex < 0 ||
                    !indices.Add(sample.originalIndex))
                    throw new InvalidOperationException("Duplicate or invalid original audio index: " + bank.id);
                if (string.IsNullOrWhiteSpace(sample.assetPath) ||
                    !sample.assetPath.StartsWith("Assets/GameContent/LegacyAudioV82/",
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(sample.guid) ||
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
            return samples.ToArray();
        }

        private static void WriteBankToPrefab(string id, string path,
            OriginalAudioBank.Sample[] samples)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                OriginalAudioBank component = prefab.GetComponent<OriginalAudioBank>();
                if (component == null) component = prefab.AddComponent<OriginalAudioBank>();
                component.Configure(id, samples);
                if (PrefabUtility.SaveAsPrefabAsset(prefab, path) == null)
                    throw new InvalidOperationException("Cannot save original audio bank: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

    }
}
#endif
