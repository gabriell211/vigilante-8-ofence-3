using System;
using System.Linq;
using NUnit.Framework;
using Vigilante8.Gameplay;

namespace Vigilante8.EditorTests
{
    [TestFixture]
    public sealed class OriginalQuestArchiveTests
    {
        private static OriginalQuestArchiveData MakeCatalog()
        {
            var campaigns = new OriginalQuestCampaign[27];
            for (int i = 0; i < campaigns.Length; i++)
            {
                bool v8 = i < 12;
                var missions = new OriginalQuestMission[v8 ? 4 : 8];
                for (int j = 0; j < missions.Length; j++)
                    missions[j] = new OriginalQuestMission
                    {
                        id = i + ":quest:" + j,
                        sourceMissionIndex = j,
                        briefingPtBr = "Missão oficial traduzida para português brasileiro.",
                        sourceBriefingSha256 = new string('a', 64),
                        sourceRecordSha256 = new string('b', 64),
                        sourceObjectiveDirective = v8 ? "PROTECT" : "",
                        sourceRouteRecordHex = v8 ? "" : new string('0', 40),
                        sourceArenaIndex = v8 ? 1 : -1,
                        targetFirst = v8 ? 10 : -1,
                        targetLast = v8 ? 20 : -1,
                        loadingCardVariant = v8 ? 0 : -1,
                        sourceTeamIndex = v8 ? -1 : i % 3,
                        sourceActorCount = 2
                    };
                campaigns[i] = new OriginalQuestCampaign
                {
                    id = (v8 ? "v8" : "v82") + ":" + i,
                    game = v8 ? "v8" : "v82",
                    sourceCharacter = "Source pilot " + i,
                    sourceCharacterIndex = i,
                    missions = missions
                };
            }
            return new OriginalQuestArchiveData
            {
                schemaVersion = 1, locale = "pt-BR",
                provenance = OriginalQuestArchive.Provenance,
                v8QuestSha256 = new string('1', 64),
                v82QuestSha256 = new string('2', 64),
                campaigns = campaigns
            };
        }

        [Test]
        public void OriginalQuestCatalogPreservesBothAuthenticCampaignCounts()
        {
            var archive = MakeCatalog();
            Assert.DoesNotThrow(() => OriginalQuestArchive.Validate(archive));
            Assert.That(OriginalQuestArchive.Campaigns(archive, "v8").Length, Is.EqualTo(12));
            Assert.That(OriginalQuestArchive.Campaigns(archive, "v82").Length, Is.EqualTo(15));
            Assert.That(archive.campaigns.Sum(c => c.missions.Length), Is.EqualTo(168));
        }

        [Test]
        public void MissingSourceHashOrAlteredTranslationNeverCountsAsOriginalQuest()
        {
            var archive = MakeCatalog();
            archive.v8QuestSha256 = "";
            Assert.Throws<InvalidOperationException>(() => OriginalQuestArchive.Validate(archive));
            archive.v8QuestSha256 = new string('1', 64);
            archive.campaigns[0].missions[0].briefingPtBr = null;
            Assert.Throws<InvalidOperationException>(() => OriginalQuestArchive.Validate(archive));
        }

        [Test]
        public void InvalidMissionOrderingAndRouteProvenanceFailClosed()
        {
            var archive = MakeCatalog();
            archive.campaigns[12].missions[0].sourceRouteRecordHex = "dummy";
            Assert.Throws<InvalidOperationException>(() => OriginalQuestArchive.Validate(archive));
            archive = MakeCatalog();
            archive.campaigns[0].missions[0].sourceMissionIndex = 3;
            Assert.Throws<InvalidOperationException>(() => OriginalQuestArchive.Validate(archive));
        }

        [Test]
        public void NoUnknownQuestGameOrSyntheticCampaignAllowed()
        {
            var archive = MakeCatalog();
            Assert.Throws<ArgumentException>(() => OriginalQuestArchive.Campaigns(
                archive, "offence3_custom"));
            archive.campaigns[0].game = "offence3_custom";
            Assert.Throws<InvalidOperationException>(() => OriginalQuestArchive.Validate(archive));
        }
    }
}
