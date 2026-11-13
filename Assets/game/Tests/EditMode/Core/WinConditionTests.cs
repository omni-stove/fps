using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WinConditionTests
    {
        [Test]
        public void OutcomesCarryTheWinner()
        {
            Assert.That(ModeOutcome.Undecided.IsDecided, Is.False);
            Assert.That(ModeOutcome.Draw.IsDecided, Is.True);
            Assert.That(ModeOutcome.Draw.Winner, Is.Null);
            Assert.That(ModeOutcome.Win(new Side(2)).Winner, Is.EqualTo((Side?)new Side(2)));
            Assert.That(ModeOutcome.Decided(null).IsDecided, Is.True);
        }

        [Test]
        public void ScoreLimitOnlyCarriesTheLimit()
        {
            var condition = new ScoreLimitWin(25d);
            var session = Session(new FreeForAll(), new NoRespawn());

            Assert.That(condition.ScoreLimit, Is.EqualTo(25d));
            Assert.That(condition.CheckStanding(session).IsDecided, Is.False);
            Assert.That(condition.CheckTimeUp(session).IsDecided, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreLimitWin(0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreLimitWin(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreLimitWin(double.PositiveInfinity));
        }

        [Test]
        public void TimeLimitPicksTheLeadingSideOrDraws()
        {
            var condition = new TimeLimitWin();
            var session = Session(new FreeForAll(), null);
            session.Join(10);
            session.Join(11);
            session.Join(12);
            session.Start();

            Assert.That(condition.ScoreLimit, Is.Null);
            Assert.That(condition.CheckTimeUp(session).IsDecided, Is.True);
            Assert.That(condition.CheckTimeUp(session).Winner, Is.Null);

            session.AddScore(new Side(1), 2d);
            session.AddScore(new Side(2), 2d);
            Assert.That(condition.CheckTimeUp(session).Winner, Is.Null);

            session.AddScore(new Side(2), 0.5d);
            Assert.That(condition.CheckTimeUp(session).Winner, Is.EqualTo((Side?)new Side(2)));
        }

        [Test]
        public void LastSideStandingWaitsUntilOneSideIsLeft()
        {
            var condition = new LastSideStandingWin();
            var session = Session(new FreeForAll(), new NoRespawn());
            session.Join(10);
            session.Join(11);
            session.Join(12);
            session.Start();

            Assert.That(condition.CheckStanding(session).IsDecided, Is.False);

            session.ReportKill(10, 11);
            Assert.That(condition.CheckStanding(session).IsDecided, Is.False);

            session.ReportKill(10, 12);
            var outcome = condition.CheckStanding(session);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.EqualTo((Side?)new Side(0)));
        }

        [Test]
        public void LastSideStandingDrawsWhenEveryoneIsOut()
        {
            var condition = new LastSideStandingWin();
            var session = Session(new FreeForAll(), new NoRespawn());
            session.Join(10);
            session.Join(11);
            session.Start();

            session.ReportDeath(10);
            session.ReportDeath(11);

            var outcome = condition.CheckStanding(session);
            Assert.That(outcome.IsDecided, Is.True);
            Assert.That(outcome.Winner, Is.Null);
        }

        [Test]
        public void LastSideStandingIgnoresPlayersWhoLeftFreeForAll()
        {
            var condition = new LastSideStandingWin();
            var session = Session(new FreeForAll(), new NoRespawn());
            session.Join(10);
            session.Join(11);
            session.Start();

            session.RemovePlayer(11);
            session.ReportDeath(10);

            Assert.That(condition.CheckStanding(session).Winner, Is.Null);
            Assert.That(condition.CheckStanding(session).IsDecided, Is.True);
        }

        [Test]
        public void LastSideStandingGivesAnEmptyFixedSideTheWin()
        {
            var condition = new LastSideStandingWin();
            var session = Session(new TwoTeams(), new NoRespawn());
            session.Join(10);
            session.Join(11);
            session.Start();

            session.RemovePlayer(11);
            Assert.That(condition.CheckStanding(session).IsDecided, Is.False);

            session.ReportDeath(10);
            Assert.That(condition.CheckStanding(session).Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void LastSideStandingNeedsPlayersWhoDoNotReturn()
        {
            var condition = new LastSideStandingWin();
            var session = Session(new TwoTeams(), new IndividualRespawn(5f));
            session.Join(10);
            session.Join(11);
            session.Start();

            session.ReportKill(10, 11);

            Assert.That(session.IsDown(11), Is.True);
            Assert.That(condition.CheckStanding(session).IsDecided, Is.False);
        }

        private static ModeSession Session(ITeamLayout layout, IRespawnPolicy respawn)
        {
            return new ModeSession(layout, 60f, new IScoreRule[0], respawn, new IWinCondition[0]);
        }
    }
}
