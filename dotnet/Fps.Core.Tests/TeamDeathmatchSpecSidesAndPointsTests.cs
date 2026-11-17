using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TeamDeathmatchSpecSidesAndPointsTests
    {
        private const int _alpha = 1;
        private const int _bravo = 11;

        [Test]
        public void TheDefaultsAreTwoSidesOfEightAndOnePointPerKill()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f);

            Assert.That(spec.SideCount, Is.EqualTo(2));
            Assert.That(spec.SideCapacity, Is.EqualTo(8));
            Assert.That(spec.PointsPerKill, Is.EqualTo(1));
        }

        [Test]
        public void TheSpecKeepsTheGivenSidesAndPoints()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f, null, 4, 3, 5);

            Assert.That(spec.SideCount, Is.EqualTo(4));
            Assert.That(spec.SideCapacity, Is.EqualTo(3));
            Assert.That(spec.PointsPerKill, Is.EqualTo(5));
        }

        [Test]
        public void AKillScoresTwoPointsWhenPointsPerKillIsTwo()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f, null, 2, 8, 2));
            match.AddPlayer(_alpha, Team.Alpha);
            match.AddPlayer(_bravo, Team.Bravo);
            match.Start();

            match.ReportKill(_alpha, _bravo);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(2));
            Assert.That(match.GetScore(Team.Bravo), Is.EqualTo(0));
            Assert.That(match.GetKills(_alpha), Is.EqualTo(1));
        }

        [Test]
        public void TheScoreLimitCountsPointsNotKills()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(4, 60f, 3f, null, 2, 8, 2));
            match.AddPlayer(_alpha, Team.Alpha);
            match.AddPlayer(_bravo, Team.Bravo);
            match.AddPlayer(_bravo + 1, Team.Bravo);
            match.Start();

            match.ReportKill(_alpha, _bravo);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));

            match.ReportKill(_alpha, _bravo + 1);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(4));
            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void ATeamKillScoresNothingEvenWithMorePointsPerKill()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f, null, 2, 8, 3));
            match.AddPlayer(_alpha, Team.Alpha);
            match.AddPlayer(2, Team.Alpha);
            match.Start();

            match.ReportKill(_alpha, 2);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(0));
        }

        [Test]
        public void TheTwoTeamModeRejectsASpecWithMoreThanTwoSides()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f, null, 3);

            var error = Assert.Throws<ArgumentException>(() => new TeamDeathmatch(spec));
            Assert.That(error.Message, Does.Contain("3"));
        }

        [TestCase(1, 8, 1, "sideCount")]
        [TestCase(0, 8, 1, "sideCount")]
        [TestCase(2, 0, 1, "sideCapacity")]
        [TestCase(2, -1, 1, "sideCapacity")]
        [TestCase(2, 8, 0, "pointsPerKill")]
        [TestCase(2, 8, -2, "pointsPerKill")]
        public void InvalidSidesOrPointsAreRejected(int sideCount, int sideCapacity, int pointsPerKill, string parameter)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(
                () => new TeamDeathmatchSpec(10, 60f, 3f, null, sideCount, sideCapacity, pointsPerKill));

            Assert.That(error.ParamName, Is.EqualTo(parameter));
        }
    }
}
