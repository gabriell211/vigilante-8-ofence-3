using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Vigilante8.Content;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchBootstrap : MonoBehaviour
    {
        private enum ScreenPage { Home, Garage, Catalog, Options, Controls, About, Playing, Pause, Results }
        private ScreenPage activePage;
        private ScreenPage optionsReturn = ScreenPage.Home;
        private readonly GameMenuSettings preferences = new GameMenuSettings();
        private VisualElement homePage, catalogPage, optionsPage, controlsPage, aboutPage, pausePage, resultsPage;
        private ScrollView garagePage;
        private VisualElement catalogEntries;
        private DropdownField catalogCategory;
        private DropdownField catalogOrigin;
        private Label garageSummary;
        private Label readinessLabel, resultsTitle, resultsDetails;
        private Button startCombat;
        private bool finished;
        private bool restoringSelections;
        private string matchVehicle = "";
        private string matchArena = "";
        private static readonly Color Accent = new Color(0.98f, 0.39f, 0.15f);
        private static readonly Color Ink = new Color(0.026f, 0.036f, 0.062f);
        private static readonly Color Surface = new Color(0.066f, 0.086f, 0.13f);
        private static readonly Color Muted = new Color(0.68f, 0.74f, 0.82f);
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
            preferences.Load();
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) throw new InvalidOperationException("Missing UI Toolkit panel");
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.025f, 0.035f, 0.065f, 0.88f);
            restoringSelections = true;
            try
            {
                BuildMenu();
                RestoreLastOfficialSelections();
            }
            finally { restoringSelections = false; }
            UpdateAvailability();
            BuildHud();
            BuildMainMenu();
            BuildCatalogBrowser();
            BuildOptionsMenu();
            BuildControlsMenu();
            BuildAboutMenu();
            BuildPauseMenu();
            BuildResultsMenu();
            Show(ScreenPage.Home);
        }

        private void BuildMenu()
        {
            menu = new VisualElement();
            menu.style.width = 520;
            menu.style.maxWidth = Length.Percent(92);
            menu.style.alignSelf = Align.Center;
            menu.style.marginTop = 24;
            menu.style.marginBottom = 30;
            menu.style.paddingLeft = 24;
            menu.style.paddingRight = 24;
            menu.style.paddingTop = 24;
            menu.style.paddingBottom = 24;
            menu.style.backgroundColor = new Color(0.065f, 0.08f, 0.13f, 0.96f);
            menu.style.borderTopLeftRadius = 16;
            menu.style.borderTopRightRadius = 16;
            menu.style.borderBottomLeftRadius = 16;
            menu.style.borderBottomRightRadius = 16;
            garagePage = new ScrollView(ScrollViewMode.Vertical);
            garagePage.style.flexGrow = 1;
            root.Add(garagePage);
            garagePage.Add(menu);
            AddHeading(menu, "VIGILANTE 8", 34);
            AddHeading(menu, "OFFENCE 3  /  UNIFIED PC EDITION", 13);

            var characters = OfficialMenuSelections.Pilots(catalog);
            characterIds = characters.Select(c => c.id).ToArray();
            characterSelector = new DropdownField("PILOTO", characters.Select(c => c.name).ToList(), 0);
            StyleDropdown(characterSelector);
            menu.Add(characterSelector);
            characterSelector.RegisterValueChangedCallback(_ =>
            {
                if (restoringSelections) return;
                SaveOfficialChoice(GameMenuSettings.CharacterKey, characterIds, characterSelector.index);
                RefreshVehicles();
                SaveOfficialChoice(GameMenuSettings.VehicleKey, vehicleIds, vehicleSelector.index);
            });

            vehicleSelector = new DropdownField("VEÍCULO");
            StyleDropdown(vehicleSelector);
            menu.Add(vehicleSelector);
            vehicleSelector.RegisterValueChangedCallback(_ =>
            {
                if (restoringSelections) return;
                SaveOfficialChoice(GameMenuSettings.VehicleKey, vehicleIds, vehicleSelector.index);
                if (characterSelector.index >= 0 && characterSelector.index < characterIds.Length &&
                    vehicleIds != null && vehicleSelector.index >= 0 && vehicleSelector.index < vehicleIds.Length)
                    GameMenuSettings.StoreVehicleForCharacter(
                        characterIds[characterSelector.index], vehicleIds[vehicleSelector.index],
                        characterIds, vehicleIds);
                UpdateAvailability();
            });
            RefreshVehicles();

            var arenas = OfficialMenuSelections.Arenas(catalog);
            arenaIds = arenas.Select(a => a.id).ToArray();
            arenaSelector = new DropdownField("ARENA", arenas.Select(a =>
                a.name + (a.bonus ? "   ·   BÔNUS N64" : "")).ToList(), 0);
            StyleDropdown(arenaSelector);
            menu.Add(arenaSelector);
            arenaSelector.RegisterValueChangedCallback(_ =>
            {
                if (restoringSelections) return;
                SaveOfficialChoice(GameMenuSettings.ArenaKey, arenaIds, arenaSelector.index);
                UpdateAvailability();
            });

            garageSummary = new Label();
            garageSummary.style.color = Muted;
            garageSummary.style.whiteSpace = WhiteSpace.Normal;
            garageSummary.style.marginTop = 7;
            garageSummary.style.marginBottom = 8;
            menu.Add(garageSummary);

            readinessLabel = new Label();
            readinessLabel.style.color = new Color(1f, 0.72f, 0.42f);
            readinessLabel.style.whiteSpace = WhiteSpace.Normal;
            readinessLabel.style.marginTop = 15;
            menu.Add(readinessLabel);
            startCombat = CreateButton("INICIAR COMBATE", StartMatch, true);
            menu.Add(startCombat);
            menu.Add(CreateButton("VER TODO O ACERVO", () => Show(ScreenPage.Catalog)));
            menu.Add(CreateButton("VOLTAR AO MENU", () => Show(ScreenPage.Home)));
            AddHeading(menu, "WASD / SETAS: dirigir   ·   SHIFT: freio   ·   ESPAÇO: disparar", 12);
            UpdateAvailability();
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
            var vehicles = OfficialMenuSelections.CarsForPilot(catalog, characterIds[index]);
            string[] available = vehicles.Select(v => v.id).ToArray();
            string remembered = GameMenuSettings.RestoreVehicleForCharacter(
                characterIds[index], characterIds, available);
            int choice = Array.IndexOf(available, remembered);
            bool wasRestoring = restoringSelections;
            restoringSelections = true;
            try
            {
                vehicleIds = available;
                vehicleSelector.choices = vehicles.Select(v =>
                    (string.IsNullOrWhiteSpace(v.officialName) ? v.name : v.officialName) +
                    "   ·   " + v.game.ToUpperInvariant()).ToList();
                vehicleSelector.index = vehicles.Length == 0 ? -1 : Mathf.Max(0, choice);
            }
            finally { restoringSelections = wasRestoring; }
            UpdateAvailability();
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
            var quit = new Button(PauseGame) { text = "PAUSAR  /  ESC" };
            quit.style.height = 35;
            hud.Add(quit);
        }

        private void StartMatch()
        {
            if (!IsMatchAvailable(out string reason))
            {
                Debug.LogWarning("Combate indisponível: " + reason);
                UpdateAvailability();
                return;
            }
            CleanupMatch();
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
            var opponents = catalog.vehicles.Where(v => v.characterId != selected.characterId &&
                IsVehicleReady(v)).OrderBy(v => v.id).ToArray();
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
            if (chase == null)
            {
                Debug.LogError("Camera has no ChaseCamera component.");
                BackToMenu();
                return;
            }
            chase.target = player.transform;
            finished = false;
            matchVehicle = selected.officialName;
            matchArena = arena.name;
            Time.timeScale = 1f;
            Show(ScreenPage.Playing);
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
            bool pressedBack = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pressedBack)
            {
                switch (activePage)
                {
                    case ScreenPage.Playing: PauseGame(); return;
                    case ScreenPage.Pause: ResumeGame(); return;
                    case ScreenPage.Options: Show(optionsReturn); return;
                    case ScreenPage.Garage:
                    case ScreenPage.Catalog:
                    case ScreenPage.Controls:
                    case ScreenPage.About: Show(ScreenPage.Home); return;
                }
            }
            if (activePage != ScreenPage.Playing || player == null) return;
            int alive = enemies.Count(enemy => enemy != null && enemy.IsAlive);
            healthLabel.text = "INTEGRIDADE   " + Mathf.RoundToInt(player.Health) +
                " / " + Mathf.RoundToInt(player.maximumHealth);
            enemiesLabel.text = "ADVERSÁRIOS   " + alive + " / 3";
            statusLabel.text = matchVehicle + "   /   " + matchArena;
            if (!finished && (!player.IsAlive || alive == 0))
            {
                finished = true;
                resultsTitle.text = player.IsAlive ? "VITÓRIA" : "DERROTA";
                resultsDetails.text = (player.IsAlive ? "TODOS OS INIMIGOS DERROTADOS" :
                    "SEU VEÍCULO FOI DESTRUÍDO") + "  ·  " + matchVehicle + "  ·  " + matchArena;
                player.SetControls(0f, 0f, 1f, false);
                Time.timeScale = 0f;
                Show(ScreenPage.Results);
            }
        }

        private void BackToMenu()
        {
            CleanupMatch();
            Show(ScreenPage.Home);
        }

        private void CleanupMatch()
        {
            Time.timeScale = 1f;
            if (chase != null) chase.target = null;
            chase = null;
            player = null;
            enemies.Clear();
            foreach (var vehicle in spawned) if (vehicle != null) Destroy(vehicle);
            spawned.Clear();
            if (arenaInstance != null) Destroy(arenaInstance);
            arenaInstance = null;
            finished = false;
        }


        private static void SaveOfficialChoice(string key, string[] ids, int index)
        {
            if (ids == null || index < 0 || index >= ids.Length) return;
            GameMenuSettings.StoreSelection(key, ids[index], ids);
        }

        private void RestoreLastOfficialSelections()
        {
            // Read all three values before any dropdown callbacks can mutate preferences.
            string originalVehicle = GameMenuSettings.RestoreSelection(
                GameMenuSettings.VehicleKey, catalog.vehicles.Select(v => v.id).ToArray());
            string originalArena = GameMenuSettings.RestoreSelection(
                GameMenuSettings.ArenaKey, arenaIds);
            string originalCharacter = GameMenuSettings.RestoreSelection(
                GameMenuSettings.CharacterKey, characterIds);
            int characterIndex = Array.IndexOf(characterIds, originalCharacter);
            if (characterIndex >= 0) characterSelector.index = characterIndex;
            RefreshVehicles();

            // A vehicle from another character is never allowed to leak into this selection.
            int vehicleIndex = Array.IndexOf(vehicleIds, originalVehicle);
            if (vehicleIndex >= 0)
                vehicleSelector.index = vehicleIndex;
            else
            {
                string remembered = GameMenuSettings.RestoreVehicleForCharacter(
                    characterIds[characterSelector.index], characterIds, vehicleIds);
                int rememberedIndex = Array.IndexOf(vehicleIds, remembered);
                if (rememberedIndex >= 0) vehicleSelector.index = rememberedIndex;
            }

            int arenaIndex = Array.IndexOf(arenaIds, originalArena);
            if (arenaIndex >= 0) arenaSelector.index = arenaIndex;
            UpdateAvailability();
        }

        private static Button CreateButton(string caption, Action clicked, bool highlighted = false)
        {
            var button = new Button(() => clicked()) { text = caption };
            button.style.minHeight = 48;
            button.style.marginTop = 9;
            button.style.marginBottom = 7;
            button.style.paddingLeft = 20;
            button.style.paddingRight = 20;
            button.style.fontSize = 15;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = highlighted ? Accent : new Color(0.15f, 0.19f, 0.26f);
            button.style.borderTopLeftRadius = 9;
            button.style.borderTopRightRadius = 9;
            button.style.borderBottomLeftRadius = 9;
            button.style.borderBottomRightRadius = 9;
            return button;
        }

        private VisualElement CreateScreen(string heading, string description, out VisualElement panel)
        {
            var overlay = new ScrollView(ScrollViewMode.Vertical);
            overlay.style.flexGrow = 1;
            overlay.style.width = Length.Percent(100);
            overlay.style.height = Length.Percent(100);
            overlay.style.backgroundColor = Ink;
            root.Add(overlay);

            panel = new VisualElement();
            panel.style.width = Length.Percent(92);
            panel.style.maxWidth = 780;
            panel.style.alignSelf = Align.Center;
            panel.style.marginTop = 45;
            panel.style.marginBottom = 40;
            panel.style.paddingTop = 30;
            panel.style.paddingLeft = 28;
            panel.style.paddingRight = 28;
            panel.style.paddingBottom = 28;
            panel.style.backgroundColor = Surface;
            panel.style.borderTopLeftRadius = 16;
            panel.style.borderTopRightRadius = 16;
            panel.style.borderBottomLeftRadius = 16;
            panel.style.borderBottomRightRadius = 16;
            overlay.Add(panel);
            var brand = new Label("VIGILANTE 8");
            brand.style.color = Accent;
            brand.style.fontSize = 42;
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(brand);
            var edition = new Label("OFFENCE 3    /    PC EDITION");
            edition.style.color = Muted;
            edition.style.fontSize = 13;
            edition.style.marginBottom = 22;
            panel.Add(edition);
            AddHeading(panel, heading, 24);
            var subtitle = new Label(description);
            subtitle.style.color = Muted;
            subtitle.style.fontSize = 14;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.marginBottom = 18;
            panel.Add(subtitle);
            return overlay;
        }

        private void BuildMainMenu()
        {
            homePage = CreateScreen("ESCOLHA SEU PILOTO. DOMINE A ARENA.",
                "Os conteúdos oficiais de Vigilante 8 e 2nd Offense em uma edição para Windows.",
                out var panel);
            var collection = new Label(catalog.characters.Length + " PILOTOS   /   " +
                catalog.vehicles.Length + " VEÍCULOS   /   " + catalog.arenas.Length + " ARENAS");
            collection.style.color = Muted;
            collection.style.fontSize = 15;
            collection.style.marginBottom = 25;
            panel.Add(collection);
            panel.Add(CreateButton("JOGAR  /  SELECIONAR VEÍCULO", () => Show(ScreenPage.Garage), true));
            panel.Add(CreateButton("CATÁLOGO OFICIAL  /  31 CARROS E 19 ARENAS",
                () => Show(ScreenPage.Catalog)));
            panel.Add(CreateButton("OPÇÕES GRÁFICAS E ÁUDIO", () => OpenOptions(ScreenPage.Home)));
            panel.Add(CreateButton("CONTROLES", () => Show(ScreenPage.Controls)));
            panel.Add(CreateButton("SOBRE O ACERVO", () => Show(ScreenPage.About)));
            panel.Add(CreateButton("SAIR DO JOGO", ExitGame));
        }

        private void BuildCatalogBrowser()
        {
            catalogPage = CreateScreen("CATÁLOGO OFICIAL",
                "Consulte e selecione todos os pilotos, carros e arenas, mesmo aqueles que ainda não estão jogáveis.",
                out var panel);
            catalogOrigin = new DropdownField("VERSÃO", new List<string> {
                "TODAS AS VERSÕES", "VIGILANTE 8", "2ND OFFENSE"
            }, 0);
            StyleDropdown(catalogOrigin);
            panel.Add(catalogOrigin);
            catalogCategory = new DropdownField("MOSTRAR", new List<string> {
                "CARROS", "PILOTOS", "ARENAS"
            }, 0);
            StyleDropdown(catalogCategory);
            panel.Add(catalogCategory);
            catalogEntries = new VisualElement();
            catalogEntries.style.marginTop = 14;
            panel.Add(catalogEntries);
            panel.Add(CreateButton("VOLTAR AO MENU", () => Show(ScreenPage.Home)));
            catalogOrigin.RegisterValueChangedCallback(_ => RefreshCatalogBrowser());
            catalogCategory.RegisterValueChangedCallback(_ => RefreshCatalogBrowser());
            RefreshCatalogBrowser();
        }

        private void RefreshCatalogBrowser()
        {
            if (catalogEntries == null || catalogCategory == null || catalogOrigin == null)
                return;
            catalogEntries.Clear();
            string game = catalogOrigin.index switch
            {
                1 => "v8",
                2 => "v82",
                _ => null
            };
            int count = 0;
            switch (catalogCategory.index)
            {
                case 0:
                    foreach (VehicleEntry car in OfficialMenuSelections.Cars(catalog, game))
                    {
                        CharacterEntry pilot = catalog.characters.First(c => c.id == car.characterId);
                        string status = IsVehicleReady(car) ? "DISPONÍVEL" : "MIGRAÇÃO PENDENTE";
                        catalogEntries.Add(CreateButton(
                            car.officialName + "   /   " + pilot.name + "   /   " + status,
                            () => SelectPilotFromCatalog(car.characterId, car.id)));
                        count++;
                    }
                    break;
                case 1:
                    foreach (CharacterEntry pilot in OfficialMenuSelections.Pilots(catalog, game))
                    {
                        int vehicleCount = catalog.vehicles.Count(v => v.characterId == pilot.id &&
                            (game == null || v.game == game));
                        catalogEntries.Add(CreateButton(pilot.name + "   /   " +
                            vehicleCount + (vehicleCount == 1 ? " VEÍCULO" : " VEÍCULOS"),
                            () => SelectPilotFromCatalog(pilot.id, null)));
                        count++;
                    }
                    break;
                case 2:
                    foreach (ArenaEntry arena in OfficialMenuSelections.Arenas(catalog, game))
                    {
                        string status = IsArenaReady(arena) ? "DISPONÍVEL" : "MIGRAÇÃO PENDENTE";
                        catalogEntries.Add(CreateButton(arena.name +
                            (arena.bonus ? "   /   EXCLUSIVA N64" : "") +
                            "   /   " + status, () => SelectArenaFromCatalog(arena.id)));
                        count++;
                    }
                    break;
            }
            var info = new Label(count + " entradas oficiais desta categoria e versão.");
            info.style.color = Muted;
            info.style.marginBottom = 14;
            catalogEntries.Insert(0, info);
        }

        private void SelectPilotFromCatalog(string pilotId, string requestedVehicleId)
        {
            int index = Array.IndexOf(characterIds, pilotId);
            if (index < 0) throw new InvalidOperationException("Piloto não encontrado no catálogo oficial");
            bool previous = restoringSelections;
            restoringSelections = true;
            try
            {
                characterSelector.index = index;
                RefreshVehicles();
                int selected = Array.IndexOf(vehicleIds, requestedVehicleId);
                if (selected >= 0) vehicleSelector.index = selected;
            }
            finally { restoringSelections = previous; }
            SaveOfficialChoice(GameMenuSettings.CharacterKey, characterIds, characterSelector.index);
            SaveOfficialChoice(GameMenuSettings.VehicleKey, vehicleIds, vehicleSelector.index);
            if (vehicleIds != null && vehicleSelector.index >= 0 &&
                vehicleSelector.index < vehicleIds.Length)
                GameMenuSettings.StoreVehicleForCharacter(pilotId,
                    vehicleIds[vehicleSelector.index], characterIds, vehicleIds);
            Show(ScreenPage.Garage);
        }

        private void SelectArenaFromCatalog(string officialArenaId)
        {
            int index = Array.IndexOf(arenaIds, officialArenaId);
            if (index < 0) throw new InvalidOperationException("Arena não encontrada no catálogo oficial");
            bool previous = restoringSelections;
            restoringSelections = true;
            try { arenaSelector.index = index; }
            finally { restoringSelections = previous; }
            SaveOfficialChoice(GameMenuSettings.ArenaKey, arenaIds, index);
            Show(ScreenPage.Garage);
        }

        private void BuildOptionsMenu()
        {
            optionsPage = CreateScreen("OPÇÕES",
                "Alterações aplicadas ao Unity e armazenadas neste computador.", out var panel);
            AddHeading(panel, "ÁUDIO", 19);
            var volume = new Slider("VOLUME GERAL", 0f, 1f) { value = preferences.MasterVolume };
            volume.style.marginBottom = 22;
            volume.RegisterValueChangedCallback(evt => preferences.SetVolume(evt.newValue));
            panel.Add(volume);
            AddHeading(panel, "GRÁFICOS", 19);
            var qualityNames = QualitySettings.names;
            if (qualityNames.Length != 0)
            {
                var quality = new DropdownField("QUALIDADE", qualityNames.ToList(),
                    GameMenuSettings.NormalizeQuality(preferences.QualityLevel, qualityNames.Length));
                StyleDropdown(quality);
                quality.RegisterValueChangedCallback(_ =>
                {
                    if (quality.index >= 0) preferences.SetQuality(quality.index);
                });
                panel.Add(quality);
            }
            else panel.Add(new Label("Nenhum perfil de qualidade está configurado no Unity."));
            var fullscreen = new Toggle("TELA CHEIA") { value = preferences.Fullscreen };
            fullscreen.style.marginTop = 15;
            fullscreen.style.marginBottom = 22;
            fullscreen.RegisterValueChangedCallback(evt => preferences.SetFullscreen(evt.newValue));
            panel.Add(fullscreen);
            panel.Add(CreateButton("VOLTAR", () => Show(optionsReturn), true));
        }

        private void OpenOptions(ScreenPage returnTo)
        {
            optionsReturn = returnTo;
            Show(ScreenPage.Options);
        }

        private void BuildControlsMenu()
        {
            controlsPage = CreateScreen("CONTROLES",
                "Comandos já conectados aos scripts reais de direção e combate.", out var panel);
            AddHeading(panel, "TECLADO", 19);
            AddHeading(panel, "W / S ou SETAS ↑ ↓     ACELERAR / RÉ", 14);
            AddHeading(panel, "A / D ou SETAS ← →     VIRAR", 14);
            AddHeading(panel, "SHIFT ESQUERDO     FREAR", 14);
            AddHeading(panel, "ESPAÇO     DISPARAR", 14);
            AddHeading(panel, "ESC     PAUSAR / RETOMAR / VOLTAR", 14);
            AddHeading(panel, "GAMEPAD", 19);
            AddHeading(panel, "GATILHOS DIREITO / ESQUERDO     ACELERAR / RÉ", 14);
            AddHeading(panel, "ANALÓGICO ESQUERDO     VIRAR", 14);
            AddHeading(panel, "BOTÃO INFERIOR     FREAR", 14);
            AddHeading(panel, "OMBRO DIREITO     DISPARAR", 14);
            AddHeading(panel, "START     PAUSAR / RETOMAR", 14);
            panel.Add(CreateButton("MENU PRINCIPAL", () => Show(ScreenPage.Home), true));
        }

        private void BuildAboutMenu()
        {
            aboutPage = CreateScreen("CONTEÚDO OFICIAL",
                "Identidades consolidadas dos dois títulos, sem repetir pilotos ou carros.",
                out var panel);
            AddHeading(panel, catalog.characters.Length + " PERSONAGENS  /  " +
                catalog.vehicles.Length + " VEÍCULOS  /  " + catalog.arenas.Length + " ARENAS", 18);
            var note = new Label("Entradas ainda não migradas aparecem na garagem como indisponíveis. " +
                "O jogo não cria modelos, armas ou cenários fictícios para substituir recursos ausentes.");
            note.style.color = Muted;
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginBottom = 18;
            panel.Add(note);
            panel.Add(CreateButton("CONSULTAR TODOS OS CARROS E FASES",
                () => Show(ScreenPage.Catalog)));
            panel.Add(CreateButton("MENU PRINCIPAL", () => Show(ScreenPage.Home), true));
        }

        private void BuildPauseMenu()
        {
            pausePage = CreateScreen("JOGO PAUSADO",
                "Continue a partida ou retorne ao menu sem deixar objetos de uma rodada anterior.",
                out var panel);
            panel.Add(CreateButton("CONTINUAR", ResumeGame, true));
            panel.Add(CreateButton("OPÇÕES", () => OpenOptions(ScreenPage.Pause)));
            panel.Add(CreateButton("VOLTAR AO MENU", BackToMenu));
        }

        private void BuildResultsMenu()
        {
            resultsPage = CreateScreen("RESULTADO DA PARTIDA",
                "Resultado determinado pelos veículos destruídos na rodada.", out var panel);
            resultsTitle = new Label();
            resultsTitle.style.color = Accent;
            resultsTitle.style.fontSize = 30;
            resultsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(resultsTitle);
            resultsDetails = new Label();
            resultsDetails.style.whiteSpace = WhiteSpace.Normal;
            resultsDetails.style.color = Muted;
            resultsDetails.style.marginBottom = 15;
            panel.Add(resultsDetails);
            panel.Add(CreateButton("JOGAR NOVAMENTE", StartMatch, true));
            panel.Add(CreateButton("SELECIONAR OUTRO VEÍCULO", () =>
            {
                CleanupMatch();
                Show(ScreenPage.Garage);
            }));
            panel.Add(CreateButton("MENU PRINCIPAL", BackToMenu));
        }

        private void Show(ScreenPage target)
        {
            activePage = target;
            if (target == ScreenPage.Garage) UpdateAvailability();
            if (garagePage != null) garagePage.style.display = target == ScreenPage.Garage ?
                DisplayStyle.Flex : DisplayStyle.None;
            if (hud != null) hud.style.display = target == ScreenPage.Playing ?
                DisplayStyle.Flex : DisplayStyle.None;
            var pages = new[] {
                homePage, catalogPage, optionsPage, controlsPage, aboutPage, pausePage, resultsPage
            };
            var ids = new[] {
                ScreenPage.Home, ScreenPage.Catalog, ScreenPage.Options, ScreenPage.Controls,
                ScreenPage.About, ScreenPage.Pause, ScreenPage.Results
            };
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].style.display =
                    target == ids[i] ? DisplayStyle.Flex : DisplayStyle.None;
            root.style.backgroundColor = target == ScreenPage.Playing ? Color.clear : Ink;

            // Keep focus on an actionable element when navigating by keyboard/gamepad.
            VisualElement focusTarget = target switch
            {
                ScreenPage.Home => homePage?.Q<Button>(),
                ScreenPage.Garage => characterSelector,
                ScreenPage.Catalog => catalogPage?.Q<Button>(),
                ScreenPage.Options => optionsPage?.Q<Slider>() ?? (VisualElement)optionsPage?.Q<Button>(),
                ScreenPage.Controls => controlsPage?.Q<Button>(),
                ScreenPage.About => aboutPage?.Q<Button>(),
                ScreenPage.Pause => pausePage?.Q<Button>(),
                ScreenPage.Results => resultsPage?.Q<Button>(),
                _ => null,
            };
            if (focusTarget != null)
                focusTarget.schedule.Execute(() =>
                {
                    if (activePage == target && focusTarget.panel != null && focusTarget.enabledInHierarchy)
                        focusTarget.Focus();
                });
        }

        private static bool IsVehicleReady(VehicleEntry vehicle)
        {
            var prefab = CatalogRepository.LoadVehiclePrefab(vehicle);
            if (prefab == null || prefab.GetComponent<LegacyVehiclePlaytest>() != null)
                return false;

            var identity = prefab.GetComponent<OfficialAssetIdentity>();
            var drive = prefab.GetComponent<VehicleDrive>();
            bool hasRealMesh = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 2) ||
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 2);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            bool materialsValid = renderers.Length > 0 && renderers.All(r =>
                r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null));
            return identity != null && identity.Matches(vehicle.id, vehicle.game) &&
                drive != null && drive.HasWeapon && hasRealMesh && materialsValid &&
                prefab.GetComponent<Rigidbody>() != null &&
                prefab.GetComponentsInChildren<Collider>(true).Length > 0;
        }

        private static bool IsArenaReady(ArenaEntry arena)
        {
            var prefab = CatalogRepository.LoadArenaPrefab(arena);
            if (prefab == null || prefab.GetComponentInChildren<LegacyVehiclePlaytest>(true) != null)
                return false;
            var identity = prefab.GetComponent<OfficialAssetIdentity>();
            var spawns = prefab.GetComponentInChildren<ArenaSpawnPoints>(true);
            bool hasRealMesh = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 2) ||
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 2);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            return identity != null && identity.Matches(arena.id, arena.game) &&
                spawns != null && spawns.HasValidSpawns(4) && hasRealMesh &&
                renderers.Length > 0 && renderers.All(r =>
                    r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null)) &&
                prefab.GetComponentsInChildren<Collider>(true).Length > 0;
        }

        private bool IsMatchAvailable(out string reason)
        {
            if (vehicleIds == null || arenaIds == null || vehicleSelector == null ||
                vehicleSelector.index < 0 || arenaSelector == null || arenaSelector.index < 0 ||
                vehicleSelector.index >= vehicleIds.Length ||
                arenaSelector.index >= arenaIds.Length)
            {
                reason = "Escolha um piloto, um veículo e uma arena.";
                return false;
            }
            if (characterSelector == null || characterSelector.index < 0 ||
                characterSelector.index >= characterIds.Length)
            {
                reason = "Selecione um piloto oficial antes do combate.";
                return false;
            }
            var selected = catalog.vehicles.First(v => v.id == vehicleIds[vehicleSelector.index]);
            var arena = catalog.arenas.First(a => a.id == arenaIds[arenaSelector.index]);
            if (!OfficialMenuSelections.OwnsVehicle(
                catalog, characterIds[characterSelector.index], selected.id))
            {
                reason = "O veículo selecionado não pertence ao piloto atual.";
                return false;
            }
            if (!IsVehicleReady(selected))
            {
                reason = "Este carro oficial ainda não possui mesh, física e armas prontas no Unity.";
                return false;
            }
            if (!IsArenaReady(arena))
            {
                reason = "Esta fase oficial ainda não tem geometria, colisão e pontos de partida validados.";
                return false;
            }
            int validDrivers = catalog.vehicles.Where(v =>
                    v.characterId != selected.characterId && IsVehicleReady(v))
                .Select(v => v.characterId).Distinct().Count();
            if (validDrivers < 3)
            {
                reason = "São necessários outros três pilotos distintos com veículos oficiais homologados.";
                return false;
            }
            reason = "Pronto: arena, veículo e três adversários oficiais disponíveis.";
            return true;
        }

        private void UpdateAvailability()
        {
            if (readinessLabel == null || startCombat == null) return;
            if (garageSummary != null)
            {
                string vehicleId = vehicleIds != null && vehicleSelector != null &&
                    vehicleSelector.index >= 0 && vehicleSelector.index < vehicleIds.Length ?
                    vehicleIds[vehicleSelector.index] : null;
                string arenaId = arenaIds != null && arenaSelector != null &&
                    arenaSelector.index >= 0 && arenaSelector.index < arenaIds.Length ?
                    arenaIds[arenaSelector.index] : null;
                VehicleEntry chosen = catalog.vehicles.FirstOrDefault(v => v.id == vehicleId);
                ArenaEntry selectedArena = catalog.arenas.FirstOrDefault(a => a.id == arenaId);
                if (chosen != null && selectedArena != null)
                {
                    CharacterEntry pilot = catalog.characters.FirstOrDefault(c => c.id == chosen.characterId);
                    string game = chosen.game == "v82" ? "2ND OFFENSE" : "VIGILANTE 8";
                    garageSummary.text = "PILOTO: " + (pilot?.name ?? chosen.characterId) +
                        "    |    CARRO: " + chosen.officialName + " (" + game + ")" +
                        "\nARENA: " + selectedArena.name +
                        "    |    PLATAFORMAS: " + string.Join(", ", selectedArena.platforms ?? Array.Empty<string>());
                }
            }
            bool available = IsMatchAvailable(out string reason);
            readinessLabel.text = reason;
            readinessLabel.style.color = available ? new Color(0.40f, 0.85f, 0.62f) :
                new Color(1f, 0.72f, 0.42f);
            startCombat.SetEnabled(available);
        }

        private void PauseGame()
        {
            if (activePage != ScreenPage.Playing) return;
            if (player != null) player.SetControls(0f, 0f, 1f, false);
            Time.timeScale = 0f;
            Show(ScreenPage.Pause);
        }

        private void ResumeGame()
        {
            if (activePage != ScreenPage.Pause) return;
            Time.timeScale = 1f;
            Show(ScreenPage.Playing);
        }

        private void ExitGame()
        {
            CleanupMatch();
            Application.Quit();
        }

        private void OnDestroy() => CleanupMatch();
    }
}
