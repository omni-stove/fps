using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CosmeticJsonTests
    {
        private const string _source = "cosmetics.json";

        private static readonly string[] _expectedItemNames = { "items" };
        private static readonly string[] _expectedCosmeticItemNames = { "id", "kind", "nameKey", "visualId", "unlockLevel" };

        [Test]
        public void ReadsEveryItem()
        {
            var catalog = CosmeticJson.Parse(
                "{\"items\": [{\"id\": \"desert\", \"kind\": \"Camo\", \"nameKey\": \"camo.desert\", \"visualId\": \"camo-desert\", \"unlockLevel\": 10},"
                + " {\"id\": \"ghillie\", \"kind\": \"Skin\", \"visualId\": \"skin-ghillie\"}]}",
                _source);

            Assert.That(catalog.Items.Count, Is.EqualTo(2));
            var desert = catalog.Items[0];
            Assert.That(desert.Id, Is.EqualTo("desert"));
            Assert.That(desert.Kind, Is.EqualTo(CosmeticKind.Camo));
            Assert.That(desert.DisplayNameKey, Is.EqualTo("camo.desert"));
            Assert.That(desert.VisualId, Is.EqualTo("camo-desert"));
            Assert.That(desert.UnlockLevel, Is.EqualTo(10));
            Assert.That(catalog.Items[1].UnlockLevel, Is.EqualTo(0));
            Assert.That(catalog.Items[1].DisplayNameKey, Is.EqualTo(string.Empty));
            Assert.That(catalog.Find("ghillie"), Is.SameAs(catalog.Items[1]));
            Assert.That(catalog.OfKind(CosmeticKind.Camo), Is.EqualTo(new[] { desert }));
            Assert.That(catalog.OfKind(CosmeticKind.Skin), Is.EqualTo(new[] { catalog.Items[1] }));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            Assert.That(CosmeticJson.Parse("{}", _source).Items, Is.Empty);
            Assert.That(CosmeticCatalog.Default.Items, Is.Empty);
            Assert.That(CosmeticCatalog.Default.Find("anything"), Is.Null);
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(CosmeticJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(CosmeticJson.CosmeticItemNames, Is.EqualTo(_expectedCosmeticItemNames));
            Assert.That(CosmeticJson.ResourcePath, Is.EqualTo("Tuning/cosmetics"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var kind = Reject("{\"items\": [{\"id\": \"a\", \"kind\": \"Hat\", \"visualId\": \"v\"}]}");
            var noVisual = Reject("{\"items\": [{\"id\": \"a\", \"kind\": \"Skin\"}]}");
            var blankVisual = Reject("{\"items\": [{\"id\": \"a\", \"kind\": \"Skin\", \"visualId\": \"\"}]}");
            var level = Reject("{\"items\": [{\"id\": \"a\", \"kind\": \"Skin\", \"visualId\": \"v\", \"unlockLevel\": -1}]}");
            var twice = Reject("{\"items\": [{\"id\": \"a\", \"kind\": \"Skin\", \"visualId\": \"v\"}, {\"id\": \"a\", \"kind\": \"Camo\", \"visualId\": \"w\"}]}");

            Assert.That(kind.Item, Is.EqualTo("items[0].kind"));
            Assert.That(noVisual.Item, Is.EqualTo("items[0].visualId"));
            Assert.That(blankVisual.Item, Is.EqualTo("items[0].visualId"));
            Assert.That(level.Item, Is.EqualTo("items[0].unlockLevel"));
            Assert.That(level.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(twice.Item, Is.EqualTo("items"), "an id is unique across kinds, since a loadout sends only the id");
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => CosmeticJson.Parse(json, _source));
        }
    }
}
