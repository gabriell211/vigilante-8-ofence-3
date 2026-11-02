#if UNITY_EDITOR
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Development-only opponent use of archived Rocket/Bastion and Missile
    /// launchers. Tactical intervals and target geometry are diagnostic Unity
    /// choices, NOT original 2nd Offense weapon-AI source values.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BotVehicleInput), typeof(VehicleDrive))]
    public sealed class OriginalBotSourceWeaponAI : MonoBehaviour
    {
        [Min(1f), SerializeField] private float rocketIntervalSeconds = 7f;
        [Min(1f), SerializeField] private float missileIntervalSeconds = 9f;

        private VehicleDrive vehicle;
        private BotVehicleInput bot;
        private LegacyVehiclePlaytest sourceCar;
        private OriginalBastionRocketLauncher rocket;
        private OriginalMissileSourceLauncher missile;
        private float nextRocketAttempt;
        private float nextMissileAttempt;

        public bool IsReleaseApproved => false;

        private void Awake()
        {
            vehicle = GetComponent<VehicleDrive>();
            bot = GetComponent<BotVehicleInput>();
            sourceCar = GetComponent<LegacyVehiclePlaytest>();
            rocket = GetComponent<OriginalBastionRocketLauncher>();
            missile = GetComponent<OriginalMissileSourceLauncher>();
        }

        private void OnEnable()
        {
            // Offset the first shots so opponents do not fire on scene load.
            nextRocketAttempt = Time.time + 2.5f;
            nextMissileAttempt = Time.time + 4f;
        }

        private void Update()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                vehicle == null || !vehicle.IsAlive || sourceCar == null ||
                bot == null || bot.target == null)
                return;

            VehicleDrive intended = bot.target.GetComponentInParent<VehicleDrive>();
            if (!bot.CanUseSourceWeapon(intended))
                return;

            // Launchers themselves validate that the target is still alive,
            // their source origin, and the archived RocketL ammunition rules.
            if (rocket != null && rocket.LockedTarget == intended &&
                Time.time >= nextRocketAttempt)
            {
                nextRocketAttempt = Time.time + Mathf.Max(1f, rocketIntervalSeconds);
                rocket.TryLaunch();
            }
            if (missile != null && missile.LockedTarget == intended &&
                Time.time >= nextMissileAttempt)
            {
                nextMissileAttempt = Time.time + Mathf.Max(1f, missileIntervalSeconds);
                missile.TryLaunch();
            }
        }
    }
}
#endif
