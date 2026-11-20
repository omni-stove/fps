using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class PaletteTuningFileTests
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
            var asset = Resources.Load<TextAsset>(TeamPaletteJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/palettes.json");

            var palettes = TeamPaletteJson.Parse(asset.text, "Resources/Tuning/palettes.json");

            Assert.That(palettes.Count, Is.EqualTo(TeamPalette.All.Count));
            for (var i = 0; i < palettes.Count; i++)
            {
                var read = palettes[i];
                var expected = TeamPalette.All[i];
                var kind = expected.Kind.ToString();

                Assert.That(read.Kind, Is.EqualTo(expected.Kind));
                Assert.That(read.Friendly, Is.EqualTo(expected.Friendly), kind + " friendly");
                Assert.That(read.Enemy, Is.EqualTo(expected.Enemy), kind + " enemy");
                Assert.That(read.Neutral, Is.EqualTo(expected.Neutral), kind + " neutral");
                Assert.That(read.Highlight, Is.EqualTo(expected.Highlight), kind + " highlight");
                Assert.That(read.Objective, Is.EqualTo(expected.Objective), kind + " objective");
                Assert.That(read.Danger, Is.EqualTo(expected.Danger), kind + " danger");
                Assert.That(read.Contested, Is.EqualTo(expected.Contested), kind + " contested");
                Assert.That(read.Safe, Is.EqualTo(expected.Safe), kind + " safe");
                Assert.That(read.UsesMarks, Is.EqualTo(expected.UsesMarks), kind + " usesMarks");
            }
        }

        [Test]
        public void KitTuningAndTeamColorsReadTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.PalettesResourcePath, Is.EqualTo("Tuning/palettes"));
            Assert.That(KitTuning.Palettes.Count, Is.EqualTo(3));
            Assert.That(TeamColors.All, Is.SameAs(KitTuning.Palettes));
            Assert.That(TeamColors.For(ColorPalette.Standard).Enemy, Is.EqualTo(new PaletteColor(1f, 0.3f, 0.25f)));
            Assert.That(TeamColors.For(ColorPalette.RedGreen).Friendly, Is.EqualTo(PaletteColor.FromBytes(86, 180, 233)));
            Assert.That(TeamColors.For(ColorPalette.BlueYellow).UsesMarks, Is.True);
        }

        [Test]
        public void TheGameContentFileIsReadBeforeResourcesAndUsedByTeamColors()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Palettes";
            _asset = new TextAsset("{\"standard\": {\"enemy\": \"#00FF00\"}}") { name = "palettes" };
            _content.PalettesTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Palettes' (palettes)");
            var standard = TeamColors.For(ColorPalette.Standard);

            Assert.That(standard.Enemy, Is.EqualTo(new PaletteColor(0f, 1f, 0f)));
            Assert.That(standard.Friendly, Is.EqualTo(TeamPalette.Standard.Friendly));
            Assert.That(TeamColors.Of(standard, Team.Bravo, Team.Alpha), Is.EqualTo(new Color(0f, 1f, 0f, 1f)));
            Assert.That(TeamColors.For(ColorPalette.RedGreen), Is.SameAs(TeamPalette.RedGreen));
        }

        [Test]
        public void ABrokenFileFallsBackToTheDefaults()
        {
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Broken";
            _asset = new TextAsset("{\"standard\": {\"enemy\": [0, 2, 0]}}") { name = "palettes" };
            _content.PalettesTuning = _asset;
            GameContent.Use(_content);
            KitTuning.Reload();

            LogAssert.Expect(LogType.Log, "[Tuning] read GameContent 'Broken' (palettes)");
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(@"(?s)^\[Tuning\] palettes JSON .* item 'standard\.enemy\[1\]' must be from 0 to 1, .*; using the default values instead$"));
            var palettes = KitTuning.Palettes;

            Assert.That(palettes, Is.SameAs(TeamPalette.All));
            Assert.That(TeamColors.For(ColorPalette.Standard), Is.SameAs(TeamPalette.Standard));
        }
    }
}
