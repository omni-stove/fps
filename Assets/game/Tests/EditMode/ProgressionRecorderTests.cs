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
    /// <summary>
    /// ProgressionRecorder が自分 (Damageable.IsLocalPlayer の者) のキルと試合の終わりを XP と戦績に数え、試合の終わりに保存し、
    /// 自分以外のキル・自滅・味方撃ちを数えないことを確かめる。調整値は ProgressionSpec.Default
    /// (キル 100・頭 +50・試合 250・勝ち +500、レベル 2 は 500 から)。
    /// </summary>
    public sealed class ProgressionRecorderTests
    {
        private const string WeaponId = "rifle";

        private readonly List<Object> _created = new List<Object>();
        private MemorySaveStorage _storage;
        private ProgressionRecorder _recorder;
        private GameObject _player;
        private GameObject _enemy;
        private GameObject _otherEnemy;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _storage = new MemorySaveStorage();
            _recorder = new ProgressionRecorder(_storage, ProgressionSpec.Default, new OfflineParticipants());
            _player = Character("Player", local: true);
            _enemy = Character("Enemy", local: false);
            _otherEnemy = Character("OtherEnemy", local: false);
        }

        [TearDown]
        public void TearDown()
        {
            _recorder.Uninstall();
            CombatEvents.ClearSubscribers();
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
        public void KillsAndTheEndOfAMatchAddXpAndStatsAndAreSaved()
        {
            _recorder.Install();

            Kill(_player, _enemy, headshot: true);
            Kill(_player, _otherEnemy, headshot: false);

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(250, 2, 0, 0, 0)));
            Assert.That(_storage.Has(ProfileStore.Slot), Is.False, "kills are saved when the match ends");

            LogAssert.Expect(LogType.Log, "[Progression] level up 1->2 total=1000");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 2, 1, 20));

            var expected = new PlayerProfile(1000, 2, 1, 1, 1);
            Assert.That(_recorder.Profile, Is.EqualTo(expected));
            Assert.That(_recorder.Level, Is.EqualTo(2));
            Assert.That(_storage.Text(ProfileStore.Slot), Is.EqualTo(expected.ToJson()));
            Assert.That(new ProfileStore(_storage).Load(), Is.EqualTo(expected));
        }

        [Test]
        public void ALostMatchCountsTheMatchButNotAWin()
        {
            _recorder.Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 3, 0));

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(250, 0, 3, 1, 0)));
            Assert.That(_storage.Text(ProfileStore.Slot), Is.EqualTo(new PlayerProfile(250, 0, 3, 1, 0).ToJson()));
        }

        [Test]
        public void KillsByOthersSuicidesAndFriendlyKillsAreNotCounted()
        {
            _recorder.Install();

            Kill(_enemy, _player, headshot: true);
            Kill(_enemy, _otherEnemy, headshot: false);
            Kill(_player, _player, headshot: false);
            CombatEvents.RaiseKilled(
                new KillFeedEntry(CombatEvents.OfflineId(_player), CombatEvents.OfflineId(_enemy), WeaponId, headshot: false, friendly: true));
            CombatEvents.RaiseKilled(new KillFeedEntry(int.MaxValue, CombatEvents.OfflineId(_enemy), WeaponId, headshot: false, friendly: false));

            Assert.That(_recorder.Profile, Is.EqualTo(PlayerProfile.Empty));

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 1, 0));

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(250, 0, 1, 1, 0)));
        }

        [Test]
        public void TheSavedProfileIsReadOnInstallAndContinued()
        {
            new ProfileStore(_storage).Save(new PlayerProfile(400, 4, 1, 1, 0));

            _recorder.Install();

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(400, 4, 1, 1, 0)));
            Assert.That(_recorder.Level, Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, "[Progression] level up 1->2 total=500");
            Kill(_player, _enemy, headshot: false);

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(500, 5, 1, 1, 0)));
            Assert.That(_recorder.Level, Is.EqualTo(2));
        }

        [Test]
        public void ABrokenSaveStartsANewProfile()
        {
            _storage.Write(ProfileStore.Slot, "not json");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[Progression\] profile rejected: "));
            LogAssert.Expect(LogType.Warning, "[Progression] kept the rejected profile in slot=profile-rejected");
            _recorder.Install();

            Assert.That(_recorder.Profile, Is.EqualTo(PlayerProfile.Empty));

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(new ProfileStore(_storage).Load(), Is.EqualTo(new PlayerProfile(250, 0, 0, 1, 0)));
            Assert.That(_storage.Text(ProfileStore.RejectedSlot), Is.EqualTo("not json"));
        }

        [Test]
        public void NothingIsRecordedBeforeInstallOrAfterUninstall()
        {
            Kill(_player, _enemy, headshot: false);
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 1, 0, 10));

            Assert.That(_recorder.IsInstalled, Is.False);
            Assert.That(_recorder.Profile, Is.EqualTo(PlayerProfile.Empty));

            _recorder.Install();
            _recorder.Uninstall();
            Kill(_player, _enemy, headshot: false);
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 1, 0, 10));

            Assert.That(_recorder.IsInstalled, Is.False);
            Assert.That(_recorder.Profile, Is.EqualTo(PlayerProfile.Empty));
            Assert.That(_storage.WriteCount, Is.Zero);
        }

        [Test]
        public void InstallingTwiceCountsEachEventOnce()
        {
            _recorder.Install();
            _recorder.Install();

            Kill(_player, _enemy, headshot: false);
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 1, 0, 10));

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(350, 1, 0, 1, 0)));
            Assert.That(_storage.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void AFailedSaveIsLoggedAndWrittenWithTheNextMatch()
        {
            _recorder.Install();
            _storage.FailWrites = true;

            LogAssert.Expect(LogType.Error, "[Progression] could not save the profile: disk full");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(250, 0, 0, 1, 0)));
            Assert.That(_storage.Has(ProfileStore.Slot), Is.False);

            _storage.FailWrites = false;
            LogAssert.Expect(LogType.Log, "[Progression] level up 1->2 total=500");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(new ProfileStore(_storage).Load(), Is.EqualTo(new PlayerProfile(500, 0, 0, 2, 0)));
        }

        [Test]
        public void TheSpecIsReadFromTheEntryBeforeResources()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Game";
            var tuning = Track(new TextAsset("{ \"killXp\": 7, \"matchXp\": 0, \"winXp\": 0 }"));
            tuning.name = "GameProgression";
            content.ProgressionTuning = tuning;
            GameContent.Use(content);
            KitTuning.Reload();

            var recorder = new ProgressionRecorder(_storage);
            try
            {
                recorder.Install();
                Kill(_player, _enemy, headshot: false);

                Assert.That(recorder.Ledger.Spec.KillXp, Is.EqualTo(7));
                Assert.That(recorder.Profile, Is.EqualTo(new PlayerProfile(7, 1, 0, 0, 0)));
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void TheShippedSpecIsReadFromResources()
        {
            GameContent.Use(Track(ScriptableObject.CreateInstance<GameContent>()));
            KitTuning.Reload();

            var spec = ProgressionRecorder.LoadSpec();

            Assert.That(ProgressionRecorder.ResourcePath, Is.EqualTo("Tuning/progression"));
            Assert.That(spec, Is.Not.SameAs(ProgressionSpec.Default));
            Assert.That(spec.KillXp, Is.EqualTo(100));
            Assert.That(spec.Unlocks.Count, Is.EqualTo(6));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(null, ProgressionSpec.Default, new OfflineParticipants()));
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(_storage, null, new OfflineParticipants()));
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(_storage, ProgressionSpec.Default, null));
        }

        private static void Kill(GameObject killer, GameObject victim, bool headshot)
        {
            CombatEvents.RaiseKilled(
                new KillFeedEntry(CombatEvents.OfflineId(killer), CombatEvents.OfflineId(victim), WeaponId, headshot, friendly: false));
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
