using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // 期待値は調整値 (レベル 2 が 500 XP から、"scope" がレベル 2 で使えるようになる) から出す。
    public sealed class ProgressionBootstrapTests
    {
        private const string ScopeId = "scope";

        private static readonly ProgressionSpec _spec = new ProgressionSpec(
            100, 0, 0, 250, 0, new[] { 0, 500 }, new[] { new ProgressionUnlock(ScopeId, 2) });

        [SetUp]
        public void SetUp()
        {
            ProgressionBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
        }

        [TearDown]
        public void TearDown()
        {
            ProgressionBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
        }

        [Test]
        public void WithoutARecorderEverythingIsAvailable()
        {
            Assert.That(ProgressionBootstrap.Recorder, Is.Null);
            Assert.That(ProgressionBootstrap.IsUnlocked(ScopeId), Is.True);
            Assert.Throws<ArgumentException>(() => ProgressionBootstrap.IsUnlocked(""));
            Assert.Throws<ArgumentException>(() => ProgressionBootstrap.IsUnlocked(null));
        }

        [Test]
        public void TheInstalledProfileDecidesWhatIsAvailableAndFollowsTheLevel()
        {
            var storage = new MemorySaveStorage();
            var recorder = ProgressionBootstrap.Install(new ProgressionRecorder(storage, _spec, new OfflineParticipants()));

            Assert.That(ProgressionBootstrap.Recorder, Is.SameAs(recorder));
            Assert.That(recorder.IsInstalled, Is.True);
            Assert.That(ProgressionBootstrap.IsUnlocked(ScopeId), Is.False, "level 1");
            Assert.That(ProgressionBootstrap.IsUnlocked("rifle"), Is.True, "ids outside the unlock table are always available");

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0));
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(recorder.Profile.Xp, Is.EqualTo(500));
            Assert.That(ProgressionBootstrap.IsUnlocked(ScopeId), Is.True, "level 2 after 500 xp");
            Assert.That(new ProfileStore(storage).Load().Matches, Is.EqualTo(2), "each finished match is saved");
        }

        [Test]
        public void TheSavedProfileIsReadOnInstall()
        {
            var storage = new MemorySaveStorage();
            new ProfileStore(storage).Save(new PlayerProfile(600, 3, 1, 2, 1));

            var recorder = ProgressionBootstrap.Install(new ProgressionRecorder(storage, _spec, new OfflineParticipants()));

            Assert.That(recorder.Profile, Is.EqualTo(new PlayerProfile(600, 3, 1, 2, 1)));
            Assert.That(recorder.Level, Is.EqualTo(2));
            Assert.That(ProgressionBootstrap.IsUnlocked(ScopeId), Is.True);
        }

        [Test]
        public void InstallingAgainUninstallsThePreviousRecorder()
        {
            var first = ProgressionBootstrap.Install(new ProgressionRecorder(new MemorySaveStorage(), _spec, new OfflineParticipants()));
            Assert.That(ProgressionBootstrap.Install(first), Is.SameAs(first));
            Assert.That(first.IsInstalled, Is.True, "installing the same recorder keeps it");

            var second = ProgressionBootstrap.Install(new ProgressionRecorder(new MemorySaveStorage(), _spec, new OfflineParticipants()));

            Assert.That(first.IsInstalled, Is.False);
            Assert.That(second.IsInstalled, Is.True);
            Assert.That(ProgressionBootstrap.Recorder, Is.SameAs(second));

            ProgressionBootstrap.Uninstall();

            Assert.That(second.IsInstalled, Is.False);
            Assert.That(ProgressionBootstrap.Recorder, Is.Null);
        }

        [Test]
        public void TheAchievementsAreRecordedWithTheProgression()
        {
            var storage = new MemorySaveStorage();
            var recorder = ProgressionBootstrap.Install(new ProgressionRecorder(storage, _spec, new OfflineParticipants()));
            var achievements = ProgressionBootstrap.Achievements;

            Assert.That(achievements, Is.Not.Null);
            Assert.That(achievements.Progression, Is.SameAs(recorder));
            Assert.That(achievements.IsInstalled, Is.True);

            ProgressionBootstrap.Uninstall();

            Assert.That(achievements.IsInstalled, Is.False);
            Assert.That(ProgressionBootstrap.Achievements, Is.Null);
        }

        [Test]
        public void TheCosmeticLockerIsPublishedWithTheProgression()
        {
            var recorder = ProgressionBootstrap.Install(new ProgressionRecorder(new MemorySaveStorage(), _spec, new OfflineParticipants()));
            var cosmetics = ProgressionBootstrap.Cosmetics;

            Assert.That(cosmetics, Is.Not.Null);
            Assert.That(cosmetics.Progression, Is.SameAs(recorder));
            Assert.That(cosmetics.IsInstalled, Is.True);
            Assert.That(GameSignals.Cosmetics.Value, Is.SameAs(cosmetics));

            ProgressionBootstrap.Uninstall();

            Assert.That(cosmetics.IsInstalled, Is.False);
            Assert.That(ProgressionBootstrap.Cosmetics, Is.Null);
            Assert.That(GameSignals.Cosmetics.Value, Is.Null);
        }

        [Test]
        public void AGivenAchievementRecorderIsInstalledWithItsProgression()
        {
            var recorder = new ProgressionRecorder(new MemorySaveStorage(), _spec, new OfflineParticipants());
            var achievements = new AchievementRecorder(recorder, AchievementSpec.Default, () => 0d);

            Assert.That(ProgressionBootstrap.Install(achievements), Is.SameAs(achievements));
            Assert.That(ProgressionBootstrap.Install(achievements), Is.SameAs(achievements));

            Assert.That(ProgressionBootstrap.Recorder, Is.SameAs(recorder));
            Assert.That(recorder.IsInstalled, Is.True);
            Assert.That(achievements.IsInstalled, Is.True);
        }

        [Test]
        public void AProfileTakenFromTheCloudIsReadAgain()
        {
            var storage = new MemorySaveStorage();
            var recorder = new ProgressionRecorder(storage, _spec, new OfflineParticipants());

            // 実績が解けて ProfileChanged が増えないよう、実績の無い表で入れる。
            ProgressionBootstrap.Install(new AchievementRecorder(recorder, new AchievementSpec(Array.Empty<AchievementDefinition>()), () => 0d));
            var changes = 0;
            recorder.ProfileChanged += _ => changes++;
            new ProfileStore(storage).Save(new PlayerProfile(600, 3, 1, 2, 1));

            ProgressionBootstrap.ReloadPulledProfile("campaign-Campaign01");

            Assert.That(recorder.Profile, Is.EqualTo(PlayerProfile.Empty), "other slots leave the profile alone");
            Assert.That(changes, Is.Zero);

            ProgressionBootstrap.ReloadPulledProfile(ProfileStore.Slot);

            Assert.That(recorder.Profile, Is.EqualTo(new PlayerProfile(600, 3, 1, 2, 1)));
            Assert.That(recorder.Level, Is.EqualTo(2));
            Assert.That(ProgressionBootstrap.IsUnlocked(ScopeId), Is.True);
            Assert.That(changes, Is.EqualTo(1));

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(new ProfileStore(storage).Load().Matches, Is.EqualTo(3), "the next save builds on the profile taken from the cloud");
        }

        [Test]
        public void OnlyThePulledProfileIsReadAgainWhenOtherRegisteredSlotsArePulledToo()
        {
            const string mission = "campaign-Campaign01";
            CloudSaveBootstrap.ClearRegisteredSlots();
            try
            {
                CloudSaveBootstrap.RegisterSlots(slot => slot == mission, new[] { mission }, text => true);
                var storage = new MemorySaveStorage();
                var backend = new FakeCloudSaveBackend { SignedInAccount = "account-1" };
                backend.Files[mission + ".json"] = CloudSaveEnvelope.Saved(mission, 5000, "{}").ToJson();
                backend.Files[ProfileStore.Slot + ".json"] = CloudSaveEnvelope.Saved(
                    ProfileStore.Slot, 5000, new PlayerProfile(600, 3, 1, 2, 1).ToJson()).ToJson();
                var sync = CloudSaveBootstrap.Create(storage, backend);
                var recorder = new ProgressionRecorder(sync, _spec, new OfflineParticipants());
                ProgressionBootstrap.Install(new AchievementRecorder(recorder, new AchievementSpec(Array.Empty<AchievementDefinition>()), () => 0d));
                var pulled = new System.Collections.Generic.List<string>();
                sync.SlotPulled += pulled.Add;
                sync.SlotPulled += ProgressionBootstrap.ReloadPulledProfile;

                for (var i = 0; i < 50 && !(sync.IsSignedIn && !sync.IsBusy && sync.PendingCount == 0); i++)
                {
                    sync.Tick(0d);
                }

                Assert.That(pulled, Is.EquivalentTo(new[] { ProfileStore.Slot, mission }), "the registered slot is synced with the profile");
                Assert.That(storage.Text(mission), Is.EqualTo("{}"));
                Assert.That(recorder.Profile, Is.EqualTo(new PlayerProfile(600, 3, 1, 2, 1)));
            }
            finally
            {
                CloudSaveBootstrap.ClearRegisteredSlots();
            }
        }

        [Test]
        public void ReloadingAnUninstalledRecorderDoesNothing()
        {
            var storage = new MemorySaveStorage();
            var recorder = new ProgressionRecorder(storage, _spec, new OfflineParticipants());
            new ProfileStore(storage).Save(new PlayerProfile(600, 3, 1, 2, 1));

            recorder.Reload();

            Assert.That(recorder.Profile, Is.EqualTo(PlayerProfile.Empty));
        }

        [Test]
        public void InstallRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ProgressionBootstrap.Install((ISaveStorage)null));
            Assert.Throws<ArgumentNullException>(() => ProgressionBootstrap.Install((ProgressionRecorder)null));
            Assert.Throws<ArgumentNullException>(() => ProgressionBootstrap.Install((AchievementRecorder)null));
            Assert.That(ProgressionBootstrap.Recorder, Is.Null);
        }
    }
}
