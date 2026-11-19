using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // クライアントは Resources の値で振る見た目と報酬の枠を出し、サーバーは server-data の値で当てて数えるので、2 つが同じであることを確かめる
    public sealed class MeleeAndStreakTuningFilesTests
    {
        [TestCase("melee.json")]
        [TestCase("streaks.json")]
        [TestCase("loot.json")]
        public void TheServerReadsTheSameTuningAsUnity(string fileName)
        {
            var root = RepositoryRoot();
            var server = File.ReadAllText(Path.Combine(root, "server-data", "tuning", fileName));
            var unity = File.ReadAllText(Path.Combine(root, "Assets", "game", "Resources", "Tuning", fileName));

            Assert.That(server, Is.EqualTo(unity));
        }

        [Test]
        public void TheServerMeleeTuningIsTheShippedDefault()
        {
            var path = Path.Combine(RepositoryRoot(), "server-data", "tuning", "melee.json");

            var spec = MeleeJson.Parse(File.ReadAllText(path), path);

            Assert.That(spec.Damage, Is.EqualTo(MeleeSpec.DefaultDamage));
            Assert.That(spec.Reach, Is.EqualTo(MeleeSpec.DefaultReach));
            Assert.That(spec.ArcDegrees, Is.EqualTo(MeleeSpec.DefaultArcDegrees));
            Assert.That(spec.SwingSeconds, Is.EqualTo(MeleeSpec.DefaultSwingSeconds));
            Assert.That(spec.ImpactSeconds, Is.EqualTo(MeleeSpec.DefaultImpactSeconds));
            Assert.That(spec.CooldownSeconds, Is.EqualTo(MeleeSpec.DefaultCooldownSeconds));
        }

        [Test]
        public void TheServerStreaksAreTheUavAtThreeAndTheAirstrikeAtFive()
        {
            var path = Path.Combine(RepositoryRoot(), "server-data", "tuning", "streaks.json");

            var spec = StreakJson.Parse(File.ReadAllText(path), path);

            Assert.That(spec.Rewards.Count, Is.EqualTo(2));
            Assert.That(spec.Rewards[0].Id, Is.EqualTo(StreakSpec.UavRewardId));
            Assert.That(spec.Rewards[0].KillsRequired, Is.EqualTo(3));
            Assert.That(spec.Rewards[1].Id, Is.EqualTo(StreakSpec.AirstrikeRewardId));
            Assert.That(spec.Rewards[1].KillsRequired, Is.EqualTo(5));
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
