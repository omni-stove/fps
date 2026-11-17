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
        public void InstallRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ProgressionBootstrap.Install((ISaveStorage)null));
            Assert.Throws<ArgumentNullException>(() => ProgressionBootstrap.Install((ProgressionRecorder)null));
            Assert.That(ProgressionBootstrap.Recorder, Is.Null);
        }
    }
}
