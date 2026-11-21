using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GraphicsJsonTests
    {
        private static readonly string[] _expectedItemNames = { "vSync", "frameRateLimit", "qualityLevel", "frameRateChoices", "headlessFrameRate" };
        private static readonly int[] _choices = { 45, 90 };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = GraphicsJson.Parse(
                "{\"vSync\": true, \"frameRateLimit\": 90, \"qualityLevel\": 0, \"frameRateChoices\": [45, 90], \"headlessFrameRate\": 30}",
                "graphics.json");

            Assert.That(spec.Defaults, Is.EqualTo(new GraphicsSettings(ScreenResolution.Native, 0, true, 90)));
            Assert.That(spec.FrameRateChoices, Is.EqualTo(_choices));
            Assert.That(spec.HeadlessFrameRate, Is.EqualTo(30));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = GraphicsJson.Parse("{}", "graphics.json");

            Assert.That(empty.Defaults, Is.EqualTo(GraphicsSettings.Default));
            Assert.That(empty.FrameRateChoices, Is.EqualTo(GraphicsSpec.DefaultFrameRateChoices));
            Assert.That(empty.HeadlessFrameRate, Is.EqualTo(GraphicsSpec.DefaultHeadlessFrameRate));
            Assert.That(GraphicsSpec.Default.HeadlessFrameRate, Is.EqualTo(60));
            Assert.That(GraphicsSpec.Default.FrameRateChoices, Is.EqualTo(new[] { 30, 60, 120, 144, 240 }));
        }

        [Test]
        public void AnEmptyChoiceListOffersOnlyNoLimit()
        {
            var spec = GraphicsJson.Parse("{\"frameRateChoices\": []}", "graphics.json");

            Assert.That(spec.FrameRateChoices, Is.Empty);
        }

        [Test]
        public void TheItemNamesAndThePathAreFixed()
        {
            Assert.That(GraphicsJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(GraphicsJson.ResourcePath, Is.EqualTo("Tuning/graphics"));
        }

        [TestCase("{\"frameRateLimit\": 10}", "frameRateLimit")]
        [TestCase("{\"frameRateLimit\": 2000}", "frameRateLimit")]
        [TestCase("{\"qualityLevel\": -2}", "qualityLevel")]
        [TestCase("{\"qualityLevel\": 64}", "qualityLevel")]
        [TestCase("{\"headlessFrameRate\": 0}", "headlessFrameRate")]
        [TestCase("{\"headlessFrameRate\": 1001}", "headlessFrameRate")]
        [TestCase("{\"frameRateChoices\": [60, 30]}", "frameRateChoices")]
        [TestCase("{\"frameRateChoices\": [60, 60]}", "frameRateChoices")]
        [TestCase("{\"frameRateChoices\": [0, 60]}", "frameRateChoices")]
        [TestCase("{\"frameRateChoices\": [5000]}", "frameRateChoices")]
        public void AValueOutOfRangeNamesItsItem(string json, string item)
        {
            var rejected = Assert.Throws<TuningJsonException>(() => GraphicsJson.Parse(json, "graphics.json"));

            Assert.That(rejected.SourceName, Is.EqualTo("graphics.json"));
            Assert.That(rejected.Item, Is.EqualTo(item));
            Assert.That(rejected.Message, Does.StartWith("graphics JSON 'graphics.json' item '" + item + "' is invalid: "));
            Assert.That(rejected.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => GraphicsJson.Parse("{\"resolution\": 2}", "graphics.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => GraphicsJson.Parse("{\"vSync\": 1}", "graphics.json"));
            var fraction = Assert.Throws<TuningJsonException>(() => GraphicsJson.Parse("{\"frameRateChoices\": [30, 59.94]}", "graphics.json"));
            var notArray = Assert.Throws<TuningJsonException>(() => GraphicsJson.Parse("{\"frameRateChoices\": 60}", "graphics.json"));
            var broken = Assert.Throws<TuningJsonException>(() => GraphicsJson.Parse("{\"vSync\": ", "graphics.json"));

            Assert.That(unknown.Item, Is.EqualTo("resolution"));
            Assert.That(unknown.Message, Does.Contain("frameRateLimit").And.Contain("headlessFrameRate"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("vSync"));
            Assert.That(fraction.Item, Is.EqualTo("frameRateChoices[1]"));
            Assert.That(notArray.Item, Is.EqualTo("frameRateChoices"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => GraphicsJson.Parse(null, "graphics.json"));
        }

        [Test]
        public void TheSpecRejectsAFixedDefaultResolution()
        {
            var fixedSize = new GraphicsSettings(new ScreenResolution(1920, 1080), -1, false, 0);

            Assert.Throws<ArgumentException>(() => new GraphicsSpec(fixedSize));
        }
    }
}
