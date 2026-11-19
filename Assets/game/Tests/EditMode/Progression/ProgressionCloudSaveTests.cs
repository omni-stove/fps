using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class ProgressionCloudSaveTests
    {
        private const string Account = "account-1";

        private long _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 1000;
            CloudSaveBootstrap.ClearRegisteredSlots();
        }

        // 動いている同期へ遅れて登録したと知らせないよう、同期を手放してから Play の開始時と同じ登録に戻す。
        [TearDown]
        public void TearDown()
        {
            CloudSaveBootstrap.EndPlay();
            CloudSaveBootstrap.ClearRegisteredSlots();
            ProgressionCloudSave.Register();
        }

        [Test]
        public void RegisteringSyncsTheProfileAndChecksItAtStartup()
        {
            Assert.That(CloudSaveBootstrap.Syncs(ProfileStore.Slot), Is.False, "the kit itself knows no save, not even the profile");

            ProgressionCloudSave.Register();

            Assert.That(ProfileStore.Slot, Is.EqualTo("profile"));
            Assert.That(CloudSaveBootstrap.Syncs(ProfileStore.Slot), Is.True);
            Assert.That(CloudSaveBootstrap.Syncs(ProfileStore.RejectedSlot), Is.False, "the kept unreadable profile stays on this device");
            Assert.That(CloudSaveBootstrap.Syncs(CloudSaveSync.ReplacedPrefix + ProfileStore.Slot), Is.False);
            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileStore.Slot }));
        }

        [Test]
        public void RegisteringTwiceListsTheProfileOnce()
        {
            ProgressionCloudSave.Register();
            ProgressionCloudSave.Register();

            Assert.That(CloudSaveBootstrap.StartupSlots, Is.EqualTo(new[] { ProfileStore.Slot }));
        }

        [Test]
        public void OnlyAProfileThisVersionReadsIsTakenFromTheCloud()
        {
            ProgressionCloudSave.Register();

            Assert.That(ProgressionCloudSave.IsSlot(ProfileStore.Slot), Is.True);
            Assert.That(ProgressionCloudSave.IsSlot(ProfileStore.RejectedSlot), Is.False);
            Assert.That(CloudSaveBootstrap.CanRead(ProfileStore.Slot, new PlayerProfile(300, 2, 1, 1, 1).ToJson()), Is.True);
            Assert.That(CloudSaveBootstrap.CanRead(ProfileStore.Slot, "{\"version\":999}"), Is.False);
            Assert.That(CloudSaveBootstrap.CanRead(ProfileStore.Slot, "{ broken"), Is.False);
        }

        [Test]
        public void TwoDevicesOnTheSameAccountShareTheProfile()
        {
            ProgressionCloudSave.Register();
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            var laptop = new MemorySaveStorage();
            var desktop = new MemorySaveStorage();

            var laptopSync = Create(laptop, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            new ProfileStore(laptopSync).Save(new PlayerProfile(300, 2, 1, 1, 1));
            Drain(laptopSync);

            var desktopSync = Create(desktop, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            Drain(desktopSync);
            Assert.That(new ProfileStore(desktopSync).Load(), Is.EqualTo(new PlayerProfile(300, 2, 1, 1, 1)));

            _clock = 9000;
            new ProfileStore(desktopSync).Save(new PlayerProfile(800, 6, 2, 3, 2));
            Drain(desktopSync);

            var laptopRestarted = Create(laptop, new FakeCloudSaveBackend(files) { SignedInAccount = Account });
            Drain(laptopRestarted);
            Assert.That(new ProfileStore(laptopRestarted).Load(), Is.EqualTo(new PlayerProfile(800, 6, 2, 3, 2)));
        }

        private CloudSaveSync Create(MemorySaveStorage local, FakeCloudSaveBackend backend)
        {
            return new CloudSaveSync(local, backend, CloudSaveBootstrap.Syncs, CloudSaveBootstrap.StartupSlots, () => _clock)
            {
                CanRead = CloudSaveBootstrap.CanRead,
            };
        }

        private static void Drain(CloudSaveSync sync)
        {
            for (var i = 0; i < 50; i++)
            {
                sync.Tick(0d);
                if (sync.IsSignedIn && !sync.IsBusy && sync.PendingCount == 0)
                {
                    return;
                }
            }

            Assert.Fail("the sync did not settle");
        }
    }
}
