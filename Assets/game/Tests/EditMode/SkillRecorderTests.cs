using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 期待値は SkillSpec.Default (初め 1000・K 32・下限 0・上限 3000) から、
    // 変化 = K × (結果 - 1 / (1 + 10^((相手 - 自分) / 400))) を四捨五入して出す。
    public sealed class SkillRecorderTests
    {
        private MemorySaveStorage _storage;
        private SkillRecorder _recorder;
        private GameContent _content;
        private TextAsset _tuning;

        [SetUp]
        public void SetUp()
        {
            SkillBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
            _storage = new MemorySaveStorage();
            _recorder = new SkillRecorder(_storage, SkillSpec.Default);
        }

        [TearDown]
        public void TearDown()
        {
            _recorder.Uninstall();
            SkillBootstrap.Uninstall();
            CombatEvents.ClearSubscribers();
            GameContent.Use(null);
            KitTuning.Reload();
            if (_content != null)
            {
                Object.DestroyImmediate(_content);
            }

            if (_tuning != null)
            {
                Object.DestroyImmediate(_tuning);
            }
        }

        [Test]
        public void ANewStorageStartsAtTheInitialRatingWithoutWriting()
        {
            LogAssert.Expect(LogType.Log, "[Skill] configured initial=1000 k=32 min=0 max=3000");
            LogAssert.Expect(LogType.Log, "[Skill] loaded rating=1000");
            _recorder.Install();

            Assert.That(SkillRecorder.Slot, Is.EqualTo("skill"));
            Assert.That(_recorder.Rating, Is.EqualTo(1000));
            Assert.That(_storage.WriteCount, Is.Zero, "nothing is written just by reading");
        }

        [Test]
        public void ARatedWinMovesTheRatingByTheOpponentAverageAndIsSaved()
        {
            _recorder.Install();

            LogAssert.Expect(LogType.Log, "[Skill] rating 1000->1024 outcome=Won opponents=1200");
            LogAssert.Expect(LogType.Log, "[Skill] saved rating=1024");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1200));

            Assert.That(_recorder.Rating, Is.EqualTo(1024));
            Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":1024}"));
        }

        [Test]
        public void ARatedLossWithoutAnOpponentAverageUsesTheInitialRating()
        {
            _storage.Write(SkillRecorder.Slot, new SkillSave(1200).ToJson());
            _recorder.Install();

            LogAssert.Expect(LogType.Log, "[Skill] rating 1200->1176 outcome=Lost opponents=1000");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 0, 0, rated: true));

            Assert.That(_recorder.Rating, Is.EqualTo(1176));
            Assert.That(SkillSave.FromJson(_storage.Text(SkillRecorder.Slot)).Rating, Is.EqualTo(1176));
        }

        [Test]
        public void ARatedDrawAgainstAnEqualSideKeepsTheRating()
        {
            _recorder.Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Draw, 0, 0, 0, rated: true, opponentRating: 1000));

            Assert.That(_recorder.Rating, Is.EqualTo(1000));
            Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":1000}"));
        }

        [Test]
        public void ARatedMatchUsesTheConfiguredKAndBounds()
        {
            var recorder = new SkillRecorder(_storage, new SkillSpec(1000, 64f, 0, 1020));
            try
            {
                recorder.Install();

                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1000));

                Assert.That(recorder.Rating, Is.EqualTo(1020), "+32 is cut at the ceiling of 1020");
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void AnOfflineMatchAgainstBotsLeavesTheRatingAndWritesNothing()
        {
            _storage.Write(SkillRecorder.Slot, new SkillSave(1234).ToJson());
            var writes = _storage.WriteCount;
            _recorder.Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 3, 0, 30));
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Lost, 0, 3, 0));

            Assert.That(_recorder.Rating, Is.EqualTo(1234));
            Assert.That(_storage.WriteCount, Is.EqualTo(writes));
        }

        [Test]
        public void TheSkillSpecIsReadFromTheEntryBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Game";
            _tuning = new TextAsset("{ \"initialRating\": 1500, \"kFactor\": 10 }") { name = "GameSkill" };
            _content.SkillTuning = _tuning;
            GameContent.Use(_content);
            KitTuning.Reload();

            var recorder = new SkillRecorder(_storage);
            try
            {
                recorder.Install();

                Assert.That(recorder.Skill.Spec.InitialRating, Is.EqualTo(1500));
                Assert.That(recorder.Rating, Is.EqualTo(1500), "a new save starts at the configured rating");

                CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true));

                Assert.That(recorder.Rating, Is.EqualTo(1505), "K 10 against the configured initial rating gives +5");
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void TheShippedSkillSpecIsReadFromResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            GameContent.Use(_content);
            KitTuning.Reload();

            var spec = SkillRecorder.LoadSpec();

            Assert.That(SkillRecorder.ResourcePath, Is.EqualTo("Tuning/skill"));
            Assert.That(spec, Is.Not.SameAs(SkillSpec.Default));
            Assert.That(spec.InitialRating, Is.EqualTo(1000));
            Assert.That(spec.KFactor, Is.EqualTo(32f));
        }

        [Test]
        public void ALegacyRatingIsTakenAndSavedWhenThereIsNoSkillSave()
        {
            _recorder.Install();

            LogAssert.Expect(LogType.Log, "[Skill] took the profile rating 1234 as rating=1234");
            LogAssert.Expect(LogType.Log, "[Skill] saved rating=1234");
            var adopted = _recorder.AdoptLegacy(1234);

            Assert.That(adopted, Is.True);
            Assert.That(_recorder.Rating, Is.EqualTo(1234));
            Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":1234}"));

            Assert.That(_recorder.AdoptLegacy(900), Is.False, "the skill save written by the first adoption wins");
            Assert.That(_recorder.Rating, Is.EqualTo(1234));
        }

        [Test]
        public void ALegacyRatingDoesNothingWhenThereIsASkillSave()
        {
            _storage.Write(SkillRecorder.Slot, new SkillSave(1176).ToJson());
            _recorder.Install();
            var writes = _storage.WriteCount;

            Assert.That(_recorder.AdoptLegacy(1234), Is.False);
            Assert.That(_recorder.Rating, Is.EqualTo(1176));
            Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":1176}"));
            Assert.That(_storage.WriteCount, Is.EqualTo(writes));
        }

        [Test]
        public void ALegacyRatingDoesNothingWhenTheSkillSaveIsBroken()
        {
            _storage.Write(SkillRecorder.Slot, "not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Skill\] skill save rejected: "));
            LogAssert.Expect(LogType.Warning, "[Skill] kept the rejected skill save in slot=skill-rejected");
            _recorder.Install();

            Assert.That(_recorder.AdoptLegacy(1234), Is.False, "the skill slot is there, so the profile rating is older");
            Assert.That(_recorder.Rating, Is.EqualTo(1000));
        }

        [Test]
        public void ALegacyRatingIsKeptInsideTheConfiguredBounds()
        {
            var recorder = new SkillRecorder(_storage, new SkillSpec(1000, 32f, 100, 2000));
            try
            {
                recorder.Install();

                LogAssert.Expect(LogType.Log, "[Skill] took the profile rating 2500 as rating=2000");
                recorder.AdoptLegacy(2500);

                Assert.That(recorder.Rating, Is.EqualTo(2000));
                Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":2000}"));
            }
            finally
            {
                recorder.Uninstall();
            }
        }

        [Test]
        public void ALegacyRatingThatCannotBeSavedIsWrittenAfterTheNextMatch()
        {
            _recorder.Install();
            _storage.FailWrites = true;

            LogAssert.Expect(LogType.Error, "[Skill] could not save the rating: disk full");
            Assert.That(_recorder.AdoptLegacy(1234), Is.True);
            Assert.That(_recorder.Rating, Is.EqualTo(1234));
            Assert.That(_storage.Has(SkillRecorder.Slot), Is.False);

            _storage.FailWrites = false;
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 3, 0, 30));

            Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":1234}"), "an unrated match still writes what was not saved");
        }

        [Test]
        public void AFailedSaveIsLoggedAndWrittenWithTheNextMatch()
        {
            _recorder.Install();
            _storage.FailWrites = true;

            LogAssert.Expect(LogType.Error, "[Skill] could not save the rating: disk full");
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1000));

            Assert.That(_recorder.Rating, Is.EqualTo(1016));
            Assert.That(_storage.Has(SkillRecorder.Slot), Is.False);

            _storage.FailWrites = false;
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1016));

            Assert.That(SkillSave.FromJson(_storage.Text(SkillRecorder.Slot)).Rating, Is.EqualTo(1032));
        }

        [Test]
        public void ABrokenSkillSaveIsKeptAsideAndStartsAtTheInitialRating()
        {
            _storage.Write(SkillRecorder.Slot, "{\"version\":1,\"rating\":-5}");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Skill\] skill save rejected: invalid skill data: "));
            LogAssert.Expect(LogType.Warning, "[Skill] kept the rejected skill save in slot=skill-rejected");
            _recorder.Install();

            Assert.That(_recorder.Rating, Is.EqualTo(1000));
            Assert.That(_storage.Text(SkillRecorder.RejectedSlot), Is.EqualTo("{\"version\":1,\"rating\":-5}"));
            Assert.That(_storage.Text(SkillRecorder.Slot), Is.EqualTo("{\"version\":1,\"rating\":-5}"), "the slot is left until the next save");
        }

        [Test]
        public void ASkillSaveFromANewerVersionIsKeptAside()
        {
            const string newer = "{\"version\":2,\"rating\":1500,\"league\":\"gold\"}";
            _storage.Write(SkillRecorder.Slot, newer);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Skill\] skill save rejected: unsupported skill data version 2"));
            LogAssert.Expect(LogType.Warning, "[Skill] kept the rejected skill save in slot=skill-rejected");
            _recorder.Install();

            Assert.That(_recorder.Rating, Is.EqualTo(1000));
            Assert.That(_storage.Text(SkillRecorder.RejectedSlot), Is.EqualTo(newer));
        }

        [Test]
        public void ReloadReadsARatingWrittenFromOutside()
        {
            _recorder.Install();
            _storage.Write(SkillRecorder.Slot, new SkillSave(1300).ToJson());

            _recorder.Reload();

            Assert.That(_recorder.Rating, Is.EqualTo(1300));

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1300));

            Assert.That(SkillSave.FromJson(_storage.Text(SkillRecorder.Slot)).Rating, Is.EqualTo(1316), "the next save builds on the reloaded rating");
        }

        [Test]
        public void NothingIsRecordedBeforeInstallOrAfterUninstall()
        {
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1000));
            _recorder.Reload();

            Assert.That(_recorder.IsInstalled, Is.False);
            Assert.That(_recorder.Rating, Is.EqualTo(1000));

            _recorder.Install();
            _recorder.Uninstall();
            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1000));

            Assert.That(_recorder.IsInstalled, Is.False);
            Assert.That(_recorder.Rating, Is.EqualTo(1000));
            Assert.That(_storage.WriteCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => _recorder.AdoptLegacy(1234));
            Assert.Throws<InvalidOperationException>(() => _recorder.Save());
        }

        [Test]
        public void InstallingTwiceRatesEachMatchOnce()
        {
            _recorder.Install();
            _recorder.Install();

            CombatEvents.RaiseMatchFinished(new MatchResult(MatchOutcome.Won, 0, 0, 0, rated: true, opponentRating: 1000));

            Assert.That(_recorder.Rating, Is.EqualTo(1016));
            Assert.That(_storage.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void TheBootstrapGivesTheInstalledRecorderAndReloadsOnlyThePulledSkillSlot()
        {
            Assert.That(SkillBootstrap.Current, Is.Null);

            var installed = SkillBootstrap.Install(_recorder);

            Assert.That(installed, Is.SameAs(_recorder));
            Assert.That(SkillBootstrap.Current, Is.SameAs(_recorder));
            Assert.That(_recorder.IsInstalled, Is.True);
            Assert.That(SkillBootstrap.Install(_recorder), Is.SameAs(_recorder), "installing the same recorder again keeps it");

            _storage.Write(SkillRecorder.Slot, new SkillSave(1400).ToJson());
            SkillBootstrap.ReloadPulledSkill("profile");
            Assert.That(_recorder.Rating, Is.EqualTo(1000));
            SkillBootstrap.ReloadPulledSkill(SkillRecorder.Slot);
            Assert.That(_recorder.Rating, Is.EqualTo(1400));

            var other = SkillBootstrap.Install(new SkillRecorder(new MemorySaveStorage(), SkillSpec.Default));
            Assert.That(_recorder.IsInstalled, Is.False, "the previous recorder is taken out");
            Assert.That(SkillBootstrap.Current, Is.SameAs(other));

            SkillBootstrap.Uninstall();
            Assert.That(SkillBootstrap.Current, Is.Null);
            Assert.That(other.IsInstalled, Is.False);
        }

        [Test]
        public void TheCloudSaveOwnsOnlyTheSkillSlotAndReadsOnlyASkillSave()
        {
            Assert.That(SkillCloudSave.IsSlot("skill"), Is.True);
            Assert.That(SkillCloudSave.IsSlot("skill-rejected"), Is.False);
            Assert.That(SkillCloudSave.IsSlot("profile"), Is.False);
            Assert.That(SkillCloudSave.IsSlot(null), Is.False);
            Assert.That(SkillCloudSave.CanRead(new SkillSave(1234).ToJson()), Is.True);
            Assert.Throws<FormatException>(() => SkillCloudSave.CanRead("{\"version\":6,\"xp\":0}"));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SkillRecorder(null, SkillSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new SkillRecorder(_storage, null));
            Assert.Throws<ArgumentNullException>(() => SkillBootstrap.Install((SkillRecorder)null));
            Assert.Throws<ArgumentNullException>(() => SkillBootstrap.Install((ISaveStorage)null));
            _recorder.Install();
            Assert.Throws<ArgumentOutOfRangeException>(() => _recorder.AdoptLegacy(-1));
        }
    }
}
