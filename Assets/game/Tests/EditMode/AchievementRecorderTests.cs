using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 期待値は AchievementSpec.Default (first-blood キル 1・marksman 頭 50・veteran 試合 25・victor 勝ち 10、近接は knife) と、
    // ProgressionSpec.Default の調整値 (キル 100・頭 +50・試合 250・勝ち +500) から出す。
    public sealed class AchievementRecorderTests
    {
        private const string RifleId = "rifle";
        private const string KnifeId = "knife";

        private readonly List<Object> _created = new List<Object>();
        private MemorySaveStorage _storage;
        private ProgressionRecorder _progression;
        private AchievementRecorder _recorder;
        private double _now;
        private GameObject _player;
        private GameObject _enemy;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            GameSignals.Achievements.Clear();
            _storage = new MemorySaveStorage();
            _now = 100d;
            (_progression, _recorder) = Recorders(_storage, AchievementSpec.Default);
            _player = Character("Player", local: true);
            _enemy = Character("Enemy", local: false);
        }

        [TearDown]
        public void TearDown()
        {
            _recorder.Uninstall();
            _progression.Uninstall();
            CombatEvents.ClearSubscribers();
            GameSignals.Achievements.Clear();
            GameContent.Use(null);
            KitTuning.Reload();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheFirstKillUnlocksAnAchievementThatIsStillThereAfterReopeningTheProfile()
        {
            Install();
            var unlocked = new List<string>();
            _recorder.Unlocked += a => unlocked.Add(a.Id);

            LogAssert.Expect(LogType.Log, "[Achievement] unlocked id=first-blood stat=Kills target=1");
            Kill(_player, _enemy, headshot: false, RifleId);

            Assert.That(unlocked, Is.EqualTo(new[] { "first-blood" }));
            Assert.That(_progression.Profile.Achievements, Is.EqualTo(new[] { "first-blood" }));

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 1, 0, 10));

            Assert.That(new ProfileStore(_storage).Load().Achievements, Is.EqualTo(new[] { "first-blood" }));

            _recorder.Uninstall();
            _progression.Uninstall();
            var (progression, recorder) = Recorders(_storage, AchievementSpec.Default);
            try
            {
                progression.Install();
                recorder.Install();
                var reopened = new List<string>();
                recorder.Unlocked += a => reopened.Add(a.Id);

                Kill(_player, _enemy, headshot: false, RifleId);

                Assert.That(progression.Profile.Achievements, Is.EqualTo(new[] { "first-blood" }));
                Assert.That(progression.Profile.Kills, Is.EqualTo(2));
                Assert.That(reopened, Is.Empty, "an achievement kept in the profile is not unlocked twice");
                Assert.That(recorder.Progress[0], Is.EqualTo(new AchievementProgress("first-blood", 2, 1, true)));
            }
            finally
            {
                recorder.Uninstall();
                progression.Uninstall();
            }
        }

        [Test]
        public void HeadshotsAndMeleeKillsAreCountedAndSavedWithTheMatch()
        {
            Install();

            Kill(_player, _enemy, headshot: true, RifleId);
            Kill(_player, _enemy, headshot: false, KnifeId);
            Kill(_player, _enemy, headshot: false, RifleId);

            Assert.That(_progression.Profile.Kills, Is.EqualTo(3));
            Assert.That(_progression.Profile.Headshots, Is.EqualTo(1));
            Assert.That(_progression.Profile.MeleeKills, Is.EqualTo(1));
            Assert.That(_storage.Has(ProfileStore.Slot), Is.False, "the counts are saved when the match ends");

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 3, 0, 30));

            var saved = new ProfileStore(_storage).Load();
            Assert.That(saved.Headshots, Is.EqualTo(1));
            Assert.That(saved.MeleeKills, Is.EqualTo(1));
            Assert.That(_storage.WriteCount, Is.EqualTo(1), "the achievements are saved together with the progression");
        }

        [Test]
        public void KillsByOthersSuicidesAndFriendlyKillsCountNothing()
        {
            Install();

            Kill(_enemy, _player, headshot: true, KnifeId);
            Kill(_player, _player, headshot: true, KnifeId);
            CombatEvents.RaiseKilled(
                new KillFeedEntry(CombatEvents.OfflineId(_player), CombatEvents.OfflineId(_enemy), KnifeId, headshot: true, friendly: true));

            Assert.That(_progression.Profile.Headshots, Is.Zero);
            Assert.That(_progression.Profile.MeleeKills, Is.Zero);
            Assert.That(_progression.Profile.Achievements, Is.Empty);
        }

        [Test]
        public void TheTimeFromTheFirstFightToTheEndOfTheMatchIsAddedToThePlayTime()
        {
            Install();

            _now = 150d;
            CombatEvents.RaiseDamageTaken(null, 10);
            _now = 160d;
            Kill(_player, _enemy, headshot: false, RifleId);
            _now = 245.6d;
            LogAssert.Expect(LogType.Log, "[Achievement] play time +95s total=95s");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 1, 0, 10));

            Assert.That(new ProfileStore(_storage).Load().PlaySeconds, Is.EqualTo(95));

            _now = 1000d;
            CombatEvents.RaiseShotFired(_player, Vector3.zero, Vector3.forward, RifleId);
            _now = 1010.5d;
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(_progression.Profile.PlaySeconds, Is.EqualTo(106), "10.5 s and the 0.6 s carried over make 11 s");
        }

        [Test]
        public void TimeSpentInTheMenuAfterASceneLoadIsNotAddedToThePlayTime()
        {
            Install();

            _now = 100d;
            CombatEvents.RaiseShotFired(_player, Vector3.zero, Vector3.forward, RifleId);
            _recorder.StopMatchClock();
            _now = 7300d;
            CombatEvents.RaiseShotFired(_player, Vector3.zero, Vector3.forward, RifleId);
            _now = 7400d;
            LogAssert.Expect(LogType.Log, "[Achievement] play time +100s total=100s");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(_progression.Profile.PlaySeconds, Is.EqualTo(100));
        }

        [Test]
        public void AMatchWithoutAFightAddsNoPlayTime()
        {
            Install();

            _now = 500d;
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(_progression.Profile.PlaySeconds, Is.Zero);
            Assert.That(_progression.Profile.Matches, Is.EqualTo(1));
        }

        [Test]
        public void TheMatchCountUnlocksTheMatchAchievementBeforeTheSave()
        {
            new ProfileStore(_storage).Save(new PlayerProfile(0, 0, 0, 24, 9));
            Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0));

            Assert.That(new ProfileStore(_storage).Load().Achievements, Is.EqualTo(new[] { "veteran", "victor" }));
        }

        [Test]
        public void AnOldProfileThatAlreadyMeetsATargetIsUnlockedOnInstall()
        {
            _storage.Write(ProfileStore.Slot, "{\"version\":3,\"xp\":900,\"kills\":3,\"assists\":0,\"deaths\":2,\"matches\":2,\"wins\":1,\"rating\":1000}");

            Install();

            Assert.That(_progression.Profile.Achievements, Is.EqualTo(new[] { "first-blood" }));
            Assert.That(_storage.Has(ProfileStore.RejectedSlot), Is.False);
        }

        [Test]
        public void TheProgressIsPublishedAndClearedOnUninstall()
        {
            Install();

            Assert.That(GameSignals.Achievements.Value, Is.EqualTo(new[]
            {
                new AchievementProgress("first-blood", 0, 1, false),
                new AchievementProgress("marksman", 0, 50, false),
                new AchievementProgress("veteran", 0, 25, false),
                new AchievementProgress("victor", 0, 10, false),
            }));

            Kill(_player, _enemy, headshot: true, RifleId);

            Assert.That(GameSignals.Achievements.Value[0], Is.EqualTo(new AchievementProgress("first-blood", 1, 1, true)));
            Assert.That(GameSignals.Achievements.Value[1], Is.EqualTo(new AchievementProgress("marksman", 1, 50, false)));

            _recorder.Uninstall();

            Assert.That(GameSignals.Achievements.HasValue, Is.False);
        }

        [Test]
        public void NothingIsRecordedAfterUninstallAndInstallingTwiceCountsOnce()
        {
            Install();
            _recorder.Install();

            Kill(_player, _enemy, headshot: true, RifleId);

            Assert.That(_progression.Profile.Headshots, Is.EqualTo(1));

            _recorder.Uninstall();
            Kill(_player, _enemy, headshot: true, RifleId);

            Assert.That(_progression.Profile.Headshots, Is.EqualTo(1));
            Assert.That(_recorder.IsInstalled, Is.False);
        }

        [Test]
        public void TheProgressionRecorderMustBeInstalledFirst()
        {
            Assert.Throws<InvalidOperationException>(() => _recorder.Install());
            Assert.That(_recorder.IsInstalled, Is.False);
        }

        [Test]
        public void TheSpecIsReadFromTheEntryBeforeResources()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Game";
            var tuning = Track(new TextAsset(
                "{ \"achievements\": [{ \"id\": \"brawler\", \"stat\": \"MeleeKills\", \"target\": 1 }], \"meleeWeaponIds\": [\"axe\"] }"));
            tuning.name = "GameAchievements";
            content.AchievementsTuning = tuning;
            GameContent.Use(content);
            KitTuning.Reload();

            var spec = AchievementRecorder.LoadSpec();

            Assert.That(spec.Achievements.Count, Is.EqualTo(1));
            Assert.That(spec.Achievements[0].Id, Is.EqualTo("brawler"));
            Assert.That(spec.MeleeWeaponIds, Is.EqualTo(new[] { "axe" }));
        }

        [Test]
        public void TheShippedSpecIsReadFromResources()
        {
            GameContent.Use(Track(ScriptableObject.CreateInstance<GameContent>()));
            KitTuning.Reload();

            var spec = AchievementRecorder.LoadSpec();

            Assert.That(spec, Is.Not.SameAs(AchievementSpec.Default));
            Assert.That(spec.Achievements.Count, Is.EqualTo(4));
            Assert.That(spec.MeleeWeaponIds, Is.EqualTo(new[] { "knife" }));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new AchievementRecorder(null));
            Assert.Throws<ArgumentNullException>(() => new AchievementRecorder(null, AchievementSpec.Default, () => 0d));
            Assert.Throws<ArgumentNullException>(() => new AchievementRecorder(_progression, null, () => 0d));
            Assert.Throws<ArgumentNullException>(() => new AchievementRecorder(_progression, AchievementSpec.Default, null));
        }

        private void Install()
        {
            _progression.Install();
            _recorder.Install();
        }

        private (ProgressionRecorder, AchievementRecorder) Recorders(ISaveStorage storage, AchievementSpec spec)
        {
            var progression = new ProgressionRecorder(storage, ProgressionSpec.Default, new OfflineParticipants());
            return (progression, new AchievementRecorder(progression, spec, () => _now));
        }

        private static void Kill(GameObject killer, GameObject victim, bool headshot, string weaponId)
        {
            CombatEvents.RaiseKilled(
                new KillFeedEntry(CombatEvents.OfflineId(killer), CombatEvents.OfflineId(victim), weaponId, headshot, friendly: false));
        }

        private GameObject Character(string name, bool local)
        {
            var character = Track(new GameObject(name));
            character.AddComponent<Damageable>().IsLocalPlayer = local;
            return character;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
