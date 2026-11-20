using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignSaveSlotsTests
    {
        [TestCase("campaign", false)]
        [TestCase("campaign-Campaign01", true)]
        [TestCase("campaign-Mission_02-b", true)]
        [TestCase("campaign-", false)]
        [TestCase("campaign-bad id", false)]
        [TestCase("profile", false)]
        [TestCase("cloud-replaced-campaign-Campaign01", false)]
        [TestCase("Campaign-Campaign01", false)]
        [TestCase(null, false)]
        public void TellsTheCampaignSlots(string slot, bool expected)
        {
            Assert.That(CampaignSaveSlots.IsSlot(slot), Is.EqualTo(expected));
        }

        [Test]
        public void EverySlotMadeForAMissionIsACampaignSlot()
        {
            Assert.That(CampaignSaveSlots.IsSlot(CampaignSaveSlots.For("Campaign01")), Is.True);
            Assert.That(CampaignSaveSlots.IsSlot(CampaignSaveSlots.For(new string('a', CampaignLaunch.MaxMissionIdLength))), Is.True);
        }
    }
}
