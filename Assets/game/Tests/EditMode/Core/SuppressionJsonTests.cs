using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SuppressionJsonTests
    {
        private static readonly string[] _expectedItemNames =
        {
            "nearMissRadius", "buildPerNearMiss", "decayDelaySeconds", "decayPerSecond",
            "suppressedLevel", "releasedLevel", "maxSpreadDegrees", "maxCrosshairSpread", "maxVignetteAlpha",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = SuppressionJson.Parse(
                "{\"nearMissRadius\": 3, \"buildPerNearMiss\": 0.5, \"decayDelaySeconds\": 2, \"decayPerSecond\": 0.1,"
                + " \"suppressedLevel\": 0.3, \"releasedLevel\": 0.05, \"maxSpreadDegrees\": 10,"
                + " \"maxCrosshairSpread\": 0.9, \"maxVignetteAlpha\": 1}",
                "suppression.json");

            Assert.That(spec.NearMissRadius, Is.EqualTo(3f));
            Assert.That(spec.BuildPerNearMiss, Is.EqualTo(0.5f));
            Assert.That(spec.DecayDelaySeconds, Is.EqualTo(2f));
            Assert.That(spec.DecayPerSecond, Is.EqualTo(0.1f));
            Assert.That(spec.SuppressedLevel, Is.EqualTo(0.3f));
            Assert.That(spec.ReleasedLevel, Is.EqualTo(0.05f));
            Assert.That(spec.MaxSpreadDegrees, Is.EqualTo(10f));
            Assert.That(spec.MaxCrosshairSpread, Is.EqualTo(0.9f));
            Assert.That(spec.MaxVignetteAlpha, Is.EqualTo(1f));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = SuppressionJson.Parse("{}", "suppression.json");
            var partial = SuppressionJson.Parse("{\"nearMissRadius\": 2.5}", "suppression.json");

            Assert.That(empty.NearMissRadius, Is.EqualTo(1.5f));
            Assert.That(empty.BuildPerNearMiss, Is.EqualTo(0.2f));
            Assert.That(empty.DecayDelaySeconds, Is.EqualTo(0.75f));
            Assert.That(empty.DecayPerSecond, Is.EqualTo(0.4f));
            Assert.That(empty.SuppressedLevel, Is.EqualTo(0.5f));
            Assert.That(empty.ReleasedLevel, Is.EqualTo(0.2f));
            Assert.That(empty.MaxSpreadDegrees, Is.EqualTo(4f));
            Assert.That(empty.MaxCrosshairSpread, Is.EqualTo(0.6f));
            Assert.That(empty.MaxVignetteAlpha, Is.EqualTo(0.65f));
            Assert.That(partial.NearMissRadius, Is.EqualTo(2.5f));
            Assert.That(partial.SuppressedLevel, Is.EqualTo(0.5f));
            Assert.That(partial.MaxVignetteAlpha, Is.EqualTo(0.65f));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(SuppressionJson.ItemNames, Is.EqualTo(_expectedItemNames));
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var radius = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"nearMissRadius\": 0}", "suppression.json"));
            var released = Assert.Throws<TuningJsonException>(
                () => SuppressionJson.Parse("{\"suppressedLevel\": 0.4, \"releasedLevel\": 0.4}", "suppression.json"));
            var vignette = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"maxVignetteAlpha\": 1.5}", "suppression.json"));

            Assert.That(radius.SourceName, Is.EqualTo("suppression.json"));
            Assert.That(radius.Item, Is.EqualTo("nearMissRadius"));
            Assert.That(radius.Message, Does.StartWith("suppression JSON 'suppression.json' item 'nearMissRadius' is invalid: "));
            Assert.That(radius.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(released.Item, Is.EqualTo("releasedLevel"));
            Assert.That(vignette.Item, Is.EqualTo("maxVignetteAlpha"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"radius\": 2}", "suppression.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"decayPerSecond\": \"fast\"}", "suppression.json"));
            var nullValue = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"decayPerSecond\": null}", "suppression.json"));
            var broken = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"nearMissRadius\": ", "suppression.json"));
            var notObject = Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("[1]", "suppression.json"));

            Assert.That(unknown.Item, Is.EqualTo("radius"));
            Assert.That(unknown.Message, Does.Contain("nearMissRadius"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("decayPerSecond"));
            Assert.That(nullValue.Item, Is.EqualTo("decayPerSecond"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.SourceName, Is.EqualTo("suppression.json"));
            Assert.That(notObject.Item, Is.Null);
            Assert.That(
                Assert.Throws<TuningJsonException>(() => SuppressionJson.Parse("{\"x\": 1}", null)).SourceName,
                Is.EqualTo("(unnamed suppression JSON)"));
            Assert.Throws<ArgumentNullException>(() => SuppressionJson.Parse(null, "suppression.json"));
        }
    }
}
