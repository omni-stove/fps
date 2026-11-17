using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PlayerProfileTests
    {
        [Test]
        public void KeepsTheValues()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2);

            Assert.That(profile.Version, Is.EqualTo(PlayerProfile.CurrentVersion));
            Assert.That(profile.Xp, Is.EqualTo(1650));
            Assert.That(profile.Kills, Is.EqualTo(12));
            Assert.That(profile.Deaths, Is.EqualTo(4));
            Assert.That(profile.Matches, Is.EqualTo(3));
            Assert.That(profile.Wins, Is.EqualTo(2));
            Assert.That(profile.ToString(), Is.EqualTo("xp=1650 kills=12 deaths=4 matches=3 wins=2"));
        }

        [Test]
        public void TheEmptyProfileStartsAtZero()
        {
            Assert.That(PlayerProfile.Empty, Is.EqualTo(new PlayerProfile(0, 0, 0, 0, 0)));
        }

        [TestCase(-1, 0, 0, 0, 0, "xp")]
        [TestCase(0, -1, 0, 0, 0, "kills")]
        [TestCase(0, 0, -1, 0, 0, "deaths")]
        [TestCase(0, 0, 0, -1, 0, "matches")]
        [TestCase(0, 0, 0, 0, -1, "wins")]
        [TestCase(0, 0, 0, 2, 3, "wins")]
        public void RejectsValuesOutOfRange(int xp, int kills, int deaths, int matches, int wins, string param)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerProfile(xp, kills, deaths, matches, wins));

            Assert.That(error.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void WritesTheVersionedJson()
        {
            Assert.That(
                new PlayerProfile(1650, 12, 4, 3, 2).ToJson(),
                Is.EqualTo("{\"version\":1,\"xp\":1650,\"kills\":12,\"deaths\":4,\"matches\":3,\"wins\":2}"));
        }

        [Test]
        public void TheJsonRoundTrips()
        {
            var profiles = new[]
            {
                PlayerProfile.Empty,
                new PlayerProfile(1650, 12, 4, 3, 2),
                new PlayerProfile(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue),
            };

            foreach (var profile in profiles)
            {
                var read = PlayerProfile.FromJson(profile.ToJson());

                Assert.That(read, Is.EqualTo(profile));
                Assert.That(read, Is.Not.SameAs(profile));
                Assert.That(read.Version, Is.EqualTo(PlayerProfile.CurrentVersion));
            }
        }

        [Test]
        public void ReadsWhitespaceAndAnyKeyOrder()
        {
            var read = PlayerProfile.FromJson("{ \"wins\": 1, \"matches\": 2, \"deaths\": 3, \"kills\": 4, \"xp\": 5, \"version\": 1 }\n");

            Assert.That(read, Is.EqualTo(new PlayerProfile(5, 4, 3, 2, 1)));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("1")]
        [TestCase("{\"version\":1,\"xp\":1,\"kills\":0,\"deaths\":0,\"matches\":0}")]
        [TestCase("{\"version\":1,\"xp\":1,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"level\":3}")]
        [TestCase("{\"version\":1,\"xp\":\"1\",\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":1,\"xp\":1.5,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":1,\"xp\":null,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":1,\"xp\":9999999999,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":1,\"xp\":-1,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":1,\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":1,\"wins\":2}")]
        [TestCase("{\"version\":1,\"xp\":0,\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":\"1\",\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        public void BrokenJsonIsAFormatError(string json)
        {
            Assert.Throws<FormatException>(() => PlayerProfile.FromJson(json));
        }

        [TestCase("0")]
        [TestCase("2")]
        [TestCase("-1")]
        public void AnUnknownVersionIsAFormatError(string version)
        {
            var json = "{\"version\":" + version + ",\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}";

            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson(json));

            Assert.That(error.Message, Is.EqualTo("unsupported profile data version " + version + " (expected 1)"));
        }

        [Test]
        public void TheVersionIsCheckedBeforeTheOtherKeys()
        {
            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson("{\"version\":2,\"rank\":\"gold\"}"));

            Assert.That(error.Message, Does.StartWith("unsupported profile data version 2"));
        }

        [Test]
        public void AValueOutOfRangeKeepsTheReason()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson("{\"version\":1,\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":1,\"wins\":2}"));

            Assert.That(error.Message, Does.StartWith("invalid profile data: wins must not be more than matches (1)"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void NullJsonIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => PlayerProfile.FromJson(null));
        }

        [Test]
        public void EqualityComparesEveryValue()
        {
            var profile = new PlayerProfile(10, 2, 1, 1, 1);

            Assert.That(profile.Equals(new PlayerProfile(10, 2, 1, 1, 1)), Is.True);
            Assert.That(profile.GetHashCode(), Is.EqualTo(new PlayerProfile(10, 2, 1, 1, 1).GetHashCode()));
            Assert.That(profile.Equals(new PlayerProfile(11, 2, 1, 1, 1)), Is.False);
            Assert.That(profile.Equals(new PlayerProfile(10, 3, 1, 1, 1)), Is.False);
            Assert.That(profile.Equals(new PlayerProfile(10, 2, 2, 1, 1)), Is.False);
            Assert.That(profile.Equals(new PlayerProfile(10, 2, 1, 2, 1)), Is.False);
            Assert.That(profile.Equals(new PlayerProfile(10, 2, 1, 1, 0)), Is.False);
            Assert.That(profile.Equals(null), Is.False);
        }
    }
}
