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
            Assert.That(data.LogLine, Does.StartWith("[Server] rules Domination objective=Domination weapons=2 from=").And.EndWith("ping=default"));
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
    }
}
