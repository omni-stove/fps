using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ExtractionJsonTests
    {
        private const string _source = "extraction.json";

        private static readonly string[] _expectedItemNames =
        {
            "raidSeconds", "extractHoldSeconds", "openPoints", "openAfterSeconds", "keepCarriedOnDeath",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = ExtractionJson.Parse(
                "{\"raidSeconds\": 600, \"extractHoldSeconds\": 5, \"openPoints\": 3, \"openAfterSeconds\": 120, \"keepCarriedOnDeath\": true}",
                _source);

            Assert.That(spec.RaidSeconds, Is.EqualTo(600f));
            Assert.That(spec.ExtractHoldSeconds, Is.EqualTo(5f));
            Assert.That(spec.OpenPoints, Is.EqualTo(3));
            Assert.That(spec.OpenAfterSeconds, Is.EqualTo(120f));
            Assert.That(spec.KeepCarriedOnDeath, Is.True);
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = ExtractionJson.Parse("{}", _source);

            Assert.That(spec.RaidSeconds, Is.EqualTo(900f));
            Assert.That(spec.ExtractHoldSeconds, Is.EqualTo(8f));
            Assert.That(spec.OpenPoints, Is.EqualTo(2));
            Assert.That(spec.OpenAfterSeconds, Is.EqualTo(0f));
            Assert.That(spec.KeepCarriedOnDeath, Is.False);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(ExtractionJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(ExtractionJson.ResourcePath, Is.EqualTo("Tuning/extraction"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var raid = Reject("{\"raidSeconds\": 0}");
            var hold = Reject("{\"extractHoldSeconds\": -1}");
            var points = Reject("{\"openPoints\": 0}");
            var tooLate = Reject("{\"raidSeconds\": 100, \"openAfterSeconds\": 95, \"extractHoldSeconds\": 5}");
            var flag = Reject("{\"keepCarriedOnDeath\": \"yes\"}");

            Assert.That(raid.Item, Is.EqualTo("raidSeconds"));
            Assert.That(raid.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(hold.Item, Is.EqualTo("extractHoldSeconds"));
            Assert.That(points.Item, Is.EqualTo("openPoints"));
            Assert.That(tooLate.Item, Is.EqualTo("openAfterSeconds"));
            Assert.That(tooLate.Message, Does.Contain("shorter than raidSeconds (100)"));
            Assert.That(flag.Item, Is.EqualTo("keepCarriedOnDeath"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => ExtractionJson.Parse(json, _source));
        }
    }
}
