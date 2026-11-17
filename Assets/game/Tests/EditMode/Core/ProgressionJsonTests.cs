using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ProgressionJsonTests
    {
        private static readonly string[] _expectedItemNames = { "killXp", "headshotXp", "assistXp", "matchXp", "winXp", "levels", "unlocks" };

        private static readonly string[] _expectedUnlockItemNames = { "id", "level" };

        private static readonly int[] _threeLevels = { 0, 100, 250 };

        private static readonly int[] _defaultLevels = { 0, 500, 1500, 3000, 5000, 7500, 10500, 14000, 18000, 22500 };

        private static readonly int[] _singleLevel = { 0 };

        private static readonly int[] _twoLevels = { 0, 100 };

        [Test]
        public void TheItemNamesFollowTheSpecArguments()
        {
            Assert.That(ProgressionJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(ProgressionJson.UnlockItemNames, Is.EqualTo(_expectedUnlockItemNames));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var spec = ProgressionJson.Parse(
                "{\"killXp\": 10, \"headshotXp\": 5, \"assistXp\": 3, \"matchXp\": 30, \"winXp\": 40, \"levels\": [0, 100, 250],"
                + " \"unlocks\": [{\"id\": \"burst\", \"level\": 2}, {\"id\": \"scope\", \"level\": 3}]}",
                "progression.json");

            Assert.That(spec.KillXp, Is.EqualTo(10));
            Assert.That(spec.HeadshotXp, Is.EqualTo(5));
            Assert.That(spec.AssistXp, Is.EqualTo(3));
            Assert.That(spec.MatchXp, Is.EqualTo(30));
            Assert.That(spec.WinXp, Is.EqualTo(40));
            Assert.That(spec.Levels, Is.EqualTo(_threeLevels));
            Assert.That(spec.MaxLevel, Is.EqualTo(3));
            Assert.That(spec.Unlocks.Count, Is.EqualTo(2));
            Assert.That(spec.Unlocks[0].Id, Is.EqualTo("burst"));
            Assert.That(spec.Unlocks[0].Level, Is.EqualTo(2));
            Assert.That(spec.Unlocks[1].Id, Is.EqualTo("scope"));
            Assert.That(spec.Unlocks[1].Level, Is.EqualTo(3));
        }

        [Test]
        public void AnEmptyObjectIsTheDefaults()
        {
            var spec = ProgressionJson.Parse("{}", "progression.json");

            Assert.That(spec.KillXp, Is.EqualTo(100));
            Assert.That(spec.HeadshotXp, Is.EqualTo(50));
            Assert.That(spec.AssistXp, Is.EqualTo(50));
            Assert.That(spec.MatchXp, Is.EqualTo(250));
            Assert.That(spec.WinXp, Is.EqualTo(500));
            Assert.That(spec.Levels, Is.EqualTo(_defaultLevels));
            Assert.That(spec.Unlocks, Is.Empty, "an omitted unlock table leaves everything usable from the start");
        }

        [Test]
        public void OmittedItemsUseTheDefaults()
        {
            var spec = ProgressionJson.Parse("{\"killXp\": 7, \"unlocks\": [{\"id\": \"burst\", \"level\": 10}]}", "progression.json");

            Assert.That(spec.KillXp, Is.EqualTo(7));
            Assert.That(spec.WinXp, Is.EqualTo(ProgressionSpec.DefaultWinXp));
            Assert.That(spec.AssistXp, Is.EqualTo(ProgressionSpec.DefaultAssistXp));
            Assert.That(spec.Levels, Is.EqualTo(ProgressionSpec.Default.Levels));
            Assert.That(spec.Unlocks[0].Level, Is.EqualTo(10), "the default table has 10 levels");
        }

        [TestCase("{\"killXp\": -1}", "killXp")]
        [TestCase("{\"headshotXp\": -1}", "headshotXp")]
        [TestCase("{\"assistXp\": -1}", "assistXp")]
        [TestCase("{\"assistXp\": 2.5}", "assistXp")]
        [TestCase("{\"matchXp\": -1}", "matchXp")]
        [TestCase("{\"winXp\": -5}", "winXp")]
        [TestCase("{\"winXp\": 1.5}", "winXp")]
        [TestCase("{\"killXp\": \"many\"}", "killXp")]
        [TestCase("{\"killXp\": null}", "killXp")]
        [TestCase("{\"levels\": []}", "levels")]
        [TestCase("{\"levels\": [5, 10]}", "levels")]
        [TestCase("{\"levels\": [0, 100, 100]}", "levels")]
        [TestCase("{\"levels\": [0, 100, 50]}", "levels")]
        [TestCase("{\"levels\": 100}", "levels")]
        [TestCase("{\"levels\": [0, 1.5]}", "levels[1]")]
        [TestCase("{\"levels\": [0, \"a\"]}", "levels[1]")]
        [TestCase("{\"unlocks\": {}}", "unlocks")]
        [TestCase("{\"unlocks\": [3]}", "unlocks[0]")]
        [TestCase("{\"unlocks\": [{\"level\": 2}]}", "unlocks[0].id")]
        [TestCase("{\"unlocks\": [{\"id\": \" \", \"level\": 2}]}", "unlocks[0].id")]
        [TestCase("{\"unlocks\": [{\"id\": \"a\"}]}", "unlocks[0].level")]
        [TestCase("{\"unlocks\": [{\"id\": \"a\", \"level\": 2}, {\"id\": \"b\", \"level\": 0}]}", "unlocks[1].level")]
        [TestCase("{\"unlocks\": [{\"id\": \"a\", \"level\": 2, \"slot\": \"optic\"}]}", "unlocks[0].slot")]
        [TestCase("{\"unlocks\": [{\"id\": \"a\", \"level\": 2}, {\"id\": \"a\", \"level\": 3}]}", "unlocks")]
        [TestCase("{\"levels\": [0, 100], \"unlocks\": [{\"id\": \"a\", \"level\": 3}]}", "unlocks")]
        [TestCase("{\"xp\": 1}", "xp")]
        public void ABadValueNamesTheItem(string json, string item)
        {
            var error = Assert.Throws<TuningJsonException>(() => ProgressionJson.Parse(json, "progression.json"));

            Assert.That(error.Item, Is.EqualTo(item));
            Assert.That(error.SourceName, Is.EqualTo("progression.json"));
            Assert.That(error.Message, Does.StartWith("progression JSON 'progression.json'"));
        }

        [Test]
        public void ARejectedValueKeepsTheSpecReason()
        {
            var error = Assert.Throws<TuningJsonException>(
                () => ProgressionJson.Parse("{\"unlocks\": [{\"id\": \"a\", \"level\": 2}, {\"id\": \"a\", \"level\": 3}]}", "progression.json"));

            Assert.That(error.Message, Does.StartWith("progression JSON 'progression.json' item 'unlocks' is invalid: duplicate unlock id 'a'"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void BrokenJsonIsRejected()
        {
            var error = Assert.Throws<TuningJsonException>(() => ProgressionJson.Parse("{\"levels\": [0, ", "progression.json"));
            Assert.That(error.Item, Is.Null);
            Assert.That(error.Message, Does.StartWith("progression JSON 'progression.json' is not valid JSON: "));

            Assert.That(
                Assert.Throws<TuningJsonException>(() => ProgressionJson.Parse("[]", null)).Message,
                Does.StartWith("progression JSON '(unnamed progression JSON)': the top level must be an object"));
            Assert.Throws<ArgumentNullException>(() => ProgressionJson.Parse(null, "progression.json"));
        }

        [Test]
        public void TheSpecChecksItsArguments()
        {
            var levels = new[] { 0, 100 };
            var none = Array.Empty<ProgressionUnlock>();

            Assert.Throws<ArgumentNullException>(() => new ProgressionSpec(1, 1, 1, 1, 1, null, none));
            Assert.Throws<ArgumentNullException>(() => new ProgressionSpec(1, 1, 1, 1, 1, levels, null));
            Assert.That(
                Assert.Throws<ArgumentException>(() => new ProgressionSpec(1, 1, 1, 1, 1, levels, new ProgressionUnlock[] { null })).ParamName,
                Is.EqualTo("unlocks"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionSpec(1, 1, -1, 1, 1, levels, none)).ParamName,
                Is.EqualTo("assistXp"));
            Assert.That(Assert.Throws<ArgumentException>(() => new ProgressionUnlock(null, 1)).ParamName, Is.EqualTo("id"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionUnlock("a", 0)).ParamName, Is.EqualTo("level"));

            var single = new ProgressionSpec(0, 0, 0, 0, 0, _singleLevel, none);
            Assert.That(single.MaxLevel, Is.EqualTo(1), "a table with only level 1 is allowed");
            Assert.That(single.ToString(), Is.EqualTo("kill=0 headshot=0 assist=0 match=0 win=0 levels=1 maxXp=0 unlocks=0"));
        }

        [Test]
        public void TheSpecKeepsItsOwnCopyOfTheTables()
        {
            var levels = new[] { 0, 100 };
            var unlocks = new[] { new ProgressionUnlock("a", 2) };
            var spec = new ProgressionSpec(1, 1, 1, 1, 1, levels, unlocks);

            levels[1] = 5;
            unlocks[0] = new ProgressionUnlock("b", 1);

            Assert.That(spec.Levels, Is.EqualTo(_twoLevels));
            Assert.That(spec.Unlocks[0].Id, Is.EqualTo("a"));
        }
    }
}
