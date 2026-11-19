using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class BattleRoyaleJsonTests
    {
        [Test]
        public void AnEmptyFileReadsTheCoreDefaults()
        {
            var spec = BattleRoyaleJson.Parse("{}", "memory");

            Assert.That(spec.SquadCount, Is.EqualTo(BattleRoyaleSession.DefaultSquadCount));
            Assert.That(spec.SquadSize, Is.EqualTo(BattleRoyaleSession.DefaultSquadSize));
            Assert.That(spec.TimeLimitSeconds, Is.Zero);
            Assert.That(spec.HasTimeLimit, Is.False);
            Assert.That(spec.BotSearchRadius, Is.EqualTo(BattleRoyaleBotPlanner.DefaultSearchRadius));
            Assert.That(spec.BotPlanIntervalSeconds, Is.EqualTo(BattleRoyaleSpec.DefaultBotPlanIntervalSeconds));
            Assert.That(spec.Zone, Is.SameAs(ZoneSpec.Default));
            Assert.That(spec.SupplyDrops, Is.SameAs(SupplyDropSpec.Default));
            Assert.That(spec.Redeploy, Is.SameAs(RedeploySpec.Default));
        }

        [Test]
        public void TheSectionsReadTheSameItemsAsTheirOwnFiles()
        {
            var spec = BattleRoyaleJson.Parse(
                "{\"squadCount\": 6, \"squadSize\": 3, \"timeLimitSeconds\": 900, \"botSearchRadius\": 40, \"botPlanIntervalSeconds\": 1,"
                + " \"zone\": {\"damageIntervalSeconds\": 0.5, \"seed\": 7, \"phases\": ["
                + "{\"waitSeconds\": 10, \"shrinkSeconds\": 5, \"radius\": 50, \"damagePerSecond\": 2},"
                + "{\"waitSeconds\": 0, \"shrinkSeconds\": 5, \"radius\": 10, \"damagePerSecond\": 6}]},"
                + " \"supplyDrops\": {\"tableId\": \"crate\", \"stages\": [{\"count\": 4, \"intervalSeconds\": 2}]},"
                + " \"redeploy\": {\"charges\": 3, \"lastPhaseIndex\": 0, \"delaySeconds\": 2}}",
                "memory");

            Assert.That(spec.SquadCount, Is.EqualTo(6));
            Assert.That(spec.SquadSize, Is.EqualTo(3));
            Assert.That(spec.HasTimeLimit, Is.True);
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(900f));
            Assert.That(spec.BotSearchRadius, Is.EqualTo(40f));
            Assert.That(spec.BotPlanIntervalSeconds, Is.EqualTo(1f));
            Assert.That(spec.Zone.DamageIntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.Zone.Seed, Is.EqualTo(7));
            Assert.That(spec.Zone.Phases.Count, Is.EqualTo(2));
            Assert.That(spec.Zone.Phases[1].WaitSeconds, Is.Zero);
            Assert.That(spec.Zone.Phases[1].Radius, Is.EqualTo(10f));
            Assert.That(spec.Zone.Phases[1].DamagePerSecond, Is.EqualTo(6f));
            Assert.That(spec.SupplyDrops.TableId, Is.EqualTo("crate"));
            Assert.That(spec.SupplyDrops.Stages[0].Count, Is.EqualTo(4));
            Assert.That(spec.SupplyDrops.Stages[0].DelaySeconds, Is.Zero);
            Assert.That(spec.SupplyDrops.FallSpeed, Is.EqualTo(SupplyDropSpec.DefaultFallSpeed));
            Assert.That(spec.Redeploy.Charges, Is.EqualTo(3));
            Assert.That(spec.Redeploy.LastPhaseIndex, Is.Zero);
            Assert.That(spec.Redeploy.DelaySeconds, Is.EqualTo(2f));
        }

        [TestCase("{\"squadCount\": 1}", "squadCount")]
        [TestCase("{\"squadCount\": 257}", "squadCount")]
        [TestCase("{\"squadSize\": 0}", "squadSize")]
        [TestCase("{\"timeLimitSeconds\": -1}", "timeLimitSeconds")]
        [TestCase("{\"botPlanIntervalSeconds\": 0}", "botPlanIntervalSeconds")]
        [TestCase("{\"zone\": {\"phases\": []}}", "zone.phases")]
        [TestCase("{\"zone\": {\"phases\": [{\"waitSeconds\": 1, \"shrinkSeconds\": 1, \"radius\": 5}]}}", "zone.phases[0].damagePerSecond")]
        [TestCase("{\"zone\": {\"phases\": [{\"waitSeconds\": 1, \"shrinkSeconds\": 1, \"radius\": 0, \"damagePerSecond\": 1}]}}", "zone.phases[0].radius")]
        [TestCase("{\"zone\": {\"phases\": [{\"waitSeconds\": 1, \"shrinkSeconds\": 1, \"radius\": 5, \"damagePerSecond\": 1},"
            + " {\"waitSeconds\": 1, \"shrinkSeconds\": 1, \"radius\": 9, \"damagePerSecond\": 1}]}}", "zone.phases")]
        [TestCase("{\"zone\": {\"damageIntervalSeconds\": 0}}", "zone.damageIntervalSeconds")]
        [TestCase("{\"supplyDrops\": {\"placeFraction\": 2}}", "supplyDrops.placeFraction")]
        [TestCase("{\"redeploy\": {\"charges\": -1}}", "redeploy.charges")]
        [TestCase("{\"zones\": {}}", "zones")]
        [TestCase("{\"zone\": {\"shrink\": 1}}", "zone.shrink")]
        public void ABadItemIsRefusedWithItsPath(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => BattleRoyaleJson.Parse(text, "battle-royale.json"));

            Assert.That(e.Item, Is.EqualTo(item), e.Message);
        }

        [Test]
        public void TheZoneFileReadsAlone()
        {
            var spec = ZoneJson.Parse("{\"phases\": [{\"waitSeconds\": 1, \"shrinkSeconds\": 2, \"radius\": 3, \"damagePerSecond\": 4}]}", "zone.json");

            Assert.That(spec.Phases.Count, Is.EqualTo(1));
            Assert.That(spec.Phases[0].ShrinkSeconds, Is.EqualTo(2f));
            Assert.That(spec.DamageIntervalSeconds, Is.EqualTo(ZoneSpec.DefaultDamageIntervalSeconds));
            Assert.That(spec.Seed, Is.Null);
        }

        [Test]
        public void TheDefaultZoneIsTheOfflineShrinkingZoneDefault()
        {
            var phases = ZoneSpec.Default.Phases;

            Assert.That(phases.Count, Is.EqualTo(3));
            Assert.That(phases[0].Radius, Is.EqualTo(35f));
            Assert.That(phases[2].DamagePerSecond, Is.EqualTo(8f));
            Assert.Throws<ArgumentException>(() => new ZoneSpec(Array.Empty<ZonePhase>()));
        }

        [Test]
        public void TheSplitReadersStillReadTheirOwnFiles()
        {
            Assert.That(SupplyDropJson.Parse("{\"reach\": 3}", "supply-drops.json").Reach, Is.EqualTo(3f));
            Assert.That(RedeployJson.Parse("{\"charges\": 2}", "redeploy.json").Charges, Is.EqualTo(2));
        }
    }
}
