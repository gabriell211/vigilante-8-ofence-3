#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Actual archived RocketL.UpdateW(1,12,10/0x422) ammunition rules.
    /// Shared by the original V82 car's normal Rocket/Bastion experiment
    /// and its 0x422 RoadRunner variant. No fabricated refills/pickups.
    /// </summary>
    [RequireComponent(typeof(VehicleDrive))]
    [DisallowMultipleComponent]
    public sealed class OriginalRocketSourceInventory : MonoBehaviour
    {
        public const int InitialCharges = 10; // RocketL.UpdateW(arg1=1)
        public const int StandardRocketCost = 1; // RocketL.UpdateW(arg1=12)
        public const int RoadRunnerCost = 2; // RocketL.UpdateW(arg1=10,arg2=0x422)
        public const int RocketDamage = 80; // RocketL.FUN_4336C
        public const int OriginalRocketLifeTicks = 240; // RocketL.FUN_4336C

        private int remainingCharges = InitialCharges;
        public int RemainingCharges => remainingCharges;
        public bool IsReleaseApproved => false;

        private void Awake() => remainingCharges = InitialCharges;

        public bool CanSpend(int cost) =>
            cost > 0 && cost <= InitialCharges && remainingCharges >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanSpend(cost) || !GetComponent<VehicleDrive>().IsAlive) return false;
            remainingCharges -= cost;
            return true;
        }

        public static int CostForMode(bool roadRunner) =>
            roadRunner ? RoadRunnerCost : StandardRocketCost;
    }
}
#endif
