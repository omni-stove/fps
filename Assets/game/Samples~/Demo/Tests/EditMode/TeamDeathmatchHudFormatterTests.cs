using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class TeamDeathmatchHudFormatterTests
    {
        private readonly TeamDeathmatchHudFormatter _formatter = new TeamDeathmatchHudFormatter();

        [Test]
        public void ItIsRegisteredForAPlainModeSession()
        {
            var status = Status(detail: TeamDeathmatchRunner.CreateSession(2, 4, 60f, 5, 1f));

            Assert.That(ModeHudFormatters.For(status), Is.InstanceOf<TeamDeathmatchHudFormatter>());
        }

        [Test]
        public void TheTimeIsTheRemainingSecondsRoundedUp()
        {
            Assert.That(Localizer.Resolve(_formatter.Time(Status(remaining: 65f))), Is.EqualTo("01:05"));
            Assert.That(Localizer.Resolve(_formatter.Time(Status(remaining: 0.2f))), Is.EqualTo("00:01"));
            Assert.That(Localizer.Resolve(_formatter.Time(Status(remaining: 0f))), Is.EqualTo("00:00"));
        }

        [Test]
        public void AModeWithoutATimeLimitHidesTheTime()
        {
            Assert.That(Localizer.Resolve(_formatter.Time(Status(remaining: null))), Is.Empty);
        }

        [Test]
        public void TheScoresPutTheOwnSideFirstAndMarkASoleLeader()
        {
            var status = Status(
                sideScores: new[] { new SideScore(Side.Alpha, 1d), new SideScore(Side.Bravo, 3.7d), new SideScore(new Side(2), 0d) },
                ownSide: Side.Bravo);

            var scores = _formatter.Scores(status);

            Assert.That(scores.Select(s => s.Side), Is.EqualTo(new[] { Side.Bravo, Side.Alpha, new Side(2) }));
            Assert.That(scores.Select(s => Localizer.Resolve(s.Text)), Is.EqualTo(new[] { "BRAVO 3", "ALPHA 1", "SIDE 2 0" }));
            Assert.That(scores.Select(s => s.IsOwn), Is.EqualTo(new[] { true, false, false }));
            Assert.That(scores.Select(s => s.IsLeading), Is.EqualTo(new[] { true, false, false }));
        }

        [Test]
        public void TiedLeadersAreNotMarked()
        {
            var status = Status(sideScores: new[] { new SideScore(Side.Alpha, 2d), new SideScore(Side.Bravo, 2d) });

            var scores = _formatter.Scores(status);

            Assert.That(scores.Select(s => s.Side), Is.EqualTo(new[] { Side.Alpha, Side.Bravo }), "without an own side the mode's order is kept");
            Assert.That(scores.Any(s => s.IsLeading || s.IsOwn), Is.False);
        }

        [Test]
        public void TheLinesAreOnlyTheRespawnCountdownWhileWaiting()
        {
            Assert.That(Lines(Status(respawn: 0.4f)), Is.EqualTo(new[] { "リスポーンまで 1 秒" }));
            Assert.That(Lines(Status(respawn: null)), Is.Empty);
        }

        [Test]
        public void TheResultNamesTheWinnerOrADrawOnlyAfterTheEnd()
        {
            Assert.That(Localizer.Resolve(_formatter.Result(Status(isEnded: true, winner: Side.Alpha))), Is.EqualTo("ALPHA の勝ち"));
            Assert.That(Localizer.Resolve(_formatter.Result(Status(isEnded: true, winner: new Side(2)))), Is.EqualTo("SIDE 2 の勝ち"));
            Assert.That(Localizer.Resolve(_formatter.Result(Status(isEnded: true, winner: null))), Is.EqualTo("引き分け"));
            Assert.That(Localizer.Resolve(_formatter.Result(Status(isEnded: false, winner: null))), Is.Empty);
        }

        [Test]
        public void ANullStatusIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => _formatter.Time(null));
            Assert.Throws<System.ArgumentNullException>(() => _formatter.Scores(null));
            Assert.Throws<System.ArgumentNullException>(() => _formatter.AppendLines(null, new List<LocalizedText>()));
            Assert.Throws<System.ArgumentNullException>(() => _formatter.AppendLines(Status(), null));
            Assert.Throws<System.ArgumentNullException>(() => _formatter.Result(null));
        }

        private string[] Lines(ModeStatus status)
        {
            var lines = new List<LocalizedText>();
            _formatter.AppendLines(status, lines);
            return lines.Select(Localizer.Resolve).ToArray();
        }

        private static ModeStatus Status(
            float? remaining = 60f,
            IReadOnlyList<SideScore> sideScores = null,
            Side? ownSide = null,
            float? respawn = null,
            bool isEnded = false,
            Side? winner = null,
            object detail = null)
        {
            return new ModeStatus(remaining, sideScores, ownSide, respawn, isEnded, winner, detail);
        }
    }
}
