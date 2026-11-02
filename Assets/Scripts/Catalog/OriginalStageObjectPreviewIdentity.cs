using System;
using UnityEngine;
namespace Vigilante8.Content
{
    /// <summary>Authentic XOBF object bank; NOT a playable arena.</summary>
    [DisallowMultipleComponent]
    public sealed class OriginalStageObjectPreviewIdentity : MonoBehaviour
    {
        [SerializeField] private string stageId;
        [SerializeField] private int sourceTriangles;
        [SerializeField] private int sourceGroups;
        public string StageId => stageId;
        public int SourceTriangles => sourceTriangles;
        public int SourceGroups => sourceGroups;
        public bool IsReleaseApproved => false;
#if UNITY_EDITOR
        public void Configure(string id, int triangles, int groups)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !id.StartsWith("v82_", StringComparison.Ordinal) || triangles <= 0 || groups <= 0)
                throw new ArgumentException("Invalid original stage object identity");
            stageId = id;
            sourceTriangles = triangles;
            sourceGroups = groups;
        }
#endif
    }
}
