using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchBootstrap : MonoBehaviour
    {
        private GameCatalog catalog;
        private VisualElement root;
        private VisualElement menu;
        private VisualElement hud;
        private DropdownField characterSelector;
        private DropdownField vehicleSelector;
        private DropdownField arenaSelector;
        private Label healthLabel;
        private Label enemiesLabel;
        private Label statusLabel;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<VehicleDrive> enemies = new List<VehicleDrive>();
        private VehicleDrive player;
        private ChaseCamera chase;
        private GameObject arenaInstance;
        private string[] characterIds;
        private string[] vehicleIds;
        private string[] arenaIds;

        private void Start()
        {
            catalog = CatalogRepository.Load();
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) throw new InvalidOperationException("Missing UI Toolkit panel");
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.025f, 0.035f, 0.065f, 0.88f);
            BuildMenu();
            BuildHud();
        }

        private void BuildMenu()
        {
            menu = new VisualElement();
            menu.style.width = 520;
            menu.style.maxWidth = Length.Percent(92);
            menu.style.marginLeft = Length.Percent(5);
            menu.style.marginTop = 42;
            menu.style.paddingLeft = 24;
            menu.style.paddingRight = 24;
            menu.style.paddingTop = 24;
            menu.style.paddingBottom = 24;
            menu.style.backgroundColor = new Color(0.065f, 0.08f, 0.13f, 0.96f);
            menu.style.borderTopLeftRadius = 16;
            menu.style.borderTopRightRadius = 16;
            menu.style.borderBottomLeftRadius = 16;
            menu.style.borderBottomRightRadius = 16;
            root.Add(menu);
            AddHeading(menu, "VIGILANTE 8", 34);
            AddHeading(menu, "OFFENCE 3  /  UNIFIED PC EDITION", 13);

            var characters = catalog.characters.OrderBy(c => c.name).ToArray();
            characterIds = characters.Select(c => c.id).ToArray();
            characterSelector = new DropdownField("PILOTO", characters.Select(c => c.name).ToList(), 0);
            StyleDropdown(characterSelector);
            menu.Add(characterSelector);
            characterSelector.RegisterValueChangedCallback(_ => RefreshVehicles());

            vehicleSelector = new DropdownField("VEÍCULO");
            StyleDropdown(vehicleSelector);
            menu.Add(vehicleSelector);
            RefreshVehicles();

            var arenas = catalog.arenas.OrderBy(a => a.name).ToArray();
            arenaIds = arenas.Select(a => a.id).ToArray();
            arenaSelector = new DropdownField("ARENA", arenas.Select(a =>
                a.name + (a.bonus ? "   ·   BÔNUS N64" : "")).ToList(), 0);
            StyleDropdown(arenaSelector);
            menu.Add(arenaSelector);

            var start = new Button(StartMatch) { text = "INICIAR COMBATE" };
            start.style.height = 52;
            start.style.marginTop = 25;
            start.style.backgroundColor = new Color(0.89f, 0.38f, 0.16f);
            start.style.color = Color.white;
            start.style.unityFontStyleAndWeight = FontStyle.Bold;
            menu.Add(start);
            AddHeading(menu, "WASD / SETAS: dirigir   ·   SHIFT: freio   ·   ESPAÇO: disparar", 12);
        }

        private static void AddHeading(VisualElement parent, string content, int size)
        {
            var label = new Label(content);
            label.style.color = Color.white;
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 10;
            parent.Add(label);
        }

        private static void StyleDropdown(DropdownField dropdown)
        {
            dropdown.style.color = Color.white;
            dropdown.style.marginBottom = 14;
            dropdown.style.minHeight = 46;
        }

        private void RefreshVehicles()
        {
            int index = characterSelector.index;
            if (index < 0 || index >= characterIds.Length) return;
            var vehicles = catalog.vehicles.Where(v => v.characterId == characterIds[index])
                .OrderBy(v => v.name).ToArray();
            vehicleIds = vehicles.Select(v => v.id).ToArray();
            vehicleSelector.choices = vehicles.Select(v => (string.IsNullOrWhiteSpace(v.officialName) ? v.name : v.officialName) +
                "   ·   " + v.game.ToUpperInvariant()).ToList();
            vehicleSelector.index = vehicles.Length > 0 ? 0 : -1;
        }

        private void BuildHud()
        {
            hud = new VisualElement();
            hud.style.position = Position.Absolute;
            hud.style.left = 24;
            hud.style.top = 24;
            hud.style.paddingLeft = 16;
            hud.style.paddingRight = 16;
            hud.style.paddingTop = 12;
            hud.style.paddingBottom = 12;
            hud.style.backgroundColor = new Color(0.03f, 0.035f, 0.06f, 0.75f);
            hud.style.display = DisplayStyle.None;
            root.Add(hud);
            healthLabel = new Label();
            enemiesLabel = new Label();
            statusLabel = new Label();
            foreach (var label in new[] {healthLabel, enemiesLabel, statusLabel})
            {
                label.style.color = Color.white;
                label.style.fontSize = 17;
                label.style.marginBottom = 8;
                hud.Add(label);
            }
            var quit = new Button(BackToMenu) { text = "VOLTAR AO MENU" };
            quit.style.height = 35;
            hud.Add(quit);
        }

        private void StartMatch()
        {
            if (vehicleIds.Length == 0 || arenaSelector.index < 0 || vehicleSelector.index < 0) return;
            BackToMenu();
            var selected = catalog.vehicles.First(v => v.id == vehicleIds[vehicleSelector.index]);
            var arena = catalog.arenas.First(a => a.id == arenaIds[arenaSelector.index]);
            var arenaAsset = CatalogRepository.LoadArenaPrefab(arena);
            var playerAsset = CatalogRepository.LoadVehiclePrefab(selected);
            if (arenaAsset == null || playerAsset == null)
            {
                Debug.LogError("Missing authored content: " + arena.assetId + " / " + selected.assetId);
                return;
            }
            arenaInstance = Instantiate(arenaAsset);
            var spawns = arenaInstance.GetComponentInChildren<ArenaSpawnPoints>();
            if (spawns == null || !spawns.HasValidSpawns(4))
            {
                Debug.LogError("Incomplete arena spawn configuration: " + arena.assetId);
                Destroy(arenaInstance);
                arenaInstance = null;
                return;
            }
            player = SpawnVehicle(selected, spawns.GetSpawn(0), true);
            if (player == null) { BackToMenu(); return; }
            var opponents = catalog.vehicles.Where(v => v.characterId != selected.characterId)
                .OrderBy(v => v.id).ToArray();
            int cursor = Array.FindIndex(opponents, v => v.id.CompareTo(selected.id) > 0);
            if (cursor < 0) cursor = 0;
            var usedDrivers = new HashSet<string> {selected.characterId};
            for (int n = 0; n < 3; n++)
            {
                VehicleEntry opponent = null;
                for (int attempt = 0; attempt < opponents.Length; attempt++)
                {
                    var candidate = opponents[(cursor + attempt) % opponents.Length];
                    if (usedDrivers.Add(candidate.characterId))
                    {
                        opponent = candidate;
                        cursor = (cursor + attempt + 1) % opponents.Length;
                        break;
                    }
                }
                if (opponent == null) break;
                var enemy = SpawnVehicle(opponent, spawns.GetSpawn(n + 1), false);
                if (enemy == null) { BackToMenu(); return; }
                enemy.gameObject.AddComponent<BotVehicleInput>().target = player.transform;
                enemies.Add(enemy);
            }
            if (enemies.Count != 3)
            {
                Debug.LogError("Match setup requires three distinct opponent characters.");
                BackToMenu();
                return;
            }
            chase = Camera.main != null ? Camera.main.GetComponent<ChaseCamera>() : null;
            if (chase == null) Debug.LogError("Camera has no ChaseCamera component.");
            else chase.target = player.transform;
            menu.style.display = DisplayStyle.None;
            root.style.backgroundColor = Color.clear;
            hud.style.display = DisplayStyle.Flex;
        }

        private VehicleDrive SpawnVehicle(VehicleEntry descriptor, Transform spawn, bool controlled)
        {
            var asset = CatalogRepository.LoadVehiclePrefab(descriptor);
            if (asset == null) { Debug.LogError("Missing authored vehicle: " + descriptor.assetId); return null; }
            var instance = Instantiate(asset, spawn.position, spawn.rotation);
            instance.name = descriptor.name;
            spawned.Add(instance);
            var drive = instance.GetComponent<VehicleDrive>();
            if (drive == null) { Debug.LogError("Missing VehicleDrive: " + descriptor.assetId); return null; }
            if (controlled) instance.AddComponent<PlayerVehicleInput>();
            return drive;
        }

        private void Update()
        {
            if (hud == null || hud.style.display.value != DisplayStyle.Flex || player == null) return;
            int alive = enemies.Count(enemy => enemy != null && enemy.IsAlive);
            healthLabel.text = "INTEGRIDADE   " + Mathf.RoundToInt(player.Health);
            enemiesLabel.text = "ADVERSÁRIOS   " + alive + " / 3";
            statusLabel.text = !player.IsAlive ? "VOCÊ FOI DESTRUÍDO" :
                alive == 0 ? "VITÓRIA" : "COMBATE EM ANDAMENTO";
        }

        private void BackToMenu()
        {
            if (chase != null) chase.target = null;
            chase = null;
            player = null;
            enemies.Clear();
            foreach (var vehicle in spawned) if (vehicle != null) Destroy(vehicle);
            spawned.Clear();
            if (arenaInstance != null) Destroy(arenaInstance);
            arenaInstance = null;
            if (menu != null) menu.style.display = DisplayStyle.Flex;
            if (hud != null) hud.style.display = DisplayStyle.None;
            if (root != null) root.style.backgroundColor = new Color(0.025f, 0.035f, 0.065f, 0.88f);
        }

        private void OnDestroy()
        {
            foreach (var vehicle in spawned) if (vehicle != null) Destroy(vehicle);
            if (arenaInstance != null) Destroy(arenaInstance);
        }
    }
}
