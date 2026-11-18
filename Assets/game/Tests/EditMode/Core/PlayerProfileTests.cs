using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PlayerProfileTests
    {
        private static readonly string[] _firstBloodVeteran = { "first-blood", "veteran" };
        private static readonly string[] _firstBlood = { "first-blood" };
        private static readonly string[] _firstBloodQuoted = { "first-blood", "a \"quoted\" id" };
        private static readonly string[] _a = { "a" };
        private static readonly string[] _paddedFirstBloodVeteran = { " first-blood ", "veteran" };
        private static readonly string[] _firstBloodVictor = { "first-blood", "victor" };
        private static readonly string[] _blank = { "" };
        private static readonly string[] _aAndPaddedA = { "a", " a" };

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
            Assert.That(profile.Headshots, Is.Zero);
            Assert.That(profile.MeleeKills, Is.Zero);
            Assert.That(profile.PlaySeconds, Is.Zero);
            Assert.That(profile.Achievements, Is.Empty);
            Assert.That(
                profile.ToString(),
                Is.EqualTo("xp=1650 kills=12 assists=7 deaths=4 matches=3 wins=2 rating=1234 headshots=0 meleeKills=0 playSeconds=0 achievements="));
        }

        [Test]
        public void KeepsTheStatsAndAchievements()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 5, 2, 3600, _paddedFirstBloodVeteran);

            Assert.That(profile.Headshots, Is.EqualTo(5));
            Assert.That(profile.MeleeKills, Is.EqualTo(2));
            Assert.That(profile.PlaySeconds, Is.EqualTo(3600));
            Assert.That(profile.Achievements, Is.EqualTo(_firstBloodVeteran));
            Assert.That(profile.HasAchievement("veteran"), Is.True);
            Assert.That(profile.HasAchievement("victor"), Is.False);
            Assert.That(profile.HasAchievement(null), Is.False);
            Assert.That(
                profile.ToString(),
                Is.EqualTo("xp=1650 kills=12 assists=7 deaths=4 matches=3 wins=2 rating=1234 headshots=5 meleeKills=2 playSeconds=3600 achievements=first-blood,veteran"));
        }

        [Test]
        public void TheAchievementListIsCopied()
        {
            var ids = new[] { "first-blood" };
            var profile = new PlayerProfile(0, 0, 0, 0, 0, 0, 1000, 0, 0, 0, ids);

            ids[0] = "victor";

            Assert.That(profile.Achievements, Is.EqualTo(_firstBlood));
        }

        [Test]
        public void WithAchievementAddsTheIdOnceAtTheEnd()
        {
            var profile = new PlayerProfile(10, 1, 0, 1, 1, 0, 1200, 1, 0, 30, _firstBlood);

            var next = profile.WithAchievement("victor");

            Assert.That(next.Achievements, Is.EqualTo(_firstBloodVictor));
            Assert.That(next, Is.EqualTo(new PlayerProfile(10, 1, 0, 1, 1, 0, 1200, 1, 0, 30, _firstBloodVictor)));
            Assert.That(profile.Achievements, Is.EqualTo(_firstBlood), "the profile itself does not change");
            Assert.That(next.WithAchievement("victor"), Is.SameAs(next));
            Assert.Throws<ArgumentException>(() => profile.WithAchievement(" "));
            Assert.Throws<ArgumentException>(() => profile.WithAchievement(null));
        }

        [Test]
        public void TheWithMethodsKeepTheOtherValues()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 5, 2, 3600, _firstBlood);

            Assert.That(profile.WithRating(1300), Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1300, 5, 2, 3600, _firstBlood)));
            Assert.That(
                profile.WithRecord(1700, 13, 5, 4, 3, 8),
                Is.EqualTo(new PlayerProfile(1700, 13, 5, 4, 3, 8, 1234, 5, 2, 3600, _firstBlood)));
            Assert.That(profile.WithStats(6, 3, 3700), Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 6, 3, 3700, _firstBlood)));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.WithStats(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.WithStats(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.WithStats(0, 0, -1));
        }

        [Test]
        public void ABlankOrRepeatedAchievementIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new PlayerProfile(0, 0, 0, 0, 0, 0, 1000, 0, 0, 0, _blank));
            Assert.Throws<ArgumentException>(() => new PlayerProfile(0, 0, 0, 0, 0, 0, 1000, 0, 0, 0, new string[] { null }));
            Assert.Throws<ArgumentException>(() => new PlayerProfile(0, 0, 0, 0, 0, 0, 1000, 0, 0, 0, _aAndPaddedA));
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
            Assert.That(PlayerProfile.CurrentVersion, Is.EqualTo(5));
            Assert.That(
                new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234).ToJson(),
                Is.EqualTo(
                    "{\"version\":5,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1234,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":{}}"));
            Assert.That(
                new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 5, 2, 3600, _firstBloodVeteran).ToJson(),
                Is.EqualTo(
                    "{\"version\":5,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1234,"
                    + "\"headshots\":5,\"meleeKills\":2,\"playSeconds\":3600,\"achievements\":[\"first-blood\",\"veteran\"],\"cosmetics\":{}}"));
            Assert.That(
                PlayerProfile.Empty.WithCosmetic(CosmeticKind.Camo, "camo-tiger").WithCosmetic(CosmeticKind.Skin, "skin-desert").ToJson(),
                Is.EqualTo(
                    "{\"version\":5,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],"
                    + "\"cosmetics\":{\"Skin\":\"skin-desert\",\"Camo\":\"camo-tiger\"}}"),
                "the kinds are written in the order of CosmeticKind");
        }

        [Test]
        public void WithCosmeticEquipsOnePerKindAndAnEmptyIdTakesItOff()
        {
            var profile = new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 5, 2, 3600, _firstBlood);

            var skinned = profile.WithCosmetic(CosmeticKind.Skin, " skin-desert ");

            Assert.That(skinned.EquippedCosmetic(CosmeticKind.Skin), Is.EqualTo("skin-desert"));
            Assert.That(skinned.EquippedCosmetic(CosmeticKind.Camo), Is.Null);
            Assert.That(profile.EquippedCosmetic(CosmeticKind.Skin), Is.Null, "the profile itself does not change");
            Assert.That(skinned.WithCosmetic(CosmeticKind.Skin, "skin-desert"), Is.SameAs(skinned));

            var replaced = skinned.WithCosmetic(CosmeticKind.Skin, "skin-night");
            Assert.That(replaced.EquippedCosmetic(CosmeticKind.Skin), Is.EqualTo("skin-night"));
            Assert.That(replaced.WithCosmetic(CosmeticKind.Skin, null), Is.EqualTo(profile));
            Assert.That(replaced.WithCosmetic(CosmeticKind.Skin, " "), Is.EqualTo(profile));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.WithCosmetic((CosmeticKind)9, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.EquippedCosmetic((CosmeticKind)9));
        }

        [Test]
        public void TheOtherWithMethodsKeepTheCosmetics()
        {
            var profile = PlayerProfile.Empty.WithCosmetic(CosmeticKind.Camo, "camo-tiger");

            Assert.That(profile.WithRating(1200).EquippedCosmetic(CosmeticKind.Camo), Is.EqualTo("camo-tiger"));
            Assert.That(profile.WithRecord(10, 1, 0, 1, 1, 0).EquippedCosmetic(CosmeticKind.Camo), Is.EqualTo("camo-tiger"));
            Assert.That(profile.WithStats(1, 0, 5).EquippedCosmetic(CosmeticKind.Camo), Is.EqualTo("camo-tiger"));
            Assert.That(profile.WithAchievement("first-blood").EquippedCosmetic(CosmeticKind.Camo), Is.EqualTo("camo-tiger"));
            Assert.That(profile.Cosmetics.Count, Is.EqualTo(1));
            Assert.That(profile.Cosmetics[CosmeticKind.Camo], Is.EqualTo("camo-tiger"));
        }

        [Test]
        public void AVersion4ProfileIsReadWithNothingEquipped()
        {
            var read = PlayerProfile.FromJson(
                "{\"version\":4,\"xp\":0,\"kills\":3,\"assists\":0,\"deaths\":0,\"matches\":1,\"wins\":0,\"rating\":1000,"
                + "\"headshots\":2,\"meleeKills\":1,\"playSeconds\":95,\"achievements\":[\"first-blood\"]}");

            Assert.That(read.Cosmetics, Is.Empty);
            Assert.That(read.Version, Is.EqualTo(5));
            Assert.That(read.ToJson(), Does.EndWith("\"achievements\":[\"first-blood\"],\"cosmetics\":{}}"), "the next save writes the current version");
        }

        [Test]
        public void AVersion5ProfileKeepsAnIdTheCatalogMayNotHave()
        {
            var read = PlayerProfile.FromJson(
                "{\"version\":5,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,"
                + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":{\"Camo\":\"removed-camo\"}}");

            Assert.That(read.EquippedCosmetic(CosmeticKind.Camo), Is.EqualTo("removed-camo"));
            Assert.That(read.EquippedCosmetic(CosmeticKind.Skin), Is.Null);
        }

        [TestCase("[]", "invalid profile data: 'cosmetics' must be an object")]
        [TestCase("{\"skin\":\"a\"}", "invalid profile data: 'cosmetics' has the unknown kind 'skin'")]
        [TestCase("{\"0\":\"a\"}", "invalid profile data: 'cosmetics' has the unknown kind '0'")]
        [TestCase("{\"Skin\":\"\"}", "invalid profile data: 'cosmetics.Skin' must be a non-empty string")]
        [TestCase("{\"Skin\":1}", "invalid profile data: 'cosmetics.Skin' must be a non-empty string")]
        public void BrokenCosmeticsAreAFormatError(string cosmetics, string message)
        {
            var json = "{\"version\":5,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,"
                + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":" + cosmetics + "}";

            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson(json));

            Assert.That(error.Message, Is.EqualTo(message));
        }

        [Test]
        public void TheCosmeticsAreRequiredFromVersion5()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson(
                    "{\"version\":5,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[]}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: missing 'cosmetics'"));
        }

        [Test]
        public void Version4HasNoCosmetics()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson(
                    "{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":{}}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: unknown key 'cosmetics'"));
        }

        [Test]
        public void AVersion1ProfileIsReadWithNoAssists()
        {
            var read = PlayerProfile.FromJson("{\"version\":1,\"xp\":1650,\"kills\":12,\"deaths\":4,\"matches\":3,\"wins\":2}");

            Assert.That(read, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 0, 1000)));
            Assert.That(read.Version, Is.EqualTo(PlayerProfile.CurrentVersion));
            Assert.That(
                read.ToJson(),
                Is.EqualTo(
                    "{\"version\":5,\"xp\":1650,\"kills\":12,\"assists\":0,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1000,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":{}}"),
                "the next save writes the current version");
        }

        [Test]
        public void AVersion3ProfileIsReadWithNoStatsOrAchievements()
        {
            var read = PlayerProfile.FromJson(
                "{\"version\":3,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1234}", 1500);

            Assert.That(read, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 0, 0, 0, null)));
            Assert.That(read.Version, Is.EqualTo(5));
            Assert.That(read.Achievements, Is.Empty);
            Assert.That(
                read.ToJson(),
                Is.EqualTo(
                    "{\"version\":5,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1234,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":{}}"),
                "the next save writes the current version");
        }

        [Test]
        public void AVersion4ProfileKeepsItsStatsAndAchievements()
        {
            var read = PlayerProfile.FromJson(
                "{\"version\":4,\"xp\":0,\"kills\":3,\"assists\":0,\"deaths\":0,\"matches\":1,\"wins\":0,\"rating\":1000,"
                + "\"headshots\":2,\"meleeKills\":1,\"playSeconds\":95,\"achievements\":[\"first-blood\"]}");

            Assert.That(read, Is.EqualTo(new PlayerProfile(0, 3, 0, 1, 0, 0, 1000, 2, 1, 95, _firstBlood)));
        }

        [Test]
        public void AVersion2ProfileKeepsItsRecordAndStartsAtTheGivenRating()
        {
            const string version2 = "{\"version\":2,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2}";

            var read = PlayerProfile.FromJson(version2, 1500);

            Assert.That(read, Is.EqualTo(new PlayerProfile(1650, 12, 4, 3, 2, 7, 1500)));
            Assert.That(read.Version, Is.EqualTo(5));
            Assert.That(
                read.ToJson(),
                Is.EqualTo(
                    "{\"version\":5,\"xp\":1650,\"kills\":12,\"assists\":7,\"deaths\":4,\"matches\":3,\"wins\":2,\"rating\":1500,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"cosmetics\":{}}"),
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
                new PlayerProfile(1650, 12, 4, 3, 2, 7, 1234, 5, 2, 3600, _firstBloodQuoted),
                new PlayerProfile(0, 0, 0, 0, 0, 0, 0, int.MaxValue, int.MaxValue, int.MaxValue, null),
                PlayerProfile.Empty.WithCosmetic(CosmeticKind.Skin, "a \"quoted\" skin").WithCosmetic(CosmeticKind.Camo, "camo-tiger"),
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
        [TestCase("{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"achievements\":[]}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":-1,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[]}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":1.5,\"achievements\":[]}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":\"first-blood\"}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[1]}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[\"\"]}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[\"a\",\"a\"]}")]
        [TestCase("{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0,\"achievements\":[],\"level\":3}")]
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
        [TestCase("6")]
        [TestCase("-1")]
        public void AnUnknownVersionIsAFormatError(string version)
        {
            var json = "{\"version\":" + version + ",\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":0}";

            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson(json));

            Assert.That(error.Message, Is.EqualTo("unsupported profile data version " + version + " (expected 1 to 5)"));
        }

        [Test]
        public void TheVersionIsCheckedBeforeTheOtherKeys()
        {
            var error = Assert.Throws<FormatException>(() => PlayerProfile.FromJson("{\"version\":6,\"rank\":\"gold\"}"));

            Assert.That(error.Message, Does.StartWith("unsupported profile data version 6"));
        }

        [Test]
        public void TheAchievementsAreRequiredFromVersion4()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson(
                    "{\"version\":4,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,"
                    + "\"headshots\":0,\"meleeKills\":0,\"playSeconds\":0}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: missing 'achievements'"));
        }

        [Test]
        public void Version3HasNoAchievements()
        {
            var error = Assert.Throws<FormatException>(
                () => PlayerProfile.FromJson(
                    "{\"version\":3,\"xp\":0,\"kills\":0,\"assists\":0,\"deaths\":0,\"matches\":0,\"wins\":0,\"rating\":1000,\"playSeconds\":5}"));

            Assert.That(error.Message, Is.EqualTo("invalid profile data: unknown key 'playSeconds'"));
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
            Assert.That(profile.Equals(profile.WithStats(1, 0, 0)), Is.False);
            Assert.That(profile.Equals(profile.WithStats(0, 1, 0)), Is.False);
            Assert.That(profile.Equals(profile.WithStats(0, 0, 1)), Is.False);
            Assert.That(profile.Equals(profile.WithAchievement("first-blood")), Is.False);
            Assert.That(profile.Equals(profile.WithCosmetic(CosmeticKind.Skin, "skin-desert")), Is.False);
            Assert.That(
                profile.WithCosmetic(CosmeticKind.Skin, "x").Equals(profile.WithCosmetic(CosmeticKind.Camo, "x")),
                Is.False,
                "the same id on another kind is another look");
            Assert.That(
                profile.WithCosmetic(CosmeticKind.Skin, "x").GetHashCode(),
                Is.EqualTo(profile.WithCosmetic(CosmeticKind.Skin, "x").GetHashCode()));
            Assert.That(
                profile.WithAchievement("a").WithAchievement("b").Equals(profile.WithAchievement("b").WithAchievement("a")),
                Is.False,
                "the order of unlocking is part of the record");
            Assert.That(
                profile.WithAchievement("a").GetHashCode(),
                Is.EqualTo(new PlayerProfile(10, 2, 1, 1, 1, 0, 1000, 0, 0, 0, _a).GetHashCode()));
            Assert.That(profile.Equals(null), Is.False);
        }
    }
}
