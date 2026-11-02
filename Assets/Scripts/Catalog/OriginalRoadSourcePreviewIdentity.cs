using System;
using UnityEngine;

namespace Vigilante8.Content
{
    /// <summary>
    /// Archived RSEG fixed-point height branch visualized as a thin guide.
    /// This is NOT official road geometry, spawn, collision or terrain height.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalRoadSourcePreviewIdentity : MonoBehaviour
    {
        [SerializeField] private string stageId;
        [SerializeField] private string rsegFileId;
        [SerializeField] private string firstJuncFileId;
        [SerializeField] private string secondJuncFileId;
        [SerializeField] private int debugSourceScale;

        public string StageId => stageId;
        public string RsegFileId => rsegFileId;
        public int DebugSourceScale => debugSourceScale;
        public bool IsReleaseApproved => false;

#if UNITY_EDITOR
        public void Configure(string stage, string road, string first, string second, int scale)
        {
            if (string.IsNullOrWhiteSpace(stage) || !stage.StartsWith("v82_", StringComparison.Ordinal) ||
                !long.TryParse(road, out long rseg) || rseg == 0 ||
                !long.TryParse(first, out long j1) || j1 == 0 ||
                !long.TryParse(second, out long j2) || j2 == 0 || j1 == j2 ||
                scale <= 0)
                throw new ArgumentException("Only original RSEG endpoints with a positive scale can be inspected");
            stageId = stage;
            rsegFileId = road;
            firstJuncFileId = first;
            secondJuncFileId = second;
            debugSourceScale = scale;
        }
#endif
    }
}
