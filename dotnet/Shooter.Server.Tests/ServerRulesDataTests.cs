using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerRulesDataTests
    {
        private static readonly string[] _darnWords = { "darn" };
        private static readonly float[] _oneAndOnePointFour = { 1f, 1.4f };
        private static readonly string[] _badwordNoobWords = { "badword", "noob" };

        private TempDirectory _first;
        private TempDirectory _second;

        [SetUp]
        public void SetUp()
        {
            _first = new TempDirectory();
            _second = new TempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _first.Dispose();
            _second.Dispose();
        }

        [Test]
        public void WithSettingsReplacesOnlyTheSettings()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);
            var settings = data.Settings.WithPrivacy(true, "FRIENDS");

            var changed = data.WithSettings(settings);

            Assert.That(changed.Settings, Is.SameAs(settings));
            Assert.That(changed.File, Is.SameAs(data.File));
            Assert.That(changed.File.Settings.PrivateMatch, Is.False, "the file stays as it was read");
            Assert.That(changed.Name, Is.EqualTo(data.Name));
            Assert.That(changed.Path, Is.EqualTo(data.Path));
            Assert.That(changed.Ping, Is.SameAs(data.Ping));
            Assert.That(changed.Grenades, Is.SameAs(data.Grenades));
            Assert.That(changed.Bots, Is.SameAs(data.Bots));
            Assert.That(changed.Items, Is.SameAs(data.Items));
            Assert.That(changed.Knockback, Is.SameAs(data.Knockback));
            Assert.That(changed.Assist, Is.SameAs(data.Assist));
            Assert.That(changed.ChatWords, Is.SameAs(data.ChatWords));
            Assert.That(changed.ChatWordsPath, Is.EqualTo(data.ChatWordsPath));
            Assert.That(data.Settings.PrivateMatch, Is.False);
        }

        [Test]
        public void LoadsTheRulesFromTheFirstRootThatHasThem()
        {
            var path = TestRules.Write(_second.Path, "Domination", TestRules.File(GameRules.Default.WithObjective(MatchObjective.Domination), name: "Domination"));

            Assert.That(ServerRulesData.TryLoad("Domination", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(data.Path, Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(data.Ping, Is.SameAs(PingSpec.Default));
            Assert.That(data.PingPath, Is.Null);
            Assert.That(data.Grenades, Is.SameAs(GrenadeCatalog.Default));
            Assert.That(data.Bots, Is.SameAs(NetBotSpec.Default));
            Assert.That(data.BotsPath, Is.Null);
            Assert.That(data.Assist, Is.SameAs(AssistSpec.Default));
            Assert.That(data.AssistPath, Is.Null);
            Assert.That(data.ChatWords, Is.SameAs(ChatWordFilterSpec.Default));
            Assert.That(data.ChatWordsPath, Is.Null);
            Assert.That(data.LogLine, Does.StartWith("[Server] rules Domination objective=Domination weapons=2 from=").And.EndWith("ping=default grenade=default bots=default inventory=default knockback=default assist=default chatWords=default walk=5 sprint=8 height=2 crouchHeight=1.2 radius=0.5 weather=default timeOfDay=default"));
        }

        [Test]
        public void ReadsTheModeSpecsAndTheWeatherAndTimeOfDayOfTheRules()
        {
            var settings = new NetMatchSettings(
                weatherId: "rain",
                timeOfDayId: "dusk",
                captureTheFlag: new CaptureTheFlagSpec(scoreLimit: 5, returnSeconds: 12f),
                hardpoint: new HardpointSpec(HardpointSpec.MinPointCount, 45f, 2f, 150, 600f, 4f),
                searchAndDestroy: new SearchAndDestroySpec(winsToWin: 6, fuseSeconds: 40f),
                freeForAll: new FreeForAllSpec(scoreLimit: 25, pointsPerKill: 2),
                killConfirmed: new KillConfirmedSpec(30, 420f, 2f, 20f, 3, 1.5f),
                gunGame: new GunGameSpec(480f, 1f, new[] { TestRules.RifleId, TestRules.HandgunId }, new[] { TestRules.HandgunId }, false));
            TestRules.Write(_first.Path, "GunGame", TestRules.File(GameRules.Default.WithObjective(MatchObjective.GunGame), settings, "GunGame"));

            Assert.That(ServerRulesData.TryLoad("GunGame", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(MatchObjective.GunGame));
            Assert.That(data.Settings.WeatherId, Is.EqualTo("rain"));
            Assert.That(data.Settings.TimeOfDayId, Is.EqualTo("dusk"));
            Assert.That(data.Settings.CaptureTheFlag.ScoreLimit, Is.EqualTo(5));
            Assert.That(data.Settings.CaptureTheFlag.ReturnSeconds, Is.EqualTo(12f));
            Assert.That(data.Settings.Hardpoint.RotateSeconds, Is.EqualTo(45f));
            Assert.That(data.Settings.Hardpoint.ScoreLimit, Is.EqualTo(150));
            Assert.That(data.Settings.SearchAndDestroy.WinsToWin, Is.EqualTo(6));
            Assert.That(data.Settings.SearchAndDestroy.FuseSeconds, Is.EqualTo(40f));
            Assert.That(data.Settings.FreeForAll.ScoreLimit, Is.EqualTo(25));
            Assert.That(data.Settings.FreeForAll.PointsPerKill, Is.EqualTo(2));
            Assert.That(data.Settings.KillConfirmed.TagLifetimeSeconds, Is.EqualTo(20f));
            Assert.That(data.Settings.KillConfirmed.PointsPerConfirm, Is.EqualTo(3));
            Assert.That(data.Settings.GunGame.TierWeaponIds, Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId }));
            Assert.That(data.Settings.GunGame.MeleeWeaponIds, Is.EqualTo(new[] { TestRules.HandgunId }));
            Assert.That(data.Settings.GunGame.DemotesOnMelee, Is.False);
            Assert.That(data.LogLine, Does.StartWith("[Server] rules GunGame objective=GunGame ").And.EndWith(" weather=rain timeOfDay=dusk"));
        }

        [Test]
        public void WithSettingsKeepsTheModeSpecsThroughPrivacy()
        {
            var gunGame = new GunGameSpec(tierWeaponIds: new[] { TestRules.RifleId });
            var settings = new NetMatchSettings(weatherId: "fog", gunGame: gunGame);
            TestRules.Write(_first.Path, "TeamDeathmatch", TestRules.File(settings: settings));
            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            var changed = data.WithSettings(data.Settings.WithPrivacy(true, "FRIENDS"));

            Assert.That(changed.Settings.WeatherId, Is.EqualTo("fog"));
            Assert.That(changed.Settings.GunGame.TierWeaponIds, Is.EqualTo(new[] { TestRules.RifleId }));
            Assert.That(changed.Settings.CaptureTheFlag, Is.SameAs(data.Settings.CaptureTheFlag));
        }

        [Test]
        public void AGunGameRulesFileWithoutTiersStopsTheLoad()
        {
            var directory = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName);
            Directory.CreateDirectory(directory);
            var text = NetMatchFile.Write(TestRules.File(name: "GunGame"))
                .Replace("\"objective\": \"None\"", "\"objective\": \"GunGame\"");
            Assert.That(text, Does.Contain("\"objective\": \"GunGame\""));
            File.WriteAllText(Path.Combine(directory, "GunGame.json"), text);

            Assert.That(ServerRulesData.TryLoad("GunGame", Roots(), out _, out var error), Is.False);

            Assert.That(error, Does.StartWith("cannot read rules GunGame: ").And.Contain("match.gunGame.tierWeaponIds"));
        }

        [Test]
        public void ReadsTheVoteKickPayloadAndEconomyOfTheRules()
        {
            var settings = new NetMatchSettings(
                voteKick: new VoteKickSpec(true, 0.7f, 25f, 90f),
                payload: new PayloadSpec(2f, 2, _oneAndOnePointFour, 8f, 0.25f, 30f, 420f),
                economy: new EconomySpec(600, 12000, 200, 3500, 1500, 300, 2700));
            TestRules.Write(_first.Path, "Payload", TestRules.File(GameRules.Default.WithObjective(MatchObjective.Payload), settings, "Payload"));

            Assert.That(ServerRulesData.TryLoad("Payload", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(MatchObjective.Payload));
            Assert.That(data.Settings.VoteKick.Enabled, Is.True);
            Assert.That(data.Settings.VoteKick.RequiredYesFraction, Is.EqualTo(0.7f));
            Assert.That(data.Settings.VoteKick.VoteSeconds, Is.EqualTo(25f));
            Assert.That(data.Settings.VoteKick.CooldownSeconds, Is.EqualTo(90f));
            Assert.That(data.Settings.Payload.CartSpeed, Is.EqualTo(2f));
            Assert.That(data.Settings.Payload.MinPushers, Is.EqualTo(2));
            Assert.That(data.Settings.Payload.PusherSpeedMultipliers, Is.EqualTo(_oneAndOnePointFour));
            Assert.That(data.Settings.Payload.RollbackDelaySeconds, Is.EqualTo(8f));
            Assert.That(data.Settings.Payload.RollbackSpeed, Is.EqualTo(0.25f));
            Assert.That(data.Settings.Payload.CheckpointBonusSeconds, Is.EqualTo(30f));
            Assert.That(data.Settings.Payload.TimeLimitSeconds, Is.EqualTo(420f));
            Assert.That(data.Settings.Economy.StartMoney, Is.EqualTo(600));
            Assert.That(data.Settings.Economy.MaxMoney, Is.EqualTo(12000));
            Assert.That(data.Settings.Economy.KillReward, Is.EqualTo(200));
            Assert.That(data.Settings.Economy.RoundWinReward, Is.EqualTo(3500));
            Assert.That(data.Settings.Economy.RoundLossReward, Is.EqualTo(1500));
            Assert.That(data.Settings.Economy.LossStreakBonus, Is.EqualTo(300));
            Assert.That(data.Settings.Economy.MaxLossReward, Is.EqualTo(2700));
        }

        [Test]
        public void ARulesFileWithoutTheVoteKickPayloadAndEconomySectionsLoadsTheirDefaults()
        {
            var directory = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName);
            Directory.CreateDirectory(directory);
            var text = NetMatchFile.Write(TestRules.File());
            var start = text.IndexOf(",\n    \"voteKick\": {", System.StringComparison.Ordinal);
            var end = text.IndexOf("\n  },\n  \"motor\"", System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0));
            Assert.That(end, Is.GreaterThan(start));
            File.WriteAllText(Path.Combine(directory, "TeamDeathmatch.json"), text.Remove(start, end - start));

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Settings.VoteKick, Is.SameAs(VoteKickSpec.Default));
            Assert.That(data.Settings.VoteKick.Enabled, Is.False);
            Assert.That(data.Settings.Payload, Is.SameAs(PayloadSpec.Default));
            Assert.That(data.Settings.Economy, Is.SameAs(EconomySpec.Default));
        }

        [TestCase("TeamDeathmatch")]
        [TestCase("SearchAndDestroy")]
        [TestCase("CaptureTheFlag")]
        public void TheCommittedRulesKeepVoteKicksOffAndTheDefaultPayloadAndEconomy(string name)
        {
            Assert.That(ServerRulesData.TryLoad(name, new[] { RepositoryRoot() }, out var data, out var error), Is.True, error);

            Assert.That(data.Settings.VoteKick.Enabled, Is.False);
            Assert.That(data.Settings.VoteKick.RequiredYesFraction, Is.EqualTo(VoteKickSpec.DefaultRequiredYesFraction));
            Assert.That(data.Settings.Payload.CartSpeed, Is.EqualTo(PayloadSpec.DefaultCartSpeed));
            Assert.That(data.Settings.Payload.PusherSpeedMultipliers, Is.EqualTo(PayloadSpec.Default.PusherSpeedMultipliers));
            Assert.That(data.Settings.Economy.StartMoney, Is.EqualTo(EconomySpec.DefaultStartMoney));
            Assert.That(data.Settings.Economy.MaxLossReward, Is.EqualTo(EconomySpec.DefaultMaxLossReward));
        }

        [Test]
        public void WithSettingsKeepsTheVoteKickPayloadAndEconomyThroughPrivacy()
        {
            var settings = new NetMatchSettings(voteKick: new VoteKickSpec(enabled: true), economy: new EconomySpec(startMoney: 0));
            TestRules.Write(_first.Path, "TeamDeathmatch", TestRules.File(settings: settings));
            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            var changed = data.WithSettings(data.Settings.WithPrivacy(true, "FRIENDS"));

            Assert.That(changed.Settings.VoteKick.Enabled, Is.True);
            Assert.That(changed.Settings.Economy.StartMoney, Is.Zero);
            Assert.That(changed.Settings.Payload, Is.SameAs(data.Settings.Payload));
        }

        [Test]
        public void TheEconomyTuningAndPlaylistsHaveTheseNames()
        {
            Assert.That(ServerRulesData.EconomyTuningFileName, Is.EqualTo("economy.json"));
            Assert.That(ServerRulesData.PlaylistsDirectoryName, Is.EqualTo("playlists"));
        }

        [TestCase("{\"match\": {\"weatherId\": \"rain/../x\"}}", "match.weatherId")]
        [TestCase("{\"match\": {\"hardpoint\": {\"rotateSeconds\": 0}}}", "match.hardpoint.rotateSeconds")]
        [TestCase("{\"match\": {\"killConfirmed\": {\"pointsPerConfirm\": 0}}}", "match.killConfirmed.pointsPerConfirm")]
        [TestCase("{\"match\": {\"voteKick\": {\"requiredYesFraction\": 2}}}", "match.voteKick.requiredYesFraction")]
        [TestCase("{\"match\": {\"payload\": {\"minPushers\": 0}}}", "match.payload.minPushers")]
        [TestCase("{\"match\": {\"payload\": {\"pusherSpeedMultipliers\": [-1]}}}", "match.payload.pusherSpeedMultipliers")]
        [TestCase("{\"match\": {\"economy\": {\"startMoney\": 99999}}}", "match.economy.startMoney")]
        public void BrokenModeItemsNameTheFileAndItem(string json, string item)
        {
            var directory = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Bad.json"), json);

            Assert.That(ServerRulesData.TryLoad("Bad", Roots(), out _, out var error), Is.False);

            Assert.That(error, Does.StartWith("cannot read rules Bad: ").And.Contain("Bad.json").And.Contain(item));
        }

        [Test]
        public void ReadsTheAssistTuningBesideTheRules()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.AssistTuningFileName), "{\"windowSeconds\": 4, \"minDamage\": 35}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Assist.WindowSeconds, Is.EqualTo(4f));
            Assert.That(data.Assist.MinDamage, Is.EqualTo(35));
            Assert.That(data.AssistPath, Does.EndWith(ServerRulesData.AssistTuningFileName));
            Assert.That(data.LogLine, Does.Contain(" assist=" + data.AssistPath + " "));
        }

        [Test]
        public void BrokenAssistTuningStopsTheLoad()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.AssistTuningFileName), "{\"minDamage\": 0}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read assist tuning: ").And.Contain("minDamage"));
        }

        [Test]
        public void ReadsTheChatWordTuningBesideTheRules()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(
                Path.Combine(tuning, ServerRulesData.ChatWordsTuningFileName),
                "{\"words\": [\"Darn\"], \"mask\": \"#\", \"action\": \"Drop\", \"wholeWordsOnly\": true}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.ChatWords.Words, Is.EqualTo(_darnWords));
            Assert.That(data.ChatWords.Mask, Is.EqualTo('#'));
            Assert.That(data.ChatWords.Action, Is.EqualTo(ChatWordFilterAction.Drop));
            Assert.That(data.ChatWords.WholeWordsOnly, Is.True);
            Assert.That(data.ChatWordsPath, Does.EndWith(ServerRulesData.ChatWordsTuningFileName));
            Assert.That(data.LogLine, Does.Contain(" chatWords=" + data.ChatWordsPath + " "));
        }

        [Test]
        public void BrokenChatWordTuningStopsTheLoad()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.ChatWordsTuningFileName), "{\"mask\": \"**\"}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read chat word tuning: ").And.Contain("mask"));
        }

        [Test]
        public void TheCommittedChatWordTuningReads()
        {
            var path = Path.Combine(RepositoryRoot(), ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, ServerRulesData.ChatWordsTuningFileName);
            var spec = ChatWordFilterJson.Parse(File.ReadAllText(path), path);

            Assert.That(spec.Words, Is.EqualTo(_badwordNoobWords));
            Assert.That(spec.Mask, Is.EqualTo(ChatWordFilterSpec.DefaultMask));
            Assert.That(spec.Action, Is.EqualTo(ChatWordFilterAction.Mask));
            Assert.That(spec.WholeWordsOnly, Is.False);
        }

        [Test]
        public void ReadsThePingTuningBesideTheRules()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.PingTuningFileName), "{\"maxMarkers\": 5}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Ping.MaxMarkers, Is.EqualTo(5));
            Assert.That(data.PingPath, Does.EndWith(ServerRulesData.PingTuningFileName));
        }

        [Test]
        public void ReadsTheGrenadeTuningBesideTheRules()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.GrenadeTuningFileName), "{\"fuseSeconds\": 1.5, \"damageRadius\": 4}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Grenades.Carried[0].FuseSeconds, Is.EqualTo(1.5f));
            Assert.That(data.Grenades.Carried[0].DamageRadius, Is.EqualTo(4f));
            Assert.That(data.GrenadePath, Does.EndWith(ServerRulesData.GrenadeTuningFileName));
        }

        [Test]
        public void BrokenGrenadeTuningStopsTheLoad()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.GrenadeTuningFileName), "{\"fuseSeconds\": -1}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read grenade tuning: "));
        }

        [Test]
        public void ReadsTheInventoryTuningBesideTheRules()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(
                Path.Combine(tuning, ServerRulesData.InventoryTuningFileName),
                "{\"rule\": {\"gridWidth\": 6, \"maxWeight\": 20}, \"items\": [{\"id\": \"0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f\", \"useHealth\": 30}]}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Items.Rule.GridWidth, Is.EqualTo(6));
            Assert.That(data.Items.Rule.MaxWeight, Is.EqualTo(20f));
            Assert.That(data.Items.Items[0].UseHealth, Is.EqualTo(30));
            Assert.That(data.ItemsPath, Does.EndWith(ServerRulesData.InventoryTuningFileName));
            Assert.That(data.LogLine, Does.Contain(" inventory=" + data.ItemsPath + " "));
        }

        [Test]
        public void BrokenInventoryTuningStopsTheLoad()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.InventoryTuningFileName), "{\"rule\": {\"gridHeight\": 3}}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read inventory tuning: "));
        }

        [Test]
        public void ReadsTheBotTuningBesideTheRules()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.BotsTuningFileName), "{\"sightRange\": 30, \"repathSeconds\": 1}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Bots.Brain.SightRange, Is.EqualTo(30f));
            Assert.That(data.Bots.RepathSeconds, Is.EqualTo(1f));
            Assert.That(data.Bots.Input.MaxTurnDegreesPerSecond, Is.EqualTo(BotInputSpec.DefaultMaxTurnDegreesPerSecond));
            Assert.That(data.BotsPath, Does.EndWith(ServerRulesData.BotsTuningFileName));
        }

        [Test]
        public void BrokenBotTuningStopsTheLoad()
        {
            TestRules.Write(_first.Path, "TeamDeathmatch");
            var tuning = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(Path.Combine(tuning, ServerRulesData.BotsTuningFileName), "{\"attackRange\": 50}");

            Assert.That(ServerRulesData.TryLoad("TeamDeathmatch", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("cannot read bots tuning: ").And.Contain("attackRange"));
        }

        [Test]
        public void TheCommittedBotTuningMatchesTheDefaults()
        {
            var path = Path.Combine(RepositoryRoot(), ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, ServerRulesData.BotsTuningFileName);
            var spec = TuningJson.ParseBots(File.ReadAllText(path), path);
            var defaults = NetBotSpec.Default;

            Assert.That(spec.Brain.SightRange, Is.EqualTo(defaults.Brain.SightRange));
            Assert.That(spec.Brain.AttackRange, Is.EqualTo(defaults.Brain.AttackRange));
            Assert.That(spec.Brain.ReactionSeconds, Is.EqualTo(defaults.Brain.ReactionSeconds));
            Assert.That(spec.Input.AimErrorPerMeter, Is.EqualTo(defaults.Input.AimErrorPerMeter));
            Assert.That(spec.Input.CrouchWhenHolding, Is.EqualTo(defaults.Input.CrouchWhenHolding));
            Assert.That(spec.NavSampleRadius, Is.EqualTo(defaults.NavSampleRadius));
            Assert.That(spec.StuckProgress, Is.EqualTo(defaults.StuckProgress));
        }

        [Test]
        public void MissingRulesTellWhereItLookedAndHowToExport()
        {
            Assert.That(ServerRulesData.TryLoad("Domination", Roots(), out _, out var error), Is.False);

            Assert.That(error, Does.StartWith("rules Domination not found: Domination.json is not in ")
                .And.Contain(Path.GetFullPath(_first.Path)).And.Contain("make export-server-rules"));
        }

        [Test]
        public void BrokenRulesNameTheFileAndItem()
        {
            var directory = Path.Combine(_first.Path, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Bad.json"), "{\"match\": {\"scoreLimit\": -1}}");

            Assert.That(ServerRulesData.TryLoad("Bad", Roots(), out _, out var error), Is.False);

            Assert.That(error, Does.StartWith("cannot read rules Bad: ").And.Contain("Bad.json").And.Contain("match.scoreLimit"));
        }

        [Test]
        public void NamesThatAreNotFileNamesAreRefused()
        {
            Assert.That(ServerRulesData.TryLoad("../x", Roots(), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("'../x'"));
        }

        private string[] Roots() => new[] { _first.Path, _second.Path };

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
