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
    // 期待値は ProgressionSpec.Default の調整値 (キル 100・頭 +50・アシスト 50・試合 250・勝ち +500、レベル 2 は 500 から) と、
    // SkillSpec.Default (初め 1000・K 32) から出す。
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
        public void AnAssistAddsTheAssistXpAndIsSavedWithTheMatch()
        {
            _recorder.Install();

            LogAssert.Expect(LogType.Log, "[Progression] assist xp=+50 total=50 level=1 levelUp=false");
            Kill(_enemy, _otherEnemy, true, _player);

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(50, 0, 0, 0, 0, 1)));
            Assert.That(_storage.Has(ProfileStore.Slot), Is.False, "assists are saved when the match ends");

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            var expected = new PlayerProfile(300, 0, 0, 1, 0, 1);
            Assert.That(_recorder.Profile, Is.EqualTo(expected));
            Assert.That(new ProfileStore(_storage).Load(), Is.EqualTo(expected));
        }

        [Test]
        public void TheKillerIsNotAlsoCountedAsAnAssister()
        {
            _recorder.Install();

            Kill(_player, _enemy, false, _player, _otherEnemy);

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(100, 1, 0, 0, 0, 0)));
        }

        [Test]
        public void OneKillGivesAtMostOneAssist()
        {
            _recorder.Install();

            Kill(_enemy, _otherEnemy, false, _player, _player);

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(50, 0, 0, 0, 0, 1)));
        }

        [Test]
        public void AssistsByOthersOnSuicidesAndOnFriendlyKillsAreNotCounted()
        {
            _recorder.Install();

            Kill(_enemy, _otherEnemy, false, _otherEnemy);
            Kill(_enemy, _otherEnemy, headshot: false);
            Kill(_enemy, _enemy, false, _player);
            CombatEvents.RaiseKilled(new KillFeedEntry(
                CombatEvents.OfflineId(_enemy),
                CombatEvents.OfflineId(_otherEnemy),
                WeaponId,
                headshot: false,
                friendly: true,
                assisterIds: new[] { CombatEvents.OfflineId(_player) }));
            CombatEvents.RaiseKilled(new KillFeedEntry(
                CombatEvents.OfflineId(_enemy),
                CombatEvents.OfflineId(_otherEnemy),
                WeaponId,
                headshot: false,
                friendly: false,
                assisterIds: new[] { int.MaxValue }));

            Assert.That(_recorder.Profile, Is.EqualTo(PlayerProfile.Empty));
        }

        [Test]
        public void AVersion1SaveKeepsItsXpAndLevelAndCountsAssistsFromThere()
        {
            _storage.Write(ProfileStore.Slot, "{\"version\":1,\"xp\":480,\"kills\":4,\"deaths\":1,\"matches\":1,\"wins\":0}");

            _recorder.Install();

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(480, 4, 1, 1, 0, 0)));
            Assert.That(_recorder.Level, Is.EqualTo(1));
            Assert.That(_storage.Has(ProfileStore.RejectedSlot), Is.False);

            LogAssert.Expect(LogType.Log, "[Progression] level up 1->2 total=530");
            Kill(_enemy, _otherEnemy, false, _player);

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(530, 4, 1, 1, 0, 1)));
            Assert.That(_recorder.Level, Is.EqualTo(2));

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0));

            Assert.That(
                _storage.Text(ProfileStore.Slot),
                Is.EqualTo("{\"version\":3,\"xp\":780,\"kills\":4,\"assists\":1,\"deaths\":1,\"matches\":2,\"wins\":0,\"rating\":1000}"));
        }

        [Test]
        public void AVersion2SaveKeepsItsRecordAndStartsAtTheConfiguredRating()
        {
            _storage.Write(ProfileStore.Slot, "{\"version\":2,\"xp\":480,\"kills\":4,\"assists\":2,\"deaths\":1,\"matches\":1,\"wins\":0}");
            var recorder = new ProgressionRecorder(_storage, ProgressionSpec.Default, new SkillSpec(1500, 32f, 0, 3000), new OfflineParticipants());
            try
            {
                recorder.Install();

                Assert.That(recorder.Profile, Is.EqualTo(new PlayerProfile(480, 4, 1, 1, 0, 2, 1500)));
                Assert.That(recorder.Level, Is.EqualTo(1));
                Assert.That(_storage.Has(ProfileStore.RejectedSlot), Is.False);

                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1500));

                Assert.That(
                    _storage.Text(ProfileStore.Slot),
                    Is.EqualTo("{\"version\":3,\"xp\":1230,\"kills\":4,\"assists\":2,\"deaths\":1,\"matches\":2,\"wins\":1,\"rating\":1516}"));
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void ARatedWinMovesTheRatingByTheOpponentAverageAndIsSaved()
        {
            _recorder.Install();

            LogAssert.Expect(LogType.Log, "[Progression] rating 1000->1024 outcome=Won opponents=1200");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1200));

            var expected = new PlayerProfile(750, 0, 0, 1, 1, 0, 1024);
            Assert.That(_recorder.Profile, Is.EqualTo(expected));
            Assert.That(new ProfileStore(_storage).Load(), Is.EqualTo(expected));
        }

        [Test]
        public void ARatedLossWithoutAnOpponentAverageUsesTheInitialRating()
        {
            new ProfileStore(_storage).Save(new PlayerProfile(0, 0, 0, 0, 0, 0, 1200));
            _recorder.Install();

            LogAssert.Expect(LogType.Log, "[Progression] rating 1200->1176 outcome=Lost opponents=1000");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0, rated: true));

            Assert.That(_recorder.Profile.Rating, Is.EqualTo(1176));
        }

        [Test]
        public void ARatedDrawAgainstAnEqualSideKeepsTheRating()
        {
            _recorder.Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Draw, 0, 0, 0, rated: true, opponentRating: 1000));

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(250, 0, 0, 1, 0, 0, 1000)));
        }

        [Test]
        public void ARatedMatchUsesTheConfiguredKAndBounds()
        {
            var recorder = new ProgressionRecorder(_storage, ProgressionSpec.Default, new SkillSpec(1000, 64f, 0, 1020), new OfflineParticipants());
            try
            {
                recorder.Install();

                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1000));

                Assert.That(recorder.Profile.Rating, Is.EqualTo(1020), "+32 is cut at the ceiling of 1020");
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void AnOfflineMatchAgainstBotsLeavesTheRating()
        {
            new ProfileStore(_storage).Save(new PlayerProfile(0, 0, 0, 0, 0, 0, 1234));
            _recorder.Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 3, 0, 30));
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 3, 0));

            Assert.That(_recorder.Profile, Is.EqualTo(new PlayerProfile(1000, 0, 3, 2, 1, 0, 1234)));
            Assert.That(new ProfileStore(_storage).Load().Rating, Is.EqualTo(1234));
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
        public void TheSkillSpecIsReadFromTheEntryBeforeResources()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Game";
            var tuning = Track(new TextAsset("{ \"initialRating\": 1500, \"kFactor\": 10 }"));
            tuning.name = "GameSkill";
            content.SkillTuning = tuning;
            GameContent.Use(content);
            KitTuning.Reload();

            var recorder = new ProgressionRecorder(_storage);
            try
            {
                recorder.Install();

                Assert.That(recorder.Skill.Spec.InitialRating, Is.EqualTo(1500));
                Assert.That(recorder.Profile.Rating, Is.EqualTo(1500), "a new profile starts at the configured rating");

                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true));

                Assert.That(recorder.Profile.Rating, Is.EqualTo(1505), "K 10 against the configured initial rating gives +5");
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void TheShippedSkillSpecIsReadFromResources()
        {
            GameContent.Use(Track(ScriptableObject.CreateInstance<GameContent>()));
            KitTuning.Reload();

            var spec = ProgressionRecorder.LoadSkillSpec();

            Assert.That(ProgressionRecorder.SkillResourcePath, Is.EqualTo("Tuning/skill"));
            Assert.That(spec, Is.Not.SameAs(SkillSpec.Default));
            Assert.That(spec.InitialRating, Is.EqualTo(1000));
            Assert.That(spec.KFactor, Is.EqualTo(32f));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(_storage, ProgressionSpec.Default, null, new OfflineParticipants()));
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(null, ProgressionSpec.Default, new OfflineParticipants()));
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(_storage, null, new OfflineParticipants()));
            Assert.Throws<ArgumentNullException>(() => new ProgressionRecorder(_storage, ProgressionSpec.Default, null));
        }

        private static void Kill(GameObject killer, GameObject victim, bool headshot, params GameObject[] assisters)
        {
            var assisterIds = new int[assisters.Length];
            for (var i = 0; i < assisters.Length; i++)
            {
                assisterIds[i] = CombatEvents.OfflineId(assisters[i]);
            }

            CombatEvents.RaiseKilled(
                new KillFeedEntry(CombatEvents.OfflineId(killer), CombatEvents.OfflineId(victim), WeaponId, headshot, friendly: false, assisterIds));
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
