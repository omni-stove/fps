using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class AssistTuningFileTests
    {
        [Test]
        public void TheShippedFileHoldsTheDefaults()
        {
            var path = Path.Combine(RepositoryRoot(), "Assets", "game", "Resources", "Tuning", "assist.json");

            var spec = AssistJson.Parse(File.ReadAllText(path), "assist.json");

            Assert.That(spec.WindowSeconds, Is.EqualTo(AssistSpec.DefaultWindowSeconds));
            Assert.That(spec.MinDamage, Is.EqualTo(AssistSpec.DefaultMinDamage));
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
