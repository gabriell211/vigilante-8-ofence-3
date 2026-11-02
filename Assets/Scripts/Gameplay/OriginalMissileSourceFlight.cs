#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Source-derived Shell -> Projectile flight from archived
    /// LegacySources/Vigilante2Unity/Assets/Scripts/Missile.cs.
    /// Original DAT_84 lock, physics1.Z/W, physics2.X and M2 fields use PS1
    /// fixed point. The aiming ratios below use Unity Rigidbody units and are
    /// development approximations, NOT certified original missile physics.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(Projectile))]
    [DisallowMultipleComponent]
    public sealed class OriginalMissileSourceFlight : MonoBehaviour
    {
        public enum SourceState { Shell, Projectile }

        public const int OriginalDamage = 60; // MissileL.UpdateW(arg1=12)
        public const int OriginalLifeTicks = 480; // Missile.UpdateW(arg1=5)
        public const int HorizontalErrorDivisor = 16; // Missile.Projectile
        public const int VerticalErrorDivisor = 8; // Missile.Projectile

        private VehicleDrive lockedTarget;
        private Rigidbody body;
        private int ticksRemaining;
        private SourceState state;
        private bool initialized;

        public VehicleDrive LockedTarget => lockedTarget;
        public SourceState State => state;
        public int TicksRemaining => ticksRemaining;
        public bool IsReleaseApproved => false;

        public void Configure(VehicleDrive target)
        {
            if (initialized || target == null || !target.IsAlive)
                throw new ArgumentException("A fresh missile requires a living, authentic battle target");
            body = GetComponent<Rigidbody>();
            if (body == null || GetComponent<Projectile>() == null ||
                GetComponent<Collider>() == null)
                throw new InvalidOperationException("Original missile flight requires a diagnostic physics projectile");

            lockedTarget = target;
            ticksRemaining = OriginalLifeTicks;
            state = SourceState.Shell;
            initialized = true;
        }

        /// <summary>Preserved Missile.UpdateW(arg1=5) Shell -> Projectile path.</summary>
        public void ActivateProjectile()
        {
            if (!initialized || state != SourceState.Shell)
                throw new InvalidOperationException("Only an initialized source shell can become a projectile");
            state = SourceState.Projectile;
        }

        /// <summary>
        /// The source applies vertical error /8 and horizontal error /16,
        /// clamping the original horizontal integer increments to +/-0x100.
        /// Here the limit is normalized by Unity speed rather than mislabelled
        /// as an exact conversion of the source fixed-point scale.
        /// </summary>
        public static Vector3 CalculateSteeringDelta(Vector3 currentVelocity,
            Vector3 desiredVelocity)
        {
            if (!Finite(currentVelocity) || !Finite(desiredVelocity))
                throw new ArgumentException("Nonfinite projectile velocity");
            float speed = Mathf.Max(1f, currentVelocity.magnitude, desiredVelocity.magnitude);
            float horizontalLimit = speed / HorizontalErrorDivisor;
            return new Vector3(
                Mathf.Clamp((desiredVelocity.x - currentVelocity.x) /
                    HorizontalErrorDivisor, -horizontalLimit, horizontalLimit),
                (desiredVelocity.y - currentVelocity.y) / VerticalErrorDivisor,
                Mathf.Clamp((desiredVelocity.z - currentVelocity.z) /
                    HorizontalErrorDivisor, -horizontalLimit, horizontalLimit));
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void FixedUpdate()
        {
            if (!initialized || state != SourceState.Projectile) return;
            if (ticksRemaining <= 0 || lockedTarget == null || !lockedTarget.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            Collider targetCollider = lockedTarget.GetComponentInChildren<Collider>();
            Vector3 destination = targetCollider != null ?
                targetCollider.bounds.center : lockedTarget.transform.position;
            Vector3 toTarget = destination - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector3 velocity = body.linearVelocity;
                float speed = Mathf.Max(1f, velocity.magnitude);
                Vector3 desired = toTarget.normalized * speed;
                body.linearVelocity = velocity + CalculateSteeringDelta(velocity, desired);
                if (body.linearVelocity.sqrMagnitude > 0.0001f)
                    body.MoveRotation(Quaternion.LookRotation(
                        body.linearVelocity.normalized, Vector3.up));
            }
            ticksRemaining--;
            if (ticksRemaining <= 0)
                Destroy(gameObject);
        }
    }
}
#endif
