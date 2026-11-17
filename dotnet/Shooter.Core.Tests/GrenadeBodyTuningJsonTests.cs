using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class GrenadeBodyTuningJsonTests
    {
        private static readonly string[] _bodyItems = { "bodyRadius", "mass", "bounciness", "friction", "angularDamping", "releaseDistance" };

        [Test]
        public void ReadsTheBallAndTheReleaseDistance()
        {
            var spec = TuningJson.ParseGrenade(
                "{\"bodyRadius\": 0.12, \"mass\": 0.9, \"bounciness\": 0.9, \"friction\": 0.2, \"angularDamping\": 1.5, \"releaseDistance\": 1.1}",
                "grenade.json");

            Assert.That(spec.BodyRadius, Is.EqualTo(0.12f));
            Assert.That(spec.Mass, Is.EqualTo(0.9f));
            Assert.That(spec.Bounciness, Is.EqualTo(0.9f));
            Assert.That(spec.Friction, Is.EqualTo(0.2f));
            Assert.That(spec.AngularDamping, Is.EqualTo(1.5f));
            Assert.That(spec.ReleaseDistance, Is.EqualTo(1.1f));
        }

        [Test]
        public void EachTypeInTheCatalogHasItsOwnBall()
        {
            var catalog = TuningJson.ParseGrenades(
                "{\"types\": [{\"id\": \"" + GrenadeSpec.DefaultId + "\", \"bounciness\": 0.9},"
                + " {\"id\": \"ecaf6e57-cb77-4707-9321-6ea5b659408f\", \"bodyRadius\": 0.1, \"releaseDistance\": 0.9}]}",
                "grenade.json");

            Assert.That(catalog.Types[0].Bounciness, Is.EqualTo(0.9f));
            Assert.That(catalog.Types[0].BodyRadius, Is.EqualTo(0.08f));
            Assert.That(catalog.Types[1].Bounciness, Is.EqualTo(0.35f));
            Assert.That(catalog.Types[1].BodyRadius, Is.EqualTo(0.1f));
            Assert.That(catalog.Types[1].ReleaseDistance, Is.EqualTo(0.9f));
        }

        [Test]
        public void LeftOutItemsKeepTheValuesTheGrenadeHadBefore()
        {
            var spec = TuningJson.ParseGrenade("{}", "grenade.json");

            Assert.That(spec.BodyRadius, Is.EqualTo(0.08f));
            Assert.That(spec.Mass, Is.EqualTo(0.4f));
            Assert.That(spec.Bounciness, Is.EqualTo(0.35f));
            Assert.That(spec.Friction, Is.EqualTo(0.6f));
            Assert.That(spec.AngularDamping, Is.EqualTo(0.05f));
            Assert.That(spec.ReleaseDistance, Is.EqualTo(0.7f));
        }

        [Test]
        public void TheItemsAreListedWithTheOtherGrenadeItems()
        {
            Assert.That(
                TuningJson.GrenadeItemNames.Skip(TuningJson.GrenadeItemNames.Count - 6),
                Is.EqualTo(_bodyItems));
        }

        [TestCase("bodyRadius", "0")]
        [TestCase("mass", "-1")]
        [TestCase("bounciness", "1.5")]
        [TestCase("bounciness", "-0.1")]
        [TestCase("friction", "-0.5")]
        [TestCase("angularDamping", "-1")]
        [TestCase("releaseDistance", "-0.1")]
        public void AnOutOfRangeValueNamesTheFileAndTheItem(string item, string value)
        {
            var e = Assert.Throws<TuningJsonException>(
                () => TuningJson.ParseGrenades("{\"types\": [{\"" + item + "\": " + value + "}]}", "grenade.json"));

            Assert.That(e.SourceName, Is.EqualTo("grenade.json"));
            Assert.That(e.Item, Is.EqualTo("types[0]." + item));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TheShippedFilesWriteTheValuesTheGrenadeHadBefore()
        {
            var catalog = TuningJson.ParseGrenades(
                File.ReadAllText(Path.Combine(RepositoryRoot(), "server-data", "tuning", "grenade.json")), "grenade.json");

            foreach (var type in catalog.Types)
            {
                Assert.That(type.BodyRadius, Is.EqualTo(0.08f), type.LogName);
                Assert.That(type.Mass, Is.EqualTo(0.4f), type.LogName);
                Assert.That(type.Bounciness, Is.EqualTo(0.35f), type.LogName);
                Assert.That(type.Friction, Is.EqualTo(0.6f), type.LogName);
                Assert.That(type.AngularDamping, Is.EqualTo(0.05f), type.LogName);
                Assert.That(type.ReleaseDistance, Is.EqualTo(0.7f), type.LogName);
            }
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
