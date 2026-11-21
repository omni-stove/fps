using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerPlayerStatusTuningTests
    {
        private TempDirectory _root;
        private string _tuning;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _tuning = Path.Combine(_root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
        }

        [TearDown]
        public void TearDown()
        {
            _root.Dispose();
        }

        [Test]
        public void WithoutFilesTheDefaultsAreUsed()
        {
            Assert.That(ServerPlayerStatusTuning.TryLoad(new[] { _root.Path }, out var tuning, out var error), Is.True, error);

            Assert.That(tuning.Stamina, Is.SameAs(StaminaSpec.Default));
            Assert.That(tuning.Stagger, Is.SameAs(StaggerTuning.Default));
            Assert.That(tuning.Suppression, Is.SameAs(SuppressionSpec.Default));
            Assert.That(tuning.StaminaPath, Is.Null);
            Assert.That(tuning.LogLine, Is.EqualTo("[Server] status tuning stamina=default stagger=default suppression=default"));
        }

        [Test]
        public void TheFilesAreReadAndNamedInTheLogLine()
        {
            Directory.CreateDirectory(_tuning);
            var stamina = Path.GetFullPath(Path.Combine(_tuning, ServerPlayerStatusTuning.StaminaFileName));
            var stagger = Path.GetFullPath(Path.Combine(_tuning, ServerPlayerStatusTuning.StaggerFileName));
            var suppression = Path.GetFullPath(Path.Combine(_tuning, ServerPlayerStatusTuning.SuppressionFileName));
            File.WriteAllText(stamina, "{\"max\": 60}");
            File.WriteAllText(stagger, "{\"player\": {\"stumbleSeconds\": 0.8}}");
            File.WriteAllText(suppression, "{\"nearMissRadius\": 3}");

            Assert.That(ServerPlayerStatusTuning.TryLoad(new[] { _root.Path }, out var tuning, out var error), Is.True, error);

            Assert.That(tuning.Stamina.Max, Is.EqualTo(60f));
            Assert.That(tuning.Stagger.Player.StumbleSeconds, Is.EqualTo(0.8f));
            Assert.That(tuning.Stagger.Enemy, Is.SameAs(StaggerSpec.EnemyDefault));
            Assert.That(tuning.Suppression.NearMissRadius, Is.EqualTo(3f));
            Assert.That(tuning.LogLine, Is.EqualTo(
                "[Server] status tuning stamina=" + stamina + " stagger=" + stagger + " suppression=" + suppression));
        }

        [TestCase(ServerPlayerStatusTuning.StaminaFileName, "{\"max\": 0}", "cannot read stamina tuning: ")]
        [TestCase(ServerPlayerStatusTuning.StaggerFileName, "{\"player\": {\"moveMultiplier\": 2}}", "cannot read stagger tuning: ")]
        [TestCase(ServerPlayerStatusTuning.SuppressionFileName, "{\"radius\": 1}", "cannot read suppression tuning: ")]
        public void AnUnreadableFileStopsTheStart(string fileName, string text, string message)
        {
            Directory.CreateDirectory(_tuning);
            File.WriteAllText(Path.Combine(_tuning, fileName), text);

            Assert.That(ServerPlayerStatusTuning.TryLoad(new[] { _root.Path }, out var tuning, out var error), Is.False);

            Assert.That(tuning, Is.Null);
            Assert.That(error, Does.StartWith(message));
        }

        [Test]
        public void TheShippedServerDataReadsAsTheDefaults()
        {
            var root = RepositoryRoot();

            Assert.That(ServerPlayerStatusTuning.TryLoad(new[] { root }, out var tuning, out var error), Is.True, error);

            Assert.That(tuning.StaggerPath, Is.Not.Null);
            Assert.That(tuning.SuppressionPath, Is.Not.Null);
            Assert.That(tuning.StaminaPath, Is.Null, "stamina has no Unity tuning file, so none is shipped");
            Assert.That(tuning.Stagger.Player.StumbleSeconds, Is.EqualTo(StaggerSpec.DefaultPlayerStumbleSeconds));
            Assert.That(tuning.Stagger.Player.MoveMultiplier, Is.EqualTo(StaggerSpec.DefaultPlayerMoveMultiplier));
            Assert.That(tuning.Suppression.NearMissRadius, Is.EqualTo(SuppressionSpec.DefaultNearMissRadius));
            Assert.That(tuning.Suppression.SuppressedLevel, Is.EqualTo(SuppressionSpec.DefaultSuppressedLevel));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName)))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
