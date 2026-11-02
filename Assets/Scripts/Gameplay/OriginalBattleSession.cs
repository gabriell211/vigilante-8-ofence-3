#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Editor-only encounter monitor for the genuine V82 terrain/car playtest.
    /// Displays real VehicleDrive health and actual official pilot identities.
    /// This is diagnostic UI, not certified PS1 gameplay or a shipping menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalBattleSession : MonoBehaviour
    {
        public enum BattleOutcome { InProgress, Victory, Defeat }

        [SerializeField] private string originalArenaId;
        [SerializeField] private string originalPlayerId;
        [SerializeField] private VehicleDrive player;
        [SerializeField] private VehicleDrive[] opponents = Array.Empty<VehicleDrive>();
        [SerializeField] private string[] opponentIds = Array.Empty<string>();

        private bool ready;
        private bool paused;
        private string arenaName;
        private string playerName;
        private string[] opponentNames;
        private BattleOutcome outcome = BattleOutcome.InProgress;
        private int playerEliminations;
        private int[] opponentEliminations = Array.Empty<int>();
        private string lastElimination = "SEM ELIMINACOES";

        public string OriginalArenaId => originalArenaId;
        public string OriginalPlayerId => originalPlayerId;
        public int RemainingOpponents => opponents?.Count(v => v != null && v.IsAlive) ?? 0;
        public int TotalOpponents => opponents?.Length ?? 0;
        public BattleOutcome Outcome => outcome;
        public int PlayerEliminations => playerEliminations;
        public int TotalEliminations => playerEliminations + opponentEliminations.Sum();
        public string LastElimination => lastElimination;
        public int GetOpponentEliminations(int index)
        {
            if (index < 0 || index >= opponentEliminations.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return opponentEliminations[index];
        }
        public bool IsReleaseApproved => false;

        public void Configure(string arenaId, string vehicleId, VehicleDrive playerDrive,
            string[] enemyIds, VehicleDrive[] enemyDrives)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Configure the source battle before Play Mode");
            if (string.IsNullOrWhiteSpace(arenaId) || string.IsNullOrWhiteSpace(vehicleId) ||
                playerDrive == null || enemyIds == null || enemyDrives == null ||
                enemyIds.Length < 1 || enemyIds.Length > 3 ||
                enemyIds.Length != enemyDrives.Length ||
                enemyIds.Any(string.IsNullOrWhiteSpace) || enemyDrives.Any(v => v == null) ||
                enemyIds.Contains(vehicleId) ||
                enemyIds.Distinct(StringComparer.Ordinal).Count() != enemyIds.Length ||
                enemyDrives.Contains(playerDrive) ||
                enemyDrives.Distinct().Count() != enemyDrives.Length)
                throw new ArgumentException("Original battle requires one player and one to three unique enemies");

            GameCatalog catalog = CatalogRepository.Load();
            if (!catalog.arenas.Any(a => a.id == arenaId && a.game == "v82") ||
                arenaId == "v82_meteor_crater")
                throw new ArgumentException("Only preserved original V82 terrain is supported");
            var chosen = new[] { vehicleId }.Concat(enemyIds).Select(id =>
                catalog.vehicles.SingleOrDefault(v => v.id == id && v.game == "v82")).ToArray();
            if (chosen.Any(v => v == null) ||
                chosen.Select(v => v.characterId).Distinct(StringComparer.Ordinal).Count() != chosen.Length ||
                playerDrive.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId != vehicleId ||
                playerDrive.GetComponent<PlayerVehicleInput>() == null)
                throw new ArgumentException("Missing original player car, pilot or controls");
            for (int i = 0; i < enemyIds.Length; i++)
                if (enemyDrives[i].GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId != enemyIds[i] ||
                    enemyDrives[i].GetComponent<BotVehicleInput>() == null)
                    throw new ArgumentException("Enemy must be an authentic original car controlled by AI");

            originalArenaId = arenaId;
            originalPlayerId = vehicleId;
            player = playerDrive;
            opponents = (VehicleDrive[])enemyDrives.Clone();
            opponentIds = (string[])enemyIds.Clone();
            opponentEliminations = new int[enemyIds.Length];
            playerEliminations = 0;
            lastElimination = "SEM ELIMINACOES";
            CacheLabels(catalog);
        }

        private void Start()
        {
            if (player == null || opponents == null || opponentIds == null ||
                opponents.Length != opponentIds.Length || opponents.Length < 1 ||
                player.GetComponent<LegacyVehiclePlaytest>()?.OriginalVehicleId != originalPlayerId)
            {
                Debug.LogError("Original battle encounter was not configured.");
                enabled = false;
                return;
            }
            CacheLabels(CatalogRepository.Load());
            opponentEliminations = new int[opponents.Length];
            playerEliminations = 0;
            lastElimination = "SEM ELIMINACOES";
            outcome = BattleOutcome.InProgress;
            ready = true;
            player.Eliminated += OnVehicleEliminated;
            foreach (VehicleDrive opponent in opponents)
                opponent.Eliminated += OnVehicleEliminated;
        }

        private void CacheLabels(GameCatalog catalog)
        {
            arenaName = catalog.arenas.First(a => a.id == originalArenaId).name;
            playerName = OfficialName(catalog, originalPlayerId);
            opponentNames = opponentIds.Select(id => OfficialName(catalog, id)).ToArray();
        }

        private static string OfficialName(GameCatalog catalog, string id)
        {
            VehicleEntry vehicle = catalog.vehicles.First(v => v.id == id);
            CharacterEntry pilot = OfficialMenuSelections.PilotForCar(catalog, vehicle);
            if (pilot == null)
                throw new InvalidOperationException("The official source pilot is missing: " + id);
            return vehicle.officialName + " / " + pilot.name;
        }

        /// <summary>Deterministic result evaluation; defeat takes precedence on simultaneous KOs.</summary>
        public static BattleOutcome EvaluateOutcome(bool playerAlive, int activeOpponents)
        {
            if (activeOpponents < 0)
                throw new ArgumentOutOfRangeException(nameof(activeOpponents));
            if (!playerAlive) return BattleOutcome.Defeat;
            return activeOpponents == 0 ? BattleOutcome.Victory : BattleOutcome.InProgress;
        }

        /// <summary>
        /// Counts only a fatal projectile hit from a participant in this exact
        /// battle. Environment deaths and external attackers never earn points.
        /// </summary>
        private void OnVehicleEliminated(VehicleDrive victim, VehicleDrive killer)
        {
            if (!ready || victim == null) return;
            bool member = victim == player || Array.IndexOf(opponents, victim) >= 0;
            if (!member) return;
            string victimName = victim == player ? playerName :
                opponentNames[Array.IndexOf(opponents, victim)];
            string killerName = "SEM AUTOR";
            if (killer != victim && killer != null)
            {
                if (killer == player)
                {
                    playerEliminations++;
                    killerName = playerName;
                }
                else
                {
                    int index = Array.IndexOf(opponents, killer);
                    if (index >= 0)
                    {
                        opponentEliminations[index]++;
                        killerName = opponentNames[index];
                    }
                }
            }
            lastElimination = killerName + " > " + victimName;
            outcome = EvaluateOutcome(player.IsAlive, RemainingOpponents);
        }

        private void Update()
        {
            if (!ready) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }
            if (paused || outcome != BattleOutcome.InProgress) return;
            outcome = EvaluateOutcome(player.IsAlive, RemainingOpponents);
        }

        private void OnGUI()
        {
            if (!ready || !Application.isPlaying) return;
            float width = Mathf.Clamp(Screen.width - 36f, 260f, 440f);
            float height = 212f + TotalOpponents * 24f;
            GUI.Box(new Rect(16f, 16f, width, height), GUIContent.none);
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.99f, 0.79f, 0.38f) }
            };
            var regular = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            float y = 23f;
            GUI.Label(new Rect(28f, y, width - 24f, 26f), "V8 2ND OFFENSE / TESTE COM ASSETS ORIGINAIS", title);
            y += 27f;
            GUI.Label(new Rect(28f, y, width - 24f, 22f), "TERRENO: " + arenaName, regular);
            y += 23f;
            GUI.Label(new Rect(28f, y, width - 24f, 22f),
                "JOGADOR: " + playerName + "  |  HP " + Mathf.CeilToInt(player.Health) +
                "  |  KOs " + playerEliminations, regular);
            y += 23f;
            for (int i = 0; i < opponents.Length; i++)
            {
                VehicleDrive enemy = opponents[i];
                string hp = enemy == null ? "SEM REFERENCIA" :
                    enemy.IsAlive ? Mathf.CeilToInt(enemy.Health).ToString() : "DESTRUIDO";
                GUI.Label(new Rect(28f, y, width - 24f, 22f),
                    "IA " + (i + 1) + ": " + opponentNames[i] + "  |  HP " + hp +
                    "  |  KOs " + opponentEliminations[i], regular);
                y += 24f;
            }
            string state = outcome == BattleOutcome.Victory ? "VITORIA (TESTE)" :
                outcome == BattleOutcome.Defeat ? "DERROTA (TESTE)" :
                "ADVERSARIOS ATIVOS: " + RemainingOpponents + "/" + TotalOpponents;
            GUI.Label(new Rect(28f, y, width - 24f, 22f), state, title);
            y += 25f;
            GUI.Label(new Rect(28f, y, width - 24f, 22f),
                "ULTIMA ELIMINACAO: " + lastElimination, regular);
            y += 24f;
            GUI.Label(new Rect(28f, y, width - 24f, 22f),
                "P: PAUSA  |  WASD: DIRIGIR  |  ESPACO: TIRO DE DIAGNOSTICO", regular);
            if (paused)
                GUI.Label(new Rect(28f, y + 22f, width - 24f, 22f), "PAUSADO (TESTE)", title);
        }

        private void OnDisable()
        {
            ready = false;
            if (player != null) player.Eliminated -= OnVehicleEliminated;
            if (opponents != null)
                foreach (VehicleDrive opponent in opponents)
                    if (opponent != null) opponent.Eliminated -= OnVehicleEliminated;
            if (paused)
            {
                paused = false;
                Time.timeScale = 1f;
            }
        }
    }
}
#endif
