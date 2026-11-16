using System.IO;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerRulesDataTests
    {
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
            Assert.That(data.LogLine, Does.StartWith("[Server] rules Domination objective=Domination weapons=2 from=").And.EndWith("ping=default grenade=default bots=default inventory=default walk=5 sprint=8 height=2 crouchHeight=1.2 radius=0.5"));
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
