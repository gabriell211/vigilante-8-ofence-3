using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;
using Vigilante8.Editor;
using Vigilante8.Gameplay;
namespace Vigilante8.EditorTests
{
    public sealed class OriginalStageObjectReviewTests
    {
        private const string Valid =
            "o v82_test\n" +
            "g v82_test_tmd_000\nv 0 0 0\nv 1 0 0\nv 0 0 1\nf 1 2 3\n" +
            "g v82_test_tmd_001\nv 0 2 0\nv 1 2 0\nv 0 2 1\nf 4 6 5\n";

        [Test]
        public void OriginalTriangleFacesRemainLocalToEachTmdGroup()
        {
            var source = OriginalStageObjectReviewBuilder.Parse(Valid, "v82_test", 6, 2, 2);
            Assert.That(source.Triangles, Is.EqualTo(2));
            Assert.That(source.Vertices, Is.EqualTo(6));
            Assert.That(source.Parts.Count, Is.EqualTo(2));
            Assert.That(source.Parts[1].Faces, Is.EqualTo(new[] {0, 2, 1}));
            Assert.That(source.Parts[1].Vertices[0].y, Is.EqualTo(2f));
        }

        [Test]
        public void InvalidOfficialSourceRecordsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => OriginalStageObjectReviewBuilder.Parse(
                Valid.Replace("f 4 6 5", "f 1 6 5"), "v82_test", 6, 2, 2));
            Assert.Throws<InvalidDataException>(() => OriginalStageObjectReviewBuilder.Parse(
                Valid.Replace("f 4 6 5", "f 4 4 5"), "v82_test", 6, 2, 2));
            Assert.Throws<InvalidDataException>(() => OriginalStageObjectReviewBuilder.Parse(
                Valid.Replace("g v82_test_tmd_001", "g ../fake"), "v82_test", 6, 2, 2));
            Assert.Throws<InvalidDataException>(() => OriginalStageObjectReviewBuilder.Parse(
                Valid, "v82_test", 6, 3, 2));
            Assert.Throws<InvalidDataException>(() => OriginalStageObjectReviewBuilder.Parse(
                Valid.Replace("v 1 0 0", "v NaN 0 0"), "v82_test", 6, 2, 2));
        }

        [Test]
        public void EightGenuineUnityPrefabsCarryNonReleaseIdentities()
        {
            OriginalStageObjectReviewBuilder.PrepareAll();
            var stages = CatalogRepository.Load().arenas.Where(x => x.game == "v82").ToArray();
            Assert.That(stages.Length, Is.EqualTo(8));
            var usedMeshPaths = new System.Collections.Generic.HashSet<string>();
            foreach (var stage in stages)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameContent/PreviewStageObjectsV82/" + stage.id + ".prefab");
                Assert.That(prefab, Is.Not.Null, stage.id);
                var identity = prefab.GetComponent<OriginalStageObjectPreviewIdentity>();
                Assert.That(identity, Is.Not.Null);
                Assert.That(identity.StageId, Is.EqualTo(stage.id));
                Assert.That(identity.SourceTriangles, Is.GreaterThan(1000));
                Assert.That(identity.IsReleaseApproved, Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
                var audio = prefab.GetComponent<OriginalAudioBank>();
                Assert.That(audio, Is.Not.Null, "Eight source stage previews must carry real WAVs");
                Assert.That(audio.SourceId, Is.EqualTo(stage.id));
                Assert.That(audio.Count, Is.GreaterThan(0));
                Assert.That(prefab.GetComponent<ArenaSpawnPoints>(), Is.Null);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.EqualTo(identity.SourceGroups));
                Assert.That(filters.Sum(f => f.sharedMesh.triangles.Length / 3),
                    Is.EqualTo(identity.SourceTriangles));
                foreach (var filter in filters)
                {
                    var path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    Assert.That(path.StartsWith("Assets/GameContent/PreviewStageMeshesV82/" +
                        stage.id + "/", StringComparison.Ordinal), Is.True, path);
                    Assert.That(usedMeshPaths.Add(path), Is.True, "Source meshes must be unique");
                }
            }
        }

        [Test]
        public void GeneratedInspectionGalleryDoesNotBecomePlayerScene()
        {
            OriginalStageObjectReviewBuilder.BuildGallery();
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                OriginalStageObjectReviewBuilder.ScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(s =>
                s.enabled && s.path == OriginalStageObjectReviewBuilder.ScenePath), Is.False);
        }
    }
}
