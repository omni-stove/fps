using System;
using System.Linq;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    public sealed class CameraShakeSettingsAssetStepTests
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__CameraShakeSettingsAssetStepTests_" + Guid.NewGuid().ToString("N");
            _path = _folder + "/Resources/" + CameraShakeSettings.ResourcePath + ".asset";
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

            var index = steps.ToList().FindIndex(step => step is CameraShakeSettingsAssetStep);
            Assert.That(index, Is.GreaterThan(0));
            Assert.That(steps[index].Name, Is.EqualTo("CameraShakeSettings"));
            Assert.That(steps[index].Order, Is.GreaterThan(PresentationCatalogAssetStep.StepOrder));
        }

        [Test]
        public void TheAssetSitsWhereTheRuntimeLoadsIt()
        {
            Assert.That(
                CameraShakeSettingsAssetStep.AssetPath,
                Does.EndWith("/Resources/Presentation/CameraShakeSettings.asset"));
        }

        [Test]
        public void BuildAtCreatesValidDefaults()
        {
            var settings = CameraShakeSettingsAssetStep.BuildAt(_path);

            Assert.That(AssetDatabase.LoadAssetAtPath<CameraShakeSettings>(_path), Is.SameAs(settings));
            settings.Validate();
            Assert.That(settings.DecayPerSecond, Is.EqualTo(CameraShakeSettings.DefaultDecayPerSecond));
            Assert.That(settings.ExplosionRadius, Is.EqualTo(CameraShakeSettings.DefaultExplosionRadius));
        }

        [Test]
        public void RebuildingKeepsTheGuidAndRestoresTheDefaults()
        {
            var first = CameraShakeSettingsAssetStep.BuildAt(_path);
            var guid = AssetDatabase.AssetPathToGUID(_path);
            first.DecayPerSecond = 9f;
            EditorUtility.SetDirty(first);
            AssetDatabase.SaveAssets();

            var second = CameraShakeSettingsAssetStep.BuildAt(_path);

            Assert.That(AssetDatabase.AssetPathToGUID(_path), Is.EqualTo(guid));
            Assert.That(second.DecayPerSecond, Is.EqualTo(CameraShakeSettings.DefaultDecayPerSecond));
        }

        [Test]
        public void BuildAtRejectsAPathOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => CameraShakeSettingsAssetStep.BuildAt("Packages/CameraShakeSettings.asset"));
        }
    }
}
