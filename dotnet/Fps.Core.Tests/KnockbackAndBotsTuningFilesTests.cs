using System.IO;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // 失敗したら make export-server-rules で書き出し直し、server-data/tuning をコミットする
    public sealed class KnockbackAndBotsTuningFilesTests
    {
        [Test]
        public void TheServerReadsTheSameKnockbackAsUnity()
        {
            var root = RepositoryRoot();
            var server = File.ReadAllText(Path.Combine(root, "server-data", "tuning", "knockback.json"));
            var unity = File.ReadAllText(Path.Combine(root, "Assets", "game", "Resources", "Tuning", "knockback.json"));

            Assert.That(server, Is.EqualTo(unity));
            Assert.That(TuningJson.ParseKnockback(server, "knockback.json").Explosion.Speed, Is.EqualTo(8f));
        }

        [Test]
        public void WithoutBotsTuningInUnityTheServerUsesTheDefaults()
        {
            var root = RepositoryRoot();
            var server = File.ReadAllText(Path.Combine(root, "server-data", "tuning", "bots.json"));

            Assert.That(File.Exists(Path.Combine(root, "Assets", "game", "Resources", "Tuning", "bots.json")), Is.False);
            Assert.That(server, Is.EqualTo("{}\n"));
            var spec = TuningJson.ParseBots(server, "bots.json");
            Assert.That(spec.Brain.SightRange, Is.EqualTo(40f));
            Assert.That(spec.Input.MaxTurnDegreesPerSecond, Is.EqualTo(240f));
            Assert.That(spec.StuckProgress, Is.EqualTo(0.5f));
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
