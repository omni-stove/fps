using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TeamAssignerTests
    {
        [Test]
        public void StartsWithAlphaWhenEverythingIsEven()
        {
            Assert.That(TeamAssigner.Choose(0, 0, 0, 0), Is.EqualTo(Team.Alpha));
            Assert.That(TeamAssigner.Choose(2, 2, 3, 3), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void PicksTheTeamWithFewerPlayers()
        {
            Assert.That(TeamAssigner.Choose(1, 0, 0, 0), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.Choose(1, 2, 0, 0), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void PlayerCountWinsOverScore()
        {
            // 点の低いほうより人数の少ないほうを優先する
            Assert.That(TeamAssigner.Choose(1, 2, 9, 0), Is.EqualTo(Team.Alpha));
            Assert.That(TeamAssigner.Choose(2, 1, 0, 9), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void EvenPlayersGoToTheLowerScore()
        {
            Assert.That(TeamAssigner.Choose(2, 2, 5, 3), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.Choose(2, 2, 3, 5), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void RejectsNegativeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(-1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(0, -1, 0, 0));
        }
    }
}
