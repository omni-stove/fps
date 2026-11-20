using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TeamPaletteJsonTests
    {
        private const string _source = "palettes.json";

        private static readonly string[] _expectedItemNames = { "standard", "redGreen", "blueYellow" };

        private static readonly string[] _expectedPaletteItemNames =
            { "friendly", "enemy", "neutral", "highlight", "objective", "danger", "contested", "safe", "usesMarks" };

        [Test]
        public void TheItemNamesAndThePathAreFixed()
        {
            Assert.That(TeamPaletteJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(TeamPaletteJson.PaletteItemNames, Is.EqualTo(_expectedPaletteItemNames));
            Assert.That(TeamPaletteJson.ResourcePath, Is.EqualTo("Tuning/palettes"));
        }

        [Test]
        public void ReadsEveryItemOfAPalette()
        {
            var palettes = TeamPaletteJson.Parse(
                "{\"redGreen\": {"
                + "\"friendly\": \"#0000FF\", \"enemy\": [1, 0.5, 0], \"neutral\": \"#808080\", \"highlight\": \"#ffff00\","
                + "\"objective\": [0.9, 0.9, 0.1], \"danger\": \"#C00000\", \"contested\": [1, 0, 1], \"safe\": \"#00A0A0\","
                + "\"usesMarks\": false}}",
                _source);
            var redGreen = palettes[1];

            Assert.That(redGreen.Kind, Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(redGreen.Friendly, Is.EqualTo(new PaletteColor(0f, 0f, 1f)));
            Assert.That(redGreen.Enemy, Is.EqualTo(new PaletteColor(1f, 0.5f, 0f)));
            Assert.That(redGreen.Neutral, Is.EqualTo(PaletteColor.FromBytes(128, 128, 128)));
            Assert.That(redGreen.Highlight, Is.EqualTo(new PaletteColor(1f, 1f, 0f)), "lower-case hex digits are read too");
            Assert.That(redGreen.Objective, Is.EqualTo(new PaletteColor(0.9f, 0.9f, 0.1f)));
            Assert.That(redGreen.Danger, Is.EqualTo(PaletteColor.FromBytes(192, 0, 0)));
            Assert.That(redGreen.Contested, Is.EqualTo(new PaletteColor(1f, 0f, 1f)));
            Assert.That(redGreen.Safe, Is.EqualTo(PaletteColor.FromBytes(0, 160, 160)));
            Assert.That(redGreen.UsesMarks, Is.False);
        }

        [Test]
        public void ThePalettesComeInTheOrderOfAll()
        {
            var palettes = TeamPaletteJson.Parse("{}", _source);

            Assert.That(palettes.Count, Is.EqualTo(3));
            Assert.That(palettes[0].Kind, Is.EqualTo(ColorPalette.Standard));
            Assert.That(palettes[1].Kind, Is.EqualTo(ColorPalette.RedGreen));
            Assert.That(palettes[2].Kind, Is.EqualTo(ColorPalette.BlueYellow));
        }

        [Test]
        public void LeftOutPalettesAreTheDefaults()
        {
            var palettes = TeamPaletteJson.Parse("{}", _source);

            Assert.That(palettes[0], Is.SameAs(TeamPalette.Standard));
            Assert.That(palettes[1], Is.SameAs(TeamPalette.RedGreen));
            Assert.That(palettes[2], Is.SameAs(TeamPalette.BlueYellow));
        }

        // 確認方法と同じ書き換え: 標準の組の敵だけを緑にする
        [Test]
        public void LeftOutItemsUseTheDefaultsOfTheirOwnPalette()
        {
            var palettes = TeamPaletteJson.Parse("{\"standard\": {\"enemy\": \"#00FF00\"}, \"blueYellow\": {}}", _source);
            var standard = palettes[0];
            var blueYellow = palettes[2];

            Assert.That(standard.Enemy, Is.EqualTo(new PaletteColor(0f, 1f, 0f)));
            Assert.That(standard.Friendly, Is.EqualTo(new PaletteColor(0.25f, 0.5f, 1f)));
            Assert.That(standard.Neutral, Is.EqualTo(new PaletteColor(0.85f, 0.85f, 0.85f)));
            Assert.That(standard.Highlight, Is.EqualTo(PaletteColor.FromBytes(255, 70, 60)));
            Assert.That(standard.Objective, Is.EqualTo(new PaletteColor(1f, 0.65f, 0.1f)));
            Assert.That(standard.Danger, Is.EqualTo(new PaletteColor(1f, 0.15f, 0.1f)));
            Assert.That(standard.Contested, Is.EqualTo(new PaletteColor(1f, 0.82f, 0.31f)));
            Assert.That(standard.Safe, Is.EqualTo(new PaletteColor(0.2f, 0.8f, 0.3f)));
            Assert.That(standard.UsesMarks, Is.False);
            Assert.That(palettes[1], Is.SameAs(TeamPalette.RedGreen));

            Assert.That(blueYellow.Kind, Is.EqualTo(ColorPalette.BlueYellow));
            Assert.That(blueYellow.Friendly, Is.EqualTo(PaletteColor.FromBytes(40, 200, 180)));
            Assert.That(blueYellow.Enemy, Is.EqualTo(PaletteColor.FromBytes(235, 60, 50)));
            Assert.That(blueYellow.Safe, Is.EqualTo(PaletteColor.FromBytes(0, 140, 90)));
            Assert.That(blueYellow.UsesMarks, Is.True);
        }

        [Test]
        public void HexAndComponentsAtTheEdgesAreRead()
        {
            var palettes = TeamPaletteJson.Parse(
                "{\"standard\": {\"friendly\": \"#000000\", \"enemy\": \"#FFFFFF\", \"neutral\": [0, 0, 0], \"highlight\": [1, 1, 1]}}", _source);

            Assert.That(palettes[0].Friendly, Is.EqualTo(new PaletteColor(0f, 0f, 0f)));
            Assert.That(palettes[0].Enemy, Is.EqualTo(new PaletteColor(1f, 1f, 1f)));
            Assert.That(palettes[0].Neutral, Is.EqualTo(new PaletteColor(0f, 0f, 0f)));
            Assert.That(palettes[0].Highlight, Is.EqualTo(new PaletteColor(1f, 1f, 1f)));
        }

        [Test]
        public void AComponentOutsideZeroToOneNamesItsElement()
        {
            var high = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": [1, 1.5, 0]}}", _source));
            var negative = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"redGreen\": {\"safe\": [-0.1, 0, 0]}}", _source));

            Assert.That(high.SourceName, Is.EqualTo(_source));
            Assert.That(high.Item, Is.EqualTo("standard.enemy[1]"));
            Assert.That(high.Message, Does.StartWith("palettes JSON 'palettes.json' item 'standard.enemy[1]' must be from 0 to 1, but is 1.5 (line 1, column "));
            Assert.That(negative.Item, Is.EqualTo("redGreen.safe[0]"));
        }

        [Test]
        public void AColorOfTheWrongShapeNamesItsItem()
        {
            var two = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": [1, 0]}}", _source));
            var text = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": [1, \"0\", 0]}}", _source));
            var number = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": 1}}", _source));
            var nullColor = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": null}}", _source));

            Assert.That(two.Item, Is.EqualTo("standard.enemy"));
            Assert.That(two.Message, Does.Contain("must have 3 numbers (r, g, b), but has 2"));
            Assert.That(text.Item, Is.EqualTo("standard.enemy[1]"));
            Assert.That(number.Item, Is.EqualTo("standard.enemy"));
            Assert.That(number.Message, Does.Contain("must be \"#RRGGBB\" or an array of 3 numbers (r, g, b) from 0 to 1, but is "));
            Assert.That(nullColor.Item, Is.EqualTo("standard.enemy"));
        }

        [TestCase("00FF00")]
        [TestCase("#0F0")]
        [TestCase("#00FF00FF")]
        [TestCase("#00GG00")]
        [TestCase("#00 FF0")]
        [TestCase("green")]
        public void AMalformedHexColorNamesItsItem(string hex)
        {
            var e = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": \"" + hex + "\"}}", _source));

            Assert.That(e.Item, Is.EqualTo("standard.enemy"));
            Assert.That(e.Message, Does.Contain("#RRGGBB").And.Contain(hex));
        }

        [Test]
        public void TheSameFriendlyAndEnemyColorIsRejected()
        {
            var same = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"friendly\": \"#00FF00\", \"enemy\": [0, 1, 0]}}", _source));
            var sameAsDefault = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": [0.25, 0.5, 1]}}", _source));

            Assert.That(same.Item, Is.EqualTo("standard.enemy"));
            Assert.That(same.Message, Does.StartWith("palettes JSON 'palettes.json' item 'standard.enemy' is invalid: "));
            Assert.That(same.Message, Does.Contain("'standard.friendly'"));
            Assert.That(sameAsDefault.Item, Is.EqualTo("standard.enemy"), "the default friendly color counts too");
        }

        [Test]
        public void UsesMarksMustBeTrueOrFalse()
        {
            var e = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"usesMarks\": \"yes\"}}", _source));

            Assert.That(e.Item, Is.EqualTo("standard.usesMarks"));
            Assert.That(e.Message, Does.Contain("must be true or false"));
        }

        [Test]
        public void AnUnknownItemAPaletteThatIsNotAnObjectOrBrokenJsonIsRejected()
        {
            var unknownPalette = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"tritan\": {}}", _source));
            var unknownColor = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"ally\": \"#00FF00\"}}", _source));
            var notObject = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": [1, 0, 0]}", _source));
            var topArray = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("[]", _source));
            var broken = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"standard\": {\"enemy\": ", _source));

            Assert.That(unknownPalette.Item, Is.EqualTo("tritan"));
            Assert.That(unknownPalette.Message, Does.Contain("standard").And.Contain("redGreen").And.Contain("blueYellow"), "lists the known items");
            Assert.That(unknownColor.Item, Is.EqualTo("standard.ally"));
            Assert.That(unknownColor.Message, Does.Contain("friendly").And.Contain("usesMarks"));
            Assert.That(notObject.Item, Is.EqualTo("standard"));
            Assert.That(topArray.Item, Is.Null);
            Assert.That(broken.Item, Is.Null);
            Assert.That(broken.Message, Does.StartWith("palettes JSON 'palettes.json' is not valid JSON: "));
            Assert.Throws<ArgumentNullException>(() => TeamPaletteJson.Parse(null, _source));
        }

        [Test]
        public void AnUnnamedSourceIsNamedByItsKind()
        {
            var e = Assert.Throws<TuningJsonException>(() => TeamPaletteJson.Parse("{\"x\": 1}", null));

            Assert.That(e.SourceName, Is.EqualTo("(unnamed palettes JSON)"));
        }
    }
}
