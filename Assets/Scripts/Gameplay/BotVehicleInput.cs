using UnityEngine;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(VehicleDrive))]
    public sealed class BotVehicleInput : MonoBehaviour
    {
        public Transform target;
        [Min(1f)] public float shootingRange = 75f;
        [Min(0.1f)] public float decisionInterval = 0.1f;
        [Min(1f)] public float obstacleDistance = 14f;
        [SerializeField] private LayerMask obstructionLayers = -1;
        private VehicleDrive vehicle;
        private float nextDecision;

        private void Awake() => vehicle = GetComponent<VehicleDrive>();
        private void FixedUpdate()
        {
            if (!vehicle.IsAlive) return;
            if (target == null)
            {
                vehicle.SetControls(0f, 0f, 1f, false);
                return;
            }
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + decisionInterval;
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f) return;
            Vector3 local = transform.InverseTransformDirection(delta.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float steering = Mathf.Clamp(angle / 45f, -1f, 1f);
            float gas = Mathf.Abs(angle) > 115f ? 0.45f : 1f;

            if (Physics.SphereCast(transform.position + Vector3.up, 1f,
                transform.forward, out RaycastHit obstacle, obstacleDistance,
                obstructionLayers, QueryTriggerInteraction.Ignore) &&
                !obstacle.collider.transform.IsChildOf(transform) &&
                !obstacle.collider.transform.IsChildOf(target))
            {
                Vector3 localAvoid = transform.InverseTransformDirection(obstacle.normal);
                steering = localAvoid.x >= 0f ? 1f : -1f;
                gas = 0.55f;
            }

            bool aligned = Mathf.Abs(angle) < 12f;
            bool inRange = delta.sqrMagnitude < shootingRange * shootingRange;
            vehicle.SetControls(gas, steering, 0f, aligned && inRange);
        }
    }
}
