using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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
        public void ForWithPalettesPicksThePaletteOfThatKindFromThem()
        {
            var green = new PaletteColor(0f, 1f, 0f);
            var replaced = new TeamPalette(
                ColorPalette.Standard,
                TeamPalette.Standard.Friendly,
                green,
                TeamPalette.Standard.Neutral,
                TeamPalette.Standard.Highlight,
                TeamPalette.Standard.Objective,
                TeamPalette.Standard.Danger,
                TeamPalette.Standard.Contested,
                TeamPalette.Standard.Safe,
                false);
            var palettes = new[] { replaced, TeamPalette.RedGreen, TeamPalette.BlueYellow };

            Assert.That(TeamPalette.For(ColorPalette.Standard, palettes), Is.SameAs(replaced));
            Assert.That(TeamPalette.For(ColorPalette.Standard, palettes).Enemy, Is.EqualTo(green));
            Assert.That(TeamPalette.For(ColorPalette.RedGreen, palettes), Is.SameAs(TeamPalette.RedGreen));
            Assert.That(TeamPalette.For(ColorPalette.Standard, TeamPalette.All), Is.SameAs(TeamPalette.Standard));
        }

        [Test]
        public void ForWithPalettesRejectsAMissingKindAnUnknownKindAndNoList()
        {
            var onlyStandard = new[] { TeamPalette.Standard };

            var missing = Assert.Throws<ArgumentException>(() => TeamPalette.For(ColorPalette.BlueYellow, onlyStandard));
            Assert.That(missing.ParamName, Is.EqualTo("palettes"));
            Assert.That(missing.Message, Does.Contain("BlueYellow"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamPalette.For((ColorPalette)3, TeamPalette.All));
            Assert.Throws<ArgumentNullException>(() => TeamPalette.For(ColorPalette.Standard, null));
        }

        [Test]
        public void TheConstructorRejectsAnUnknownKind()
        {
            var c = TeamPalette.Standard;

            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPalette(
                (ColorPalette)3, c.Friendly, c.Enemy, c.Neutral, c.Highlight, c.Objective, c.Danger, c.Contested, c.Safe, false));
        }

        [Test]
        public void AllListsEveryKindOnceInOrder()
        {
            // Unity の .NET Standard 2.1 には Enum.GetValues<T>() が無いため、非ジェネリック版を使う
#pragma warning disable CA2263
            var kinds = (ColorPalette[])Enum.GetValues(typeof(ColorPalette));
#pragma warning restore CA2263

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

        // 今までの爆弾の設置場所 (橙と赤)・拠点の競合 (黄)・解除された爆弾 (緑) の色を変えない
        [Test]
        public void TheStandardPaletteKeepsTheObjectiveColorsUsedSoFar()
        {
            var standard = TeamPalette.Standard;

            Assert.That(standard.Objective, Is.EqualTo(new PaletteColor(1f, 0.65f, 0.1f)));
            Assert.That(standard.Danger, Is.EqualTo(new PaletteColor(1f, 0.15f, 0.1f)));
            Assert.That(standard.Contested, Is.EqualTo(new PaletteColor(1f, 0.82f, 0.31f)));
            Assert.That(standard.Safe, Is.EqualTo(new PaletteColor(0.2f, 0.8f, 0.3f)));
        }

        // 同じ画面に並ぶ色の組。爆弾と設置場所 (目的の物・危険・解除済み) と、拠点の輪 (競合とチームと中立)
        private static readonly string[][] _sideBySide =
        {
            new[] { "Objective", "Danger" },
            new[] { "Objective", "Safe" },
            new[] { "Danger", "Safe" },
            new[] { "Contested", "Friendly" },
            new[] { "Contested", "Enemy" },
            new[] { "Contested", "Neutral" },
        };

        private const float _minimumDifference = 0.15f;

        // 1 型・2 型色覚では赤と緑の差が見えないので、明るさか青と黄の軸で離れていないと区別できない
        [TestCase(ColorPalette.Standard)]
        [TestCase(ColorPalette.RedGreen)]
        [TestCase(ColorPalette.BlueYellow)]
        public void TheNewColorsAreNotToldApartByRedAndGreenAlone(ColorPalette kind)
        {
            var palette = TeamPalette.For(kind);

            foreach (var pair in _sideBySide)
            {
                var a = RoleColor(palette, pair[0]);
                var b = RoleColor(palette, pair[1]);
                var lightness = Math.Abs(Luminance(a) - Luminance(b));
                var blueYellow = Math.Abs(BlueYellowAxis(a) - BlueYellowAxis(b));

                Assert.That(
                    lightness >= _minimumDifference || blueYellow >= _minimumDifference,
                    Is.True,
                    kind + ": " + pair[0] + " and " + pair[1] + " differ only in red and green (lightness " + lightness + ", blue-yellow " + blueYellow + ")");
            }
        }

        // 3 型色覚では青と黄の差が見えないので、明るさか赤と緑の軸で離れていないと区別できない
        [Test]
        public void BlueYellowDoesNotTellTheNewColorsApartByBlueAndYellowAlone()
        {
            var palette = TeamPalette.BlueYellow;

            foreach (var pair in _sideBySide)
            {
                var a = RoleColor(palette, pair[0]);
                var b = RoleColor(palette, pair[1]);
                var lightness = Math.Abs(Luminance(a) - Luminance(b));
                var redGreen = Math.Abs(RedGreenAxis(a) - RedGreenAxis(b));

                Assert.That(
                    lightness >= _minimumDifference || redGreen >= _minimumDifference,
                    Is.True,
                    pair[0] + " and " + pair[1] + " differ only in blue and yellow (lightness " + lightness + ", red-green " + redGreen + ")");
            }
        }

        [TestCase(ColorPalette.RedGreen)]
        [TestCase(ColorPalette.BlueYellow)]
        public void AccessiblePalettesChangeTheNewColorsFromStandard(ColorPalette kind)
        {
            var palette = TeamPalette.For(kind);
            var standard = TeamPalette.Standard;

            Assert.That(palette.Objective, Is.Not.EqualTo(standard.Objective));
            Assert.That(palette.Danger, Is.Not.EqualTo(standard.Danger));
            Assert.That(palette.Contested, Is.Not.EqualTo(standard.Contested));
            Assert.That(palette.Safe, Is.Not.EqualTo(standard.Safe));
        }

        private static PaletteColor RoleColor(TeamPalette palette, string role)
        {
            switch (role)
            {
                case "Objective":
                    return palette.Objective;
                case "Danger":
                    return palette.Danger;
                case "Safe":
                    return palette.Safe;
                case "Contested":
                    return palette.Contested;
                case "Friendly":
                    return palette.Friendly;
                case "Enemy":
                    return palette.Enemy;
                case "Neutral":
                    return palette.Neutral;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "unknown role");
            }
        }

        // Rec. 709 の重み。ガンマを外さずに近似する
        private static float Luminance(PaletteColor color)
        {
            return (0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B);
        }

        private static float BlueYellowAxis(PaletteColor color)
        {
            return color.B - ((color.R + color.G) * 0.5f);
        }

        private static float RedGreenAxis(PaletteColor color)
        {
            return color.R - color.G;
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
            Assert.That(palette.Objective, Is.Not.EqualTo(palette.Danger));
            Assert.That(palette.Objective, Is.Not.EqualTo(palette.Safe));
            Assert.That(palette.Danger, Is.Not.EqualTo(palette.Safe));
            Assert.That(palette.Contested, Is.Not.EqualTo(palette.Friendly));
            Assert.That(palette.Contested, Is.Not.EqualTo(palette.Enemy));
            Assert.That(palette.Contested, Is.Not.EqualTo(palette.Neutral));
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
