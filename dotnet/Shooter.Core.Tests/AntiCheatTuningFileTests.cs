using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AntiCheatTuningFileTests
    {
        [Test]
        public void TheServerFileHoldsTheDefaultsWithEveryFeatureOn()
        {
            var path = Path.Combine(RepositoryRoot(), "server-data", "tuning", "anticheat.json");

            var tuning = AntiCheatTuningJson.Parse(File.ReadAllText(path), path);
            var defaults = AntiCheatTuning.Default;

            Assert.That(tuning.Conceal.Enabled, Is.True);
            Assert.That(tuning.Conceal.MaxDistance, Is.EqualTo(defaults.Conceal.MaxDistance));
            Assert.That(tuning.Conceal.MarginDistance, Is.EqualTo(defaults.Conceal.MarginDistance));
            Assert.That(tuning.Conceal.MarginSeconds, Is.EqualTo(defaults.Conceal.MarginSeconds));
            Assert.That(tuning.Violations.Enabled, Is.True);
            Assert.That(tuning.Violations.WindowSeconds, Is.EqualTo(defaults.Violations.WindowSeconds));
            Assert.That(tuning.Violations.ShotRejectionLimit, Is.EqualTo(defaults.Violations.ShotRejectionLimit));
            Assert.That(tuning.Violations.OverBudgetLimit, Is.EqualTo(defaults.Violations.OverBudgetLimit));
            Assert.That(tuning.Stats.Enabled, Is.True);
            Assert.That(tuning.Stats.WindowSeconds, Is.EqualTo(defaults.Stats.WindowSeconds));
            Assert.That(tuning.Stats.HeadshotRatio, Is.EqualTo(defaults.Stats.HeadshotRatio));
            Assert.That(tuning.Stats.MinReactionMs, Is.EqualTo(defaults.Stats.MinReactionMs));
            Assert.That(tuning.Stats.SnapDegreesPerTick, Is.EqualTo(defaults.Stats.SnapDegreesPerTick));
            Assert.That(tuning.Stats.WallTrackSeconds, Is.EqualTo(defaults.Stats.WallTrackSeconds));
            Assert.That(tuning.Stats.MinSamples, Is.EqualTo(defaults.Stats.MinSamples));
            Assert.That(tuning.Evidence.Enabled, Is.True);
            Assert.That(tuning.Evidence.Seconds, Is.EqualTo(defaults.Evidence.Seconds));
            Assert.That(tuning.Evidence.MaxFilesPerMinute, Is.EqualTo(defaults.Evidence.MaxFilesPerMinute));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "server-data")))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
