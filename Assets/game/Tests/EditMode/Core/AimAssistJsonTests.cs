using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AimAssistJsonTests
    {
        private static readonly string[] _expectedItemNames =
        {
            "slowdownDegrees", "slowdownScale", "magnetismDegrees", "magnetismDegreesPerSecond", "magnetismSeconds", "maxDistance",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = AimAssistJson.Parse(
                "{\"slowdownDegrees\": 8, \"slowdownScale\": 0.25, \"magnetismDegrees\": 4.5,"
                + " \"magnetismDegreesPerSecond\": 45, \"magnetismSeconds\": 0.6, \"maxDistance\": 80}",
                "aim-assist.json");

            Assert.That(spec.SlowdownDegrees, Is.EqualTo(8f));
            Assert.That(spec.SlowdownScale, Is.EqualTo(0.25f));
            Assert.That(spec.MagnetismDegrees, Is.EqualTo(4.5f));
            Assert.That(spec.MagnetismDegreesPerSecond, Is.EqualTo(45f));
            Assert.That(spec.MagnetismSeconds, Is.EqualTo(0.6f));
            Assert.That(spec.MaxDistance, Is.EqualTo(80f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = AimAssistJson.Parse("{}", "aim-assist.json");
            var partial = AimAssistJson.Parse("{\"maxDistance\": 30}", "aim-assist.json");

            Assert.That(empty.SlowdownDegrees, Is.EqualTo(5f));
            Assert.That(empty.SlowdownScale, Is.EqualTo(0.5f));
            Assert.That(empty.MagnetismDegrees, Is.EqualTo(3f));
            Assert.That(empty.MagnetismDegreesPerSecond, Is.EqualTo(30f));
            Assert.That(empty.MagnetismSeconds, Is.EqualTo(0.3f));
            Assert.That(empty.MaxDistance, Is.EqualTo(50f));
            Assert.That(partial.MaxDistance, Is.EqualTo(30f));
            Assert.That(partial.SlowdownDegrees, Is.EqualTo(5f));
            Assert.That(partial.MagnetismSeconds, Is.EqualTo(0.3f));
        }

        [Test]
        public void TheDefaultSpecHoldsTheDefaultConstants()
        {
            var spec = AimAssistSpec.Default;

            Assert.That(spec.SlowdownDegrees, Is.EqualTo(AimAssistSpec.DefaultSlowdownDegrees));
            Assert.That(spec.SlowdownScale, Is.EqualTo(AimAssistSpec.DefaultSlowdownScale));
            Assert.That(spec.MagnetismDegrees, Is.EqualTo(AimAssistSpec.DefaultMagnetismDegrees));
            Assert.That(spec.MagnetismDegreesPerSecond, Is.EqualTo(AimAssistSpec.DefaultMagnetismDegreesPerSecond));
            Assert.That(spec.MagnetismSeconds, Is.EqualTo(AimAssistSpec.DefaultMagnetismSeconds));
            Assert.That(spec.MaxDistance, Is.EqualTo(AimAssistSpec.DefaultMaxDistance));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(AimAssistJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void ZeroTurnsAPartOff()
        {
            var spec = AimAssistJson.Parse(
                "{\"slowdownDegrees\": 0, \"slowdownScale\": 0, \"magnetismDegrees\": 0, \"magnetismDegreesPerSecond\": 0, \"magnetismSeconds\": 0}",
                "aim-assist.json");

            Assert.That(spec.SlowdownDegrees, Is.EqualTo(0f));
            Assert.That(spec.SlowdownScale, Is.EqualTo(0f));
            Assert.That(spec.MagnetismDegrees, Is.EqualTo(0f));
            Assert.That(spec.MagnetismDegreesPerSecond, Is.EqualTo(0f));
            Assert.That(spec.MagnetismSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var distance = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"maxDistance\": 0}", "aim-assist.json"));
            var slowdown = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"slowdownDegrees\": 91}", "aim-assist.json"));
            var scale = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"slowdownScale\": 1.5}", "aim-assist.json"));
            var magnetism = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"magnetismDegrees\": -1}", "aim-assist.json"));
            var rate = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"magnetismDegreesPerSecond\": -5}", "aim-assist.json"));
            var seconds = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"magnetismSeconds\": -0.1}", "aim-assist.json"));

            Assert.That(distance.SourceName, Is.EqualTo("aim-assist.json"));
            Assert.That(distance.Item, Is.EqualTo("maxDistance"));
            Assert.That(distance.Message, Does.StartWith("aim assist JSON 'aim-assist.json' item 'maxDistance' is invalid: "));
            Assert.That(distance.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(slowdown.Item, Is.EqualTo("slowdownDegrees"));
            Assert.That(scale.Item, Is.EqualTo("slowdownScale"));
            Assert.That(magnetism.Item, Is.EqualTo("magnetismDegrees"));
            Assert.That(rate.Item, Is.EqualTo("magnetismDegreesPerSecond"));
            Assert.That(seconds.Item, Is.EqualTo("magnetismSeconds"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"range\": 2}", "aim-assist.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"slowdownScale\": \"half\"}", "aim-assist.json"));
            var nullValue = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"slowdownScale\": null}", "aim-assist.json"));
            var broken = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"maxDistance\": ", "aim-assist.json"));
            var notObject = Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("[1]", "aim-assist.json"));

            Assert.That(unknown.Item, Is.EqualTo("range"));
            Assert.That(unknown.Message, Does.Contain("maxDistance"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("slowdownScale"));
            Assert.That(nullValue.Item, Is.EqualTo("slowdownScale"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.SourceName, Is.EqualTo("aim-assist.json"));
            Assert.That(notObject.Item, Is.Null);
            Assert.That(
                Assert.Throws<TuningJsonException>(() => AimAssistJson.Parse("{\"x\": 1}", null)).SourceName,
                Is.EqualTo("(unnamed aim assist JSON)"));
            Assert.Throws<ArgumentNullException>(() => AimAssistJson.Parse(null, "aim-assist.json"));
        }
    }
}
