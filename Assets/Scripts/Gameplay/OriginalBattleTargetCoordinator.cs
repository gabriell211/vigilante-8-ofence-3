#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Development-only free-for-all targeting for the 2nd Offense source-car battle.
    /// Participants are real, distinct source vehicles; no generic enemies are built.
    /// Never claims equivalence with the original PS1 opponent AI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalBattleTargetCoordinator : MonoBehaviour
    {
        [Min(0.05f)] public float refreshSeconds = 0.35f;
        [SerializeField] private VehicleDrive player;
        [SerializeField] private VehicleDrive[] opponents = Array.Empty<VehicleDrive>();

        private VehicleDrive[] participants = Array.Empty<VehicleDrive>();
        private BotVehicleInput[] botInputs = Array.Empty<BotVehicleInput>();
        private float nextRefresh;
        public bool IsReleaseApproved => false;
        public int ParticipantCount => participants?.Length ?? 0;

        public void Configure(VehicleDrive playerVehicle, VehicleDrive[] enemyVehicles)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Configure the battle targeting before Play Mode");
            if (playerVehicle == null || enemyVehicles == null ||
                enemyVehicles.Length < 1 || enemyVehicles.Length > 3 ||
                enemyVehicles.Any(v => v == null || v == playerVehicle) ||
                enemyVehicles.Distinct().Count() != enemyVehicles.Length ||
                playerVehicle.GetComponent<PlayerVehicleInput>() == null ||
                enemyVehicles.Any(v => v.GetComponent<BotVehicleInput>() == null))
                throw new ArgumentException("One genuine player car and one to three distinct AI cars are required");

            VehicleDrive[] all = new[] { playerVehicle }.Concat(enemyVehicles).ToArray();
            string[] ids = all.Select(v => v.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId)
                .ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
                ids.Any(id => !id.StartsWith("v82_", StringComparison.Ordinal)))
                throw new ArgumentException("Only different original V82 source cars may enter targeting");

            GameCatalog catalog = CatalogRepository.Load();
            var official = ids.Select(id => catalog.vehicles.SingleOrDefault(v =>
                v.game == "v82" && v.id == id)).ToArray();
            if (official.Any(v => v == null) ||
                official.Select(v => v.characterId).Distinct(StringComparer.Ordinal).Count()
                    != official.Length)
                throw new ArgumentException("Source battle targeting requires distinct official pilots");

            player = playerVehicle;
            opponents = (VehicleDrive[])enemyVehicles.Clone();
            Initialize();
        }

        private void Awake()
        {
            if (player != null && opponents != null && opponents.Length > 0)
                Initialize();
        }

        private void Initialize()
        {
            if (player == null || opponents == null || opponents.Length < 1 ||
                opponents.Length > 3 || opponents.Any(v => v == null || v == player) ||
                opponents.Distinct().Count() != opponents.Length)
            {
                participants = Array.Empty<VehicleDrive>();
                botInputs = Array.Empty<BotVehicleInput>();
                return;
            }
            participants = new[] { player }.Concat(opponents).ToArray();
            botInputs = opponents.Select(v => v.GetComponent<BotVehicleInput>()).ToArray();
        }

        /// <summary>
        /// Deterministic nearest-live-opponent selection. Index order breaks ties
        /// so a physics frame cannot select an arbitrary target.
        /// </summary>
        public static VehicleDrive SelectTarget(VehicleDrive seeker,
            IReadOnlyList<VehicleDrive> candidates)
        {
            if (seeker == null || !seeker.IsAlive || candidates == null) return null;
            VehicleDrive nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                VehicleDrive candidate = candidates[i];
                if (candidate == null || candidate == seeker || !candidate.IsAlive) continue;
                float distance = (candidate.transform.position - seeker.transform.position).sqrMagnitude;
                if (float.IsNaN(distance) || float.IsInfinity(distance) ||
                    distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        private void Start()
        {
            if (participants.Length == 0 || botInputs.Any(bot => bot == null))
            {
                Debug.LogError("Source battle targeting was not correctly configured");
                enabled = false;
                return;
            }
            RefreshTargets();
        }

        private void Update()
        {
            if (participants.Length == 0 || Time.time < nextRefresh) return;
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            nextRefresh = Time.time + Mathf.Max(0.05f, refreshSeconds);
            var playerLauncher = player.GetComponent<OriginalBastionRocketLauncher>();
            if (playerLauncher != null)
                playerLauncher.SetTarget(SelectTarget(player, participants));
            for (int i = 0; i < opponents.Length; i++)
            {
                BotVehicleInput bot = botInputs[i];
                if (bot == null) continue;
                VehicleDrive target = SelectTarget(opponents[i], participants);
                bot.target = target == null ? null : target.transform;
                var launcher = opponents[i].GetComponent<OriginalBastionRocketLauncher>();
                if (launcher != null) launcher.SetTarget(target);
            }
        }
    }
}
#endif
