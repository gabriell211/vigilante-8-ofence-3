using System;
using UnityEngine;
namespace Vigilante8.Content
{
    /// <summary>Original Unity Geo Mesh; never a completed gameplay arena.</summary>
    [DisallowMultipleComponent]
    public sealed class OriginalTerrainPreviewIdentity : MonoBehaviour
    {
        [SerializeField] private string stageId;
        [SerializeField] private string originalGuid;
        [SerializeField] private int sourceVertices;
        [SerializeField] private int sourceTriangles;
        public string StageId => stageId;
        public string OriginalGuid => originalGuid;
        public int SourceVertices => sourceVertices;
        public int SourceTriangles => sourceTriangles;
        public bool IsReleaseApproved => false;
#if UNITY_EDITOR
        public void Configure(string id, string guid, int vertices, int triangles)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("v82_", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(guid) || guid.Length != 32
                || vertices < 3 || triangles < 1)
                throw new ArgumentException("Invalid original terrain identity");
            stageId = id;
            originalGuid = guid;
            sourceVertices = vertices;
            sourceTriangles = triangles;
        }
#endif
    }
}
