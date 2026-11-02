#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Launches the archived Rocket/Bastion homing state during source-car
    /// inspection only. The source RocketL charge count is preserved, while
    /// original meshes, physics conversion and per-mode dispatch need review.
    /// </summary>
    [RequireComponent(typeof(VehicleDrive))]
    [DisallowMultipleComponent]
    public sealed class OriginalBastionRocketLauncher : MonoBehaviour
    {
        private VehicleDrive vehicle;
        private VehicleDrive lockedTarget;
        private int shotsLaunched;

        public VehicleDrive LockedTarget => lockedTarget;
        public int ShotsLaunched => shotsLaunched;
        public bool IsReleaseApproved => false;

        private void Awake() => vehicle = GetComponent<VehicleDrive>();

        public void SetTarget(VehicleDrive target)
        {
            if (target == vehicle || (target != null && !target.IsAlive))
            {
                lockedTarget = null;
                return;
            }
            lockedTarget = target;
        }

        /// <summary>
        /// Invoked on a distinct input press. Shares RocketL's actual ten-charge
        /// inventory with the separate 0x422 RoadRunner mode.
        /// </summary>
        public bool TryLaunch()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                lockedTarget == null || !lockedTarget.IsAlive ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                !vehicle.HasWeapon)
                return false;

            var inventory = GetComponent<OriginalRocketSourceInventory>();
            if (inventory == null || !inventory.CanSpend(
                OriginalRocketSourceInventory.StandardRocketCost) ||
                !vehicle.LaunchBastionSourceRocket(lockedTarget) ||
                !inventory.TrySpend(OriginalRocketSourceInventory.StandardRocketCost))
                return false;
            shotsLaunched++;
            return true;
        }
    }
}
#endif
