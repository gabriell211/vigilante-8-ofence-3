using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    public sealed class OriginalStageAudioBindingTests
    {
        [Serializable]
        private sealed class OriginalManifest
        {
            public int schemaVersion;
            public SourceBank[] banks;
        }

        [Serializable]
        private sealed class SourceBank
        {
            public string id;
            public string kind;
            public SourceSample[] samples;
        }

        [Serializable]
        private sealed class SourceSample
        {
            public int originalIndex;
            public string assetPath;
            public string guid;
        }

        [Test]
        public void EightRealStageBanksPreserveSourceSampleIndexesAndGuids()
        {
            OriginalStageObjectReviewBuilder.PrepareAll();
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/GameContent/LegacyAudioV82/catalog.json");
            Assert.That(source, Is.Not.Null);
            var manifest = JsonUtility.FromJson<OriginalManifest>(source.text);
            Assert.That(manifest.schemaVersion, Is.EqualTo(1));
            Assert.That(manifest.banks.Length, Is.EqualTo(26));
            SourceBank[] stages = manifest.banks.Where(b => b.kind == "arena").ToArray();
            Assert.That(stages.Length, Is.EqualTo(8));
            Assert.That(stages.Select(x => x.id).Distinct().Count(), Is.EqualTo(8));

            foreach (SourceBank stage in stages)
            {
                string path = "Assets/GameContent/PreviewStageObjectsV82/" + stage.id + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, stage.id);
                Assert.That(prefab.GetComponent<OriginalStageObjectPreviewIdentity>()?.StageId,
                    Is.EqualTo(stage.id));
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
                Assert.That(prefab.GetComponentInChildren<Collider>(true), Is.Null);

                OriginalAudioBank audio = prefab.GetComponent<OriginalAudioBank>();
                Assert.That(audio, Is.Not.Null, stage.id);
                Assert.That(audio.SourceId, Is.EqualTo(stage.id));
                Assert.That(audio.Count, Is.EqualTo(stage.samples.Length));
                foreach (SourceSample sample in stage.samples)
                {
                    Assert.That(audio.TryGetOriginalSample(sample.originalIndex, out AudioClip clip),
                        Is.True, stage.id + " original index " + sample.originalIndex);
                    Assert.That(clip, Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(sample.assetPath));
                    Assert.That(AssetDatabase.AssetPathToGUID(sample.assetPath),
                        Is.EqualTo(sample.guid).IgnoreCase);
                }
                Assert.That(audio.TryGetOriginalSample(
                    stage.samples.Max(x => x.originalIndex) + 1, out _), Is.False);
            }
        }
    }
}
