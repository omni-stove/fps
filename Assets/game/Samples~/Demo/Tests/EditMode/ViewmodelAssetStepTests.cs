using System;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // リポジトリの Assets/game/Presentation は触らず、一時フォルダに作る。
    public sealed class ViewmodelAssetStepTests
    {
        private string _folder;
        private string _viewmodelFolder;
        private string _catalogPath;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__ViewmodelAssetStepTests_" + Guid.NewGuid().ToString("N");
            _viewmodelFolder = _folder + "/Viewmodels";
            _catalogPath = _folder + "/Resources/Presentation/ViewmodelCatalog.asset";
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
        public void ResourcePathFollowsThePresentationCatalogRule()
        {
            Assert.That(ViewmodelCatalog.ResourcePath, Is.EqualTo(PresentationCatalog.CatalogResourcePath(ViewmodelCatalog.CatalogName)));
            Assert.That(ViewmodelCatalog.ResourcePath, Is.EqualTo("Presentation/ViewmodelCatalog"));
            Assert.That(ViewmodelAssetStep.CatalogPath, Is.EqualTo(DemoSample.Root + "/Presentation/Resources/Presentation/ViewmodelCatalog.asset"));
        }

        [Test]
        public void TheBatchEntryRunsTheStepAfterThePresentationCatalog()
        {
            var steps = PresentationAssetSteps.Discover();

            Assert.That(steps.Count(step => step is ViewmodelAssetStep), Is.EqualTo(1));
            Assert.That(new ViewmodelAssetStep().Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
            Assert.That(new ViewmodelAssetStep().Name, Is.EqualTo("Viewmodel"));
        }

        [Test]
        public void BuildMakesAViewmodelForEveryWeaponWithAMuzzleSocket()
        {
            var catalog = ViewmodelAssetStep.BuildAt(_viewmodelFolder, _catalogPath);

            Assert.That(catalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<ViewmodelCatalog>(_catalogPath)));
            Assert.DoesNotThrow(() => catalog.Validate());
            foreach (var spec in ViewmodelAssetStep.WeaponSpecs())
            {
                Assert.That(catalog.TryGetWeapon(spec.Id, out var entry), Is.True, spec.Id);
                Assert.That(entry.Prefab.name, Is.EqualTo(ViewmodelAssetStep.PrefabName(spec.Category)));
                Assert.That(AssetDatabase.GetAssetPath(entry.Prefab), Does.StartWith(_viewmodelFolder + "/"));
                Assert.That(WeaponSockets.FindSocket(entry.Prefab.transform, WeaponSockets.MuzzleName), Is.Not.Null, spec.Id);
                Assert.That(WeaponSockets.FindSocket(entry.Prefab.transform, WeaponSockets.ShellEjectName), Is.Not.Null, spec.Id);
                Assert.That(entry.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty, "the boxes have no colliders");
                Assert.That(entry.Prefab.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty);
                Assert.That(entry.TryGetSwaySettings(out _, out _), Is.True);
            }
        }

        [Test]
        public void LongerWeaponCategoriesGetLongerGuns()
        {
            var catalog = ViewmodelAssetStep.BuildAt(_viewmodelFolder, _catalogPath);

            Assert.That(catalog.TryGetWeapon(WeaponAssetBuilder.RifleId, out var rifle), Is.True);
            Assert.That(catalog.TryGetWeapon(WeaponAssetBuilder.HandgunId, out var handgun), Is.True);
            var rifleMuzzle = WeaponSockets.FindSocket(rifle.Prefab.transform, WeaponSockets.MuzzleName);
            var handgunMuzzle = WeaponSockets.FindSocket(handgun.Prefab.transform, WeaponSockets.MuzzleName);

            Assert.That(rifleMuzzle.position.z, Is.GreaterThan(handgunMuzzle.position.z + 0.2f));
            Assert.That(
                ViewmodelAssetStep.ShapeFor(WeaponCategory.SniperRifle).TotalLength,
                Is.GreaterThan(ViewmodelAssetStep.ShapeFor(WeaponCategory.SubmachineGun).TotalLength));
        }

        [Test]
        public void EveryCategoryHasAShapeAndValidSway()
        {
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                var shape = ViewmodelAssetStep.ShapeFor(category);
                Assert.That(shape.TotalLength, Is.GreaterThan(0f), category.ToString());
                Assert.That(ViewmodelAssetStep.SwayFor(category), Is.Not.Null, category.ToString());
            }
        }

        [Test]
        public void RebuildingKeepsTheGuids()
        {
            var first = ViewmodelAssetStep.BuildAt(_viewmodelFolder, _catalogPath);
            first.TryGetWeapon(WeaponAssetBuilder.RifleId, out var rifle);
            var catalogGuid = AssetDatabase.AssetPathToGUID(_catalogPath);
            var prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rifle.Prefab));

            var second = ViewmodelAssetStep.BuildAt(_viewmodelFolder, _catalogPath);
            second.TryGetWeapon(WeaponAssetBuilder.RifleId, out var rebuilt);

            Assert.That(AssetDatabase.AssetPathToGUID(_catalogPath), Is.EqualTo(catalogGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rebuilt.Prefab)), Is.EqualTo(prefabGuid));
        }

        [Test]
        public void BuildRejectsPathsOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => ViewmodelAssetStep.BuildAt("Packages/x", _catalogPath));
            Assert.Throws<ArgumentException>(() => ViewmodelAssetStep.BuildAt(_viewmodelFolder, "Packages/x.asset"));
        }

        [Test]
        public void ValidateNamesEveryProblem()
        {
            var catalog = ScriptableObject.CreateInstance<ViewmodelCatalog>();
            var prefab = new GameObject("Model");
            try
            {
                catalog.name = "Broken";
                catalog.SetWeapons(new[]
                {
                    new ViewmodelEntry("a", prefab, Vector3.zero, new ViewmodelSwayParameters()),
                    new ViewmodelEntry("a", prefab, Vector3.zero, new ViewmodelSwayParameters()),
                    new ViewmodelEntry("b", null, Vector3.zero, new ViewmodelSwayParameters()),
                    new ViewmodelEntry(string.Empty, prefab, Vector3.zero, new ViewmodelSwayParameters()),
                    null,
                });

                var error = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

                Assert.That(error.Message, Does.Contain("already used"));
                Assert.That(error.Message, Does.Contain("prefab is missing"));
                Assert.That(error.Message, Does.Contain("has no weapon id"));
                Assert.That(error.Message, Does.Contain("is empty"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void AnEmptyCatalogIsValidAndFindsNothing()
        {
            var catalog = ScriptableObject.CreateInstance<ViewmodelCatalog>();
            try
            {
                Assert.DoesNotThrow(() => catalog.Validate());
                Assert.That(catalog.TryGetWeapon("x", out _), Is.False);
                Assert.That(catalog.TryGetWeapon(null, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void SwayParametersRoundTripTheSettings()
        {
            var settings = new ViewmodelSwaySettings(bobHorizontalMeters: 0.02f, kickPitchDegrees: 3f, adsSwayScale: 0.3f);

            var roundTrip = new ViewmodelSwayParameters(settings).ToSettings();

            Assert.That(roundTrip.BobHorizontalMeters, Is.EqualTo(0.02f));
            Assert.That(roundTrip.KickPitchDegrees, Is.EqualTo(3f));
            Assert.That(roundTrip.AdsSwayScale, Is.EqualTo(0.3f));
            Assert.That(roundTrip.KickReturnPerSecond, Is.EqualTo(ViewmodelSwaySettings.DefaultKickReturnPerSecond));
        }
    }
}
