using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class CampaignCloudSaveTests
    {
        [SetUp]
        public void SetUp()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();
        }

        // 次のテストや Edit Mode の保存が、Play の開始時と同じ登録で動くよう戻す。
        [TearDown]
        public void TearDown()
        {
            CloudSaveBootstrap.ClearRegisteredSlots();
            ProgressionCloudSave.Register();
            CampaignCloudSave.Register();
        }

        [Test]
        public void RegisteringSyncsTheCampaignSlotsAndChecksTheLegacySlotAtStartup()
        {
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign01")), Is.False);

            CampaignCloudSave.Register();

            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.Legacy), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign01")), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.For("Campaign02")), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(CampaignSaveSlots.Prefix), Is.False, "a prefix without a mission id is not a campaign slot");
            Assert.That(CloudSaveBootstrap.Syncs(ProfileStore.Slot), Is.False, "only the registered slots are synced");
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { CampaignSaveSlots.Legacy }));
        }

        [Test]
        public void RegisteringTwiceKeepsOneRegistration()
        {
            CampaignCloudSave.Register();
            CampaignCloudSave.Register();

            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { CampaignSaveSlots.Legacy }));
        }

        [Test]
        public void TheCampaignSlotsAreListedAfterTheProfileWhenBothRegister()
        {
            ProgressionCloudSave.Register();
            CampaignCloudSave.Register();

            Assert.That(CloudSaveBootstrap.Syncs(ProfileStore.Slot), Is.True);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileStore.Slot, CampaignSaveSlots.Legacy }));
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
                new[] { new WeaponAmmo(CampaignSave.LegacyWeaponId, 10, 20) },
                CampaignSave.LegacyWeaponId,
                new[] { new ObjectiveProgress("reach", ObjectiveState.Active, 0) });

            Assert.That(CloudSaveBootstrap.CanRead(slot, save.ToJson()), Is.True);
            Assert.That(CloudSaveBootstrap.CanRead(slot, "{ broken"), Is.False);
            Assert.That(CloudSaveBootstrap.CanRead(CampaignSaveSlots.Legacy, "{\"version\":999}"), Is.False);
        }
    }
}
