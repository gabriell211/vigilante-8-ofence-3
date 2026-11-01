#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vigilante8.Editor
{
    /// <summary>
    /// Strict OBJ reader for the source-derived TMD triangle exports.
    /// It never relies on Unity ModelImporter's MTL material naming and never
    /// manufactures meshes, materials, faces or UVs absent from the source.
    /// </summary>
    public static class OriginalObjMeshBuilder
    {
        public sealed class Face
        {
            public readonly int[] vertices = new int[3];
            public readonly int[] texcoords = new int[3];
            public string material;
        }

        public sealed class Group
        {
            public string name;
            public readonly List<Face> faces = new List<Face>();
        }

        public sealed class Document
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> texcoords = new List<Vector2>();
            public readonly List<Group> groups = new List<Group>();
            public int TriangleCount => groups.Sum(g => g.faces.Count);
        }

        public static Document Parse(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidDataException("Empty source-derived OBJ");
            var document = new Document();
            Group group = null;
            string material = null;
            int lineNumber = 0;
            foreach (string raw in source.Split(new[] { '\n' }, StringSplitOptions.None))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "v":
                        if (parts.Length != 4) throw Error(lineNumber, "Invalid vertex");
                        document.vertices.Add(new Vector3(
                            Float(parts[1]), Float(parts[2]), Float(parts[3])));
                        break;
                    case "vt":
                        if (parts.Length != 3) throw Error(lineNumber, "Invalid original UV");
                        document.texcoords.Add(new Vector2(Float(parts[1]), Float(parts[2])));
                        break;
                    case "g":
                        if (parts.Length != 2 || !SafeName(parts[1]))
                            throw Error(lineNumber, "Unsafe original TMD group");
                        if (document.groups.Any(g => g.name == parts[1]))
                            throw Error(lineNumber, "Repeated original TMD group");
                        group = new Group { name = parts[1] };
                        document.groups.Add(group);
                        break;
                    case "usemtl":
                        if (parts.Length != 2 || !SafeName(parts[1]))
                            throw Error(lineNumber, "Invalid original material name");
                        material = parts[1];
                        break;
                    case "f":
                        if (group == null || material == null || parts.Length != 4)
                            throw Error(lineNumber, "Only original triangulated faces with material allowed");
                        var face = new Face { material = material };
                        int withUv = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            string[] vertex = parts[i + 1].Split('/');
                            if (vertex.Length < 1 || vertex.Length > 2)
                                throw Error(lineNumber, "Unsupported OBJ face format");
                            face.vertices[i] = Index(vertex[0], document.vertices.Count, lineNumber);
                            face.texcoords[i] = -1;
                            if (vertex.Length == 2)
                            {
                                face.texcoords[i] = Index(vertex[1], document.texcoords.Count, lineNumber);
                                withUv++;
                            }
                        }
                        if (withUv != 0 && withUv != 3)
                            throw Error(lineNumber, "Partial UV face");
                        if (face.vertices.Distinct().Count() != 3)
                            throw Error(lineNumber, "Degenerate original triangle");
                        group.faces.Add(face);
                        break;
                    case "mtllib":
                    case "o":
                        if (parts.Length != 2) throw Error(lineNumber, "Invalid OBJ metadata");
                        break;
                    default:
                        throw Error(lineNumber, "Unknown source-derived OBJ statement: " + parts[0]);
                }
            }
            if (document.vertices.Count < 3 || document.groups.Count < 1 ||
                document.TriangleCount < 1)
                throw new InvalidDataException("No genuine original triangle geometry found");
            if (document.groups.Any(g => g.faces.Count == 0))
                throw new InvalidDataException("Empty original TMD group");
            return document;
        }

        private static bool SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.All(c => (c >= 'a' && c <= 'z') ||
                                  (c >= 'A' && c <= 'Z') ||
                                  (c >= '0' && c <= '9') || c == '_');
        }

        private static float Float(string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ||
                float.IsNaN(f) || float.IsInfinity(f))
                throw new InvalidDataException("Invalid original OBJ coordinate");
            return f;
        }

        private static int Index(string text, int total, int line)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ||
                n <= 0 || n > total)
                throw Error(line, "Invalid global OBJ vertex/UV index");
            return n - 1;
        }

        private static InvalidDataException Error(int line, string description)
            => new InvalidDataException("OBJ line " + line + ": " + description);

        public static GameObject CreateInspectionRoot(
            string vehicleId,
            string text,
            IReadOnlyDictionary<string, Material> sourceMaterials,
            string meshRoot,
            int expectedTriangles)
        {
            if (!SafeName(vehicleId) || !meshRoot.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Invalid original source model identity/path");

            Document document = Parse(text);
            if (document.TriangleCount != expectedTriangles)
                throw new InvalidDataException(vehicleId + ": original triangle count changed from " +
                                               expectedTriangles + " to " + document.TriangleCount);
            foreach (Group group in document.groups)
            foreach (Face face in group.faces)
                if (sourceMaterials == null || !sourceMaterials.ContainsKey(face.material) ||
                    sourceMaterials[face.material] == null)
                    throw new InvalidDataException("Missing source material: " + face.material);

            EnsureFolder(meshRoot);
            var root = new GameObject(vehicleId);
            try
            {
                int emittedTriangles = 0;
                foreach (Group group in document.groups)
                {
                    var vertexList = new List<Vector3>();
                    var uvList = new List<Vector2>();
                    var materialNames = new List<string>();
                    var indicesByMaterial = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                    foreach (Face face in group.faces)
                    {
                        if (!indicesByMaterial.TryGetValue(face.material, out List<int> indices))
                        {
                            indices = new List<int>();
                            indicesByMaterial.Add(face.material, indices);
                            materialNames.Add(face.material);
                        }
                        for (int i = 0; i < 3; i++)
                        {
                            indices.Add(vertexList.Count);
                            vertexList.Add(document.vertices[face.vertices[i]]);
                            uvList.Add(face.texcoords[i] < 0 ?
                                Vector2.zero : document.texcoords[face.texcoords[i]]);
                        }
                        emittedTriangles++;
                    }

                    var mesh = new Mesh
                    {
                        name = group.name,
                        indexFormat = IndexFormat.UInt32
                    };
                    mesh.SetVertices(vertexList);
                    mesh.SetUVs(0, uvList);
                    mesh.subMeshCount = materialNames.Count;
                    for (int i = 0; i < materialNames.Count; i++)
                        mesh.SetTriangles(indicesByMaterial[materialNames[i]], i, false);
                    mesh.RecalculateBounds();
                    mesh.RecalculateNormals();

                    string meshPath = meshRoot + "/" + group.name + ".asset";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                        existing = mesh;
                    }
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        EditorUtility.SetDirty(existing);
                    }
                    var child = new GameObject(group.name);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = existing;
                    child.AddComponent<MeshRenderer>().sharedMaterials =
                        materialNames.Select(name => sourceMaterials[name]).ToArray();
                }
                if (emittedTriangles != expectedTriangles)
                    throw new InvalidDataException(vehicleId + ": missing original polygon faces");
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }
}
#endif
