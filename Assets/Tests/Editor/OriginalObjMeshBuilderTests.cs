using System;
using System.IO;
using NUnit.Framework;
using Vigilante8.Editor;

namespace Vigilante8.EditorTests
{
    public sealed class OriginalObjMeshBuilderTests
    {
        private const string Original =
            "mtllib sample.mtl\n" +
            "o v82_test\n" +
            "g v82_test_tmd_000\n" +
            "v 0 0 0\n" +
            "v 1 0 0\n" +
            "v 0 0 1\n" +
            "vt 0 0\n" +
            "vt 1 0\n" +
            "vt 0 1\n" +
            "usemtl texture_001\n" +
            "f 1/1 2/2 3/3\n";

        [Test]
        public void SourceParserPreservesOriginalTrianglesUvsAndMaterials()
        {
            var parsed = OriginalObjMeshBuilder.Parse(Original);
            Assert.That(parsed.TriangleCount, Is.EqualTo(1));
            Assert.That(parsed.groups.Count, Is.EqualTo(1));
            Assert.That(parsed.groups[0].name, Is.EqualTo("v82_test_tmd_000"));
            Assert.That(parsed.groups[0].faces[0].material, Is.EqualTo("texture_001"));
            Assert.That(parsed.groups[0].faces[0].texcoords, Is.EqualTo(new[] {0, 1, 2}));
        }

        [Test]
        public void SourceParserRejectsInventedOrBrokenGeometry()
        {
            Assert.Throws<InvalidDataException>(() =>
                OriginalObjMeshBuilder.Parse(Original.Replace("f 1/1 2/2 3/3", "f 1/1 2/2 99/3")));
        }

        [Test]
        public void SourceParserRejectsUnsafeOriginalGroupNames()
        {
            Assert.Throws<InvalidDataException>(() =>
                OriginalObjMeshBuilder.Parse(Original.Replace("v82_test_tmd_000", "../output")));
        }

        [Test]
        public void SourceParserRejectsNaNPositions()
        {
            Assert.Throws<InvalidDataException>(() =>
                OriginalObjMeshBuilder.Parse(Original.Replace("v 1 0 0", "v NaN 0 0")));
        }

        [Test]
        public void SourceParserRejectsNongenuineMaterialFallbacks()
        {
            Assert.Throws<InvalidDataException>(() =>
                OriginalObjMeshBuilder.Parse(Original.Replace("usemtl texture_001", "usemtl ../bad")));
        }
    }
}
