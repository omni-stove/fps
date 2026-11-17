using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AssistJsonTests
    {
        [Test]
        public void ReadsEveryItem()
        {
            var spec = AssistJson.Parse("{\"windowSeconds\": 6.5, \"minDamage\": 35}", "assist.json");

            Assert.That(spec.WindowSeconds, Is.EqualTo(6.5f));
            Assert.That(spec.MinDamage, Is.EqualTo(35));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = AssistJson.Parse("{}", "assist.json");
            var partial = AssistJson.Parse("{\"minDamage\": 50}", "assist.json");

            Assert.That(empty.WindowSeconds, Is.EqualTo(10f));
            Assert.That(empty.MinDamage, Is.EqualTo(20));
            Assert.That(partial.WindowSeconds, Is.EqualTo(10f));
            Assert.That(partial.MinDamage, Is.EqualTo(50));
        }

        [Test]
        public void TheDefaultSpecHoldsTheDefaultConstants()
        {
            Assert.That(AssistSpec.Default.WindowSeconds, Is.EqualTo(AssistSpec.DefaultWindowSeconds));
            Assert.That(AssistSpec.Default.MinDamage, Is.EqualTo(AssistSpec.DefaultMinDamage));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(AssistJson.ItemNames, Is.EqualTo(new[] { "windowSeconds", "minDamage" }));
        }

        [Test]
        public void AnOutOfRangeValueNamesItsItem()
        {
            var window = Assert.Throws<TuningJsonException>(() => AssistJson.Parse("{\"windowSeconds\": 0}", "assist.json"));
            var damage = Assert.Throws<TuningJsonException>(() => AssistJson.Parse("{\"minDamage\": 0}", "assist.json"));

            Assert.That(window.SourceName, Is.EqualTo("assist.json"));
            Assert.That(window.Item, Is.EqualTo("windowSeconds"));
            Assert.That(window.Message, Does.StartWith("assist JSON 'assist.json' item 'windowSeconds' is invalid: "));
            Assert.That(window.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(damage.Item, Is.EqualTo("minDamage"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => AssistJson.Parse("{\"seconds\": 2}", "assist.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => AssistJson.Parse("{\"minDamage\": \"many\"}", "assist.json"));
            var broken = Assert.Throws<TuningJsonException>(() => AssistJson.Parse("{\"minDamage\": ", "assist.json"));

            Assert.That(unknown.Item, Is.EqualTo("seconds"));
            Assert.That(unknown.Message, Does.Contain("windowSeconds"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("minDamage"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => AssistJson.Parse(null, "assist.json"));
        }

        [Test]
        public void TheWindowCountsUpToItsEdge()
        {
            var spec = new AssistSpec(windowSeconds: 10f, minDamage: 20);

            Assert.That(spec.InWindow(100.0, 110.0), Is.True);
            Assert.That(spec.InWindow(100.0, 110.01), Is.False);
        }
    }
}
