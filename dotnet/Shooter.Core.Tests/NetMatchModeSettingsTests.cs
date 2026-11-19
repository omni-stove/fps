using System;
using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchModeSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";
        private const string _knifeId = "5f0c2d8e-71a4-4b39-9e6d-3a8b1c4f2e07";

        private static NetMatchFile File(NetMatchSettings settings, MatchObjective objective = MatchObjective.None)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                objective.ToString(),
                GameRules.Default.WithObjective(objective),
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match, string objective = "None")
        {
            return "{\"rules\": {\"objective\": \"" + objective + "\"}, \"match\": " + match
                + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void DefaultsAreTheCoreDefaultsOfEachMode()
        {
            var settings = NetMatchSettings.Default;

            Assert.That(settings.WeatherId, Is.Null);
            Assert.That(settings.TimeOfDayId, Is.Null);
            Assert.That(settings.CaptureTheFlag, Is.SameAs(CaptureTheFlagSpec.Default));
            Assert.That(settings.Hardpoint, Is.SameAs(HardpointSpec.Default));
            Assert.That(settings.SearchAndDestroy, Is.SameAs(SearchAndDestroySpec.Default));
            Assert.That(settings.FreeForAll, Is.SameAs(FreeForAllSpec.Default));
            Assert.That(settings.KillConfirmed, Is.SameAs(KillConfirmedSpec.Default));
            Assert.That(settings.GunGame, Is.SameAs(GunGameSpec.Default));
            Assert.That(settings.VoteKick, Is.SameAs(VoteKickSpec.Default));
            Assert.That(settings.Payload, Is.SameAs(PayloadSpec.Default));
            Assert.That(settings.Economy, Is.SameAs(EconomySpec.Default));
        }

        [Test]
        public void TheVoteKickPayloadAndEconomyDefaultsHaveTheseValues()
        {
            var voteKick = VoteKickSpec.Default;
            Assert.That(voteKick.Enabled, Is.False, "a server owner turns vote kicks on");
            Assert.That(voteKick.RequiredYesFraction, Is.EqualTo(0.6f));
            Assert.That(voteKick.VoteSeconds, Is.EqualTo(30f));
            Assert.That(voteKick.CooldownSeconds, Is.EqualTo(120f));

            var payload = PayloadSpec.Default;
            Assert.That(payload.CartSpeed, Is.EqualTo(1.5f));
            Assert.That(payload.MinPushers, Is.EqualTo(1));
            Assert.That(payload.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f, 1.5f, 2f }));
            Assert.That(payload.RollbackDelaySeconds, Is.EqualTo(10f));
            Assert.That(payload.RollbackSpeed, Is.EqualTo(0.5f));
            Assert.That(payload.CheckpointBonusSeconds, Is.EqualTo(60f));
            Assert.That(payload.TimeLimitSeconds, Is.EqualTo(300f));

            var economy = EconomySpec.Default;
            Assert.That(economy.StartMoney, Is.EqualTo(800));
            Assert.That(economy.MaxMoney, Is.EqualTo(16000));
            Assert.That(economy.KillReward, Is.EqualTo(300));
            Assert.That(economy.RoundWinReward, Is.EqualTo(3000));
            Assert.That(economy.RoundLossReward, Is.EqualTo(1400));
            Assert.That(economy.LossStreakBonus, Is.EqualTo(500));
            Assert.That(economy.MaxLossReward, Is.EqualTo(3400));
        }

        [Test]
        public void TheCoreDefaultsOfEachModeHaveTheseValues()
        {
            var ctf = CaptureTheFlagSpec.Default;
            Assert.That(ctf.ScoreLimit, Is.EqualTo(3));
            Assert.That(ctf.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(ctf.RespawnSeconds, Is.EqualTo(5f));
            Assert.That(ctf.PickupRadius, Is.EqualTo(1.5f));
            Assert.That(ctf.CaptureRadius, Is.EqualTo(2f));
            Assert.That(ctf.ReturnSeconds, Is.EqualTo(20f));
            Assert.That(ctf.PointsPerCapture, Is.EqualTo(1));

            var hardpoint = HardpointSpec.Default;
            Assert.That(hardpoint.PointCount, Is.EqualTo(2));
            Assert.That(hardpoint.RotateSeconds, Is.EqualTo(30f));
            Assert.That(hardpoint.PointsPerSecond, Is.EqualTo(1f));
            Assert.That(hardpoint.ScoreLimit, Is.EqualTo(100));
            Assert.That(hardpoint.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(hardpoint.RespawnSeconds, Is.EqualTo(3f));

            var snd = SearchAndDestroySpec.Default;
            Assert.That(snd.SiteCount, Is.EqualTo(2));
            Assert.That(snd.RoundSeconds, Is.EqualTo(120f));
            Assert.That(snd.PlantSeconds, Is.EqualTo(3f));
            Assert.That(snd.DefuseSeconds, Is.EqualTo(5f));
            Assert.That(snd.FuseSeconds, Is.EqualTo(30f));
            Assert.That(snd.WinsToWin, Is.EqualTo(4));
            Assert.That(snd.PreparationSeconds, Is.EqualTo(5f));
            Assert.That(snd.SwapEveryRounds, Is.EqualTo(2));
            Assert.That(snd.BombPickupRadius, Is.EqualTo(1.5f));
            Assert.That(snd.DefuseRadius, Is.EqualTo(2f));

            var ffa = FreeForAllSpec.Default;
            Assert.That(ffa.ScoreLimit, Is.EqualTo(15));
            Assert.That(ffa.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(ffa.RespawnSeconds, Is.EqualTo(3f));
            Assert.That(ffa.PointsPerKill, Is.EqualTo(1));

            var kc = KillConfirmedSpec.Default;
            Assert.That(kc.ScoreLimit, Is.EqualTo(20));
            Assert.That(kc.TimeLimitSeconds, Is.EqualTo(300f));
            Assert.That(kc.RespawnSeconds, Is.EqualTo(3f));
            Assert.That(kc.TagLifetimeSeconds, Is.EqualTo(30f));
            Assert.That(kc.PointsPerConfirm, Is.EqualTo(1));
            Assert.That(kc.TagPickupRadius, Is.EqualTo(1f));

            var gunGame = GunGameSpec.Default;
            Assert.That(gunGame.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(gunGame.RespawnSeconds, Is.EqualTo(2f));
            Assert.That(gunGame.TierWeaponIds, Is.Empty);
            Assert.That(gunGame.MeleeWeaponIds, Is.Empty);
            Assert.That(gunGame.DemotesOnMelee, Is.True);
            Assert.That(gunGame.HasTiers, Is.False);
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(
                weatherId: "heavy_rain",
                timeOfDayId: "night-1",
                captureTheFlag: new CaptureTheFlagSpec(5, 480f, 4f, 2f, 3f, 15f, 2),
                hardpoint: new HardpointSpec(HardpointSpec.MinPointCount, 45f, 2.5f, 250, 900f, 1.5f),
                searchAndDestroy: new SearchAndDestroySpec(2, 90f, 4f, 6f, 35f, 6, 10f, 3, 2.5f, 3f),
                freeForAll: new FreeForAllSpec(30, 420f, 1f, 2),
                killConfirmed: new KillConfirmedSpec(40, 360f, 2.5f, 25f, 2, 1.25f),
                gunGame: new GunGameSpec(720f, 0.5f, new[] { _handgunId, _knifeId }, new[] { _knifeId }, false),
                voteKick: new VoteKickSpec(true, 0.75f, 20f, 300f),
                payload: new PayloadSpec(2.25f, 2, new[] { 1f, 1.25f }, 5f, 0f, 45f, 480f),
                economy: new EconomySpec(1000, 9000, 250, 2500, 1200, 400, 2800));

            var read = NetMatchFile.Parse(NetMatchFile.Write(File(original, MatchObjective.GunGame)), "GunGame.json").Settings;

            Assert.That(read.VoteKick.Enabled, Is.True);
            Assert.That(read.VoteKick.RequiredYesFraction, Is.EqualTo(0.75f));
            Assert.That(read.VoteKick.VoteSeconds, Is.EqualTo(20f));
            Assert.That(read.VoteKick.CooldownSeconds, Is.EqualTo(300f));

            Assert.That(read.Payload.CartSpeed, Is.EqualTo(2.25f));
            Assert.That(read.Payload.MinPushers, Is.EqualTo(2));
            Assert.That(read.Payload.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f, 1.25f }));
            Assert.That(read.Payload.RollbackDelaySeconds, Is.EqualTo(5f));
            Assert.That(read.Payload.RollbackSpeed, Is.Zero);
            Assert.That(read.Payload.CheckpointBonusSeconds, Is.EqualTo(45f));
            Assert.That(read.Payload.TimeLimitSeconds, Is.EqualTo(480f));

            Assert.That(read.Economy.StartMoney, Is.EqualTo(1000));
            Assert.That(read.Economy.MaxMoney, Is.EqualTo(9000));
            Assert.That(read.Economy.KillReward, Is.EqualTo(250));
            Assert.That(read.Economy.RoundWinReward, Is.EqualTo(2500));
            Assert.That(read.Economy.RoundLossReward, Is.EqualTo(1200));
            Assert.That(read.Economy.LossStreakBonus, Is.EqualTo(400));
            Assert.That(read.Economy.MaxLossReward, Is.EqualTo(2800));

            Assert.That(read.WeatherId, Is.EqualTo("heavy_rain"));
            Assert.That(read.TimeOfDayId, Is.EqualTo("night-1"));

            Assert.That(read.CaptureTheFlag.ScoreLimit, Is.EqualTo(5));
            Assert.That(read.CaptureTheFlag.TimeLimitSeconds, Is.EqualTo(480f));
            Assert.That(read.CaptureTheFlag.RespawnSeconds, Is.EqualTo(4f));
            Assert.That(read.CaptureTheFlag.PickupRadius, Is.EqualTo(2f));
            Assert.That(read.CaptureTheFlag.CaptureRadius, Is.EqualTo(3f));
            Assert.That(read.CaptureTheFlag.ReturnSeconds, Is.EqualTo(15f));
            Assert.That(read.CaptureTheFlag.PointsPerCapture, Is.EqualTo(2));

            Assert.That(read.Hardpoint.PointCount, Is.EqualTo(HardpointSpec.MinPointCount));
            Assert.That(read.Hardpoint.RotateSeconds, Is.EqualTo(45f));
            Assert.That(read.Hardpoint.PointsPerSecond, Is.EqualTo(2.5f));
            Assert.That(read.Hardpoint.ScoreLimit, Is.EqualTo(250));
            Assert.That(read.Hardpoint.TimeLimitSeconds, Is.EqualTo(900f));
            Assert.That(read.Hardpoint.RespawnSeconds, Is.EqualTo(1.5f));

            Assert.That(read.SearchAndDestroy.SiteCount, Is.EqualTo(SearchAndDestroySpec.DefaultSiteCount));
            Assert.That(read.SearchAndDestroy.RoundSeconds, Is.EqualTo(90f));
            Assert.That(read.SearchAndDestroy.PlantSeconds, Is.EqualTo(4f));
            Assert.That(read.SearchAndDestroy.DefuseSeconds, Is.EqualTo(6f));
            Assert.That(read.SearchAndDestroy.FuseSeconds, Is.EqualTo(35f));
            Assert.That(read.SearchAndDestroy.WinsToWin, Is.EqualTo(6));
            Assert.That(read.SearchAndDestroy.PreparationSeconds, Is.EqualTo(10f));
            Assert.That(read.SearchAndDestroy.SwapEveryRounds, Is.EqualTo(3));
            Assert.That(read.SearchAndDestroy.BombPickupRadius, Is.EqualTo(2.5f));
            Assert.That(read.SearchAndDestroy.DefuseRadius, Is.EqualTo(3f));

            Assert.That(read.FreeForAll.ScoreLimit, Is.EqualTo(30));
            Assert.That(read.FreeForAll.TimeLimitSeconds, Is.EqualTo(420f));
            Assert.That(read.FreeForAll.RespawnSeconds, Is.EqualTo(1f));
            Assert.That(read.FreeForAll.PointsPerKill, Is.EqualTo(2));

            Assert.That(read.KillConfirmed.ScoreLimit, Is.EqualTo(40));
            Assert.That(read.KillConfirmed.TimeLimitSeconds, Is.EqualTo(360f));
            Assert.That(read.KillConfirmed.RespawnSeconds, Is.EqualTo(2.5f));
            Assert.That(read.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(25f));
            Assert.That(read.KillConfirmed.PointsPerConfirm, Is.EqualTo(2));
            Assert.That(read.KillConfirmed.TagPickupRadius, Is.EqualTo(1.25f));

            Assert.That(read.GunGame.TimeLimitSeconds, Is.EqualTo(720f));
            Assert.That(read.GunGame.RespawnSeconds, Is.EqualTo(0.5f));
            Assert.That(read.GunGame.TierWeaponIds, Is.EqualTo(new[] { _handgunId, _knifeId }));
            Assert.That(read.GunGame.MeleeWeaponIds, Is.EqualTo(new[] { _knifeId }));
            Assert.That(read.GunGame.DemotesOnMelee, Is.False);
        }

        [Test]
        public void WritingWhatWasReadGivesTheSameText()
        {
            var settings = new NetMatchSettings(
                weatherId: "clear",
                gunGame: new GunGameSpec(tierWeaponIds: new[] { _handgunId }),
                voteKick: new VoteKickSpec(true, 0.5f, 15f, 0f),
                payload: new PayloadSpec(pusherSpeedMultipliers: new[] { 0.8f, 1.3f, 1.7f, 1.9f }),
                economy: new EconomySpec(startMoney: 0, lossStreakBonus: 0));
            var text = NetMatchFile.Write(File(settings));

            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "TeamDeathmatch.json")), Is.EqualTo(text));
        }

        [Test]
        public void TheDefaultsAreWrittenAsTheseItems()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Contain("\"weatherId\": \"\","));
            Assert.That(text, Does.Contain("\"timeOfDayId\": \"\","));
            Assert.That(text, Does.Contain(
                "\"captureTheFlag\": {\n      \"scoreLimit\": 3,\n      \"timeLimitSeconds\": 600,\n      \"respawnSeconds\": 5,\n"
                + "      \"pickupRadius\": 1.5,\n      \"captureRadius\": 2,\n      \"returnSeconds\": 20,\n      \"pointsPerCapture\": 1\n    },"));
            Assert.That(text, Does.Contain(
                "\"hardpoint\": {\n      \"rotateSeconds\": 30,\n      \"pointsPerSecond\": 1,\n      \"scoreLimit\": 100,\n"
                + "      \"timeLimitSeconds\": 300,\n      \"respawnSeconds\": 3\n    },"));
            Assert.That(text, Does.Contain(
                "\"searchAndDestroy\": {\n      \"roundSeconds\": 120,\n      \"plantSeconds\": 3,\n      \"defuseSeconds\": 5,\n"
                + "      \"fuseSeconds\": 30,\n      \"winsToWin\": 4,\n      \"preparationSeconds\": 5,\n      \"swapEveryRounds\": 2,\n"
                + "      \"bombPickupRadius\": 1.5,\n      \"defuseRadius\": 2\n    },"));
            Assert.That(text, Does.Contain(
                "\"freeForAll\": {\n      \"scoreLimit\": 15,\n      \"timeLimitSeconds\": 300,\n      \"respawnSeconds\": 3,\n"
                + "      \"pointsPerKill\": 1\n    },"));
            Assert.That(text, Does.Contain(
                "\"killConfirmed\": {\n      \"scoreLimit\": 20,\n      \"timeLimitSeconds\": 300,\n      \"respawnSeconds\": 3,\n"
                + "      \"tagLifetimeSeconds\": 30,\n      \"pointsPerConfirm\": 1,\n      \"tagPickupRadius\": 1\n    },"));
            Assert.That(text, Does.Contain(
                "\"gunGame\": {\n      \"timeLimitSeconds\": 600,\n      \"respawnSeconds\": 2,\n      \"tierWeaponIds\": [],\n"
                + "      \"meleeWeaponIds\": [],\n      \"demotesOnMelee\": true\n    },"));
            Assert.That(text, Does.Contain(
                "\"voteKick\": {\n      \"enabled\": false,\n      \"requiredYesFraction\": 0.6,\n      \"voteSeconds\": 30,\n"
                + "      \"cooldownSeconds\": 120\n    },"));
            Assert.That(text, Does.Contain(
                "\"payload\": {\n      \"cartSpeed\": 1.5,\n      \"minPushers\": 1,\n      \"pusherSpeedMultipliers\": [1, 1.5, 2],\n"
                + "      \"rollbackDelaySeconds\": 10,\n      \"rollbackSpeed\": 0.5,\n      \"checkpointBonusSeconds\": 60,\n"
                + "      \"timeLimitSeconds\": 300\n    },"));
            Assert.That(text, Does.Contain(
                "\"economy\": {\n      \"startMoney\": 800,\n      \"maxMoney\": 16000,\n      \"killReward\": 300,\n"
                + "      \"roundWinReward\": 3000,\n      \"roundLossReward\": 1400,\n      \"lossStreakBonus\": 500,\n"
                + "      \"maxLossReward\": 3400\n    }\n  },"));
        }

        [Test]
        public void MissingItemsReadAsTheDefaults()
        {
            var settings = NetMatchFile.Parse(Text("{}"), "TeamDeathmatch.json").Settings;

            Assert.That(settings.WeatherId, Is.Null);
            Assert.That(settings.TimeOfDayId, Is.Null);
            Assert.That(settings.CaptureTheFlag, Is.SameAs(CaptureTheFlagSpec.Default));
            Assert.That(settings.Hardpoint, Is.SameAs(HardpointSpec.Default));
            Assert.That(settings.SearchAndDestroy, Is.SameAs(SearchAndDestroySpec.Default));
            Assert.That(settings.FreeForAll, Is.SameAs(FreeForAllSpec.Default));
            Assert.That(settings.KillConfirmed, Is.SameAs(KillConfirmedSpec.Default));
            Assert.That(settings.GunGame, Is.SameAs(GunGameSpec.Default));
            Assert.That(settings.VoteKick, Is.SameAs(VoteKickSpec.Default));
            Assert.That(settings.Payload, Is.SameAs(PayloadSpec.Default));
            Assert.That(settings.Economy, Is.SameAs(EconomySpec.Default));
        }

        [Test]
        public void MissingItemsInTheVoteKickPayloadAndEconomyReadAsTheirDefaults()
        {
            var settings = NetMatchFile.Parse(
                Text("{\"voteKick\": {\"enabled\": true}, \"payload\": {\"minPushers\": 3}, \"economy\": {}}"),
                "TeamDeathmatch.json").Settings;

            Assert.That(settings.VoteKick.Enabled, Is.True);
            Assert.That(settings.VoteKick.RequiredYesFraction, Is.EqualTo(VoteKickSpec.DefaultRequiredYesFraction));
            Assert.That(settings.VoteKick.VoteSeconds, Is.EqualTo(VoteKickSpec.DefaultVoteSeconds));
            Assert.That(settings.VoteKick.CooldownSeconds, Is.EqualTo(VoteKickSpec.DefaultCooldownSeconds));
            Assert.That(settings.Payload.MinPushers, Is.EqualTo(3));
            Assert.That(settings.Payload.CartSpeed, Is.EqualTo(PayloadSpec.DefaultCartSpeed));
            Assert.That(settings.Payload.PusherSpeedMultipliers, Is.EqualTo(PayloadSpec.Default.PusherSpeedMultipliers));
            Assert.That(settings.Payload.TimeLimitSeconds, Is.EqualTo(PayloadSpec.DefaultTimeLimitSeconds));
            Assert.That(settings.Economy.StartMoney, Is.EqualTo(EconomySpec.DefaultStartMoney));
            Assert.That(settings.Economy.MaxLossReward, Is.EqualTo(EconomySpec.DefaultMaxLossReward));
        }

        [Test]
        public void HandWrittenVoteKickPayloadAndEconomySectionsReadAsWritten()
        {
            var file = NetMatchFile.Parse(
                Text("{\"voteKick\": {\"enabled\": true, \"requiredYesFraction\": 1, \"voteSeconds\": 12.5, \"cooldownSeconds\": 0},"
                    + " \"payload\": {\"cartSpeed\": 3, \"minPushers\": 2, \"pusherSpeedMultipliers\": [1, 2],"
                    + " \"rollbackDelaySeconds\": 0, \"rollbackSpeed\": 1, \"checkpointBonusSeconds\": 0, \"timeLimitSeconds\": 90},"
                    + " \"economy\": {\"startMoney\": 0, \"maxMoney\": 5000, \"killReward\": 0, \"roundWinReward\": 100,"
                    + " \"roundLossReward\": 50, \"lossStreakBonus\": 25, \"maxLossReward\": 50}}",
                    "Payload"),
                "Payload.json");

            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Payload));
            var s = file.Settings;
            Assert.That(s.VoteKick.Enabled, Is.True);
            Assert.That(s.VoteKick.RequiredYesFraction, Is.EqualTo(1f));
            Assert.That(s.VoteKick.VoteSeconds, Is.EqualTo(12.5f));
            Assert.That(s.VoteKick.CooldownSeconds, Is.Zero);
            Assert.That(s.Payload.CartSpeed, Is.EqualTo(3f));
            Assert.That(s.Payload.MinPushers, Is.EqualTo(2));
            Assert.That(s.Payload.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f, 2f }));
            Assert.That(s.Payload.RollbackDelaySeconds, Is.Zero);
            Assert.That(s.Payload.RollbackSpeed, Is.EqualTo(1f));
            Assert.That(s.Payload.CheckpointBonusSeconds, Is.Zero);
            Assert.That(s.Payload.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(s.Economy.StartMoney, Is.Zero);
            Assert.That(s.Economy.MaxMoney, Is.EqualTo(5000));
            Assert.That(s.Economy.KillReward, Is.Zero);
            Assert.That(s.Economy.RoundWinReward, Is.EqualTo(100));
            Assert.That(s.Economy.RoundLossReward, Is.EqualTo(50));
            Assert.That(s.Economy.LossStreakBonus, Is.EqualTo(25));
            Assert.That(s.Economy.MaxLossReward, Is.EqualTo(50));
        }

        [Test]
        public void MissingItemsInAModeReadAsThatModesDefaults()
        {
            var settings = NetMatchFile.Parse(
                Text("{\"captureTheFlag\": {\"scoreLimit\": 7}, \"killConfirmed\": {}, \"gunGame\": {\"respawnSeconds\": 0}}"),
                "TeamDeathmatch.json").Settings;

            Assert.That(settings.CaptureTheFlag.ScoreLimit, Is.EqualTo(7));
            Assert.That(settings.CaptureTheFlag.ReturnSeconds, Is.EqualTo(CaptureTheFlagSpec.DefaultReturnSeconds));
            Assert.That(settings.KillConfirmed.ScoreLimit, Is.EqualTo(KillConfirmedSpec.DefaultScoreLimit));
            Assert.That(settings.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(KillConfirmedSpec.DefaultTagLifetimeSeconds));
            Assert.That(settings.GunGame.RespawnSeconds, Is.Zero);
            Assert.That(settings.GunGame.TimeLimitSeconds, Is.EqualTo(GunGameSpec.DefaultTimeLimitSeconds));
            Assert.That(settings.GunGame.TierWeaponIds, Is.Empty);
            Assert.That(settings.GunGame.DemotesOnMelee, Is.True);
        }

        [Test]
        public void AnEmptyWeatherOrTimeOfDayMeansTheMapsOwn()
        {
            var settings = new NetMatchSettings(weatherId: string.Empty, timeOfDayId: string.Empty);

            Assert.That(settings.WeatherId, Is.Null);
            Assert.That(settings.TimeOfDayId, Is.Null);
        }

        [Test]
        public void WithPrivacyKeepsTheModeSettings()
        {
            var original = new NetMatchSettings(
                weatherId: "snow",
                timeOfDayId: "noon",
                captureTheFlag: new CaptureTheFlagSpec(scoreLimit: 4),
                hardpoint: new HardpointSpec(HardpointSpec.MinPointCount, 20f, 1f, 50, 200f, 2f),
                searchAndDestroy: new SearchAndDestroySpec(winsToWin: 2),
                freeForAll: new FreeForAllSpec(scoreLimit: 9),
                killConfirmed: new KillConfirmedSpec(10, 200f, 1f, 10f),
                gunGame: new GunGameSpec(tierWeaponIds: new[] { _handgunId }),
                voteKick: new VoteKickSpec(enabled: true),
                payload: new PayloadSpec(cartSpeed: 2f),
                economy: new EconomySpec(startMoney: 1200));

            var settings = original.WithPrivacy(true, "abc");

            Assert.That(settings.VoteKick, Is.SameAs(original.VoteKick));
            Assert.That(settings.Payload, Is.SameAs(original.Payload));
            Assert.That(settings.Economy, Is.SameAs(original.Economy));

            Assert.That(settings.WeatherId, Is.EqualTo("snow"));
            Assert.That(settings.TimeOfDayId, Is.EqualTo("noon"));
            Assert.That(settings.CaptureTheFlag, Is.SameAs(original.CaptureTheFlag));
            Assert.That(settings.Hardpoint, Is.SameAs(original.Hardpoint));
            Assert.That(settings.SearchAndDestroy, Is.SameAs(original.SearchAndDestroy));
            Assert.That(settings.FreeForAll, Is.SameAs(original.FreeForAll));
            Assert.That(settings.KillConfirmed, Is.SameAs(original.KillConfirmed));
            Assert.That(settings.GunGame, Is.SameAs(original.GunGame));
        }

        [TestCase("{\"weatherId\": \"rain storm\"}", "match.weatherId")]
        [TestCase("{\"timeOfDayId\": \"dusk.json\"}", "match.timeOfDayId")]
        [TestCase("{\"weatherId\": 3}", "match.weatherId")]
        [TestCase("{\"captureTheFlag\": {\"scoreLimit\": 0}}", "match.captureTheFlag.scoreLimit")]
        [TestCase("{\"captureTheFlag\": {\"timeLimitSeconds\": 0}}", "match.captureTheFlag.timeLimitSeconds")]
        [TestCase("{\"captureTheFlag\": {\"respawnSeconds\": -1}}", "match.captureTheFlag.respawnSeconds")]
        [TestCase("{\"captureTheFlag\": {\"pickupRadius\": 0}}", "match.captureTheFlag.pickupRadius")]
        [TestCase("{\"captureTheFlag\": {\"captureRadius\": -2}}", "match.captureTheFlag.captureRadius")]
        [TestCase("{\"captureTheFlag\": {\"returnSeconds\": 0}}", "match.captureTheFlag.returnSeconds")]
        [TestCase("{\"captureTheFlag\": {\"pointsPerCapture\": 0}}", "match.captureTheFlag.pointsPerCapture")]
        [TestCase("{\"hardpoint\": {\"rotateSeconds\": 0}}", "match.hardpoint.rotateSeconds")]
        [TestCase("{\"hardpoint\": {\"pointsPerSecond\": -1}}", "match.hardpoint.pointsPerSecond")]
        [TestCase("{\"hardpoint\": {\"scoreLimit\": 0}}", "match.hardpoint.scoreLimit")]
        [TestCase("{\"hardpoint\": {\"timeLimitSeconds\": 0}}", "match.hardpoint.timeLimitSeconds")]
        [TestCase("{\"hardpoint\": {\"respawnSeconds\": -0.5}}", "match.hardpoint.respawnSeconds")]
        [TestCase("{\"searchAndDestroy\": {\"roundSeconds\": 0}}", "match.searchAndDestroy.roundSeconds")]
        [TestCase("{\"searchAndDestroy\": {\"plantSeconds\": 0}}", "match.searchAndDestroy.plantSeconds")]
        [TestCase("{\"searchAndDestroy\": {\"defuseSeconds\": 0}}", "match.searchAndDestroy.defuseSeconds")]
        [TestCase("{\"searchAndDestroy\": {\"fuseSeconds\": 0}}", "match.searchAndDestroy.fuseSeconds")]
        [TestCase("{\"searchAndDestroy\": {\"winsToWin\": 0}}", "match.searchAndDestroy.winsToWin")]
        [TestCase("{\"searchAndDestroy\": {\"preparationSeconds\": -1}}", "match.searchAndDestroy.preparationSeconds")]
        [TestCase("{\"searchAndDestroy\": {\"swapEveryRounds\": -1}}", "match.searchAndDestroy.swapEveryRounds")]
        [TestCase("{\"searchAndDestroy\": {\"bombPickupRadius\": 0}}", "match.searchAndDestroy.bombPickupRadius")]
        [TestCase("{\"searchAndDestroy\": {\"defuseRadius\": 0}}", "match.searchAndDestroy.defuseRadius")]
        [TestCase("{\"freeForAll\": {\"scoreLimit\": 0}}", "match.freeForAll.scoreLimit")]
        [TestCase("{\"freeForAll\": {\"timeLimitSeconds\": 0}}", "match.freeForAll.timeLimitSeconds")]
        [TestCase("{\"freeForAll\": {\"respawnSeconds\": -1}}", "match.freeForAll.respawnSeconds")]
        [TestCase("{\"freeForAll\": {\"pointsPerKill\": 0}}", "match.freeForAll.pointsPerKill")]
        [TestCase("{\"killConfirmed\": {\"scoreLimit\": 0}}", "match.killConfirmed.scoreLimit")]
        [TestCase("{\"killConfirmed\": {\"timeLimitSeconds\": 0}}", "match.killConfirmed.timeLimitSeconds")]
        [TestCase("{\"killConfirmed\": {\"respawnSeconds\": -1}}", "match.killConfirmed.respawnSeconds")]
        [TestCase("{\"killConfirmed\": {\"tagLifetimeSeconds\": 0}}", "match.killConfirmed.tagLifetimeSeconds")]
        [TestCase("{\"killConfirmed\": {\"pointsPerConfirm\": 0}}", "match.killConfirmed.pointsPerConfirm")]
        [TestCase("{\"killConfirmed\": {\"tagPickupRadius\": 0}}", "match.killConfirmed.tagPickupRadius")]
        [TestCase("{\"gunGame\": {\"timeLimitSeconds\": 0}}", "match.gunGame.timeLimitSeconds")]
        [TestCase("{\"gunGame\": {\"respawnSeconds\": -1}}", "match.gunGame.respawnSeconds")]
        [TestCase("{\"gunGame\": {\"tierWeaponIds\": [\" \"]}}", "match.gunGame.tierWeaponIds")]
        [TestCase("{\"gunGame\": {\"tierWeaponIds\": [3]}}", "match.gunGame.tierWeaponIds[0]")]
        [TestCase("{\"gunGame\": {\"meleeWeaponIds\": [\"\"]}}", "match.gunGame.meleeWeaponIds")]
        [TestCase("{\"gunGame\": {\"demotesOnMelee\": 1}}", "match.gunGame.demotesOnMelee")]
        [TestCase("{\"voteKick\": {\"enabled\": \"yes\"}}", "match.voteKick.enabled")]
        [TestCase("{\"voteKick\": {\"requiredYesFraction\": 0}}", "match.voteKick.requiredYesFraction")]
        [TestCase("{\"voteKick\": {\"requiredYesFraction\": 1.01}}", "match.voteKick.requiredYesFraction")]
        [TestCase("{\"voteKick\": {\"voteSeconds\": 0}}", "match.voteKick.voteSeconds")]
        [TestCase("{\"voteKick\": {\"cooldownSeconds\": -1}}", "match.voteKick.cooldownSeconds")]
        [TestCase("{\"payload\": {\"cartSpeed\": 0}}", "match.payload.cartSpeed")]
        [TestCase("{\"payload\": {\"minPushers\": 0}}", "match.payload.minPushers")]
        [TestCase("{\"payload\": {\"minPushers\": 1.5}}", "match.payload.minPushers")]
        [TestCase("{\"payload\": {\"pusherSpeedMultipliers\": []}}", "match.payload.pusherSpeedMultipliers")]
        [TestCase("{\"payload\": {\"pusherSpeedMultipliers\": [1, 0]}}", "match.payload.pusherSpeedMultipliers")]
        [TestCase("{\"payload\": {\"pusherSpeedMultipliers\": [1, \"fast\"]}}", "match.payload.pusherSpeedMultipliers[1]")]
        [TestCase("{\"payload\": {\"rollbackDelaySeconds\": -1}}", "match.payload.rollbackDelaySeconds")]
        [TestCase("{\"payload\": {\"rollbackSpeed\": -0.5}}", "match.payload.rollbackSpeed")]
        [TestCase("{\"payload\": {\"checkpointBonusSeconds\": -1}}", "match.payload.checkpointBonusSeconds")]
        [TestCase("{\"payload\": {\"timeLimitSeconds\": 0}}", "match.payload.timeLimitSeconds")]
        [TestCase("{\"economy\": {\"startMoney\": -1}}", "match.economy.startMoney")]
        [TestCase("{\"economy\": {\"startMoney\": 20000}}", "match.economy.startMoney")]
        [TestCase("{\"economy\": {\"maxMoney\": 0}}", "match.economy.maxMoney")]
        [TestCase("{\"economy\": {\"killReward\": -1}}", "match.economy.killReward")]
        [TestCase("{\"economy\": {\"roundWinReward\": -1}}", "match.economy.roundWinReward")]
        [TestCase("{\"economy\": {\"roundLossReward\": -1}}", "match.economy.roundLossReward")]
        [TestCase("{\"economy\": {\"lossStreakBonus\": -1}}", "match.economy.lossStreakBonus")]
        [TestCase("{\"economy\": {\"maxLossReward\": 1000}}", "match.economy.maxLossReward")]
        public void RejectsAValueOutOfRangeAtItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "TeamDeathmatch.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("TeamDeathmatch.json").And.Contain(item));
        }

        [TestCase("{\"hardpoint\": {\"pointCount\": 3}}", "match.hardpoint.pointCount")]
        [TestCase("{\"searchAndDestroy\": {\"siteCount\": 3}}", "match.searchAndDestroy.siteCount")]
        [TestCase("{\"freeForAll\": {\"pointsPerAssist\": 1}}", "match.freeForAll.pointsPerAssist")]
        [TestCase("{\"payload\": {\"route\": []}}", "match.payload.route")]
        [TestCase("{\"voteKick\": {\"banned\": true}}", "match.voteKick.banned")]
        [TestCase("{\"economy\": {\"prices\": {}}}", "match.economy.prices")]
        public void RejectsTheCountsTheMapDecidesAndUnknownItems(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "TeamDeathmatch.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("unknown item '" + item + "'"));
        }

        [TestCase("{\"captureTheFlag\": 3}", "match.captureTheFlag")]
        [TestCase("{\"gunGame\": null}", "match.gunGame")]
        [TestCase("{\"gunGame\": {\"tierWeaponIds\": \"" + _handgunId + "\"}}", "match.gunGame.tierWeaponIds")]
        [TestCase("{\"voteKick\": true}", "match.voteKick")]
        [TestCase("{\"payload\": []}", "match.payload")]
        [TestCase("{\"economy\": 800}", "match.economy")]
        [TestCase("{\"payload\": {\"pusherSpeedMultipliers\": 2}}", "match.payload.pusherSpeedMultipliers")]
        public void RejectsAModeThatIsNotAnObject(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "TeamDeathmatch.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void AGunGameMatchWithoutTiersIsRejectedAtTheTiers()
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text("{}", "GunGame"), "GunGame.json"));

            Assert.That(e.Item, Is.EqualTo("match.gunGame.tierWeaponIds"));
            Assert.That(e.Message, Does.Contain("GunGame.json").And.Contain("at least one weapon id"));
        }

        [Test]
        public void AGunGameMatchWithoutTheMatchObjectIsRejectedAtTheTiers()
        {
            var text = "{\"rules\": {\"objective\": \"GunGame\"}, \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "GunGame.json"));

            Assert.That(e.Item, Is.EqualTo("match.gunGame.tierWeaponIds"));
        }

        [Test]
        public void AGunGameMatchWithTiersReads()
        {
            var file = NetMatchFile.Parse(Text("{\"gunGame\": {\"tierWeaponIds\": [\" " + _handgunId + " \"]}}", "GunGame"), "GunGame.json");

            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.GunGame));
            Assert.That(file.Settings.GunGame.TierWeaponIds, Is.EqualTo(new[] { _handgunId }));
        }

        [Test]
        public void AGunGameFileWithoutTiersCannotBeMade()
        {
            var e = Assert.Throws<ArgumentException>(() => File(NetMatchSettings.Default, MatchObjective.GunGame));

            Assert.That(e.ParamName, Is.EqualTo("settings"));
        }

        [TestCase(MatchObjective.CaptureTheFlag)]
        [TestCase(MatchObjective.Hardpoint)]
        [TestCase(MatchObjective.SearchAndDestroy)]
        [TestCase(MatchObjective.FreeForAll)]
        [TestCase(MatchObjective.KillConfirmed)]
        [TestCase(MatchObjective.Payload)]
        public void TheOtherNewObjectivesReadWithTheDefaults(MatchObjective objective)
        {
            var file = NetMatchFile.Parse(Text("{}", objective.ToString()), objective + ".json");

            Assert.That(file.Rules.Objective, Is.EqualTo(objective));
            Assert.That(file.Settings.ScoreLimit, Is.EqualTo(NetMatchSettings.DefaultScoreLimit));
        }

        [Test]
        public void RejectsAnIdLongerThanTheLimit()
        {
            var id = new string('a', NetMatchSettings.MaxEnvironmentIdLength + 1);

            var e = Assert.Throws<ArgumentException>(() => new NetMatchSettings(weatherId: id));

            Assert.That(e.ParamName, Is.EqualTo("weatherId"));
            Assert.That(new NetMatchSettings(timeOfDayId: id.Substring(1)).TimeOfDayId, Is.EqualTo(id.Substring(1)));
        }

        [Test]
        public void GunGameSpecMakesTheLadderOfItsTiers()
        {
            var ladder = new GunGameSpec(tierWeaponIds: new[] { _handgunId, _knifeId }, meleeWeaponIds: new[] { _knifeId }).ToLadder();

            Assert.That(ladder.TierCount, Is.EqualTo(2));
            Assert.That(ladder.Tiers[0].WeaponId, Is.EqualTo(_handgunId));
            Assert.That(ladder.Tiers[1].WeaponId, Is.EqualTo(_knifeId));
            Assert.That(ladder.IsMelee(_knifeId), Is.True);
            Assert.That(ladder.DemotesOnMelee, Is.True);
            Assert.Throws<InvalidOperationException>(() => GunGameSpec.Default.ToLadder());
        }

        [Test]
        public void FreeForAllSpecMakesASessionWithItsLimits()
        {
            var session = new FreeForAllSpec(scoreLimit: 7, timeLimitSeconds: 120f).CreateSession();

            Assert.That(session.TimeLimitSeconds, Is.EqualTo(120f));
        }

        [Test]
        public void EveryCommittedRulesFileListsTheModeItems()
        {
            var items = new[]
            {
                NetMatchFile.WeatherIdItem, NetMatchFile.TimeOfDayIdItem, NetMatchFile.CaptureTheFlagItem, NetMatchFile.HardpointItem,
                NetMatchFile.SearchAndDestroyItem, NetMatchFile.FreeForAllItem, NetMatchFile.KillConfirmedItem, NetMatchFile.GunGameItem,
                NetMatchFile.VoteKickItem, NetMatchFile.PayloadItem, NetMatchFile.EconomyItem,
            };
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var text = System.IO.File.ReadAllText(path);
                foreach (var item in items)
                {
                    Assert.That(text, Does.Contain("\"" + item + "\": "), path + " " + item);
                }
            }
        }

        // 節を足す前の例のファイルで動いていた試合が、節を足したあとも同じ値で動くことを固定する
        [Test]
        public void EveryCommittedRulesFileReadsTheVoteKickPayloadAndEconomyAtTheirDefaults()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var settings = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path).Settings;

                AssertSameValues(settings.VoteKick, VoteKickSpec.Default, path);
                AssertSameValues(settings.Payload, PayloadSpec.Default, path);
                AssertSameValues(settings.Economy, EconomySpec.Default, path);
            }
        }

        [Test]
        public void ACommittedRulesFileWithoutTheNewSectionsReadsTheSameValues()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "TeamDeathmatch.json");
            var text = System.IO.File.ReadAllText(path);
            var start = text.IndexOf(",\n    \"voteKick\": {", StringComparison.Ordinal);
            var end = text.IndexOf("\n  },\n  \"motor\"", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "the committed file lists the vote kick section after gun game");
            Assert.That(end, Is.GreaterThan(start), "the economy section closes the match object");
            var withoutSections = text.Remove(start, end - start);
            Assert.That(withoutSections, Does.Not.Contain("\"voteKick\"").And.Not.Contain("\"payload\"").And.Not.Contain("\"economy\""));

            var before = NetMatchFile.Parse(withoutSections, path).Settings;
            var after = NetMatchFile.Parse(text, path).Settings;

            Assert.That(before.VoteKick, Is.SameAs(VoteKickSpec.Default));
            Assert.That(before.Payload, Is.SameAs(PayloadSpec.Default));
            Assert.That(before.Economy, Is.SameAs(EconomySpec.Default));
            AssertSameValues(after.VoteKick, before.VoteKick, path);
            AssertSameValues(after.Payload, before.Payload, path);
            AssertSameValues(after.Economy, before.Economy, path);
            Assert.That(after.ScoreLimit, Is.EqualTo(before.ScoreLimit));
            Assert.That(after.GunGame.DemotesOnMelee, Is.EqualTo(before.GunGame.DemotesOnMelee));
        }

        [Test]
        public void ACommittedRulesFileEditedToTurnOnVoteKicksReadsTheNewValues()
        {
            var text = System.IO.File.ReadAllText(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "SearchAndDestroy.json"));
            var edits = new[]
            {
                ("\"enabled\": false,", "\"enabled\": true,"),
                ("\"requiredYesFraction\": 0.6,", "\"requiredYesFraction\": 0.5,"),
                ("\"startMoney\": 800,", "\"startMoney\": 1500,"),
                ("\"pusherSpeedMultipliers\": [1, 1.5, 2],", "\"pusherSpeedMultipliers\": [1],"),
            };
            foreach (var (from, to) in edits)
            {
                Assert.That(text, Does.Contain(from));
                text = text.Replace(from, to);
            }

            var settings = NetMatchFile.Parse(text, "SearchAndDestroy.json").Settings;

            Assert.That(settings.VoteKick.Enabled, Is.True);
            Assert.That(settings.VoteKick.RequiredYesFraction, Is.EqualTo(0.5f));
            Assert.That(settings.Economy.StartMoney, Is.EqualTo(1500));
            Assert.That(settings.Payload.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f }));
        }

        [TestCase(0, 0f)]
        [TestCase(1, 1.5f)]
        [TestCase(2, 2.25f)]
        [TestCase(3, 3f)]
        [TestCase(8, 3f)]
        public void PayloadSpecSpeedsUpWithMorePushersUpToTheLastMultiplier(int pushers, float speed)
        {
            Assert.That(PayloadSpec.Default.SpeedFor(pushers), Is.EqualTo(speed));
        }

        [Test]
        public void PayloadSpecDoesNotMoveBelowTheMinimumPushers()
        {
            var spec = new PayloadSpec(cartSpeed: 2f, minPushers: 2, pusherSpeedMultipliers: new[] { 1f, 1.5f });

            Assert.That(spec.SpeedFor(1), Is.Zero);
            Assert.That(spec.SpeedFor(2), Is.EqualTo(2f));
            Assert.That(spec.SpeedFor(3), Is.EqualTo(3f));
            Assert.That(spec.SpeedFor(10), Is.EqualTo(3f));
        }

        [Test]
        public void PayloadSpecKeepsItsOwnCopyOfTheMultipliers()
        {
            var multipliers = new[] { 1f, 2f };
            var spec = new PayloadSpec(pusherSpeedMultipliers: multipliers);

            multipliers[1] = 9f;

            Assert.That(spec.PusherSpeedMultipliers, Is.EqualTo(new[] { 1f, 2f }));
        }

        [TestCase(1, 1400)]
        [TestCase(2, 1900)]
        [TestCase(3, 2400)]
        [TestCase(5, 3400)]
        [TestCase(6, 3400)]
        [TestCase(int.MaxValue, 3400)]
        public void EconomySpecRaisesTheLossRewardWithTheStreakUpToItsCap(int lossStreak, int reward)
        {
            Assert.That(EconomySpec.Default.LossReward(lossStreak), Is.EqualTo(reward));
        }

        [Test]
        public void EconomySpecWithoutAStreakBonusPaysTheSameLossReward()
        {
            var spec = new EconomySpec(lossStreakBonus: 0);

            Assert.That(spec.LossReward(1), Is.EqualTo(EconomySpec.DefaultRoundLossReward));
            Assert.That(spec.LossReward(10), Is.EqualTo(EconomySpec.DefaultRoundLossReward));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.LossReward(0));
        }

        [TestCase(float.NaN)]
        [TestCase(-0.1f)]
        [TestCase(0f)]
        [TestCase(1.5f)]
        public void VoteKickSpecRefusesAFractionOutsideZeroToOne(float fraction)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickSpec(requiredYesFraction: fraction));

            Assert.That(e.ParamName, Is.EqualTo("requiredYesFraction"));
        }

        [TestCase(float.PositiveInfinity, "voteSeconds")]
        [TestCase(float.NaN, "voteSeconds")]
        public void VoteKickSpecRefusesANonFiniteVoteTime(float seconds, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new VoteKickSpec(voteSeconds: seconds));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void PayloadSpecRefusesANonFiniteMultiplier()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PayloadSpec(pusherSpeedMultipliers: new[] { 1f, float.PositiveInfinity }));

            Assert.That(e.ParamName, Is.EqualTo("pusherSpeedMultipliers"));
            Assert.That(e.Message, Does.Contain("pusherSpeedMultipliers[1]"));
        }

        private static void AssertSameValues(VoteKickSpec actual, VoteKickSpec expected, string path)
        {
            Assert.That(actual.Enabled, Is.EqualTo(expected.Enabled), path);
            Assert.That(actual.RequiredYesFraction, Is.EqualTo(expected.RequiredYesFraction), path);
            Assert.That(actual.VoteSeconds, Is.EqualTo(expected.VoteSeconds), path);
            Assert.That(actual.CooldownSeconds, Is.EqualTo(expected.CooldownSeconds), path);
        }

        private static void AssertSameValues(PayloadSpec actual, PayloadSpec expected, string path)
        {
            Assert.That(actual.CartSpeed, Is.EqualTo(expected.CartSpeed), path);
            Assert.That(actual.MinPushers, Is.EqualTo(expected.MinPushers), path);
            Assert.That(actual.PusherSpeedMultipliers, Is.EqualTo(expected.PusherSpeedMultipliers), path);
            Assert.That(actual.RollbackDelaySeconds, Is.EqualTo(expected.RollbackDelaySeconds), path);
            Assert.That(actual.RollbackSpeed, Is.EqualTo(expected.RollbackSpeed), path);
            Assert.That(actual.CheckpointBonusSeconds, Is.EqualTo(expected.CheckpointBonusSeconds), path);
            Assert.That(actual.TimeLimitSeconds, Is.EqualTo(expected.TimeLimitSeconds), path);
        }

        private static void AssertSameValues(EconomySpec actual, EconomySpec expected, string path)
        {
            Assert.That(actual.StartMoney, Is.EqualTo(expected.StartMoney), path);
            Assert.That(actual.MaxMoney, Is.EqualTo(expected.MaxMoney), path);
            Assert.That(actual.KillReward, Is.EqualTo(expected.KillReward), path);
            Assert.That(actual.RoundWinReward, Is.EqualTo(expected.RoundWinReward), path);
            Assert.That(actual.RoundLossReward, Is.EqualTo(expected.RoundLossReward), path);
            Assert.That(actual.LossStreakBonus, Is.EqualTo(expected.LossStreakBonus), path);
            Assert.That(actual.MaxLossReward, Is.EqualTo(expected.MaxLossReward), path);
        }
    }
}
