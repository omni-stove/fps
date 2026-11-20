using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AntiCheatTuningJsonTests
    {
        private static readonly string[] _expectedItemNames = { "conceal", "violations", "stats", "evidence" };
        private static readonly string[] _expectedConcealItemNames = { "enabled", "maxDistance", "marginDistance", "marginSeconds" };
        private static readonly string[] _expectedViolationItemNames = { "enabled", "windowSeconds", "shotRejectionLimit", "overBudgetLimit" };

        private static readonly string[] _expectedStatsItemNames =
        {
            "enabled", "windowSeconds", "headshotRatio", "minReactionMs", "snapDegreesPerTick", "wallTrackSeconds",
        };

        private static readonly string[] _expectedEvidenceItemNames = { "enabled", "seconds" };

        [Test]
        public void TheItemNamesFollowTheSettingsArguments()
        {
            Assert.That(AntiCheatTuningJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(AntiCheatTuningJson.ConcealItemNames, Is.EqualTo(_expectedConcealItemNames));
            Assert.That(AntiCheatTuningJson.ViolationItemNames, Is.EqualTo(_expectedViolationItemNames));
            Assert.That(AntiCheatTuningJson.StatsItemNames, Is.EqualTo(_expectedStatsItemNames));
            Assert.That(AntiCheatTuningJson.EvidenceItemNames, Is.EqualTo(_expectedEvidenceItemNames));
        }

        [Test]
        public void EveryFeatureIsOffByDefault()
        {
            var tuning = AntiCheatTuning.Default;

            Assert.That(tuning.Conceal.Enabled, Is.False);
            Assert.That(tuning.Violations.Enabled, Is.False);
            Assert.That(tuning.Stats.Enabled, Is.False);
            Assert.That(tuning.Evidence.Enabled, Is.False);
        }

        [Test]
        public void TheDefaultsAreTheDocumentedValues()
        {
            var tuning = AntiCheatTuning.Default;

            Assert.That(tuning.Conceal.MaxDistance, Is.EqualTo(150f));
            Assert.That(tuning.Conceal.MarginDistance, Is.EqualTo(1.5f));
            Assert.That(tuning.Conceal.MarginSeconds, Is.EqualTo(0.5f));
            Assert.That(tuning.Violations.WindowSeconds, Is.EqualTo(30f));
            Assert.That(tuning.Violations.ShotRejectionLimit, Is.EqualTo(60));
            Assert.That(tuning.Violations.OverBudgetLimit, Is.EqualTo(120));
            Assert.That(tuning.Stats.WindowSeconds, Is.EqualTo(120f));
            Assert.That(tuning.Stats.HeadshotRatio, Is.EqualTo(0.6f));
            Assert.That(tuning.Stats.MinReactionMs, Is.EqualTo(120f));
            Assert.That(tuning.Stats.SnapDegreesPerTick, Is.EqualTo(45f));
            Assert.That(tuning.Stats.WallTrackSeconds, Is.EqualTo(1.5f));
            Assert.That(tuning.Evidence.Seconds, Is.EqualTo(30f));
        }

        [Test]
        public void ReadsEveryItem()
        {
            var tuning = AntiCheatTuningJson.Parse(
                "{\"conceal\": {\"enabled\": true, \"maxDistance\": 80, \"marginDistance\": 2, \"marginSeconds\": 0.25},"
                + " \"violations\": {\"enabled\": true, \"windowSeconds\": 10, \"shotRejectionLimit\": 5, \"overBudgetLimit\": 7},"
                + " \"stats\": {\"enabled\": true, \"windowSeconds\": 60, \"headshotRatio\": 0.8, \"minReactionMs\": 90,"
                + " \"snapDegreesPerTick\": 30, \"wallTrackSeconds\": 2},"
                + " \"evidence\": {\"enabled\": true, \"seconds\": 45}}",
                "anticheat.json");

            Assert.That(tuning.Conceal.Enabled, Is.True);
            Assert.That(tuning.Conceal.MaxDistance, Is.EqualTo(80f));
            Assert.That(tuning.Conceal.MarginDistance, Is.EqualTo(2f));
            Assert.That(tuning.Conceal.MarginSeconds, Is.EqualTo(0.25f));
            Assert.That(tuning.Violations.Enabled, Is.True);
            Assert.That(tuning.Violations.WindowSeconds, Is.EqualTo(10f));
            Assert.That(tuning.Violations.ShotRejectionLimit, Is.EqualTo(5));
            Assert.That(tuning.Violations.OverBudgetLimit, Is.EqualTo(7));
            Assert.That(tuning.Stats.Enabled, Is.True);
            Assert.That(tuning.Stats.WindowSeconds, Is.EqualTo(60f));
            Assert.That(tuning.Stats.HeadshotRatio, Is.EqualTo(0.8f));
            Assert.That(tuning.Stats.MinReactionMs, Is.EqualTo(90f));
            Assert.That(tuning.Stats.SnapDegreesPerTick, Is.EqualTo(30f));
            Assert.That(tuning.Stats.WallTrackSeconds, Is.EqualTo(2f));
            Assert.That(tuning.Evidence.Enabled, Is.True);
            Assert.That(tuning.Evidence.Seconds, Is.EqualTo(45f));
        }

        [Test]
        public void AnEmptyObjectIsTheDefaults()
        {
            var tuning = AntiCheatTuningJson.Parse("{}", "anticheat.json");

            Assert.That(tuning.Conceal, Is.SameAs(AntiCheatTuning.ConcealSettings.Default));
            Assert.That(tuning.Violations, Is.SameAs(AntiCheatTuning.ViolationSettings.Default));
            Assert.That(tuning.Stats, Is.SameAs(AntiCheatTuning.StatsSettings.Default));
            Assert.That(tuning.Evidence, Is.SameAs(AntiCheatTuning.EvidenceSettings.Default));
        }

        [Test]
        public void OmittedItemsUseTheirDefaults()
        {
            var tuning = AntiCheatTuningJson.Parse(
                "{\"conceal\": {\"enabled\": true}, \"violations\": {\"shotRejectionLimit\": 3}, \"stats\": {}, \"evidence\": {\"seconds\": 10}}",
                "anticheat.json");

            Assert.That(tuning.Conceal.Enabled, Is.True);
            Assert.That(tuning.Conceal.MaxDistance, Is.EqualTo(AntiCheatTuning.ConcealSettings.DefaultMaxDistance));
            Assert.That(tuning.Conceal.MarginDistance, Is.EqualTo(AntiCheatTuning.ConcealSettings.DefaultMarginDistance));
            Assert.That(tuning.Conceal.MarginSeconds, Is.EqualTo(AntiCheatTuning.ConcealSettings.DefaultMarginSeconds));
            Assert.That(tuning.Violations.Enabled, Is.False);
            Assert.That(tuning.Violations.ShotRejectionLimit, Is.EqualTo(3));
            Assert.That(tuning.Violations.WindowSeconds, Is.EqualTo(AntiCheatTuning.ViolationSettings.DefaultWindowSeconds));
            Assert.That(tuning.Violations.OverBudgetLimit, Is.EqualTo(AntiCheatTuning.ViolationSettings.DefaultOverBudgetLimit));
            Assert.That(tuning.Stats.Enabled, Is.False);
            Assert.That(tuning.Stats.HeadshotRatio, Is.EqualTo(AntiCheatTuning.StatsSettings.DefaultHeadshotRatio));
            Assert.That(tuning.Stats.MinReactionMs, Is.EqualTo(AntiCheatTuning.StatsSettings.DefaultMinReactionMs));
            Assert.That(tuning.Evidence.Enabled, Is.False);
            Assert.That(tuning.Evidence.Seconds, Is.EqualTo(10f));
        }

        [Test]
        public void ABadValueNamesTheItem()
        {
            var error = Assert.Throws<TuningJsonException>(
                () => AntiCheatTuningJson.Parse("{\"conceal\": {\"maxDistance\": 0}}", "anticheat.json"));
            Assert.That(error.Item, Is.EqualTo("conceal.maxDistance"));
            Assert.That(error.SourceName, Is.EqualTo("anticheat.json"));
            Assert.That(error.Message, Does.StartWith("anticheat JSON 'anticheat.json' item 'conceal.maxDistance' is invalid: "));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());

            Assert.That(ItemOf("{\"conceal\": {\"marginSeconds\": -1}}"), Is.EqualTo("conceal.marginSeconds"));
            Assert.That(ItemOf("{\"violations\": {\"shotRejectionLimit\": 0}}"), Is.EqualTo("violations.shotRejectionLimit"));
            Assert.That(ItemOf("{\"violations\": {\"overBudgetLimit\": 1.5}}"), Is.EqualTo("violations.overBudgetLimit"));
            Assert.That(ItemOf("{\"stats\": {\"headshotRatio\": 1.5}}"), Is.EqualTo("stats.headshotRatio"));
            Assert.That(ItemOf("{\"stats\": {\"snapDegreesPerTick\": 181}}"), Is.EqualTo("stats.snapDegreesPerTick"));
            Assert.That(ItemOf("{\"stats\": {\"wallTrackSeconds\": 0}}"), Is.EqualTo("stats.wallTrackSeconds"));
            Assert.That(ItemOf("{\"evidence\": {\"seconds\": -3}}"), Is.EqualTo("evidence.seconds"));
            Assert.That(ItemOf("{\"evidence\": {\"enabled\": 1}}"), Is.EqualTo("evidence.enabled"));
            Assert.That(ItemOf("{\"stats\": {\"windowSeconds\": \"long\"}}"), Is.EqualTo("stats.windowSeconds"));
            Assert.That(ItemOf("{\"conceal\": {\"enabled\": null}}"), Is.EqualTo("conceal.enabled"));
        }

        [Test]
        public void UnknownItemsAreRejected()
        {
            Assert.That(ItemOf("{\"esp\": {}}"), Is.EqualTo("esp"));
            Assert.That(ItemOf("{\"conceal\": {\"seconds\": 1}}"), Is.EqualTo("conceal.seconds"));
            Assert.That(ItemOf("{\"evidence\": {\"windowSeconds\": 1}}"), Is.EqualTo("evidence.windowSeconds"));
            Assert.That(ItemOf("{\"stats\": true}"), Is.EqualTo("stats"));
        }

        [Test]
        public void BrokenJsonIsRejected()
        {
            var error = Assert.Throws<TuningJsonException>(() => AntiCheatTuningJson.Parse("{\"conceal\": ", "anticheat.json"));
            Assert.That(error.Item, Is.Null);
            Assert.That(error.Message, Does.StartWith("anticheat JSON 'anticheat.json' is not valid JSON: "));

            Assert.That(
                Assert.Throws<TuningJsonException>(() => AntiCheatTuningJson.Parse("[]", null)).Message,
                Does.StartWith("anticheat JSON '(unnamed anticheat JSON)': the top level must be an object"));
            Assert.Throws<ArgumentNullException>(() => AntiCheatTuningJson.Parse(null, "anticheat.json"));
        }

        [Test]
        public void TheTuningNeedsEverySection()
        {
            var conceal = AntiCheatTuning.ConcealSettings.Default;
            var violations = AntiCheatTuning.ViolationSettings.Default;
            var stats = AntiCheatTuning.StatsSettings.Default;
            var evidence = AntiCheatTuning.EvidenceSettings.Default;

            Assert.Throws<ArgumentNullException>(() => new AntiCheatTuning(null, violations, stats, evidence));
            Assert.Throws<ArgumentNullException>(() => new AntiCheatTuning(conceal, null, stats, evidence));
            Assert.Throws<ArgumentNullException>(() => new AntiCheatTuning(conceal, violations, null, evidence));
            Assert.Throws<ArgumentNullException>(() => new AntiCheatTuning(conceal, violations, stats, null));
        }

        private static string ItemOf(string json)
        {
            return Assert.Throws<TuningJsonException>(() => AntiCheatTuningJson.Parse(json, "anticheat.json")).Item;
        }
    }
}
