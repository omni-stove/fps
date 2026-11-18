using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AchievementJsonTests
    {
        private const string _source = "achievements.json";

        private static readonly string[] _expectedItemNames = { "achievements" };
        private static readonly string[] _expectedAchievementItemNames = { "id", "nameKey", "descriptionKey", "stat", "target", "hidden" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = AchievementJson.Parse(
                "{\"achievements\": [{\"id\": \"brawler\", \"nameKey\": \"a.brawler\", \"descriptionKey\": \"a.brawler.text\","
                + " \"stat\": \"MeleeKills\", \"target\": 20, \"hidden\": true},"
                + " {\"id\": \"survivor\", \"stat\": \"Deaths\", \"target\": 100}]}",
                _source);

            Assert.That(spec.Achievements.Count, Is.EqualTo(2));
            var brawler = spec.Achievements[0];
            Assert.That(brawler.Id, Is.EqualTo("brawler"));
            Assert.That(brawler.DisplayNameKey, Is.EqualTo("a.brawler"));
            Assert.That(brawler.DescriptionKey, Is.EqualTo("a.brawler.text"));
            Assert.That(brawler.Stat, Is.EqualTo(PlayerStat.MeleeKills));
            Assert.That(brawler.Target, Is.EqualTo(20));
            Assert.That(brawler.Hidden, Is.True);
            var survivor = spec.Achievements[1];
            Assert.That(survivor.DisplayNameKey, Is.EqualTo(string.Empty));
            Assert.That(survivor.DescriptionKey, Is.EqualTo(string.Empty));
            Assert.That(survivor.Hidden, Is.False);
            Assert.That(spec.Find("survivor"), Is.SameAs(survivor));
            Assert.That(spec.Find("missing"), Is.Null);
            Assert.That(spec.Find(null), Is.Null);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = AchievementJson.Parse("{}", _source);

            Assert.That(spec.Achievements, Is.SameAs(AchievementSpec.DefaultAchievements));
            Assert.That(spec.Achievements.Count, Is.EqualTo(4));
            Assert.That(spec.Achievements[0].Id, Is.EqualTo("first-blood"));
            Assert.That(spec.Achievements[0].DisplayNameKey, Is.EqualTo("achievement.first-blood"));
            Assert.That(spec.Achievements[0].DescriptionKey, Is.EqualTo("achievement.first-blood.description"));
            Assert.That(spec.Achievements[0].Stat, Is.EqualTo(PlayerStat.Kills));
            Assert.That(spec.Achievements[0].Target, Is.EqualTo(1));
            Assert.That(spec.Achievements[1].Id, Is.EqualTo("marksman"));
            Assert.That(spec.Achievements[1].Stat, Is.EqualTo(PlayerStat.Headshots));
            Assert.That(spec.Achievements[1].Target, Is.EqualTo(50));
            Assert.That(spec.Achievements[2].Id, Is.EqualTo("veteran"));
            Assert.That(spec.Achievements[2].Stat, Is.EqualTo(PlayerStat.MatchesPlayed));
            Assert.That(spec.Achievements[2].Target, Is.EqualTo(25));
            Assert.That(spec.Achievements[3].Id, Is.EqualTo("victor"));
            Assert.That(spec.Achievements[3].Stat, Is.EqualTo(PlayerStat.MatchesWon));
            Assert.That(spec.Achievements[3].Target, Is.EqualTo(10));
            Assert.That(spec.Achievements[3].Hidden, Is.False);
        }

        [Test]
        public void AnEmptyListTurnsTheAchievementsOff()
        {
            Assert.That(AchievementJson.Parse("{\"achievements\": []}", _source).Achievements, Is.Empty);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(AchievementJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(AchievementJson.AchievementItemNames, Is.EqualTo(_expectedAchievementItemNames));
            Assert.That(AchievementJson.ResourcePath, Is.EqualTo("Tuning/achievements"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var target = Reject("{\"achievements\": [{\"id\": \"a\", \"stat\": \"Kills\", \"target\": 0}]}");
            var stat = Reject("{\"achievements\": [{\"id\": \"a\", \"stat\": \"Score\", \"target\": 1}]}");
            var noStat = Reject("{\"achievements\": [{\"id\": \"a\", \"target\": 1}]}");
            var blankId = Reject("{\"achievements\": [{\"id\": \" \", \"stat\": \"Kills\", \"target\": 1}]}");
            var twice = Reject("{\"achievements\": [{\"id\": \"a\", \"stat\": \"Kills\", \"target\": 1}, {\"id\": \"a\", \"stat\": \"Deaths\", \"target\": 2}]}");
            var unknown = Reject("{\"achievements\": [{\"id\": \"a\", \"stat\": \"Kills\", \"target\": 1, \"icon\": \"x\"}]}");
            var notArray = Reject("{\"achievements\": {}}");

            Assert.That(target.Item, Is.EqualTo("achievements[0].target"));
            Assert.That(target.Message, Does.StartWith("achievement JSON 'achievements.json' item 'achievements[0].target' is invalid: "));
            Assert.That(target.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(stat.Item, Is.EqualTo("achievements[0].stat"));
            Assert.That(noStat.Item, Is.EqualTo("achievements[0].stat"));
            Assert.That(blankId.Item, Is.EqualTo("achievements[0].id"));
            Assert.That(twice.Item, Is.EqualTo("achievements"));
            Assert.That(twice.Message, Does.Contain("'a' twice"));
            Assert.That(unknown.Item, Is.EqualTo("achievements[0].icon"));
            Assert.That(notArray.Item, Is.EqualTo("achievements"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => AchievementJson.Parse("{\"x\": 1}", null)).SourceName,
                Is.EqualTo("(unnamed achievement JSON)"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => AchievementJson.Parse(json, _source));
        }
    }
}
