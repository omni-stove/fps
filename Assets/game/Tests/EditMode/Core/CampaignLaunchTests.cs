using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignLaunchTests
    {
        [Test]
        public void KeepsTheMissionAndDifficulty()
        {
            var launch = new CampaignLaunch("Campaign01", CampaignDifficulty.Hard);

            Assert.That(launch.MissionId, Is.EqualTo("Campaign01"));
            Assert.That(launch.Difficulty, Is.EqualTo(CampaignDifficulty.Hard));
            Assert.That(launch.ToString(), Is.EqualTo("Campaign01/hard"));
        }

        [TestCase("Campaign01")]
        [TestCase("a")]
        [TestCase("mission_2-b")]
        public void AcceptsSlotSafeMissionIds(string id)
        {
            Assert.That(CampaignLaunch.IsValidMissionId(id), Is.True);
            Assert.That(new CampaignLaunch(id, CampaignDifficulty.Normal).MissionId, Is.EqualTo(id));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Campaign 01")]
        [TestCase("../campaign")]
        [TestCase("a/b")]
        [TestCase("a\\b")]
        [TestCase("ミッション")]
        [TestCase("a.b")]
        public void RejectsIdsThatCannotNameASlot(string id)
        {
            Assert.That(CampaignLaunch.IsValidMissionId(id), Is.False);
            Assert.Throws<ArgumentException>(() => new CampaignLaunch(id, CampaignDifficulty.Normal));
            Assert.Throws<ArgumentException>(() => CampaignSaveSlots.For(id));
        }

        [Test]
        public void RejectsATooLongIdAndNull()
        {
            Assert.That(CampaignLaunch.IsValidMissionId(new string('a', CampaignLaunch.MaxMissionIdLength)), Is.True);
            Assert.That(CampaignLaunch.IsValidMissionId(new string('a', CampaignLaunch.MaxMissionIdLength + 1)), Is.False);
            Assert.That(CampaignLaunch.IsValidMissionId(null), Is.False);
            Assert.Throws<ArgumentNullException>(() => new CampaignLaunch(null, CampaignDifficulty.Normal));
            Assert.Throws<ArgumentNullException>(() => CampaignSaveSlots.For(null));
        }

        [Test]
        public void RejectsAnUnknownDifficulty()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignLaunch("Campaign01", (CampaignDifficulty)99));
        }

        [Test]
        public void EachMissionHasItsOwnSlot()
        {
            Assert.That(CampaignSaveSlots.For("Campaign01"), Is.EqualTo("campaign-Campaign01"));
            Assert.That(CampaignSaveSlots.For("Campaign02"), Is.EqualTo("campaign-Campaign02"));
            Assert.That(CampaignSaveSlots.For("Campaign01"), Is.Not.EqualTo(CampaignSaveSlots.Legacy));
        }

        [Test]
        public void OnlyCampaign01ReadsTheLegacySlot()
        {
            Assert.That(CampaignSaveSlots.Legacy, Is.EqualTo("campaign"));
            Assert.That(CampaignSaveSlots.ReadsLegacy("Campaign01"), Is.True);
            Assert.That(CampaignSaveSlots.ReadsLegacy("campaign01"), Is.False);
            Assert.That(CampaignSaveSlots.ReadsLegacy("Campaign02"), Is.False);
            Assert.That(CampaignSaveSlots.ReadsLegacy(null), Is.False);
        }
    }
}
