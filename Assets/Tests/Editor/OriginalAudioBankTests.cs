using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.EditorTests
{
    public sealed class OriginalAudioBankTests
    {
        [Test]
        public void AllImportedOriginalWavsHaveDistinctAssetGuids()
        {
            string root = "Assets/GameContent/LegacyAudioV82";
            string[] clips = AssetDatabase.FindAssets("t:AudioClip", new[] { root });
            Assert.That(clips.Length, Is.EqualTo(234));
            var guids = clips.Select(g => AssetDatabase.AssetPathToGUID(
                AssetDatabase.GUIDToAssetPath(g)));
            Assert.That(guids.Distinct().Count(), Is.EqualTo(234));
            foreach (string guid in clips)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(path.StartsWith(root + "/", StringComparison.Ordinal), Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(path), Is.Not.Null);
            }
        }

        [Test]
        public void InspectionPrefabsNeverReceiveUnverifiedWeaponAudioRoles()
        {
            string path = "Assets/GameContent/PreviewPrefabsV82";
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                var bank = prefab.GetComponent<OriginalAudioBank>();
                if (bank != null)
                {
                    Assert.That(bank.Count, Is.GreaterThan(0));
                    Assert.That(bank.SourceId.StartsWith("v82_", StringComparison.Ordinal), Is.True);
                }
            }
        }
    }
}
