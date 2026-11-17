using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class DamageNumberCatalogAssetStepTests
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__DamageNumberCatalogAssetStepTests_" + Guid.NewGuid().ToString("N");
            _path = _folder + "/Resources/" + DamageNumberCatalog.ResourcePath + ".asset";
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
        public void TheBatchEntryRunsTheStepAfterThePresentationCatalog()
        {
            var steps = PresentationAssetSteps.Discover();

            var index = steps.ToList().FindIndex(step => step is DamageNumberCatalogAssetStep);
            Assert.That(index, Is.GreaterThan(0));
            Assert.That(steps[index].Name, Is.EqualTo("DamageNumberCatalog"));
            Assert.That(steps[index].Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void TheAssetSitsWhereTheRuntimeLoadsIt()
        {
            Assert.That(
                DamageNumberCatalogAssetStep.AssetPath,
                Does.EndWith("/Resources/Presentation/DamageNumberCatalog.asset"));
        }

        [Test]
        public void TheDemoShowsDamageNumbers()
        {
            var catalog = Resources.Load<DamageNumberCatalog>(DamageNumberCatalog.ResourcePath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Enabled, Is.True);
        }

        [Test]
        public void BuildAtTurnsTheNumbersOnWithTheDefaultLook()
        {
            var catalog = DamageNumberCatalogAssetStep.BuildAt(_path);

            Assert.That(AssetDatabase.LoadAssetAtPath<DamageNumberCatalog>(_path), Is.SameAs(catalog));
            catalog.Validate();
            Assert.That(catalog.Enabled, Is.True);
            var settings = catalog.ToSettings();
            Assert.That(settings.LifetimeSeconds, Is.EqualTo(DamageNumberSettings.DefaultLifetimeSeconds));
            Assert.That(settings.RiseDistance, Is.EqualTo(DamageNumberSettings.DefaultRiseDistance));
            Assert.That(settings.FadeFraction, Is.EqualTo(DamageNumberSettings.DefaultFadeFraction));
            Assert.That(settings.MaxCount, Is.EqualTo(DamageNumberSettings.DefaultMaxCount));
        }

        [Test]
        public void RebuildingKeepsTheGuidAndRestoresTheDefaults()
        {
            var first = DamageNumberCatalogAssetStep.BuildAt(_path);
            var guid = AssetDatabase.AssetPathToGUID(_path);
            first.SetSettings(new DamageNumberSettings(3f, 10f, 0f, 2));
            EditorUtility.SetDirty(first);
            AssetDatabase.SaveAssets();

            var second = DamageNumberCatalogAssetStep.BuildAt(_path);

            Assert.That(AssetDatabase.AssetPathToGUID(_path), Is.EqualTo(guid));
            Assert.That(second.ToSettings().LifetimeSeconds, Is.EqualTo(DamageNumberSettings.DefaultLifetimeSeconds));
            Assert.That(second.ToSettings().MaxCount, Is.EqualTo(DamageNumberSettings.DefaultMaxCount));
        }

        [Test]
        public void BuildAtRejectsAPathOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => DamageNumberCatalogAssetStep.BuildAt("Packages/DamageNumberCatalog.asset"));
        }
    }
}
