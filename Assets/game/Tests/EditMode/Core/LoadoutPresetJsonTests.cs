using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutPresetJsonTests
    {
        private static readonly string[] _expectedItemNames = { "maxPresets", "maxNameLength" };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = LoadoutPresetJson.Parse("{\"maxPresets\": 3, \"maxNameLength\": 12}", "loadout-presets.json");

            Assert.That(spec.MaxPresets, Is.EqualTo(3));
            Assert.That(spec.MaxNameLength, Is.EqualTo(12));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var empty = LoadoutPresetJson.Parse("{}", "loadout-presets.json");

            Assert.That(empty.MaxPresets, Is.EqualTo(5));
            Assert.That(empty.MaxNameLength, Is.EqualTo(24));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(LoadoutPresetJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(LoadoutPresetJson.ResourcePath, Is.EqualTo("Tuning/loadout-presets"));
        }

        [Test]
        public void ZeroPresetsNamesItsItem()
        {
            var zero = Assert.Throws<TuningJsonException>(() => LoadoutPresetJson.Parse("{\"maxPresets\": 0}", "loadout-presets.json"));
            var shortName = Assert.Throws<TuningJsonException>(() => LoadoutPresetJson.Parse("{\"maxNameLength\": 0}", "loadout-presets.json"));

            Assert.That(zero.SourceName, Is.EqualTo("loadout-presets.json"));
            Assert.That(zero.Item, Is.EqualTo("maxPresets"));
            Assert.That(zero.Message, Does.StartWith("loadout presets JSON 'loadout-presets.json' item 'maxPresets' is invalid: "));
            Assert.That(zero.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(shortName.Item, Is.EqualTo("maxNameLength"));
        }

        [Test]
        public void AnUnknownItemAWrongTypeOrBrokenJsonIsRejected()
        {
            var unknown = Assert.Throws<TuningJsonException>(() => LoadoutPresetJson.Parse("{\"presets\": 2}", "loadout-presets.json"));
            var wrongType = Assert.Throws<TuningJsonException>(() => LoadoutPresetJson.Parse("{\"maxPresets\": \"many\"}", "loadout-presets.json"));
            var broken = Assert.Throws<TuningJsonException>(() => LoadoutPresetJson.Parse("{\"maxPresets\": ", "loadout-presets.json"));

            Assert.That(unknown.Item, Is.EqualTo("presets"));
            Assert.That(unknown.Message, Does.Contain("maxPresets"), "lists the known items");
            Assert.That(wrongType.Item, Is.EqualTo("maxPresets"));
            Assert.That(broken.Item, Is.Null);
            Assert.Throws<ArgumentNullException>(() => LoadoutPresetJson.Parse(null, "loadout-presets.json"));
        }
    }
}
