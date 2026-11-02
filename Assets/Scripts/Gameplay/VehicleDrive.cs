using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleDrive : MonoBehaviour
    {
        [Header("Vehicle-specific handling")]
        [Min(200f)] public float massKg = 1400f;
        [Min(1f)] public float acceleration = 25f;
        [Min(1f)] public float maximumSpeed = 42f;
        [Min(1f)] public float reverseSpeed = 12f;
        [Range(5f, 150f)] public float turningSpeed = 85f;
        [Range(0.1f, 20f)] public float lateralGrip = 7f;
        [Min(1f)] public float brakingPower = 35f;
        [Min(1f)] public float maximumHealth = 100f;
        [Min(0.05f)] public float fireCooldown = 0.4f;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private LayerMask groundLayers = -1;

        public event Action<VehicleDrive> Destroyed;
        /// <summary>
        /// Fired exactly once on destruction. The second argument is the vehicle
        /// that delivered the fatal hit, or null for unattributed damage.
        /// </summary>
        public event Action<VehicleDrive, VehicleDrive> Eliminated;
        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        public bool HasWeapon => muzzle != null && projectilePrefab != null;
        private Rigidbody body;
        private float throttle;
        private float steer;
        private float brake;
        private bool firing;
        private float nextShot;

#if UNITY_EDITOR
        private OriginalMachineGunSourceTest sourceMachineGun;

        /// <summary>Development-only loadout for imported test meshes.
        /// Never represents a verified original special weapon.</summary>
        public void ConfigurePlaytestLoadout(Transform sourceMuzzle, Projectile projectile)
        {
            if (sourceMuzzle == null || projectile == null ||
                !sourceMuzzle.IsChildOf(transform))
                throw new ArgumentException("An attached muzzle and an authored test projectile are required");
            muzzle = sourceMuzzle;
            projectilePrefab = projectile;
        }
        /// <summary>
        /// A source-informed Rocket/Bastion physics experiment. Reuses the
        /// existing INVISIBLE diagnostic projectile, not an official rocket
        /// model or verified damage number. Never available in player builds.
        /// </summary>
        public bool LaunchBastionSourceRocket(VehicleDrive target)
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                !IsAlive || !HasWeapon || target == null || target == this ||
                !target.IsAlive || GetComponent<OriginalBastionRocketLauncher>() == null)
                return false;

            Projectile shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            shot.impactDamage = OriginalRocketSourceInventory.RocketDamage;
            shot.lifetimeSeconds =
                OriginalRocketSourceInventory.OriginalRocketLifeTicks * Time.fixedDeltaTime;
            var flight = shot.gameObject.AddComponent<OriginalBastionRocketFlight>();
            int ticks = OriginalRocketSourceInventory.OriginalRocketLifeTicks;
            flight.Configure(target, ticks);
            shot.Launch(this, muzzle.forward);
            flight.EnterBastion();
            return true;
        }

        /// <summary>
        /// MissileL.UpdateW(12) launches a 60-damage shot; Missile.UpdateW(5)
        /// transitions Shell -> Projectile with 480 original update cycles.
        /// Only the verified TMD-car Editor playtest may call this.
        /// </summary>
        public bool LaunchSourceMissile(VehicleDrive target)
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                !IsAlive || !HasWeapon || target == null || target == this ||
                !target.IsAlive || GetComponent<LegacyVehiclePlaytest>() == null ||
                GetComponent<OriginalMissileSourceLauncher>() == null)
                return false;

            Projectile shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            shot.impactDamage = OriginalMissileSourceFlight.OriginalDamage;
            shot.lifetimeSeconds =
                OriginalMissileSourceFlight.OriginalLifeTicks * Time.fixedDeltaTime;
            var flight = shot.gameObject.AddComponent<OriginalMissileSourceFlight>();
            flight.Configure(target);
            shot.Launch(this, muzzle.forward);
            flight.ActivateProjectile();
            return true;
        }

        /// <summary>
        /// Rocket.RoadRunner source mode: stick to the victim for 360 engine
        /// events then apply -40 through the vehicle health model. Both
        /// timing and damage-body segmentation remain provisional in Unity.
        /// </summary>
        public bool LaunchSourceRoadRunner()
        {
            if (!Application.isPlaying || Time.timeScale <= 0f ||
                !IsAlive || !HasWeapon ||
                GetComponent<LegacyVehiclePlaytest>() == null ||
                GetComponent<OriginalRoadRunnerSourceLauncher>() == null)
                return false;

            Projectile shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            // RocketL creates an 80-damage, 240-tick rocket; RoadRunner's
            // separate 360-tick -40 effect only applies after attachment.
            shot.impactDamage = OriginalRocketSourceInventory.RocketDamage;
            shot.lifetimeSeconds =
                (OriginalRocketSourceInventory.OriginalRocketLifeTicks +
                    OriginalRoadRunnerSourceCharge.AttachedSourceTicks) *
                Time.fixedDeltaTime + 2f;
            shot.gameObject.AddComponent<OriginalRoadRunnerSourceCharge>().Configure(this);
            shot.Launch(this, muzzle.forward);
            return true;
        }
