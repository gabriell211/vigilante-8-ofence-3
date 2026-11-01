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
#endif

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = massKg;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Health = maximumHealth;
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

            if (firing && HasWeapon && Time.time >= nextShot)
            {
                nextShot = Time.time + fireCooldown;
                Projectile shot = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
                shot.Launch(this, muzzle.forward);
            }
        }

        public void TakeDamage(float damage)
        {
            if (!IsAlive || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return;
            Health = Mathf.Max(0f, Health - damage);
            if (Health > 0f) return;
            SetControls(0f, 0f, 1f, false);
            Destroyed?.Invoke(this);
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;
            body.isKinematic = true;
        }
    }
}
