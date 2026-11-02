using System;
using UnityEngine;
using Vigilante8.Gameplay;

namespace Vigilante8.Content
{
    /// <summary>
    /// Verbatim V82 GameManager.VehicleData fields kept alongside authentic TMD meshes.
    /// Physics mapping below is an explicit PROTOTYPE calibration, not a verified
    /// reproduction of the PS1 fixed-point engine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegacyVehicleHandlingProfile : MonoBehaviour
    {
        [SerializeField] private string vehicleId;
        [SerializeField] private string sourceEnum;
        [SerializeField] private int rawHalfHealth;
        [SerializeField] private int rawLightness;
        [SerializeField] private int dat0e, dat0f, dat10, dat11, dat12, dat13, dat15, dat2a;

        public string VehicleId => vehicleId;
        public string SourceEnum => sourceEnum;
        public int RawHalfHealth => rawHalfHealth;
        public int RawLightness => rawLightness;
        public int RawAccelerationField => dat10;
        public int RawSpeedField => dat12;
        public int RawTurningField => dat13;

#if UNITY_EDITOR
        public void Configure(string id, string originalEnum, int health, int lightness,
            int field0e, int field0f, int field10, int field11, int field12,
            int field13, int field15, int field2a)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("v82_", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(originalEnum) || health <= 0 || lightness <= 0 ||
                field10 < 0 || field11 < 0 || field12 < 0 || field13 < 0 ||
                field15 < 0 || field2a < 0)
                throw new ArgumentException("Invalid original V82 source vehicle configuration");
            vehicleId = id;
            sourceEnum = originalEnum;
            rawHalfHealth = health;
            rawLightness = lightness;
            dat0e = field0e;
            dat0f = field0f;
            dat10 = field10;
            dat11 = field11;
            dat12 = field12;
            dat13 = field13;
            dat15 = field15;
            dat2a = field2a;
        }

        public void ApplyProvisionalHandling(VehicleDrive drive)
        {
            if (drive == null || rawHalfHealth <= 0 || rawLightness <= 0)
                throw new InvalidOperationException("Missing source-informed development car");

            // Do NOT treat these scaling functions as reverse-engineered PS1 physics.
            // Different cars receive different tuning from their genuine data.
            drive.massKg = Mathf.Clamp(6000000f / rawLightness, 850f, 4300f);
            drive.maximumHealth = Mathf.Clamp(rawHalfHealth / 8f, 60f, 200f);
            drive.acceleration = Mathf.Clamp(9f + dat10 * 0.35f, 10f, 40f);
            drive.maximumSpeed = Mathf.Clamp(18f + dat12 * 0.20f, 20f, 70f);
            drive.turningSpeed = Mathf.Clamp(25f + dat13 * 0.45f, 35f, 140f);
            drive.lateralGrip = Mathf.Clamp(2f + dat11 * 0.055f, 2f, 12f);
            drive.brakingPower = Mathf.Clamp(20f + dat15 * 0.32f, 20f, 55f);
        }
#endif
    }
}
