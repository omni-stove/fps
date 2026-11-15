using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GrenadeTuningFilesTests
    {
        private static readonly string[] _shippedNameKeys =
        {
            "grenade.frag", "grenade.smoke", "grenade.flash", "grenade.incendiary", "grenade.sticky",
        };

        [Test]
        public void TheServerReadsTheSameGrenadeTypesAsUnity()
        {
            var root = RepositoryRoot();
            var server = File.ReadAllText(Path.Combine(root, "server-data", "tuning", "grenade.json"));
            var unity = File.ReadAllText(Path.Combine(root, "Assets", "game", "Resources", "Tuning", "grenade.json"));

            Assert.That(server, Is.EqualTo(unity));
        }

        [Test]
        public void OnlyTheFragGrenadeIsCarriedAndItIsUnchanged()
        {
            var catalog = TuningJson.ParseGrenades(
                File.ReadAllText(Path.Combine(RepositoryRoot(), "Assets", "game", "Resources", "Tuning", "grenade.json")), "grenade.json");

            Assert.That(
                catalog.Types.Select(t => t.NameKey),
                Is.EqualTo(_shippedNameKeys));
            var frag = catalog.Carried.Single();
            var expected = GrenadeSpec.Default;
            Assert.That(frag.Id, Is.EqualTo(expected.Id));
            Assert.That(frag.FuseSeconds, Is.EqualTo(expected.FuseSeconds));
            Assert.That(frag.ThrowSeconds, Is.EqualTo(expected.ThrowSeconds));
            Assert.That(frag.ThrowSpeed, Is.EqualTo(expected.ThrowSpeed));
            Assert.That(frag.ThrowLiftSpeed, Is.EqualTo(expected.ThrowLiftSpeed));
            Assert.That(frag.MaxDamage, Is.EqualTo(expected.MaxDamage));
            Assert.That(frag.DamageRadius, Is.EqualTo(expected.DamageRadius));
            Assert.That(frag.FullDamageRadius, Is.EqualTo(expected.FullDamageRadius));
            Assert.That(frag.StructureDamageAtCenter, Is.EqualTo(expected.StructureDamageAtCenter));
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
