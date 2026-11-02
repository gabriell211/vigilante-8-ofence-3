#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Source-informed Rocket.UpdateW(2) -> Bastion.UpdateW(0) targeting from
    /// LegacySources/Vigilante2Unity/Assets/Scripts/Rocket.cs.
    /// This drives a diagnostic collider, NOT the original rocket model,
    /// fixed-point velocity, visual effects, audio, or certified source ticks.
    /// </summary>
    [RequireComponent(typeof(Projectile), typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class OriginalBastionRocketFlight : MonoBehaviour
    {
        public enum SourceState { Rocket, Bastion }
        public const int StrongSteeringThreshold = 211;
        public const int TargetValidationInterval = 16;

        private Rigidbody body;
        private VehicleDrive lockedTarget;
        private int remainingSourceTicks;
        private bool initialized;
        private SourceState sourceState;

        public SourceState State => sourceState;
        public int RemainingSourceTicks => remainingSourceTicks;
        public VehicleDrive LockedTarget => lockedTarget;
        public bool IsReleaseApproved => false;

        public void Configure(VehicleDrive target, int lifetimeTicks)
        {
            if (Application.isPlaying && initialized)
                throw new InvalidOperationException("An original rocket may only be configured once");
            if (target == null || !target.IsAlive || lifetimeTicks < 1)
                throw new ArgumentException("A live car target and positive source lifetime are required");
            body = GetComponent<Rigidbody>();
            if (body == null || GetComponent<Projectile>() == null ||
                GetComponent<Collider>() == null)
                throw new InvalidOperationException("The diagnostic rocket needs Unity projectile physics");
            lockedTarget = target;
            remainingSourceTicks = lifetimeTicks;
            sourceState = SourceState.Rocket;
            initialized = true;
        }

        /// <summary>
        /// The legacy Rocket changes to Bastion on UpdateW(arg1=2).
        /// Launchers explicitly issue this state transition after firing.
        /// </summary>
        public void EnterBastion()
        {
            if (!initialized || sourceState != SourceState.Rocket)
                throw new InvalidOperationException("Bastion requires an initialized Rocket state");
            sourceState = SourceState.Bastion;
        }

        /// <summary>
        /// The source steers gently while M2 >= 211 and strongly below 211.
        /// Its integer velocities are normalized to Unity units here; these
        /// coefficients approximate the phase relationship, not PS1 dynamics.
        /// </summary>
        public static float SourceSteeringWeight(int sourceTicksRemaining)
        {
            if (sourceTicksRemaining < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceTicksRemaining));
            return sourceTicksRemaining < StrongSteeringThreshold ? 0.5f : 1f / 16f;
        }

        private void FixedUpdate()
        {
            if (!initialized || sourceState != SourceState.Bastion) return;
            if (remainingSourceTicks <= 0 ||
                (remainingSourceTicks % TargetValidationInterval == 0 &&
                    (lockedTarget == null || !lockedTarget.IsAlive)))
            {
                Destroy(gameObject);
                return;
            }

            if (lockedTarget != null && lockedTarget.IsAlive)
            {
                Vector3 delta = lockedTarget.transform.position + Vector3.up -
                    transform.position;
                if (delta.sqrMagnitude > 0.01f)
                {
                    Vector3 oldVelocity = body.linearVelocity;
                    float speed = Mathf.Max(1f, oldVelocity.magnitude);
                    Vector3 desired = delta.normalized * speed;
                    body.linearVelocity = Vector3.Lerp(oldVelocity, desired,
                        SourceSteeringWeight(remainingSourceTicks));
                    if (body.linearVelocity.sqrMagnitude > 0.01f)
                        body.MoveRotation(Quaternion.LookRotation(
                            body.linearVelocity.normalized, Vector3.up));
                }
            }
            remainingSourceTicks--;
        }
    }
}
#endif
