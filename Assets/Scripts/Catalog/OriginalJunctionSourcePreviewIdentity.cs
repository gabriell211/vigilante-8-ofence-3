using System;
using UnityEngine;

namespace Vigilante8.Content
{
    /// <summary>
    /// Real source TMD displayed at a JUNC_DB-derived DEBUG-scale coordinate.
    /// The original junction's terrain deformation is NOT reconstructed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalJunctionSourcePreviewIdentity : MonoBehaviour
    {
        [SerializeField] private string stageId;
        [SerializeField] private string junctionFileId;
        [SerializeField] private string originalTmdGroup;
        [SerializeField] private Vector3Int originalFixedPointPosition;
        [SerializeField] private int sourcePreviewScale;

        public string StageId => stageId;
        public string JunctionFileId => junctionFileId;
        public string OriginalTmdGroup => originalTmdGroup;
        public Vector3Int OriginalFixedPointPosition => originalFixedPointPosition;
        public int SourcePreviewScale => sourcePreviewScale;
        public bool IsReleaseApproved => false;

#if UNITY_EDITOR
        public void Configure(string stage, string junction, string group,
            Vector3Int raw, int factor)
        {
            if (string.IsNullOrWhiteSpace(stage) || !stage.StartsWith("v82_", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(junction) || string.IsNullOrWhiteSpace(group) ||
                !group.StartsWith(stage + "_tmd_", StringComparison.Ordinal) || factor <= 0)
                throw new ArgumentException("Only traceable original JUNC/TMD preview metadata is allowed");
            stageId = stage;
            junctionFileId = junction;
            originalTmdGroup = group;
            originalFixedPointPosition = raw;
            sourcePreviewScale = factor;
        }
#endif
    }
}