#endif

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = massKg;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Health = maximumHealth;
#if UNITY_EDITOR
            sourceMachineGun = GetComponent<OriginalMachineGunSourceTest>();
#endif
        }

        public void SetControls(float gas, float turning, float braking, bool shoot)
        {
            throttle = Mathf.Clamp(gas, -1f, 1f);
            steer = Mathf.Clamp(turning, -1f, 1f);
            brake = Mathf.Clamp01(braking);
            firing = shoot;
        }

        private void FixedUpdate()
        {
            if (!IsAlive) return;
            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.4f,
                Vector3.down, out _, 2.6f, groundLayers, QueryTriggerInteraction.Ignore);
            if (grounded)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
                float limit = throttle >= 0f ? maximumSpeed : reverseSpeed;
                if (Mathf.Abs(localVelocity.z) < limit ||
                    Mathf.Sign(localVelocity.z) != Mathf.Sign(throttle))
                {
                    body.AddForce(transform.forward * throttle * acceleration, ForceMode.Acceleration);
                }
                body.AddForce(-transform.right * localVelocity.x * lateralGrip, ForceMode.Acceleration);
                if (brake > 0.01f && body.linearVelocity.sqrMagnitude > 0.1f)
                    body.AddForce(-body.linearVelocity.normalized * brakingPower * brake,
                        ForceMode.Acceleration);
                float steeringFactor = Mathf.Clamp01(Mathf.Abs(localVelocity.z) / 6f);
                float reverse = localVelocity.z < -0.5f ? -1f : 1f;
                body.MoveRotation(body.rotation * Quaternion.Euler(
                    0f, steer * turningSpeed * steeringFactor * reverse * Time.fixedDeltaTime, 0f));
            }

            bool shouldShoot = firing && HasWeapon && Time.time >= nextShot;
#if UNITY_EDITOR
            // On genuine source-car test prefabs only, honor the original
            // MGun tag/heat state machine instead of the placeholder cooldown.
            if (sourceMachineGun != null)
                shouldShoot = HasWeapon && sourceMachineGun.TryFire(firing);
#endif
            if (shouldShoot)
            {
                nextShot = Time.time + fireCooldown;
                Projectile shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
#if UNITY_EDITOR
                if (sourceMachineGun != null)
                {
                    // The original Bullet.cs uses 45 source update ticks and
                    // MGun.cs uses a 7-point base hit. Mapping ticks to
                    // Unity fixedDeltaTime is explicitly a provisional choice.
                    shot.impactDamage = OriginalMachineGunSourceTest.NormalBulletDamage;
                    shot.lifetimeSeconds =
                        OriginalMachineGunSourceTest.BulletLifeSourceTicks * Time.fixedDeltaTime;
                }
#endif
                shot.Launch(this, muzzle.forward);
            }
        }

        public void TakeDamage(float damage) => TakeDamage(damage, null);

        /// <summary>
        /// Attributes only the *fatal* valid hit to its real projectile owner.
        /// Untagged/environmental hits do not invent a killer or carry over
        /// an earlier attacker. Existing uncredited damage callers still work.
        /// </summary>
        public void TakeDamage(float damage, VehicleDrive attacker)
        {
            if (!IsAlive || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return;
            Health = Mathf.Max(0f, Health - damage);
            if (Health > 0f) return;
            SetControls(0f, 0f, 1f, false);
            Destroyed?.Invoke(this);
            Eliminated?.Invoke(this, attacker != this ? attacker : null);
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;
            body.isKinematic = true;
        }
    }
}
