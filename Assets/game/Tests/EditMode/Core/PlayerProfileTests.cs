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
            var profile = new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234);

            Assert.That(profile.Version, Is.EqualTo(PlayerProfile.CurrentVersion));
            Assert.That(profile.Xp, Is.EqualTo(1650));
            Assert.That(profile.Kills, Is.EqualTo(12));
            Assert.That(profile.Assists, Is.EqualTo(7));
            Assert.That(profile.Deaths, Is.EqualTo(4));
            Assert.That(profile.Matches, Is.EqualTo(3));
            Assert.That(profile.Wins, Is.EqualTo(2));
            Assert.That(profile.Rating, Is.EqualTo(1234));
            Assert.That(profile.ToString(), Is.EqualTo("xp=1650 kills=12 assists=7 deaths=4 matches=3 wins=2 rating=1234"));
        }

        [Test]
        public void TheProfileWithoutAssistsHasNone()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2);

            Assert.That(profile.Assists, Is.Zero);
            Assert.That(profile, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 0)));
        }

        [Test]
        public void TheProfileWithoutARatingHasTheDefaultInitialRating()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2, 7);

            Assert.That(profile.Rating, Is.EqualTo(1000));
            Assert.That(profile, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1000)));
        }

        [Test]
        public void TheEmptyProfileStartsAtZero()
        {
            Assert.That(PlayerProfile.Empty, Is.EqualTo(new PlayerProfile(0, 0, 0, 0, 0, 0)));
        }

        [Test]
        public void AStartedProfileHasOnlyTheGivenRating()
        {
            Assert.That(PlayerProfile.Started(1500), Is.EqualTo(new PlayerProfile(0, 0, 0, 0, 0, 0, 1500)));
        }

        [Test]
        public void WithRatingKeepsTheOtherValues()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2, 7, 1000);

            Assert.That(profile.WithRating(1016), Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1016)));
            Assert.That(profile.Rating, Is.EqualTo(1000));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.WithRating(-1));
        }

        [TestCase(-1, 0, 0, 0, 0, 0, 0, "xp")]
        [TestCase(0, -1, 0, 0, 0, 0, 0, "kills")]
        [TestCase(0, 0, -1, 0, 0, 0, 0, "deaths")]
        [TestCase(0, 0, 0, -1, 0, 0, 0, "matches")]
        [TestCase(0, 0, 0, 0, -1, 0, 0, "wins")]
        [TestCase(0, 0, 0, 0, 0, -1, 0, "assists")]
        [TestCase(0, 0, 0, 0, 0, 0, -1, "rating")]
        [TestCase(0, 0, 0, 2, 3, 0, 0, "wins")]
        public void RejectsValuesOutOfRange(int xp, int kills, int deaths, int matches, int wins, int assists, int rating, string param)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerProfile(xp, kills, deaths, matches, wins, assists, rating));

            Assert.That(error.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void WritesTheVersionedJson()
        {
            Assert.That(PlayerProfile.CurrentVersion, Is.EqualTo(3));
            Assert.That(
                new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234).ToJson(),
                Is.EqualTo("{\"version\":3,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1234}"));
        }

        [Test]
        public void AVersion1ProfileIsReadWithNoAssists()
        {
            var read = PlayerProfile.FromJson("{\"version\":1,\"xp\":1650,\"kills\":12,\"deaths\":4,\"matches\":3,\"wins\":2}");

            Assert.That(read, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 0, 1000)));
            Assert.That(read.Version, Is.EqualTo(PlayerProfile.CurrentVersion));
            Assert.That(
                read.ToJson(),
                Is.EqualTo("{\"version\":3,\"xp\":1650,\"kills\":12,\"assists\":0,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1000}"),
                "the next save writes the current version");
        }

        [Test]
        public void AVersion2ProfileKeepsItsRecordAndStartsAtTheGivenRating()
        {
            const string version2 = "{\"version\":2,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2}";

            var read = PlayerProfile.FromJson(version2, 1500);

            Assert.That(read, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1500)));
            Assert.That(read.Version, Is.EqualTo(3));
            Assert.That(
                read.ToJson(),
                Is.EqualTo("{\"version\":3,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1500}"),
                "the next save writes the current version");
            Assert.That(PlayerProfile.FromJson(version2).Rating, Is.EqualTo(1000), "without an initial rating the default is used");
        }

        [Test]
        public void AVersion1ProfileStartsAtTheGivenRating()
        {
            var read = PlayerProfile.FromJson("{\"version\":1,\"xp\":1650,\"kills\":12,\"deaths\":4,\"matches\":3,\"wins\":2}", 800);

            Assert.That(read, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 0, 800)));
        }

        [Test]
        public void AVersion3ProfileKeepsItsOwnRatingOverTheInitialRating()
        {
            var read = PlayerProfile.FromJson(
                "{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1234}", 1500);

            Assert.That(read.Rating, Is.EqualTo(1234));
        }

        [Test]
        public void ANegativeInitialRatingIsRejected()
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(
                () => PlayerProfile.FromJson("{\"version\":2,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0}", -1));

            Assert.That(error.ParamName, Is.EqualTo("initialRating"));
        }

        [Test]
        public void TheJsonRoundTrips()
        {
            var profiles = new[]
            {
                PlayerProfile.Empty,
                new PlayerProfile(1650, 12, 4, 3, 2, 7),
                new PlayerProfile(1650, 12, 4, 3, 2, 7, 0),
                new PlayerProfile(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue),
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

            var current = PlayerProfile.FromJson(
                "{ \"assists\": 6, \"wins\": 1, \"matches\": 2, \"deaths\": 3, \"kills\": 4, \"xp\": 5, \"version\": 2 }\n");

            Assert.That(current, Is.EqualTo(new PlayerProfile(5, 4, 3, 2, 1, 6)));

            var rated = PlayerProfile.FromJson(
                "{ \"rating\": 7, \"assists\": 6, \"wins\": 1, \"matches\": 2, \"deaths\": 3, \"kills\": 4, \"xp\": 5, \"version\": 3 }\n");

            Assert.That(rated, Is.EqualTo(new PlayerProfile(5, 4, 3, 2, 1, 6, 7)));
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
        [TestCase("{\"version\":1,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":2,\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":2,\"xp\":0,\"kills\":0,\"assists\":-1,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":2,\"xp\":0,\"kills\":0,\"assists\":\"2\",\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":2,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"level\":3}")]
        [TestCase("{\"version\":2,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000}")]
        [TestCase("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0}")]
        [TestCase("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":-1}")]
        [TestCase("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1.5}")]
        [TestCase("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":\"1000\"}")]
        [TestCase("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"level\":3}")]
        public void BrokenJsonIsAFormatError(string json)
        {
            Assert.Throws<FormatException>(() => PlayerProfile.FromJson(json));
        }

        [Test]
        public void TheAssistsAreRequiredFromVersion2()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson("{\"version\":2,\"xp\":0,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: missing 'assists'"));
        }

        [Test]
        public void TheRatingIsRequiredFromVersion3()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: missing 'rating'"));
        }

        [Test]
        public void Version2HasNoRating()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson("{\"version\":2,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":5}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: unknown key 'rating'"));
        }

        [Test]
        public void Version1HasNoAssists()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson("{\"version\":1,\"xp\":0,\"kills\":0,\"assists\":3,\"deaths\":0,\"matches\":0,\"wins\":0}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: unknown key 'assists'"));
        }

        [TestCase("0")]
        [TestCase("4")]
        [TestCase("-1")]
        public void AnUnknownVersionIsAFormatError(string version)
        {
            var json = "{\"version\":" + version + ",\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":0}";

            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson(json));

            Assert.That(error.Message, Is.EqualTo("unsupported profile data version " + version + " (expected 1 to 3)"));
        }

        [Test]
        public void TheVersionIsCheckedBeforeTheOtherKeys()
        {
            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson("{\"version\":4,\"rank\":\"gold\"}"));

            Assert.That(error.Message, Does.StartWith("unsupported profile data version 4"));
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
            Assert.That(profile.Equals(new PlayerProfile(10, 2, 1, 1, 1, 1)), Is.False);
            Assert.That(profile.Equals(new PlayerProfile(10, 2, 1, 1, 1, 0, 1001)), Is.False);
            Assert.That(profile.Equals(null), Is.False);
        }
    }
}
