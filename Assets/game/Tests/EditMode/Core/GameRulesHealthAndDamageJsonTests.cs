using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GameRulesHealthAndDamageJsonTests
    {
        private const string _source = "Rules/HealthAndDamage.json";

        [Test]
        public void DefaultsAreTheCurrentBehaviour()
        {
            Assert.That(GameRules.Default.Actions.ReviveRange, Is.EqualTo(1.5f));
            Assert.That(GameRules.Default.Actions.ReviveRange, Is.EqualTo(ReviveReach.DefaultRange));
            Assert.That(GameRules.Default.PlayerMaxHealth, Is.EqualTo(100));
            Assert.That(GameRules.Default.ZoneDamage, Is.SameAs(ZoneDamageMultipliers.Default));
            Assert.That(GameRules.Default.ZoneDamage.Head, Is.EqualTo(2.0));
            Assert.That(GameRules.Default.ZoneDamage.Body, Is.EqualTo(1.0));
            Assert.That(GameRules.Default.ZoneDamage.Limb, Is.EqualTo(0.75));
            Assert.That(GameRules.Default.ZoneDamage.WeakPoint, Is.EqualTo(2.0));
        }

        [Test]
        public void OmittedItemsKeepTheDefaults()
        {
            var rules = GameRulesJson.Parse("{}", _source);

            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(ReviveReach.DefaultRange));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(GameRules.DefaultPlayerMaxHealth));
            AssertSameZoneDamage(rules.ZoneDamage, ZoneDamageMultipliers.Default);
        }

        [Test]
        public void OmittedItemsInsideTheNestedObjectsKeepTheDefaults()
        {
            var rules = GameRulesJson.Parse("{\"actions\": {\"cover\": \"On\"}, \"zoneDamage\": {\"head\": 3}}", _source);

            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(ReviveReach.DefaultRange));
            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(3.0));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(1.0));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0.75));
            Assert.That(rules.ZoneDamage.WeakPoint, Is.EqualTo(2.0));
        }

        [Test]
        public void ReadsTheWrittenValues()
        {
            var rules = GameRulesJson.Parse(
                "{\"actions\": {\"reviveRange\": 2.5}, \"playerMaxHealth\": 150, \"zoneDamage\": {\"head\": 1.4, \"body\": 1.1, \"limb\": 0.9}}",
                _source);

            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(2.5f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(150));
            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(1.4));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(1.1));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0.9));
            Assert.That(rules.ZoneDamage.WeakPoint, Is.EqualTo(ZoneDamageMultipliers.Default.WeakPoint), "weak point is not a rule item");
        }

        [Test]
        public void ZeroMultipliersAreAllowed()
        {
            var rules = GameRulesJson.Parse("{\"zoneDamage\": {\"head\": 0, \"body\": 0, \"limb\": 0}}", _source);

            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(0.0));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(0.0));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0.0));
        }

        [Test]
        public void WritesTheItems()
        {
            var written = GameRulesJson.Write(GameRules.Default);

            Assert.That(written, Does.Contain("\"reviveRange\": 1.5"));
            Assert.That(written, Does.Contain("\"playerMaxHealth\": 100"));
            Assert.That(written, Does.Contain("\"zoneDamage\": {"));
            Assert.That(written, Does.Contain("\"head\": 2"));
            Assert.That(written, Does.Contain("\"body\": 1"));
            Assert.That(written, Does.Contain("\"limb\": 0.75"));
            Assert.That(written, Does.Not.Contain("weakPoint"));
        }

        [Test]
        public void WriteThenParseGivesTheSameValues()
        {
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithReviveRange(2.5f))
                .WithPlayerMaxHealth(175)
                .WithZoneDamage(new ZoneDamageMultipliers(1.4, 1.1, 0.1, ZoneDamageMultipliers.Default.WeakPoint));

            var written = GameRulesJson.Write(rules);
            var read = GameRulesJson.Parse(written, _source);

            Assert.That(read.Actions.ReviveRange, Is.EqualTo(2.5f));
            Assert.That(read.PlayerMaxHealth, Is.EqualTo(175));
            AssertSameZoneDamage(read.ZoneDamage, rules.ZoneDamage);
            Assert.That(GameRulesJson.Write(read), Is.EqualTo(written), "writing is stable");
        }

        [Test]
        public void ParseOverBaseRulesKeepsTheBaseValues()
        {
            var baseRules = GameRules.Default
                .WithActions(ActionRules.Default.WithReviveRange(3f))
                .WithPlayerMaxHealth(200)
                .WithZoneDamage(new ZoneDamageMultipliers(1.5, 1.0, 0.5, 4.0));

            var rules = GameRulesJson.Parse("{\"zoneDamage\": {\"body\": 0.8}}", _source, baseRules);

            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(3f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(200));
            AssertSameZoneDamage(rules.ZoneDamage, new ZoneDamageMultipliers(1.5, 0.8, 0.5, 4.0));
        }

        [TestCase("{\"actions\": {\"reviveRange\": 0}}", "actions.reviveRange")]
        [TestCase("{\"actions\": {\"reviveRange\": -1}}", "actions.reviveRange")]
        [TestCase("{\"actions\": {\"reviveRange\": 1e39}}", "actions.reviveRange")]
        [TestCase("{\"actions\": {\"reviveRange\": \"2\"}}", "actions.reviveRange")]
        [TestCase("{\"playerMaxHealth\": 0}", "playerMaxHealth")]
        [TestCase("{\"playerMaxHealth\": -5}", "playerMaxHealth")]
        [TestCase("{\"playerMaxHealth\": 100.5}", "playerMaxHealth")]
        [TestCase("{\"playerMaxHealth\": null}", "playerMaxHealth")]
        [TestCase("{\"zoneDamage\": {\"head\": -0.5}}", "zoneDamage.head")]
        [TestCase("{\"zoneDamage\": {\"body\": -1}}", "zoneDamage.body")]
        [TestCase("{\"zoneDamage\": {\"limb\": 1e400}}", "zoneDamage.limb")]
        [TestCase("{\"zoneDamage\": {\"head\": true}}", "zoneDamage.head")]
        [TestCase("{\"zoneDamage\": {\"weakPoint\": 3}}", "zoneDamage.weakPoint")]
        [TestCase("{\"zoneDamage\": 2}", "zoneDamage")]
        [TestCase("{\"zoneDamage\": null}", "zoneDamage")]
        [TestCase("{\"head\": 2}", "head")]
        public void InvalidValueIsRejectedWithTheFileAndItemName(string json, string item)
        {
            var e = Assert.Throws<GameRulesJsonException>(() => GameRulesJson.Parse(json, _source));

            Assert.That(e.SourceName, Is.EqualTo(_source));
            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain(_source));
            Assert.That(e.Message, Does.Contain("'" + item + "'"));
        }

        [Test]
        public void ConstructorsRejectInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionRules.Default.WithReviveRange(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionRules.Default.WithReviveRange(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ActionRules.Default.WithReviveRange(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameRules.Default.WithPlayerMaxHealth(0));
            Assert.Throws<ArgumentNullException>(() => GameRules.Default.WithZoneDamage(null));
        }

        [Test]
        public void WithersChangeOnlyTheirItem()
        {
            var actions = ActionRules.Default.WithReviveRange(4f);
            Assert.That(actions.ReviveRange, Is.EqualTo(4f));
            Assert.That(actions.Jump, Is.EqualTo(ActionRules.Default.Jump));
            Assert.That(actions.WithCover(Cover.On).ReviveRange, Is.EqualTo(4f), "other withers keep the range");

            var rules = GameRules.Default.WithPlayerMaxHealth(250);
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(250));
            Assert.That(rules.WithViewMode(ViewMode.ThirdPerson).PlayerMaxHealth, Is.EqualTo(250), "other withers keep the health");

            var zones = new ZoneDamageMultipliers(1.2, 1.0, 0.6, 2.0);
            Assert.That(rules.WithZoneDamage(zones).ZoneDamage, Is.SameAs(zones));
            Assert.That(rules.WithZoneDamage(zones).WithMaxWeapons(2).ZoneDamage, Is.SameAs(zones), "other withers keep the multipliers");
        }

        private static void AssertSameZoneDamage(ZoneDamageMultipliers actual, ZoneDamageMultipliers expected)
        {
            Assert.That(actual.Head, Is.EqualTo(expected.Head), "zoneDamage.head");
            Assert.That(actual.Body, Is.EqualTo(expected.Body), "zoneDamage.body");
            Assert.That(actual.Limb, Is.EqualTo(expected.Limb), "zoneDamage.limb");
            Assert.That(actual.WeakPoint, Is.EqualTo(expected.WeakPoint), "weakPoint");
        }
    }
}
