#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// MissileL.UpdateW(1) initializes maxHalfHealth=12; its special
    /// UpdateW(10, 0x442) subtracts ONE only while maxHalfHealth > 1.
    /// Normal UpdateW(12) in the archived source does not itself subtract,
    /// so we must NOT pretend the normal missile uses one unit here.
    /// </summary>
    [RequireComponent(typeof(VehicleDrive))]
    [DisallowMultipleComponent]
    public sealed class OriginalMissileSourceInventory : MonoBehaviour
    {
        public const int InitialSourceCharges = 12;
        public const int HaloSpecialCost = 1;
        private int remainingCharges = InitialSourceCharges;

        public int RemainingCharges => remainingCharges;
        public bool IsReleaseApproved => false;

        private void Awake() => remainingCharges = InitialSourceCharges;

        public bool CanFireHalo =>
            remainingCharges > HaloSpecialCost &&
            GetComponent<VehicleDrive>()?.IsAlive == true;

        public bool TrySpendHalo()
        {
            if (!CanFireHalo) return false;
            remainingCharges -= HaloSpecialCost;
            return true;
        }
    }
}
#endif
