using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ModeHudTextTests
    {
        private static readonly string[] _expectedOwnFirst = { "BRAVO 0", "ALPHA 2", "SIDE 2 1" };
        private static readonly string[] _expectedById = { "ALPHA 2", "BRAVO 0", "SIDE 2 1" };

        private static ModeSession ThreeSides(double scoreLimit = 10d)
        {
            var session = new ModeSession(
                new Squads(3, 4),
                90f,
                new IScoreRule[] { new KillScore() },
                new IndividualRespawn(0f),
                new IWinCondition[] { new ScoreLimitWin(scoreLimit), new TimeLimitWin() });
            session.AddPlayer(0, Side.Alpha);
            session.AddPlayer(1, Side.Bravo);
            session.AddPlayer(2, new Side(2));
            return session;
        }

        [Test]
        public void SideNamesFollowTheTeamNames()
        {
            Assert.That(ModeHudText.SideName(Side.Alpha), Is.EqualTo("ALPHA"));
            Assert.That(ModeHudText.SideName(Side.Bravo), Is.EqualTo("BRAVO"));
            Assert.That(ModeHudText.SideName(new Side(2)), Is.EqualTo("SIDE 2"));
        }

        [Test]
        public void DisplayedScoresAreFlooredAndClamped()
        {
            Assert.That(ModeHudText.DisplayScore(3.7d), Is.EqualTo(3));
            Assert.That(ModeHudText.DisplayScore(-1d), Is.Zero);
            Assert.That(ModeHudText.DisplayScore(double.NaN), Is.Zero);
            Assert.That(ModeHudText.DisplayScore(1e12d), Is.EqualTo(int.MaxValue));
            Assert.That(ModeHudText.ScoreText(new Side(2), 3.7d), Is.EqualTo("SIDE 2 3"));
        }

        [Test]
        public void ScoresPutTheOwnSideFirstAndMarkTheLeader()
        {
            var session = ThreeSides();
            session.Start();
            session.ReportKill(0, 1);
            session.ReportKill(0, 2);
            session.ReportKill(2, 1);

            var lines = ModeHudText.Scores(session, Side.Bravo);

            Assert.That(Texts(lines), Is.EqualTo(_expectedOwnFirst));
            Assert.That(lines[0].IsOwn, Is.True);
            Assert.That(lines[1].IsOwn, Is.False);
            Assert.That(lines[1].IsLeading, Is.True);
            Assert.That(lines[0].IsLeading, Is.False);
            Assert.That(lines[2].IsLeading, Is.False);
        }

        [Test]
        public void ScoresWithoutAnOwnSideKeepIdOrderAndNoLeaderOnATie()
        {
            var session = ThreeSides();
            session.Start();
            session.ReportKill(0, 1);
            session.ReportKill(0, 2);
            session.ReportKill(2, 1);

            Assert.That(Texts(ModeHudText.Scores(session, null)), Is.EqualTo(_expectedById));
            Assert.That(Texts(ModeHudText.Scores(session, new Side(7))), Is.EqualTo(_expectedById));

            var tied = ThreeSides();
            foreach (var line in ModeHudText.Scores(tied, null))
            {
                Assert.That(line.IsLeading, Is.False);
            }
        }

        [Test]
        public void TimeAndResultFollowTheSession()
        {
            var session = ThreeSides(1d);
            Assert.That(ModeHudText.Time(session), Is.EqualTo("01:30"));
            Assert.That(ModeHudText.Result(session), Is.EqualTo(HudFormat.NoResult));

            session.Start();
            session.ReportKill(2, 0);

            Assert.That(ModeHudText.Result(session), Is.EqualTo("SIDE 2 の勝ち"));
        }

        [Test]
        public void ADrawIsShownAsADraw()
        {
            var session = ThreeSides();
            session.Start();
            session.Tick(90f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(ModeHudText.Result(session), Is.EqualTo(HudFormat.Draw));
        }

        [Test]
        public void RejectsNullSession()
        {
            Assert.Throws<ArgumentNullException>(() => ModeHudText.Scores(null, null));
            Assert.Throws<ArgumentNullException>(() => ModeHudText.Time(null));
            Assert.Throws<ArgumentNullException>(() => ModeHudText.Result(null));
        }

        private static string[] Texts(System.Collections.Generic.IReadOnlyList<ModeHudScoreLine> lines)
        {
            var texts = new string[lines.Count];
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i] = lines[i].Text;
            }

            return texts;
        }
    }
}
