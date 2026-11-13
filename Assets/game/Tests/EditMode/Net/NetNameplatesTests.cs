using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="NetNameplates"/> のネームプレートの文字と色が、自分のチームから見た敵味方と設定の色の組で決まること。
    /// </summary>
    public sealed class NetNameplatesTests
    {
        [Test]
        public void TheStandardPaletteShowsOnlyTheNameInTheTeamColor()
        {
            var (text, color) = NetNameplates.Describe(TeamPalette.Standard, Team.Bravo, Team.Alpha, 3);

            Assert.That(text, Is.EqualTo(KillFeedFormat.Player(3)));
            Assert.That(color, Is.EqualTo(TeamColors.ToColor(TeamPalette.Standard.Enemy)));
        }

        [Test]
        public void RedGreenMarksAlliesWithADiamondAndEnemiesWithATriangle()
        {
            var ally = NetNameplates.Describe(TeamPalette.RedGreen, Team.Bravo, Team.Bravo, 2);
            var enemy = NetNameplates.Describe(TeamPalette.RedGreen, Team.Alpha, Team.Bravo, 5);

            Assert.That(ally.Text, Is.EqualTo("◆ " + KillFeedFormat.Player(2)));
            Assert.That(ally.Color, Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Friendly)));
            Assert.That(enemy.Text, Is.EqualTo("▲ " + KillFeedFormat.Player(5)));
            Assert.That(enemy.Color, Is.EqualTo(TeamColors.ToColor(TeamPalette.RedGreen.Enemy)));
        }

        [Test]
        public void BlueYellowAlsoMarksTheSides()
        {
            var ally = NetNameplates.Describe(TeamPalette.BlueYellow, Team.Alpha, Team.Alpha, 1);
            var enemy = NetNameplates.Describe(TeamPalette.BlueYellow, Team.Bravo, Team.Alpha, 4);

            Assert.That(ally.Text, Does.StartWith(TeamPalette.FriendlyMark));
            Assert.That(enemy.Text, Does.StartWith(TeamPalette.EnemyMark));
            Assert.That(ally.Color, Is.Not.EqualTo(enemy.Color));
        }
    }
}
