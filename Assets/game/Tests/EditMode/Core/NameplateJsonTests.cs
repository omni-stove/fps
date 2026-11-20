using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NameplateJsonTests
    {
        private static readonly string[] _expectedItemNames = { "friendlyRange", "enemyRange" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = NameplateJson.Parse("{\"friendlyRange\": 45.5, \"enemyRange\": 20}", "nameplates.json");

            Assert.That(spec.FriendlyRange, Is.EqualTo(45.5f));
            Assert.That(spec.EnemyRange, Is.EqualTo(20f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = NameplateJson.Parse("{}", "nameplates.json");
            var onlyEnemy = NameplateJson.Parse("{\"enemyRange\": 10}", "nameplates.json");

            Assert.That(empty.FriendlyRange, Is.EqualTo(60f));
            Assert.That(empty.EnemyRange, Is.EqualTo(35f));
            Assert.That(onlyEnemy.FriendlyRange, Is.EqualTo(NameplateSpec.DefaultFriendlyRange));
            Assert.That(onlyEnemy.EnemyRange, Is.EqualTo(10f));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArgumentsAndThePathIsUnderTuning()
        {
            Assert.That(NameplateJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(NameplateJson.ResourcePath, Is.EqualTo("Tuning/nameplates"));
        }

        [Test]
        public void ZeroTurnsARelationOffButANegativeRangeNamesItsItem()
        {
            var off = NameplateJson.Parse("{\"friendlyRange\": 0, \"enemyRange\": 0}", "nameplates.json");
            var negative = Assert.Throws<TuningJsonException>(() => NameplateJson.Parse("{\"enemyRange\": -1}", "nameplates.json"));

            Assert.That(off.FriendlyRange, Is.EqualTo(0f));
            Assert.That(off.EnemyRange, Is.EqualTo(0f));
            Assert.That(negative.SourceName, Is.EqualTo("nameplates.json"));
            Assert.That(negative.Item, Is.EqualTo("enemyRange"));
            Assert.That(negative.Message, Does.StartWith("nameplates JSON 'nameplates.json' item 'enemyRange' is invalid: "));
            Assert.That(negative.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());

            var friendly = Assert.Throws<TuningJsonException>(() => NameplateJson.Parse("{\"friendlyRange\": -5}", "nameplates.json"));
            Assert.That(friendly.Item, Is.EqualTo("friendlyRange"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => NameplateJson.Parse("{\"range\": 2}", "nameplates.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => NameplateJson.Parse("{\"friendlyRange\": \"far\"}", "nameplates.json"));
            var broken = Assert.Throws<TuningJsonException>(() => NameplateJson.Parse("{\"enemyRange\": ", "nameplates.json"));

            Assert.That(unknown.Item, Is.EqualTo("range"));
            Assert.That(unknown.Message, Does.Contain("friendlyRange").And.Contain("enemyRange"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("friendlyRange"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => NameplateJson.Parse(null, "nameplates.json"));
        }
    }
}
