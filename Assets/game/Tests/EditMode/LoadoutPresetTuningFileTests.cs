using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutPresetTuningFileTests
    {
        private GameContent _content;
        private TextAsset _asset;

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            if (_asset != null)
            {
                Object.DestroyImmediate(_asset);
            }

            if (_content != null)
            {
                Object.DestroyImmediate(_content);
            }

            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(LoadoutPresetJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/loadout-presets.json");

            var spec = LoadoutPresetJson.Parse(asset.text, "Resources/Tuning/loadout-presets.json");

            Assert.That(spec.MaxPresets, Is.EqualTo(LoadoutPresetSpec.DefaultMaxPresets));
            Assert.That(spec.MaxNameLength, Is.EqualTo(LoadoutPresetSpec.DefaultMaxNameLength));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.LoadoutPresetsResourcePath, Is.EqualTo("Tuning/loadout-presets"));
            Assert.That(KitTuning.LoadoutPresets.MaxPresets, Is.EqualTo(5));
            Assert.That(KitTuning.LoadoutPresets.MaxNameLength, Is.EqualTo(24));
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Presets";
            _asset = new TextAsset("{\"maxPresets\": 2}") { name = "loadout-presets" };
            _content.LoadoutPresetsTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Presets' (loadout-presets)");
            var spec = KitTuning.LoadoutPresets;

            Assert.That(spec.MaxPresets, Is.EqualTo(2));
            Assert.That(spec.MaxNameLength, Is.EqualTo(LoadoutPresetSpec.DefaultMaxNameLength));
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"maxPresets\": 0}") { name = "loadout-presets" };
            _content.LoadoutPresetsTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (loadout-presets)");
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] loadout presets JSON .*'maxPresets'.*; using the default values instead$"));

            Assert.That(KitTuning.LoadoutPresets, Is.SameAs(LoadoutPresetSpec.Default));
        }
    }
}
