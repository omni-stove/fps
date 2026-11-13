using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ModeLogTests
    {
        private static ModeSession ThreeSides(double scoreLimit = 3d)
        {
            var session = new ModeSession(
                new Squads(3, 4),
                60f,
                new IScoreRule[] { new KillScore() },
                new IndividualRespawn(1f),
                new IWinCondition[] { new ScoreLimitWin(scoreLimit), new TimeLimitWin() });
            session.AddPlayer(0, new Side(0));
            session.AddPlayer(1, new Side(1));
            session.AddPlayer(2, new Side(2));
            return session;
        }

        [Test]
        public void StateListsEverySideScore()
        {
            var session = ThreeSides();
            session.Start();
            session.ReportKill(0, 1);
            session.Tick(2.5f);

            Assert.That(ModeLog.State(session), Is.EqualTo("[Mode] state=Playing remaining=57.5 score=0:1,1:0,2:0"));
            Assert.That(ModeLog.Score(session), Is.EqualTo("[Mode] score 0=1 1=0 2=0"));
        }

        [Test]
        public void StateNamesTheWinnerAfterTheEnd()
        {
            var session = ThreeSides(1d);
            session.Start();
            session.ReportKill(2, 0);

            Assert.That(ModeLog.State(session), Is.EqualTo("[Mode] state=Ended remaining=60.0 score=0:0,1:0,2:1 winner=2"));
            Assert.That(ModeLog.Ended(session.Winner), Is.EqualTo("[Mode] ended winner=2"));
            Assert.That(ModeLog.Ended(null), Is.EqualTo("[Mode] ended winner=draw"));
        }

        [Test]
        public void EventsUseInvariantNumbers()
        {
            Assert.That(ModeLog.Joined(3, "Enemy-2", new Side(2)), Is.EqualTo("[Mode] joined player=3 name=Enemy-2 side=2"));
            Assert.That(ModeLog.Joined(3, null, new Side(2)), Is.EqualTo("[Mode] joined player=3 name=- side=2"));
            Assert.That(ModeLog.Kill(0, 2), Is.EqualTo("[Mode] kill killer=0 victim=2"));
            Assert.That(ModeLog.TeamKill(1, 4), Is.EqualTo("[Mode] teamkill killer=1 victim=4"));
            Assert.That(ModeLog.Death(5), Is.EqualTo("[Mode] death player=5"));
            Assert.That(
                ModeLog.Spawn(2, new Side(1), new Vector3(4f, 0f, -6.126f)),
                Is.EqualTo("[Mode] spawn player=2 side=1 at=(4.00, 0.00, -6.13)"));
            Assert.That(ModeLog.Restart(), Is.EqualTo("[Mode] restart"));
        }

        [Test]
        public void FractionalScoresKeepTwoDecimals()
        {
            Assert.That(ModeLog.FormatScore(3d), Is.EqualTo("3"));
            Assert.That(ModeLog.FormatScore(2.345d), Is.EqualTo("2.35"));
        }

        [Test]
        public void RejectsNullSession()
        {
            Assert.Throws<ArgumentNullException>(() => ModeLog.State(null));
            Assert.Throws<ArgumentNullException>(() => ModeLog.Score(null));
        }
    }
}
