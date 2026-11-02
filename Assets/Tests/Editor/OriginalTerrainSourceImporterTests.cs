using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vigilante8.Content;
using Vigilante8.Editor;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    public sealed class OriginalTerrainSourceImporterTests
    {
        [Test]
        public void SevenDistinctNativeOriginalTerrainsImportWithOriginalGuids()
        {
            OriginalTerrainSourceImporter.BuildGallery();
            var arenas = CatalogRepository.Load().arenas.Where(x => x.game == "v82").ToArray();
            Assert.That(arenas.Length, Is.EqualTo(8));
            var used = new System.Collections.Generic.HashSet<string>();
            foreach (var arena in arenas.Where(x => x.id != "v82_meteor_crater"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameContent/PreviewTerrainV82/" + arena.id + ".prefab");
                Assert.That(prefab, Is.Not.Null, arena.id);
                var identity = prefab.GetComponent<OriginalTerrainPreviewIdentity>();
                Assert.That(identity, Is.Not.Null);
                Assert.That(identity.StageId, Is.EqualTo(arena.id));
                Assert.That(identity.IsReleaseApproved, Is.False);
                Assert.That(prefab.GetComponent<OfficialAssetIdentity>(), Is.Null);
                Assert.That(prefab.GetComponent<ArenaSpawnPoints>(), Is.Null);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Mesh mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(identity.SourceVertices));
                ulong triangleIndices = 0;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    triangleIndices += mesh.GetIndexCount(i);
                Assert.That(triangleIndices / 3, Is.EqualTo((ulong)identity.SourceTriangles));
                string meshAsset = AssetDatabase.GetAssetPath(mesh);
                Assert.That(used.Add(meshAsset), Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(meshAsset),
                    Is.EqualTo(identity.OriginalGuid));
            }
            Assert.That(used.Count, Is.EqualTo(7));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/PreviewTerrainV82/v82_meteor_crater.prefab"), Is.Null);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled &&
                s.path == OriginalTerrainSourceImporter.GalleryPath), Is.False);
        }
    }
}
