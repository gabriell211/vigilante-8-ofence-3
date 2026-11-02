using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    [Serializable]
    public sealed class OriginalQuestArchiveData
    {
        public int schemaVersion;
        public string locale;
        public string provenance;
        public string v8QuestSha256;
        public string v82QuestSha256;
        public OriginalQuestCampaign[] campaigns;
    }

    [Serializable]
    public sealed class OriginalQuestCampaign
    {
        public string id;
        public string game;
        public string sourceCharacter;
        public int sourceCharacterIndex;
        public OriginalQuestMission[] missions;
    }

    [Serializable]
    public sealed class OriginalQuestMission
    {
        public string id;
        public int sourceMissionIndex;
        public string briefingPtBr;
        public string sourceBriefingSha256;
        public string sourceRecordSha256;
        public string sourceObjectiveDirective;
        public string sourceRouteRecordHex;
        public int sourceArenaIndex;
        public int targetFirst;
        public int targetLast;
        public int loadingCardVariant;
        public int sourceTeamIndex;
        public int sourceActorCount;
    }

    /// <summary>
    /// Read-only, source-validated Quest mission archive. This does NOT
    /// reimplement original gameplay objectives, spawning, scene transitions,
    /// or the PS1 Quest state machine; it can only display verified translations.
    /// </summary>
    public static class OriginalQuestArchive
    {
        public const string Resource = "Missions/original_quests_ptbr";
        public const string Provenance = "v8-v82-source-quest-bin-verbatim-mechanics-ptbr-text-only";
        public const int ExpectedV8Campaigns = 12;
        public const int ExpectedV8Missions = 48;
        public const int ExpectedV82Campaigns = 15;
        public const int ExpectedV82Missions = 120;

        private static bool Sha256(string value) =>
            !string.IsNullOrEmpty(value) && value.Length == 64 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        public static OriginalQuestArchiveData TryLoad()
        {
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return null;
            var catalog = JsonUtility.FromJson<OriginalQuestArchiveData>(asset.text);
            Validate(catalog);
            return catalog;
        }

        public static void Validate(OriginalQuestArchiveData data)
        {
            if (data == null || data.schemaVersion != 1 ||
                data.locale != "pt-BR" || data.provenance != Provenance ||
                !Sha256(data.v8QuestSha256) || !Sha256(data.v82QuestSha256) ||
                data.campaigns == null ||
                data.campaigns.Length != ExpectedV8Campaigns + ExpectedV82Campaigns)
                throw new InvalidOperationException("Acervo de missões originais inválido.");

            var campaignIds = new HashSet<string>(StringComparer.Ordinal);
            int v8Campaigns = 0, v82Campaigns = 0, v8Missions = 0, v82Missions = 0;
            foreach (OriginalQuestCampaign campaign in data.campaigns)
            {
                if (campaign == null || string.IsNullOrWhiteSpace(campaign.id) ||
                    string.IsNullOrWhiteSpace(campaign.sourceCharacter) ||
                    !campaignIds.Add(campaign.id) || campaign.missions == null ||
                    campaign.missions.Length == 0)
                    throw new InvalidOperationException("Campanha original incompleta.");

                bool original = campaign.game == "v8";
                bool sequel = campaign.game == "v82";
                if (!original && !sequel)
                    throw new InvalidOperationException("Jogo de origem não reconhecido.");
                if (original) { v8Campaigns++; v8Missions += campaign.missions.Length; }
                else { v82Campaigns++; v82Missions += campaign.missions.Length; }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < campaign.missions.Length; i++)
                {
                    OriginalQuestMission mission = campaign.missions[i];
                    if (mission == null ||
                        mission.sourceMissionIndex != i ||
                        string.IsNullOrWhiteSpace(mission.id) || !ids.Add(mission.id) ||
                        string.IsNullOrWhiteSpace(mission.briefingPtBr) ||
                        !Sha256(mission.sourceBriefingSha256) ||
                        !Sha256(mission.sourceRecordSha256) ||
                        mission.sourceActorCount < 0 || mission.sourceActorCount > 64 ||
                        mission.sourceTeamIndex < -1 || mission.sourceTeamIndex > 2 ||
                        mission.sourceArenaIndex < -1 ||
                        mission.sourceRouteRecordHex == null ||
                        mission.sourceObjectiveDirective == null)
                        throw new InvalidOperationException("Dados de missão adulterados.");

                    if (original &&
                        (mission.sourceArenaIndex < 0 || mission.sourceArenaIndex > 9 ||
                         mission.targetFirst < 0 || mission.targetLast < mission.targetFirst ||
                         mission.sourceTeamIndex != -1))
                        throw new InvalidOperationException("Missão original V8 inválida.");
                    if (sequel &&
                        (mission.sourceRouteRecordHex.Length != 40 ||
                         mission.sourceRouteRecordHex.Any(c =>
                             !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) ||
                         mission.sourceTeamIndex < 0))
                        throw new InvalidOperationException("Missão original V82 inválida.");
                }
            }
            if (v8Campaigns != ExpectedV8Campaigns ||
                v82Campaigns != ExpectedV82Campaigns ||
                v8Missions != ExpectedV8Missions || v82Missions != ExpectedV82Missions)
                throw new InvalidOperationException(
                    "O acervo não contém as 48 + 120 missões de origem.");
        }

        public static OriginalQuestCampaign[] Campaigns(
            OriginalQuestArchiveData data, string game = null)
        {
            Validate(data);
            if (game != null && game != "v8" && game != "v82")
                throw new ArgumentException("Versão original desconhecida.", nameof(game));
            return data.campaigns.Where(c => game == null || c.game == game).ToArray();
        }
    }
}
