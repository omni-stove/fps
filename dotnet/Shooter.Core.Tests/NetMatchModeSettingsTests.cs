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
                gunGame: new GunGameSpec(720f, 0.5f, new[] { _handgunId, _knifeId }, new[] { _knifeId }, false));

            var read = NetMatchFile.Parse(NetMatchFile.Write(File(original, MatchObjective.GunGame)), "GunGame.json").Settings;

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
            var settings = new NetMatchSettings(weatherId: "clear", gunGame: new GunGameSpec(tierWeaponIds: new[] { _handgunId }));
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
                + "      \"meleeWeaponIds\": [],\n      \"demotesOnMelee\": true\n    }\n  },"));
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
                gunGame: new GunGameSpec(tierWeaponIds: new[] { _handgunId }));

            var settings = original.WithPrivacy(true, "abc");

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
        public void RejectsAValueOutOfRangeAtItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "TeamDeathmatch.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("TeamDeathmatch.json").And.Contain(item));
        }

        [TestCase("{\"hardpoint\": {\"pointCount\": 3}}", "match.hardpoint.pointCount")]
        [TestCase("{\"searchAndDestroy\": {\"siteCount\": 3}}", "match.searchAndDestroy.siteCount")]
        [TestCase("{\"freeForAll\": {\"pointsPerAssist\": 1}}", "match.freeForAll.pointsPerAssist")]
        public void RejectsTheCountsTheMapDecidesAndUnknownItems(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "TeamDeathmatch.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("unknown item '" + item + "'"));
        }

        [TestCase("{\"captureTheFlag\": 3}", "match.captureTheFlag")]
        [TestCase("{\"gunGame\": null}", "match.gunGame")]
        [TestCase("{\"gunGame\": {\"tierWeaponIds\": \"" + _handgunId + "\"}}", "match.gunGame.tierWeaponIds")]
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
    }
}
