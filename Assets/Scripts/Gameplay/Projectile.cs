using System.Collections.Generic;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class Projectile : MonoBehaviour
    {
        [Min(1f)] public float impactDamage = 15f;
        [Min(1f)] public float muzzleVelocity = 80f;
        [Min(0.1f)] public float lifetimeSeconds = 5f;
        [Min(0f)] public float explosionRadius = 0f;
        [SerializeField] private GameObject impactEffect;

        private VehicleDrive owner;
        private bool impacted;

        private void Awake()
        {
            var rigidbody = GetComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Launch(VehicleDrive source, Vector3 forward)
        {
            owner = source;
            var ownColliders = source.GetComponentsInChildren<Collider>();
            var projectileCollider = GetComponent<Collider>();
            foreach (var ownCollider in ownColliders)
                if (ownCollider.enabled) Physics.IgnoreCollision(projectileCollider, ownCollider);
            GetComponent<Rigidbody>().linearVelocity = forward.normalized * muzzleVelocity;
            Destroy(gameObject, lifetimeSeconds);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (impacted || owner == null) return;
            if (collision.collider.transform.IsChildOf(owner.transform)) return;
            impacted = true;
            if (explosionRadius <= 0f)
            {
                var victim = collision.collider.GetComponentInParent<VehicleDrive>();
                if (victim != null && victim != owner) victim.TakeDamage(impactDamage, owner);
            }
            else
            {
                var affected = new HashSet<VehicleDrive>();
                foreach (var hit in Physics.OverlapSphere(transform.position, explosionRadius))
                {
                    var victim = hit.GetComponentInParent<VehicleDrive>();
                    if (victim == null || victim == owner || !affected.Add(victim)) continue;
                    float distance = Vector3.Distance(transform.position, victim.transform.position);
                    float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                    victim.TakeDamage(impactDamage * falloff, owner);
                }
            }

            if (impactEffect != null)
                Destroy(Instantiate(impactEffect, transform.position, Quaternion.identity), 6f);
            Destroy(gameObject);
        }
    }
}
