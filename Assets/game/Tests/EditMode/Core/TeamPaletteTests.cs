using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class TeamPaletteTests
    {
        [TestCase(ColorPalette.Standard)]
        [TestCase(ColorPalette.RedGreen)]
        [TestCase(ColorPalette.BlueYellow)]
        public void ForReturnsThePaletteOfThatKind(ColorPalette kind)
        {
            Assert.That(TeamPalette.For(kind).Kind, Is.EqualTo(kind));
        }

        [Test]
        public void ForRejectsUnknownKinds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamPalette.For((ColorPalette)3));
        }

        [Test]
        public void AllListsEveryKindOnceInOrder()
        {
            var kinds = (ColorPalette[])Enum.GetValues(typeof(ColorPalette));

            Assert.That(TeamPalette.All.Count, Is.EqualTo(kinds.Length));
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.That(TeamPalette.All[i].Kind, Is.EqualTo(kinds[i]));
            }
        }

        // 今までの見た目 (キャラクターと拠点の青・赤、拠点の中立の灰、頭への命中の赤) を変えない
        [Test]
        public void TheStandardPaletteKeepsTheColorsUsedSoFar()
        {
            var standard = TeamPalette.Standard;

            Assert.That(standard.Friendly, Is.EqualTo(new PaletteColor(0.25f, 0.5f, 1f)));
            Assert.That(standard.Enemy, Is.EqualTo(new PaletteColor(1f, 0.3f, 0.25f)));
            Assert.That(standard.Neutral, Is.EqualTo(new PaletteColor(0.85f, 0.85f, 0.85f)));
            Assert.That(standard.Highlight, Is.EqualTo(PaletteColor.FromBytes(255, 70, 60)));
        }

        [Test]
        public void RedGreenMakesTheTeamsBlueAndOrange()
        {
            var palette = TeamPalette.RedGreen;

            Assert.That(palette.Friendly.B, Is.GreaterThan(palette.Friendly.R), "blue");
            Assert.That(palette.Enemy.R, Is.GreaterThan(palette.Enemy.G), "orange");
            Assert.That(palette.Enemy.G, Is.GreaterThan(palette.Enemy.B), "orange");
        }

        [TestCase(ColorPalette.Standard)]
        [TestCase(ColorPalette.RedGreen)]
        [TestCase(ColorPalette.BlueYellow)]
        public void EveryRoleHasItsOwnColor(ColorPalette kind)
        {
            var palette = TeamPalette.For(kind);

            Assert.That(palette.Friendly, Is.Not.EqualTo(palette.Enemy));
            Assert.That(palette.Friendly, Is.Not.EqualTo(palette.Neutral));
            Assert.That(palette.Enemy, Is.Not.EqualTo(palette.Neutral));
            Assert.That(palette.Highlight, Is.Not.EqualTo(palette.Neutral));
        }

        [TestCase(Team.Alpha, Team.Alpha, TeamRelation.Friendly)]
        [TestCase(Team.Bravo, Team.Bravo, TeamRelation.Friendly)]
        [TestCase(Team.Alpha, Team.Bravo, TeamRelation.Enemy)]
        [TestCase(Team.Bravo, Team.Alpha, TeamRelation.Enemy)]
        public void RelationComparesWithTheViewersTeam(Team team, Team viewer, TeamRelation expected)
        {
            Assert.That(TeamPalette.RelationOf(team, viewer), Is.EqualTo(expected));
        }

        [Test]
        public void NoOwnerIsNeutral()
        {
            Assert.That(TeamPalette.RelationOf(null, Team.Alpha), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(TeamPalette.RelationOf(null, null), Is.EqualTo(TeamRelation.Neutral));
        }

        // 観戦だけで自分のチームが無くても、2 チームの色は分ける
        [Test]
        public void WithoutAViewerAlphaIsFriendlyAndBravoIsEnemy()
        {
            Assert.That(TeamPalette.RelationOf(Team.Alpha, null), Is.EqualTo(TeamRelation.Friendly));
            Assert.That(TeamPalette.RelationOf(Team.Bravo, null), Is.EqualTo(TeamRelation.Enemy));
        }

        [Test]
        public void ColorOfPicksTheColorOfTheRelation()
        {
            var palette = TeamPalette.RedGreen;

            Assert.That(palette.ColorOf(TeamRelation.Friendly), Is.EqualTo(palette.Friendly));
            Assert.That(palette.ColorOf(TeamRelation.Enemy), Is.EqualTo(palette.Enemy));
            Assert.That(palette.ColorOf(TeamRelation.Neutral), Is.EqualTo(palette.Neutral));
            Assert.That(palette.ColorOf(Team.Bravo, Team.Bravo), Is.EqualTo(palette.Friendly));
            Assert.That(palette.ColorOf(Team.Bravo, Team.Alpha), Is.EqualTo(palette.Enemy));
            Assert.That(palette.ColorOf(null, Team.Alpha), Is.EqualTo(palette.Neutral));
            Assert.Throws<ArgumentOutOfRangeException>(() => palette.ColorOf((TeamRelation)3));
        }

        [Test]
        public void TheStandardPaletteHasNoMarks()
        {
            Assert.That(TeamPalette.Standard.UsesMarks, Is.False);
            Assert.That(TeamPalette.Standard.MarkOf(TeamRelation.Friendly), Is.Empty);
            Assert.That(TeamPalette.Standard.MarkOf(TeamRelation.Enemy), Is.Empty);
            Assert.That(TeamPalette.Standard.NameplateText(TeamRelation.Enemy, "P3"), Is.EqualTo("P3"));
        }

        // 色を見分けにくい人向けの組は、色だけに頼らず形でも敵味方を分ける
        [TestCase(ColorPalette.RedGreen)]
        [TestCase(ColorPalette.BlueYellow)]
        public void AccessiblePalettesMarkFriendsWithADiamondAndEnemiesWithATriangle(ColorPalette kind)
        {
            var palette = TeamPalette.For(kind);

            Assert.That(palette.UsesMarks, Is.True);
            Assert.That(palette.MarkOf(TeamRelation.Friendly), Is.EqualTo("◆"));
            Assert.That(palette.MarkOf(TeamRelation.Enemy), Is.EqualTo("▲"));
            Assert.That(palette.MarkOf(TeamRelation.Neutral), Is.Empty);
            Assert.That(palette.NameplateText(TeamRelation.Friendly, "P2"), Is.EqualTo("◆ P2"));
            Assert.That(palette.NameplateText(TeamRelation.Enemy, "P3"), Is.EqualTo("▲ P3"));
            Assert.That(palette.NameplateText(TeamRelation.Enemy, null), Is.EqualTo("▲ "));
        }

        [Test]
        public void PaletteColorRejectsComponentsOutsideZeroToOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaletteColor(-0.1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaletteColor(0f, 1.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PaletteColor(0f, 0f, float.NaN));
        }

        [Test]
        public void PaletteColorFromBytesScalesTo255()
        {
            var color = PaletteColor.FromBytes(255, 0, 51);

            Assert.That(color.R, Is.EqualTo(1f));
            Assert.That(color.G, Is.EqualTo(0f));
            Assert.That(color.B, Is.EqualTo(0.2f).Within(1e-6f));
        }
    }
}
