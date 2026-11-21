using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class GraphicsTuningFileTests
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
            var asset = Resources.Load<TextAsset>(GraphicsJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/graphics.json");

            var spec = GraphicsJson.Parse(asset.text, "Resources/Tuning/graphics.json");

            Assert.That(spec.Defaults, Is.EqualTo(GraphicsSettings.Default));
            Assert.That(spec.FrameRateChoices, Is.EqualTo(GraphicsSpec.DefaultFrameRateChoices));
            Assert.That(spec.HeadlessFrameRate, Is.EqualTo(GraphicsSpec.DefaultHeadlessFrameRate));
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.GraphicsResourcePath, Is.EqualTo("Tuning/graphics"));
            Assert.That(KitTuning.Graphics.HeadlessFrameRate, Is.EqualTo(60));
            Assert.That(KitTuning.Graphics.Defaults.VSync, Is.False);
            Assert.That(KitTuning.Graphics.Defaults.FrameRateLimit, Is.EqualTo(0));
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResources()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Graphics";
            _asset = new TextAsset("{\"vSync\": true, \"frameRateLimit\": 60, \"headlessFrameRate\": 30}") { name = "graphics" };
            _content.GraphicsTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Graphics' (graphics)");
            var spec = KitTuning.Graphics;

            Assert.That(spec.Defaults, Is.EqualTo(new GraphicsSettings(ScreenResolution.Native, -1, true, 60)));
            Assert.That(spec.HeadlessFrameRate, Is.EqualTo(30));
            Assert.That(new GraphicsSettingsStore("Shooter.Tests.GraphicsTuning.Unused").Defaults, Is.EqualTo(spec.Defaults), "the store's defaults come from the data");
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"frameRateLimit\": 5}") { name = "graphics" };
            _content.GraphicsTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (graphics)");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] graphics JSON .* item 'frameRateLimit' is invalid: .*; using the default values instead$"));
            var spec = KitTuning.Graphics;

            Assert.That(spec, Is.SameAs(GraphicsSpec.Default));
        }
    }
}
