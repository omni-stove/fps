using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class ModeStatusFormatTests
    {
        [Test]
        public void TheScoresPutTheOwnSideFirstAndMarkASoleLeader()
        {
            var status = Status(new[] { new SideScore(Side.Alpha, 3.7d), new SideScore(Side.Bravo, 1d), new SideScore(new Side(2), 0d) }, Side.Bravo);

            var scores = ModeStatusFormat.Scores(status);

            Assert.That(scores.Count, Is.EqualTo(3));
            Assert.That(scores[0].Side, Is.EqualTo(Side.Bravo));
            Assert.That(scores[0].IsOwn, Is.True);
            Assert.That(scores[1].Side, Is.EqualTo(Side.Alpha));
            Assert.That(scores[1].IsLeading, Is.True);
            Assert.That(Localizer.Resolve(scores[1].Text), Is.EqualTo(Localizer.Resolve(ModeHudText.ScoreText(Side.Alpha, 3d))));
            Assert.That(scores[2].Side, Is.EqualTo(new Side(2)));
            Assert.That(scores[2].IsOwn || scores[2].IsLeading, Is.False);
        }

        [Test]
        public void TiedScoresHaveNoLeader()
        {
            var scores = ModeStatusFormat.Scores(Status(new[] { new SideScore(Side.Alpha, 2d), new SideScore(Side.Bravo, 2d) }, null));

            Assert.That(scores[0].IsLeading || scores[1].IsLeading, Is.False);
            Assert.That(scores[0].Side, Is.EqualTo(Side.Alpha), "without an own side the mode's order is kept");
        }

        [Test]
        public void TheRespawnLineRoundsTheSecondsUp()
        {
            var lines = new List<LocalizedText>();

            ModeStatusFormat.AppendRespawn(new ModeStatus(null, null, Side.Alpha, 1.2f, false, null, null), lines);
            ModeStatusFormat.AppendRespawn(Status(null, Side.Alpha), lines);

            Assert.That(lines, Is.EqualTo(new[] { HudText.Respawn(2) }));
        }

        [Test]
        public void TheTimeAndTheResultFollowTheStatus()
        {
            var playing = new ModeStatus(75.2f, null, null, null, false, null, null);
            var won = new ModeStatus(0f, null, null, null, true, new Side(2), null);
            var drawn = new ModeStatus(0f, null, null, null, true, null, null);

            Assert.That(Localizer.Resolve(ModeStatusFormat.Remaining(playing)), Is.EqualTo("01:16"));
            Assert.That(Localizer.Resolve(ModeStatusFormat.Remaining(Status(null, null))), Is.EqualTo("99:59"), "a mode without a time limit shows the cap");
            Assert.That(Localizer.Resolve(ModeStatusFormat.Clock(5d)), Is.EqualTo("00:05"));
            Assert.That(ModeStatusFormat.Result(playing).IsEmpty, Is.True);
            Assert.That(ModeStatusFormat.Result(won), Is.EqualTo(HudText.Wins(ModeHudText.SideName(new Side(2)))));
            Assert.That(ModeStatusFormat.Result(drawn), Is.EqualTo(HudText.Draw));
        }

        private static ModeStatus Status(IReadOnlyList<SideScore> scores, Side? ownSide)
        {
            return new ModeStatus(null, scores, ownSide, null, false, null, null);
        }
    }
}
