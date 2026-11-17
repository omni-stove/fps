using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class SkillTuningFileTests
    {
        [Test]
        public void TheShippedFileHoldsTheDefaults()
        {
            var path = Path.Combine(RepositoryRoot(), "Assets", "game", "Resources", "Tuning", "skill.json");

            var spec = SkillJson.Parse(File.ReadAllText(path), "skill.json");

            Assert.That(spec.InitialRating, Is.EqualTo(SkillSpec.DefaultInitialRating));
            Assert.That(spec.KFactor, Is.EqualTo(SkillSpec.DefaultKFactor));
            Assert.That(spec.MinRating, Is.EqualTo(SkillSpec.DefaultMinRating));
            Assert.That(spec.MaxRating, Is.EqualTo(SkillSpec.DefaultMaxRating));
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
