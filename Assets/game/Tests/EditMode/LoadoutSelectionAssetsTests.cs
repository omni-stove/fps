using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class LoadoutSelectionAssetsTests
    {
        private LoadoutTestAssets _assets;

        [SetUp]
        public void SetUp()
        {
            _assets = new LoadoutTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            _assets.Dispose();
        }

        [Test]
        public void ListsTheCatalogBySlotAndStartsFromTheDefaultLoadout()
        {
            Assert.That(
                LoadoutSelectionAssets.TryCreate(_assets.Loadout, _assets.Catalog, null, null, out var selection, out var error),
                Is.True, error);

            Assert.That(selection.SlotCount, Is.EqualTo(2));
            Assert.That(selection.OptionsAt(0).Count, Is.EqualTo(2));
            Assert.That(selection.OptionsAt(0)[0].Id, Is.EqualTo(TestWeapons.RifleId));
            Assert.That(selection.OptionsAt(0)[1].Id, Is.EqualTo(LoadoutTestAssets.SmgId));
            Assert.That(selection.OptionsAt(1).Count, Is.EqualTo(1));
            Assert.That(selection.OptionsAt(1)[0].Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(selection.WeaponIds, Is.EqualTo(new[] { TestWeapons.RifleId, TestWeapons.HandgunId }));
            Assert.That(selection.RestoredPreference, Is.False);
        }

        [Test]
        public void StartsFromThePreferenceThenTheFallback()
        {
            var saved = new[] { LoadoutTestAssets.SmgId, TestWeapons.HandgunId };

            LoadoutSelectionAssets.TryCreate(_assets.Loadout, _assets.Catalog, saved, null, out var restored, out _);
            Assert.That(restored.RestoredPreference, Is.True);
            Assert.That(restored.WeaponIds, Is.EqualTo(saved));

            LoadoutSelectionAssets.TryCreate(_assets.Loadout, _assets.Catalog, null, saved, out var fromFallback, out _);
            Assert.That(fromFallback.RestoredPreference, Is.False);
            Assert.That(fromFallback.WeaponIds, Is.EqualTo(saved));
        }

        [Test]
        public void FailsWithAReasonWhenAnAssetIsMissingOrBroken()
        {
            Assert.That(LoadoutSelectionAssets.TryCreate(null, _assets.Catalog, null, null, out var selection, out var error), Is.False);
            Assert.That(selection, Is.Null);
            Assert.That(error, Does.Contain("loadout"));

            Assert.That(LoadoutSelectionAssets.TryCreate(_assets.Loadout, null, null, null, out _, out error), Is.False);
            Assert.That(error, Does.Contain("catalog"));

            var empty = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.That(LoadoutSelectionAssets.TryCreate(_assets.Loadout, empty, null, null, out _, out error), Is.False);
                Assert.That(error, Does.Contain("no weapons"));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }
    }
}
