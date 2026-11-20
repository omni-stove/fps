using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class CampaignCloudSaveTests
    {
        // キャンペーンのテストは進行のアセンブリを引かないので、先に登録する別のセーブとして進行と同じ名前のスロットをテストで登録する。
        private const string ProfileSlot = "profile";

        private const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";

        [SetUp]
        public void SetUp()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();
        }

        // 次のテストや Edit Mode の保存が、Play の開始時と同じキャンペーンの登録で動くよう戻す。進行の登録は次の Play の開始時に入れ直される。
        [TearDown]
        public void TearDown()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();
            CampaignCloudSave.Register();
        }

        [Test]
        public void RegisteringSyncsTheCampaignSlotsWithoutAnyAtStartup()
        {
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign01")), Is.False);

            CampaignCloudSave.Register();

            Assert.That(CloudSaveBootstrap.Syncs("campaign"), Is.False, "the old shared slot is no longer a campaign slot");
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign01")), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign02")), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.Prefix), Is.False, "a prefix without a mission id is not a campaign slot");
            Assert.That(CloudSaveBootstrap.Syncs(ProfileSlot), Is.False, "only the registered slots are synced");
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.Empty);
        }

        [Test]
        public void RegisteringTwiceKeepsOneRegistration()
        {
            CampaignCloudSave.Register();
            CampaignCloudSave.Register();

            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign01")), Is.True);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.Empty);
        }

        [Test]
        public void TheCampaignSlotsAreListedAfterAnEarlierRegistration()
        {
            CloudSaveBootstrap.RegisterSlots(slot => slot == ProfileSlot, new[] { ProfileSlot }, text => true);
            CampaignCloudSave.Register();

            Assert.That(CloudSaveBootstrap.Syncs(ProfileSlot), Is.True);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileSlot }));
        }

        [Test]
        public void OnlyACampaignSaveThisVersionReadsIsTakenFromTheCloud()
        {
            CampaignCloudSave.Register();
            var slot = CampaignSaveSlots.For("Campaign01");
            var save = new CampaignSave(
                "Campaign01",
                "start",
                100,
                new[] { new WeaponAmmo(RifleId, 10, 20) },
                RifleId,
                new[] { new ObjectiveProgress("reach", ObjectiveState.Active, 0) });

            Assert.That(CloudSaveBootstrap.CanRead(slot, save.ToJson()), Is.True);
            Assert.That(CloudSaveBootstrap.CanRead(slot, "{ broken"), Is.False);
            Assert.That(CloudSaveBootstrap.CanRead(slot, "{\"version\":999}"), Is.False);
        }
    }
}
