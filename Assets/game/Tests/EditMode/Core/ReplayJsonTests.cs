using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ReplayJsonTests
    {
        private static readonly string[] _expectedItemNames = { "freeSpeed" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = ReplayJson.Parse("{\"freeSpeed\": 12.5}", "replay.json");

            Assert.That(spec.FreeSpeed, Is.EqualTo(12.5f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = ReplayJson.Parse("{}", "replay.json");

            Assert.That(empty.FreeSpeed, Is.EqualTo(6f));
            Assert.That(ReplaySpec.Default.FreeSpeed, Is.EqualTo(ReplaySpec.DefaultFreeSpeed));
            Assert.That(ReplaySpec.DefaultFreeSpeed, Is.EqualTo(DeathCamFraming.DefaultFreeSpeed));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(ReplayJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void ZeroStopsTheCameraButANegativeSpeedNamesItsItem()
        {
            var still = ReplayJson.Parse("{\"freeSpeed\": 0}", "replay.json");
            var negative = Assert.Throws<TuningJsonException>(() => ReplayJson.Parse("{\"freeSpeed\": -1}", "replay.json"));

            Assert.That(still.FreeSpeed, Is.EqualTo(0f));
            Assert.That(negative.SourceName, Is.EqualTo("replay.json"));
            Assert.That(negative.Item, Is.EqualTo("freeSpeed"));
            Assert.That(negative.Message, Does.StartWith("replay JSON 'replay.json' item 'freeSpeed' is invalid: "));
            Assert.That(negative.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => ReplayJson.Parse("{\"speed\": 2}", "replay.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => ReplayJson.Parse("{\"freeSpeed\": \"fast\"}", "replay.json"));
            var broken = Assert.Throws<TuningJsonException>(() => ReplayJson.Parse("{\"freeSpeed\": ", "replay.json"));

            Assert.That(unknown.Item, Is.EqualTo("speed"));
            Assert.That(unknown.Message, Does.Contain("freeSpeed"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("freeSpeed"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => ReplayJson.Parse(null, "replay.json"));
        }
    }
}
