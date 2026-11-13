using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="WeaponAssetBuilder"/> を一時フォルダに走らせて、出力と GUID の保持を確かめる。
    /// リポジトリの Assets/game/Weapons は触らない。
    /// </summary>
    public sealed class WeaponAssetBuilderTests
    {
        private string _folder;
        private string _riflePath;
        private string _handgunPath;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__WeaponAssetBuilderTests_" + Guid.NewGuid().ToString("N");
            _riflePath = _folder + "/Rifle.asset";
            _handgunPath = _folder + "/Handgun.asset";
            _catalogPath = _folder + "/WeaponCatalog.asset";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void DefaultPathsAreUnderGameWeapons()
        {
            Assert.That(WeaponAssetBuilder.RiflePath, Is.EqualTo("Assets/game/Weapons/Rifle.asset"));
            Assert.That(WeaponAssetBuilder.HandgunPath, Is.EqualTo("Assets/game/Weapons/Handgun.asset"));
            Assert.That(WeaponAssetBuilder.CatalogPath, Is.EqualTo("Assets/game/Weapons/WeaponCatalog.asset"));
        }

        [Test]
        public void IdsAreFixedDistinctUuids()
        {
            Assert.That(WeaponAssetBuilder.RifleId, Is.Not.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(WeaponAssetBuilder.RifleSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(WeaponAssetBuilder.HandgunSpec().Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));
            Assert.That(WeaponAssetBuilder.RifleSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleSpec().Id), "rebuilding keeps the id");
        }

        [Test]
        public void LoadDefinitionReturnsTheBuiltAsset()
        {
            Build();

            var rifle = WeaponAssetBuilder.LoadDefinition(_riflePath);

            Assert.That(rifle, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath)));
            Assert.That(rifle.ToSpec().Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void LoadDefinitionRejectsAMissingOrWrongAsset()
        {
            Assert.Throws<InvalidOperationException>(() => WeaponAssetBuilder.LoadDefinition(_riflePath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.LoadDefinition("Rifle.asset"));

            Build();
            Assert.Throws<InvalidOperationException>(
                () => WeaponAssetBuilder.LoadDefinition(_catalogPath), "a catalog is not a weapon definition");
        }

        [Test]
        public void LoadDefinitionRejectsAnInvalidAsset()
        {
            Build();
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            rifle.MagazineSize = 0;

            Assert.Throws<WeaponDefinitionException>(() => WeaponAssetBuilder.LoadDefinition(_riflePath));
        }

        [Test]
        public void BuildsRifleHandgunAndCatalogInThatOrder()
        {
            Build();

            var rifleAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            var handgunAsset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_handgunPath);
            var catalogAsset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            Assert.That(rifleAsset, Is.Not.Null);
            Assert.That(handgunAsset, Is.Not.Null);
            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(catalogAsset.Weapons, Is.EqualTo(new[] { rifleAsset, handgunAsset }));

            var catalog = catalogAsset.ToCatalog();
            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.All[0].Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(catalog.All[1].Id, Is.EqualTo(WeaponAssetBuilder.HandgunId));

            var rifle = catalog.Get(WeaponAssetBuilder.RifleId);
            Assert.That(rifle.Category, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(rifle.FireMode, Is.EqualTo(FireMode.Auto));
            Assert.That(rifle.MagazineSize, Is.EqualTo(30));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));
            Assert.That(rifle.FireInterval, Is.EqualTo(0.1f));
            Assert.That(rifle.ReloadSeconds, Is.EqualTo(2f));
            Assert.That(rifle.Damage, Is.EqualTo(25));
            Assert.That(rifle.Range, Is.EqualTo(100f));
            Assert.That(rifle.Falloff.StartDistance, Is.EqualTo(20f));
            Assert.That(rifle.Falloff.EndDistance, Is.EqualTo(60f));
            Assert.That(rifle.Falloff.MinMultiplier, Is.EqualTo(0.5f));

            var handgun = catalog.Get(WeaponAssetBuilder.HandgunId);
            Assert.That(handgun.Category, Is.EqualTo(WeaponCategory.Handgun));
            Assert.That(handgun.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(handgun.MagazineSize, Is.EqualTo(12));
            Assert.That(handgun.ReserveAmmo, Is.EqualTo(48));
            Assert.That(handgun.FireInterval, Is.EqualTo(0.2f));
            Assert.That(handgun.ReloadSeconds, Is.EqualTo(1.5f));
            Assert.That(handgun.Damage, Is.EqualTo(34));
            Assert.That(handgun.Range, Is.EqualTo(60f));
            Assert.That(handgun.Falloff.StartDistance, Is.EqualTo(15f));
            Assert.That(handgun.Falloff.EndDistance, Is.EqualTo(40f));
            Assert.That(handgun.Falloff.MinMultiplier, Is.EqualTo(0.6f));
        }

        [Test]
        public void SecondRunKeepsTheGuidsAndRestoresTheValues()
        {
            Build();
            var rifleGuid = AssetDatabase.AssetPathToGUID(_riflePath);
            var handgunGuid = AssetDatabase.AssetPathToGUID(_handgunPath);
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            Assert.That(rifleGuid, Is.Not.Empty);
            Assert.That(handgunGuid, Is.Not.Empty);
            Assert.That(catalogGuid, Is.Not.Empty);

            // 手で書き換えられたアセットも、2 回目で GUID を保ったまま既定の値に戻る。
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(_riflePath);
            rifle.Damage = 1;
            rifle.Id = "00000000-0000-4000-8000-000000000099";
            rifle.Category = WeaponCategory.SniperRifle;
            EditorUtility.SetDirty(rifle);
            var catalogAsset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(_catalogPath);
            catalogAsset.SetWeapons(new[] { rifle });
            EditorUtility.SetDirty(catalogAsset);
            AssetDatabase.SaveAssets();

            var rebuilt = Build();

            Assert.That(AssetDatabase.AssetPathToGUID(_riflePath), Is.EqualTo(rifleGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(_handgunPath), Is.EqualTo(handgunGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(rebuilt, Is.SameAs(catalogAsset));
            var restored = rebuilt.ToCatalog().Get(WeaponAssetBuilder.RifleId);
            Assert.That(restored.Damage, Is.EqualTo(25));
            Assert.That(restored.Category, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(rebuilt.Weapons, Has.Count.EqualTo(2));
        }

        [Test]
        public void RefusesToOverwriteAnAssetOfAnotherType()
        {
            Build();
            AssetDatabase.DeleteAsset(_handgunPath);
            var other = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            AssetDatabase.CreateAsset(other, _handgunPath);

            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_handgunPath), Is.InstanceOf<WeaponCatalogAsset>());
        }

        [Test]
        public void RejectsInvalidOrSharedPaths()
        {
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildAt("Rifle.asset", _handgunPath, _catalogPath));
            Assert.Throws<ArgumentException>(() => WeaponAssetBuilder.BuildAt(_riflePath, _riflePath, _catalogPath));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void ConfigureDoesNotSaveAnInvalidCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.Throws<WeaponDefinitionException>(
                    () => WeaponAssetBuilder.Configure(catalog, Array.Empty<WeaponDefinition>()));
                Assert.Throws<WeaponDefinitionException>(
                    () => SceneBuildHelpers.CreateOrUpdateAsset<WeaponCatalogAsset>(
                        _catalogPath, c => WeaponAssetBuilder.Configure(c, Array.Empty<WeaponDefinition>())));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(_catalogPath), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        private WeaponCatalogAsset Build()
        {
            return WeaponAssetBuilder.BuildAt(_riflePath, _handgunPath, _catalogPath);
        }
    }
}
