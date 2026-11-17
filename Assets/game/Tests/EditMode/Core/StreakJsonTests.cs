using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StreakJsonTests
    {
        private const string _source = "streaks.json";

        private static readonly string[] _expectedItemNames = { "rewards", "uav", "airstrike" };
        private static readonly string[] _expectedRewardItemNames = { "id", "nameKey", "killsRequired" };
        private static readonly string[] _expectedUavItemNames = { "durationSeconds", "range" };
        private static readonly string[] _expectedAirstrikeItemNames = { "shellCount", "intervalSeconds", "spread", "explosion" };
        private static readonly string[] _expectedExplosionItemNames = { "maxDamage", "radius", "fullDamageRadius", "structureDamageAtCenter" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = StreakJson.Parse(
                "{\"rewards\": [{\"id\": \"airstrike\", \"nameKey\": \"reward.bombs\", \"killsRequired\": 4},"
                + " {\"id\": \"uav\", \"nameKey\": \"reward.radar\", \"killsRequired\": 7}],"
                + " \"uav\": {\"durationSeconds\": 12.5, \"range\": 150},"
                + " \"airstrike\": {\"shellCount\": 8, \"intervalSeconds\": 0.2, \"spread\": 3,"
                + " \"explosion\": {\"maxDamage\": 90, \"radius\": 4, \"fullDamageRadius\": 1, \"structureDamageAtCenter\": 40}}}",
                _source);

            Assert.That(spec.Rewards.Count, Is.EqualTo(2));
            Assert.That(spec.Rewards[0].Id, Is.EqualTo("airstrike"));
            Assert.That(spec.Rewards[0].DisplayNameKey, Is.EqualTo("reward.bombs"));
            Assert.That(spec.Rewards[0].KillsRequired, Is.EqualTo(4));
            Assert.That(spec.Rewards[1].Id, Is.EqualTo("uav"));
            Assert.That(spec.Rewards[1].DisplayNameKey, Is.EqualTo("reward.radar"));
            Assert.That(spec.Rewards[1].KillsRequired, Is.EqualTo(7));
            Assert.That(spec.Uav.DurationSeconds, Is.EqualTo(12.5f));
            Assert.That(spec.Uav.Range, Is.EqualTo(150f));
            Assert.That(spec.Airstrike.ShellCount, Is.EqualTo(8));
            Assert.That(spec.Airstrike.IntervalSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.Airstrike.Spread, Is.EqualTo(3f));
            Assert.That(spec.Airstrike.Explosion.Damage.MaxDamage, Is.EqualTo(90));
            Assert.That(spec.Airstrike.Explosion.Damage.Radius, Is.EqualTo(4f));
            Assert.That(spec.Airstrike.Explosion.Damage.FullDamageRadius, Is.EqualTo(1f));
            Assert.That(spec.Airstrike.Explosion.StructureDamageAtCenter, Is.EqualTo(40));
            Assert.That(spec.Airstrike.Explosion.Detonation, Is.EqualTo(DetonationTrigger.OnImpact));
            Assert.That(spec.FindReward("uav"), Is.SameAs(spec.Rewards[1]));
            Assert.That(spec.FindReward("missing"), Is.Null);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = StreakJson.Parse("{}", _source);
            var partial = StreakJson.Parse("{\"uav\": {\"range\": 50}, \"airstrike\": {\"explosion\": {\"radius\": 10}}}", _source);

            Assert.That(empty.Rewards.Count, Is.EqualTo(2));
            Assert.That(empty.Rewards[0].Id, Is.EqualTo("uav"));
            Assert.That(empty.Rewards[0].DisplayNameKey, Is.EqualTo("streak.uav"));
            Assert.That(empty.Rewards[0].KillsRequired, Is.EqualTo(3));
            Assert.That(empty.Rewards[1].Id, Is.EqualTo("airstrike"));
            Assert.That(empty.Rewards[1].DisplayNameKey, Is.EqualTo("streak.airstrike"));
            Assert.That(empty.Rewards[1].KillsRequired, Is.EqualTo(5));
            Assert.That(empty.Uav.DurationSeconds, Is.EqualTo(20f));
            Assert.That(empty.Uav.Range, Is.EqualTo(80f));
            Assert.That(empty.Airstrike.ShellCount, Is.EqualTo(5));
            Assert.That(empty.Airstrike.IntervalSeconds, Is.EqualTo(0.35f));
            Assert.That(empty.Airstrike.Spread, Is.EqualTo(6f));
            Assert.That(empty.Airstrike.Explosion.Damage.MaxDamage, Is.EqualTo(150));
            Assert.That(empty.Airstrike.Explosion.Damage.Radius, Is.EqualTo(6f));
            Assert.That(empty.Airstrike.Explosion.Damage.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(empty.Airstrike.Explosion.StructureDamageAtCenter, Is.EqualTo(100));

            Assert.That(partial.Uav.Range, Is.EqualTo(50f));
            Assert.That(partial.Uav.DurationSeconds, Is.EqualTo(20f));
            Assert.That(partial.Airstrike.ShellCount, Is.EqualTo(5));
            Assert.That(partial.Airstrike.Explosion.Damage.Radius, Is.EqualTo(10f));
            Assert.That(partial.Airstrike.Explosion.Damage.MaxDamage, Is.EqualTo(150));
            Assert.That(partial.Airstrike.Explosion.Damage.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(partial.Airstrike.Explosion.StructureDamageAtCenter, Is.EqualTo(100));
        }

        [Test]
        public void AnEmptyRewardListTurnsTheRewardsOff()
        {
            var spec = StreakJson.Parse("{\"rewards\": []}", _source);

            Assert.That(spec.Rewards, Is.Empty);
            Assert.That(spec.Uav.Range, Is.EqualTo(80f));
        }

        [Test]
        public void ARewardWithoutANameKeyHasAnEmptyKey()
        {
            var spec = StreakJson.Parse("{\"rewards\": [{\"id\": \"uav\", \"killsRequired\": 2}]}", _source);

            Assert.That(spec.Rewards[0].DisplayNameKey, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(StreakJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(StreakJson.RewardItemNames, Is.EqualTo(_expectedRewardItemNames));
            Assert.That(StreakJson.UavItemNames, Is.EqualTo(_expectedUavItemNames));
            Assert.That(StreakJson.AirstrikeItemNames, Is.EqualTo(_expectedAirstrikeItemNames));
            Assert.That(StreakJson.ExplosionItemNames, Is.EqualTo(_expectedExplosionItemNames));
            Assert.That(StreakJson.ResourcePath, Is.EqualTo("Tuning/streaks"));
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var kills = Reject("{\"rewards\": [{\"id\": \"uav\", \"killsRequired\": 0}]}");
            var range = Reject("{\"uav\": {\"range\": 0}}");
            var duration = Reject("{\"uav\": {\"durationSeconds\": -1}}");
            var shells = Reject("{\"airstrike\": {\"shellCount\": 0}}");
            var interval = Reject("{\"airstrike\": {\"intervalSeconds\": -0.1}}");
            var spread = Reject("{\"airstrike\": {\"spread\": -1}}");
            var damage = Reject("{\"airstrike\": {\"explosion\": {\"maxDamage\": -1}}}");
            var full = Reject("{\"airstrike\": {\"explosion\": {\"radius\": 2, \"fullDamageRadius\": 2}}}");
            var structure = Reject("{\"airstrike\": {\"explosion\": {\"structureDamageAtCenter\": -5}}}");

            Assert.That(kills.SourceName, Is.EqualTo(_source));
            Assert.That(kills.Item, Is.EqualTo("rewards[0].killsRequired"));
            Assert.That(kills.Message, Does.StartWith("streak JSON 'streaks.json' item 'rewards[0].killsRequired' is invalid: "));
            Assert.That(kills.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(range.Item, Is.EqualTo("uav.range"));
            Assert.That(duration.Item, Is.EqualTo("uav.durationSeconds"));
            Assert.That(shells.Item, Is.EqualTo("airstrike.shellCount"));
            Assert.That(interval.Item, Is.EqualTo("airstrike.intervalSeconds"));
            Assert.That(spread.Item, Is.EqualTo("airstrike.spread"));
            Assert.That(damage.Item, Is.EqualTo("airstrike.explosion.maxDamage"));
            Assert.That(full.Item, Is.EqualTo("airstrike.explosion.fullDamageRadius"));
            Assert.That(structure.Item, Is.EqualTo("airstrike.explosion.structureDamageAtCenter"));
        }

        [Test]
        public void AnUnknownOrRepeatedRewardIdIsRejected()
        {
            var unknown = Reject("{\"rewards\": [{\"id\": \"nuke\", \"killsRequired\": 25}]}");
            var twice = Reject("{\"rewards\": [{\"id\": \"uav\", \"killsRequired\": 3}, {\"id\": \"uav\", \"killsRequired\": 6}]}");
            var blank = Reject("{\"rewards\": [{\"id\": \" \", \"killsRequired\": 3}]}");

            Assert.That(unknown.Item, Is.EqualTo("rewards"));
            Assert.That(unknown.Message, Does.Contain("nuke"));
            Assert.That(unknown.Message, Does.Contain("uav, airstrike"), "lists the known ids");
            Assert.That(unknown.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(twice.Item, Is.EqualTo("rewards"));
            Assert.That(twice.Message, Does.Contain("twice"));
            Assert.That(blank.Item, Is.EqualTo("rewards[0].id"));
        }

        [Test]
        public void AMissingRewardItemIsRejected()
        {
            var noId = Reject("{\"rewards\": [{\"killsRequired\": 3}]}");
            var noKills = Reject("{\"rewards\": [{\"id\": \"uav\"}]}");

            Assert.That(noId.Item, Is.EqualTo("rewards[0].id"));
            Assert.That(noKills.Item, Is.EqualTo("rewards[0].killsRequired"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Reject("{\"nuke\": {}}");
            var unknownInReward = Reject("{\"rewards\": [{\"id\": \"uav\", \"killsRequired\": 3, \"icon\": \"x\"}]}");
            var detonation = Reject("{\"airstrike\": {\"explosion\": {\"detonation\": \"OnImpact\"}}}");
            var fraction = Reject("{\"rewards\": [{\"id\": \"uav\", \"killsRequired\": 2.5}]}");
            var wrongType = Reject("{\"uav\": {\"range\": \"far\"}}");
            var notArray = Reject("{\"rewards\": {}}");
            var notObject = Reject("{\"uav\": 3}");
            var nullValue = Reject("{\"airstrike\": null}");
            var broken = Reject("{\"uav\": ");
            var topArray = Reject("[1]");

            Assert.That(unknown.Item, Is.EqualTo("nuke"));
            Assert.That(unknown.Message, Does.Contain("rewards"), "lists the known items");
            Assert.That(unknownInReward.Item, Is.EqualTo("rewards[0].icon"));
            Assert.That(detonation.Item, Is.EqualTo("airstrike.explosion.detonation"));
            Assert.That(fraction.Item, Is.EqualTo("rewards[0].killsRequired"));
            Assert.That(wrongType.Item, Is.EqualTo("uav.range"));
            Assert.That(notArray.Item, Is.EqualTo("rewards"));
            Assert.That(notObject.Item, Is.EqualTo("uav"));
            Assert.That(nullValue.Item, Is.EqualTo("airstrike"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.SourceName, Is.EqualTo(_source));
            Assert.That(topArray.Item, Is.Null);
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StreakJson.Parse("{\"x\": 1}", null)).SourceName,
                Is.EqualTo("(unnamed streak JSON)"));
            Assert.Throws<ArgumentNullException>(() => StreakJson.Parse(null, _source));
        }

        [Test]
        public void TheSpecChecksItsValues()
        {
            Assert.Throws<ArgumentException>(() => new StreakSpec(new StreakReward[] { null }));
            Assert.Throws<ArgumentException>(() => new StreakReward(null, "streak.uav", 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakReward("uav", "streak.uav", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakUavSpec(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakAirstrikeSpec(intervalSeconds: float.NaN));
            Assert.That(new StreakReward(" uav ", " streak.uav ", 3).Id, Is.EqualTo("uav"));
            Assert.That(new StreakReward(" uav ", " streak.uav ", 3).DisplayNameKey, Is.EqualTo("streak.uav"));
            Assert.That(new StreakAirstrikeSpec(spread: 0f).Spread, Is.EqualTo(0f));
            Assert.That(new StreakAirstrikeSpec(intervalSeconds: 0f).IntervalSeconds, Is.EqualTo(0f));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => StreakJson.Parse(json, _source));
        }
    }
}
