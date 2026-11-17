using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MinimapJsonTests
    {
        private static readonly string[] _expectedItemNames =
        {
            "radius", "scale", "updateIntervalSeconds", "spottedSeconds", "rotateWithPlayer",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var lines = new List<string>();
            var spec = MinimapJson.Parse(
                "{\"radius\": 80, \"scale\": 1.5, \"updateIntervalSeconds\": 0.25, \"spottedSeconds\": 5, \"rotateWithPlayer\": false}",
                "minimap.json",
                lines.Add);

            Assert.That(spec.Radius, Is.EqualTo(80f));
            Assert.That(spec.Scale, Is.EqualTo(1.5f));
            Assert.That(spec.UpdateIntervalSeconds, Is.EqualTo(0.25f));
            Assert.That(spec.SpottedSeconds, Is.EqualTo(5f));
            Assert.That(spec.RotateWithPlayer, Is.False);
            Assert.That(spec.MapRadius, Is.EqualTo(120f));
            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var lines = new List<string>();
            var empty = MinimapJson.Parse("{}", "minimap.json", lines.Add);
            var partial = MinimapJson.Parse("{\"spottedSeconds\": 8}", "minimap.json", lines.Add);

            Assert.That(empty.Radius, Is.EqualTo(50f));
            Assert.That(empty.Scale, Is.EqualTo(2f));
            Assert.That(empty.UpdateIntervalSeconds, Is.EqualTo(0.1f));
            Assert.That(empty.SpottedSeconds, Is.EqualTo(3f));
            Assert.That(empty.RotateWithPlayer, Is.True);
            Assert.That(partial.SpottedSeconds, Is.EqualTo(8f));
            Assert.That(partial.Radius, Is.EqualTo(50f));
            Assert.That(partial.RotateWithPlayer, Is.True);
            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(MinimapJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(MinimapJson.ResourcePath, Is.EqualTo("Tuning/minimap"));
        }

        [Test]
        public void AnOutOfRangeValueFallsBackToItsDefaultAndIsLogged()
        {
            var lines = new List<string>();
            var spec = MinimapJson.Parse(
                "{\"radius\": 0, \"scale\": -1, \"updateIntervalSeconds\": 0.5, \"spottedSeconds\": 0, \"rotateWithPlayer\": false}",
                "minimap.json",
                lines.Add);

            Assert.That(spec.Radius, Is.EqualTo(50f));
            Assert.That(spec.Scale, Is.EqualTo(2f));
            Assert.That(spec.UpdateIntervalSeconds, Is.EqualTo(0.5f), "an item in range is kept");
            Assert.That(spec.SpottedSeconds, Is.EqualTo(3f));
            Assert.That(spec.RotateWithPlayer, Is.False, "an item in range is kept");
            Assert.That(lines, Is.EqualTo(new[]
            {
                "[Minimap] minimap.json item 'radius' is 0, which is not positive and finite; using the default 50 instead",
                "[Minimap] minimap.json item 'scale' is -1, which is not positive and finite; using the default 2 instead",
                "[Minimap] minimap.json item 'spottedSeconds' is 0, which is not positive and finite; using the default 3 instead",
            }));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var lines = new List<string>();
            var unknown = Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("{\"range\": 2}", "minimap.json", lines.Add));
            var wrongType = Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("{\"scale\": \"big\"}", "minimap.json", lines.Add));
            var wrongBool = Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("{\"rotateWithPlayer\": 1}", "minimap.json", lines.Add));
            var nullValue = Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("{\"radius\": null}", "minimap.json", lines.Add));
            var broken = Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("{\"radius\": ", "minimap.json", lines.Add));
            var notObject = Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("[1]", "minimap.json", lines.Add));

            Assert.That(unknown.Item, Is.EqualTo("range"));
            Assert.That(unknown.SourceName, Is.EqualTo("minimap.json"));
            Assert.That(unknown.Message, Does.Contain("spottedSeconds"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("scale"));
            Assert.That(wrongBool.Item, Is.EqualTo("rotateWithPlayer"));
            Assert.That(nullValue.Item, Is.EqualTo("radius"));
            Assert.That(broken.Item, Is.Null);
            Assert.That(notObject.Item, Is.Null);
            Assert.That(
                Assert.Throws<TuningJsonException>(() => MinimapJson.Parse("{\"x\": 1}", null, lines.Add)).SourceName,
                Is.EqualTo("(unnamed minimap JSON)"));
            Assert.That(lines, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => MinimapJson.Parse(null, "minimap.json", lines.Add));
            Assert.Throws<ArgumentNullException>(() => MinimapJson.Parse("{}", "minimap.json", null));
        }

        [Test]
        public void TheSpecRejectsOutOfRangeValuesByItemName()
        {
            var radius = Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapSpec(radius: 0f));
            var scale = Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapSpec(scale: float.PositiveInfinity));
            var interval = Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapSpec(updateIntervalSeconds: float.NaN));
            var spotted = Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapSpec(spottedSeconds: -1f));

            Assert.That(radius.ParamName, Is.EqualTo(MinimapJson.RadiusItem));
            Assert.That(scale.ParamName, Is.EqualTo(MinimapJson.ScaleItem));
            Assert.That(interval.ParamName, Is.EqualTo(MinimapJson.UpdateIntervalSecondsItem));
            Assert.That(spotted.ParamName, Is.EqualTo(MinimapJson.SpottedSecondsItem));
        }

        [Test]
        public void TheConfiguredLineShowsEveryValue()
        {
            Assert.That(
                MinimapLog.Configured(MinimapSpec.Default),
                Is.EqualTo("[Minimap] configured radius=50 scale=2 update=0.1s spotted=3s rotate=true"));
            Assert.That(
                MinimapLog.Configured(new MinimapSpec(80f, 1.5f, 0.25f, 5f, false)),
                Is.EqualTo("[Minimap] configured radius=80 scale=1.5 update=0.25s spotted=5s rotate=false"));
            Assert.Throws<ArgumentNullException>(() => MinimapLog.Configured(null));
        }
    }
}
