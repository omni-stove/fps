using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class NetMatchDefinitionModesTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private NetMatchDefinition _match;
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _match = Make<NetMatchDefinition>();
            _content = Make<GameContent>();
            GameContent.Use(_content);
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void NothingWiredPlaysTheCoreDefaultsUnderTheMapsWeatherAndTime()
        {
            var settings = _match.ToSettings();

            Assert.That(settings.WeatherId, Is.Null);
            Assert.That(settings.TimeOfDayId, Is.Null);
            Assert.That(settings.CaptureTheFlag, Is.SameAs(CaptureTheFlagSpec.Default));
            Assert.That(settings.Hardpoint, Is.SameAs(HardpointSpec.Default));
            Assert.That(settings.SearchAndDestroy, Is.SameAs(SearchAndDestroySpec.Default));
            Assert.That(settings.FreeForAll, Is.SameAs(FreeForAllSpec.Default));
            Assert.That(settings.KillConfirmed, Is.SameAs(KillConfirmedSpec.Default));
            Assert.That(settings.GunGame, Is.SameAs(GunGameSpec.Default));
        }

        [Test]
        public void TheWeatherAndTimeOfDayIdsAreCarried()
        {
            _match.WeatherId = "rain";
            _match.TimeOfDayId = "dusk";

            var settings = _match.ToSettings();

            Assert.That(settings.WeatherId, Is.EqualTo("rain"));
            Assert.That(settings.TimeOfDayId, Is.EqualTo("dusk"));
        }

        [TestCase("rain storm")]
        [TestCase("rain/../x")]
        public void AnIdThatIsNotLettersDigitsOrDashesIsRefused(string id)
        {
            _match.WeatherId = id;

            var e = Assert.Throws<ArgumentException>(() => _match.ToSettings());

            Assert.That(e.ParamName, Is.EqualTo("weatherId"));
        }

        [Test]
        public void TheWiredModeAssetsAreRead()
        {
            var ctf = Make<CaptureTheFlagDefinition>();
            ctf.CopyFrom(new CaptureTheFlagSpec(scoreLimit: 5, returnSeconds: 12f));
            var hardpoint = Make<HardpointDefinition>();
            hardpoint.RotateSeconds = 45f;
            hardpoint.ScoreLimit = 150;
            var snd = Make<SearchAndDestroyDefinition>();
            snd.CopyFrom(new SearchAndDestroySpec(winsToWin: 6, fuseSeconds: 40f));
            var ffa = Make<FreeForAllDefinition>();
            ffa.ScoreLimit = 25;
            ffa.PointsPerKill = 2;
            var kc = Make<KillConfirmedDefinition>();
            kc.CopyFrom(new KillConfirmedSpec(30, 420f, 2f, 20f, 3, 1.5f));
            var ladder = Make<GunGameLadderDefinition>();
            ladder.TimeLimitSeconds = 480f;
            ladder.RespawnSeconds = 1f;
            ladder.DemotesOnMelee = false;
            _match.CaptureTheFlag = ctf;
            _match.Hardpoint = hardpoint;
            _match.SearchAndDestroy = snd;
            _match.FreeForAll = ffa;
            _match.KillConfirmed = kc;
            _match.GunGameLadder = ladder;

            var settings = _match.ToSettings();

            Assert.That(settings.CaptureTheFlag.ScoreLimit, Is.EqualTo(5));
            Assert.That(settings.CaptureTheFlag.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(settings.Hardpoint.RotateSeconds, Is.EqualTo(45f));
            Assert.That(settings.Hardpoint.ScoreLimit, Is.EqualTo(150));
            Assert.That(settings.Hardpoint.PointCount, Is.EqualTo(HardpointSpec.MinPointCount));
            Assert.That(settings.SearchAndDestroy.WinsToWin, Is.EqualTo(6));
            Assert.That(settings.SearchAndDestroy.FuseSeconds, Is.EqualTo(40f));
            Assert.That(settings.SearchAndDestroy.SiteCount, Is.EqualTo(SearchAndDestroySpec.DefaultSiteCount));
            Assert.That(settings.FreeForAll.ScoreLimit, Is.EqualTo(25));
            Assert.That(settings.FreeForAll.PointsPerKill, Is.EqualTo(2));
            Assert.That(settings.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(20f));
            Assert.That(settings.KillConfirmed.PointsPerConfirm, Is.EqualTo(3));
            Assert.That(settings.GunGame.TimeLimitSeconds, Is.EqualTo(480f));
            Assert.That(settings.GunGame.RespawnSeconds, Is.EqualTo(1f));
            Assert.That(settings.GunGame.DemotesOnMelee, Is.False);
            Assert.That(settings.GunGame.TierWeaponIds, Is.Empty, "a ladder without tiers still exports the rules that do not play gun game");
        }

        [Test]
        public void TheGameContentModeAssetsAreReadWhenNothingIsWired()
        {
            var ffa = Make<FreeForAllDefinition>();
            ffa.ScoreLimit = 40;
            var kc = Make<KillConfirmedDefinition>();
            kc.CopyFrom(new KillConfirmedSpec(12, 200f, 1f, 9f));
            _content.FreeForAll = ffa;
            _content.KillConfirmed = kc;

            var settings = _match.ToSettings();

            Assert.That(settings.FreeForAll.ScoreLimit, Is.EqualTo(40));
            Assert.That(settings.KillConfirmed.ScoreLimit, Is.EqualTo(12));
            Assert.That(settings.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(9f));
        }

        [Test]
        public void TheWiredModeAssetWinsOverGameContent()
        {
            var wired = Make<FreeForAllDefinition>();
            wired.ScoreLimit = 11;
            var content = Make<FreeForAllDefinition>();
            content.ScoreLimit = 22;
            _match.FreeForAll = wired;
            _content.FreeForAll = content;

            Assert.That(_match.ToSettings().FreeForAll.ScoreLimit, Is.EqualTo(11));
        }

        [Test]
        public void AnInvalidModeAssetStopsTheSettings()
        {
            var ffa = Make<FreeForAllDefinition>();
            ffa.ScoreLimit = 0;
            ffa.name = "BrokenFreeForAll";
            _match.FreeForAll = ffa;

            var e = Assert.Throws<InvalidOperationException>(() => _match.ToSettings());

            Assert.That(e.Message, Does.Contain("BrokenFreeForAll").And.Contain("scoreLimit"));
        }

        [Test]
        public void TheNewObjectivesFollowTheOlderOnesInTheEnum()
        {
            Assert.That((int)MatchObjective.CaptureTheFlag, Is.EqualTo(3));
            Assert.That((int)MatchObjective.GunGame, Is.EqualTo(8));
            Assert.That((int)MatchObjective.Payload, Is.EqualTo(9));
        }

        [Test]
        public void AnUntouchedMatchPlaysTheVoteKickPayloadAndEconomyDefaults()
        {
            var settings = _match.ToSettings();

            Assert.That(settings.VoteKick.Enabled, Is.False);
            Assert.That(settings.VoteKick.RequiredYesFraction, Is.EqualTo(VoteKickSpec.DefaultRequiredYesFraction));
            Assert.That(settings.VoteKick.VoteSeconds, Is.EqualTo(VoteKickSpec.DefaultVoteSeconds));
            Assert.That(settings.VoteKick.CooldownSeconds, Is.EqualTo(VoteKickSpec.DefaultCooldownSeconds));
            Assert.That(settings.Payload.CartSpeed, Is.EqualTo(PayloadSpec.DefaultCartSpeed));
            Assert.That(settings.Payload.MinPushers, Is.EqualTo(PayloadSpec.DefaultMinPushers));
            Assert.That(settings.Payload.PusherSpeedMultipliers, Is.EqualTo(PayloadSpec.Default.PusherSpeedMultipliers));
            Assert.That(settings.Payload.RollbackDelaySeconds, Is.EqualTo(PayloadSpec.DefaultRollbackDelaySeconds));
            Assert.That(settings.Payload.RollbackSpeed, Is.EqualTo(PayloadSpec.DefaultRollbackSpeed));
            Assert.That(settings.Payload.CheckpointBonusSeconds, Is.EqualTo(PayloadSpec.DefaultCheckpointBonusSeconds));
            Assert.That(settings.Payload.TimeLimitSeconds, Is.EqualTo(PayloadSpec.DefaultTimeLimitSeconds));
            Assert.That(settings.Economy.StartMoney, Is.EqualTo(EconomySpec.DefaultStartMoney));
            Assert.That(settings.Economy.MaxMoney, Is.EqualTo(EconomySpec.DefaultMaxMoney));
            Assert.That(settings.Economy.KillReward, Is.EqualTo(EconomySpec.DefaultKillReward));
            Assert.That(settings.Economy.RoundWinReward, Is.EqualTo(EconomySpec.DefaultRoundWinReward));
            Assert.That(settings.Economy.RoundLossReward, Is.EqualTo(EconomySpec.DefaultRoundLossReward));
            Assert.That(settings.Economy.LossStreakBonus, Is.EqualTo(EconomySpec.DefaultLossStreakBonus));
            Assert.That(settings.Economy.MaxLossReward, Is.EqualTo(EconomySpec.DefaultMaxLossReward));
        }

        [Test]
        public void TheVoteKickPayloadAndEconomyValuesAreCarried()
        {
            _match.SetVoteKick(new VoteKickSpec(true, 0.8f, 20f, 45f));
            _match.SetPayload(new PayloadSpec(2.5f, 2, new[] { 1f, 1.2f, 1.4f }, 6f, 0f, 40f, 600f));
            _match.SetEconomy(new EconomySpec(1000, 10000, 250, 2800, 1300, 450, 2650));

            var settings = _match.ToSettings();

            Assert.That(settings.VoteKick.Enabled, Is.True);
            Assert.That(settings.VoteKick.RequiredYesFraction, Is.EqualTo(0.8f));
            Assert.That(settings.VoteKick.VoteSeconds, Is.EqualTo(20f));
            Assert.That(settings.VoteKick.CooldownSeconds, Is.EqualTo(45f));
            Assert.That(settings.Payload.CartSpeed, Is.EqualTo(2.5f));
            Assert.That(settings.Payload.MinPushers, Is.EqualTo(2));
            Assert.That(settings.Payload.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f, 1.2f, 1.4f }));
            Assert.That(settings.Payload.RollbackDelaySeconds, Is.EqualTo(6f));
            Assert.That(settings.Payload.RollbackSpeed, Is.Zero);
            Assert.That(settings.Payload.CheckpointBonusSeconds, Is.EqualTo(40f));
            Assert.That(settings.Payload.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(settings.Economy.StartMoney, Is.EqualTo(1000));
            Assert.That(settings.Economy.MaxMoney, Is.EqualTo(10000));
            Assert.That(settings.Economy.KillReward, Is.EqualTo(250));
            Assert.That(settings.Economy.RoundWinReward, Is.EqualTo(2800));
            Assert.That(settings.Economy.RoundLossReward, Is.EqualTo(1300));
            Assert.That(settings.Economy.LossStreakBonus, Is.EqualTo(450));
            Assert.That(settings.Economy.MaxLossReward, Is.EqualTo(2650));
        }

        [Test]
        public void TheCarriedValuesSurviveTheServerFile()
        {
            _match.SetVoteKick(new VoteKickSpec(enabled: true, cooldownSeconds: 10f));
            _match.SetPayload(new PayloadSpec(minPushers: 3));
            _match.SetEconomy(new EconomySpec(startMoney: 2000));
            var settings = _match.ToSettings();
            var handgun = new WeaponSpec(
                "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45", "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            var file = new NetMatchFile(
                "Payload",
                GameRules.Default.WithObjective(MatchObjective.Payload),
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);

            var read = NetMatchFile.Parse(NetMatchFile.Write(file), "Payload.json").Settings;

            Assert.That(read.VoteKick.Enabled, Is.True);
            Assert.That(read.VoteKick.CooldownSeconds, Is.EqualTo(10f));
            Assert.That(read.Payload.MinPushers, Is.EqualTo(3));
            Assert.That(read.Economy.StartMoney, Is.EqualTo(2000));
        }

        [Test]
        public void ValuesAtTheEdgeOfTheirRangeAreCarried()
        {
            _match.SetVoteKick(new VoteKickSpec(requiredYesFraction: 1f, cooldownSeconds: 0f));
            _match.SetPayload(new PayloadSpec(pusherSpeedMultipliers: new[] { 1f }, rollbackDelaySeconds: 0f, checkpointBonusSeconds: 0f));
            _match.SetEconomy(new EconomySpec(startMoney: 1000, maxMoney: 1000, roundLossReward: 100, lossStreakBonus: 0, maxLossReward: 100));

            var settings = _match.ToSettings();

            Assert.That(settings.VoteKick.RequiredYesFraction, Is.EqualTo(1f));
            Assert.That(settings.Payload.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f }));
            Assert.That(settings.Economy.StartMoney, Is.EqualTo(settings.Economy.MaxMoney));
        }

        // Inspector の Min・Range では項目どうしの関係 (初めのお金と上限など) や空の並びを止められないので、ToSettings が名前付きで断る
        [TestCase("_payloadPusherSpeedMultipliers", "pusherSpeedMultipliers")]
        [TestCase("_economyStartMoney", "startMoney")]
        [TestCase("_economyMaxLossReward", "maxLossReward")]
        [TestCase("_voteKickRequiredYesFraction", "requiredYesFraction")]
        public void AnOutOfRangeValueStopsTheSettingsAtItsName(string field, string param)
        {
            var serialized = new SerializedObject(_match);
            var property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field);
            switch (field)
            {
                case "_payloadPusherSpeedMultipliers":
                    property.arraySize = 0;
                    break;
                case "_economyStartMoney":
                    property.intValue = EconomySpec.DefaultMaxMoney + 1;
                    break;
                case "_economyMaxLossReward":
                    property.intValue = EconomySpec.DefaultRoundLossReward - 1;
                    break;
                default:
                    property.floatValue = 0f;
                    break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            var e = Assert.Throws(Is.InstanceOf<ArgumentException>(), () => _match.ToSettings());

            Assert.That(((ArgumentException)e).ParamName, Is.EqualTo(param));
        }

        [Test]
        public void SettingASpecRefusesNull()
        {
            Assert.Throws<ArgumentNullException>(() => _match.SetVoteKick(null));
            Assert.Throws<ArgumentNullException>(() => _match.SetPayload(null));
            Assert.Throws<ArgumentNullException>(() => _match.SetEconomy(null));
        }

        private T Make<T>()
            where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _objects.Add(made);
            return made;
        }
    }
}
