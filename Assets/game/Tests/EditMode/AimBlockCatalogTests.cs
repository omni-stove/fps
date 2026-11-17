using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AimBlockCatalogTests
    {
        private AimBlockCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<AimBlockCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void ANewCatalogIsOnWithTheDefaultMarginAndACodeBuiltMarker()
        {
            var settings = _catalog.ToSettings();

            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.Margin, Is.EqualTo(0.3f));
            Assert.That(_catalog.Marker, Is.Null);
        }

        [Test]
        public void SettingsRoundTrip()
        {
            _catalog.SetSettings(new AimBlockSettings(false, 0.75f));

            var settings = _catalog.ToSettings();
            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Margin, Is.EqualTo(0.75f));
            Assert.That(_catalog.Enabled, Is.False);
        }

        [Test]
        public void ResetToDefaultsTurnsItBackOnAndDropsTheMarker()
        {
            var image = new Texture2D(2, 2);
            try
            {
                _catalog.SetSettings(new AimBlockSettings(false, 2f));
                _catalog.Marker = image;

                _catalog.ResetToDefaults();

                Assert.That(_catalog.ToSettings().Enabled, Is.True);
                Assert.That(_catalog.ToSettings().Margin, Is.EqualTo(AimBlockSettings.DefaultMargin));
                Assert.That(_catalog.Marker, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void SetSettingsRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _catalog.SetSettings(null));
        }

        [Test]
        public void ValidatePassesForTheDefaults()
        {
            Assert.DoesNotThrow(() => _catalog.Validate());
        }
    }
}
