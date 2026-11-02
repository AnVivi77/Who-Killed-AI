using NUnit.Framework;
using UnityEngine;

namespace UnityMysteryExhibition.Tests
{
    public sealed class CaseProgressServiceTests
    {
        private CaseConfigAsset config;
        private CaseProgressService progress;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CaseConfigAsset>();
            config.caseId = "test";
            config.suspects = new[]
            {
                new SuspectData { id = "S01", displayName = "One", eventVideoAssetId = "video_s01" },
                new SuspectData { id = "S02", displayName = "Two", eventVideoAssetId = "video_s02" }
            };
            config.clues = new[]
            {
                new ClueData { id = "C01", suspectId = "S01", slotIndex = 1, targetName = "T01" },
                new ClueData { id = "C02", suspectId = "S01", slotIndex = 2, targetName = "T02" },
                new ClueData { id = "C03", suspectId = "S02", slotIndex = 1, targetName = "T03" },
                new ClueData { id = "C04", suspectId = "S02", slotIndex = 2, targetName = "T04" }
            };
            progress = new CaseProgressService(config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void DuplicateDiscoveryDoesNotIncreaseCount()
        {
            Assert.IsTrue(progress.DiscoverClue("C01").isNew);
            Assert.IsFalse(progress.DiscoverClue("C01").isNew);
            Assert.AreEqual(1, progress.DiscoveredCount);
        }

        [Test]
        public void CompletingBothCluesCompletesOnlyThatSuspect()
        {
            progress.DiscoverClue("C02");
            Assert.IsFalse(progress.IsSuspectComplete("S01"));
            progress.DiscoverClue("C01");
            Assert.IsTrue(progress.IsSuspectComplete("S01"));
            Assert.IsFalse(progress.IsSuspectComplete("S02"));
        }

        [Test]
        public void FinaleGateRequiresAllEventsResolved()
        {
            progress.DiscoverClue("C01");
            progress.DiscoverClue("C02");
            progress.DiscoverClue("C03");
            progress.DiscoverClue("C04");
            Assert.IsFalse(progress.ReadyForFinale);
            progress.TryMarkEventResolved("S01");
            Assert.IsFalse(progress.ReadyForFinale);
            progress.TryMarkEventResolved("S02");
            Assert.IsTrue(progress.ReadyForFinale);
        }
    }
}
