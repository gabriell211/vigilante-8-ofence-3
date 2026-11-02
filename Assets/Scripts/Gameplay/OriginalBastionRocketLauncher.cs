#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Launches the archived Rocket/Bastion homing state during source-car
    /// inspection only. The actual rocket mesh, exact PS1 damage and ammunition
    /// table are not available in the certified Unity runtime.
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
        /// Invoked on a distinct input press. There is no invented ammo count:
        /// this is a single-purpose Editor test of a real Rocket.cs state.
        /// </summary>
        public bool TryLaunch()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive ||
                lockedTarget == null || !lockedTarget.IsAlive ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                !vehicle.HasWeapon)
                return false;

            if (!vehicle.LaunchBastionSourceRocket(lockedTarget))
                return false;
            shotsLaunched++;
            return true;
        }
    }
}
#endif
