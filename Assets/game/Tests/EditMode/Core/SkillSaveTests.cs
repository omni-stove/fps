using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SkillSaveTests
    {
        [Test]
        public void KeepsTheRating()
        {
            var save = new SkillSave(1234);

            Assert.That(save.Version, Is.EqualTo(SkillSave.CurrentVersion));
            Assert.That(save.Rating, Is.EqualTo(1234));
            Assert.That(save.ToString(), Is.EqualTo("rating=1234"));
        }

        [Test]
        public void WritesTheVersionedJson()
        {
            Assert.That(SkillSave.CurrentVersion, Is.EqualTo(1));
            Assert.That(new SkillSave(1234).ToJson(), Is.EqualTo("{\"version\":1,\"rating\":1234}"));
            Assert.That(new SkillSave(0).ToJson(), Is.EqualTo("{\"version\":1,\"rating\":0}"));
        }

        [TestCase(0)]
        [TestCase(1000)]
        [TestCase(int.MaxValue)]
        public void TheJsonRoundTrips(int rating)
        {
            var save = new SkillSave(rating);

            var read = SkillSave.FromJson(save.ToJson());

            Assert.That(read, Is.EqualTo(save));
            Assert.That(read, Is.Not.SameAs(save));
            Assert.That(read.GetHashCode(), Is.EqualTo(save.GetHashCode()));
        }

        [Test]
        public void ReadsWhitespaceAndAnyKeyOrder()
        {
            Assert.That(SkillSave.FromJson("{ \"rating\": 1176, \"version\": 1 }\n").Rating, Is.EqualTo(1176));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("1")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"rating\":1000}")]
        [TestCase("{\"version\":\"1\",\"rating\":1000}")]
        [TestCase("{\"version\":1,\"rating\":\"1000\"}")]
        [TestCase("{\"version\":1,\"rating\":1000.5}")]
        [TestCase("{\"version\":1,\"rating\":null}")]
        [TestCase("{\"version\":1,\"rating\":9999999999}")]
        [TestCase("{\"version\":1,\"rating\":1000,\"rating\":1001}")]
        public void BrokenJsonIsAFormatError(string json)
        {
            Assert.Throws<FormatException>(() => SkillSave.FromJson(json));
        }

        [Test]
        public void AnUnknownKeyIsAFormatError()
        {
            var error = Assert.Throws<FormatException>(() => SkillSave.FromJson("{\"version\":1,\"rating\":1000,\"xp\":5}"));

            Assert.That(error.Message, Is.EqualTo("invalid skill data: unknown key 'xp'"));
        }

        [Test]
        public void AMissingRatingIsAFormatError()
        {
            var error = Assert.Throws<FormatException>(() => SkillSave.FromJson("{\"version\":1}"));

            Assert.That(error.Message, Is.EqualTo("invalid skill data: missing 'rating'"));
        }

        [Test]
        public void ANegativeRatingKeepsTheReason()
        {
            var error = Assert.Throws<FormatException>(() => SkillSave.FromJson("{\"version\":1,\"rating\":-1}"));

            Assert.That(error.Message, Does.StartWith("invalid skill data: rating must not be negative"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [TestCase("0")]
        [TestCase("2")]
        [TestCase("-1")]
        public void AnUnknownVersionIsAFormatError(string version)
        {
            var error = Assert.Throws<FormatException>(() => SkillSave.FromJson("{\"version\":" + version + ",\"rating\":1000}"));

            Assert.That(error.Message, Is.EqualTo("unsupported skill data version " + version + " (expected 1 to 1)"));
        }

        [Test]
        public void TheVersionIsCheckedBeforeTheOtherKeys()
        {
            var error = Assert.Throws<FormatException>(() => SkillSave.FromJson("{\"version\":2,\"league\":\"gold\"}"));

            Assert.That(error.Message, Does.StartWith("unsupported skill data version 2"));
        }

        [Test]
        public void RejectsANegativeRatingAndNullJson()
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new SkillSave(-1)).ParamName, Is.EqualTo("rating"));
            Assert.Throws<ArgumentNullException>(() => SkillSave.FromJson(null));
        }

        [Test]
        public void EqualityComparesTheRating()
        {
            Assert.That(new SkillSave(1000).Equals(new SkillSave(1000)), Is.True);
            Assert.That(new SkillSave(1000).Equals(new SkillSave(1001)), Is.False);
            Assert.That(new SkillSave(1000).Equals(null), Is.False);
            Assert.That(new SkillSave(1000).Equals((object)new SkillSave(1000)), Is.True);
        }
    }
}
