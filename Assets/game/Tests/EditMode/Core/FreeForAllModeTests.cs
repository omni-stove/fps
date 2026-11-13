using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class FreeForAllModeTests
    {
        private const float _timeLimit = 60f;
        private const int _scoreLimit = 3;
        private const float _respawn = 2f;

        [Test]
        public void EveryPlayerGetsTheirOwnSide()
        {
            var session = Create();

            Assert.That(session.Join(10), Is.EqualTo(new Side(0)));
            Assert.That(session.Join(11), Is.EqualTo(new Side(1)));
            session.AddPlayer(12, new Side(4));
            Assert.That(session.Join(13), Is.EqualTo(new Side(2)));

            Assert.That(session.Layout, Is.InstanceOf<FreeForAll>());
            Assert.Throws<InvalidOperationException>(() => session.AddPlayer(14, new Side(4)), "a side holds only one player");
        }

        [Test]
        public void EachKillScoresOnePointForTheKiller()
        {
            var session = Started(3);

            session.ReportKill(0, 1);
            session.ReportKill(2, 0);

            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(1d));
            Assert.That(session.GetScore(new Side(1)), Is.Zero);
            Assert.That(session.GetScore(new Side(2)), Is.EqualTo(1d));
            Assert.That(session.GetKills(0), Is.EqualTo(1));
            Assert.That(session.GetDeaths(0), Is.EqualTo(1));
        }

        [Test]
        public void ADeathWithoutAKillerScoresNothing()
        {
            var session = Started(2);

            session.ReportDeath(1);
            session.ReportKill(0, 0);

            Assert.That(session.GetScore(new Side(0)), Is.Zero);
            Assert.That(session.GetScore(new Side(1)), Is.Zero);
            Assert.That(session.GetDeaths(0), Is.EqualTo(1));
            Assert.That(session.GetDeaths(1), Is.EqualTo(1));
        }

        [Test]
        public void TheFallenComeBackAfterTheRespawnSeconds()
        {
            var session = Started(2);
            var respawned = -1;
            session.RespawnReady += id => respawned = id;

            session.ReportKill(0, 1);
            Assert.That(session.IsAwaitingRespawn(1), Is.True);

            session.Tick(_respawn / 2f);
            Assert.That(respawned, Is.EqualTo(-1));

            session.Tick(_respawn / 2f);
            Assert.That(respawned, Is.EqualTo(1));
            Assert.That(session.IsDown(1), Is.False);
        }

        [Test]
        public void ReachingTheScoreLimitWinsAtOnce()
        {
            var session = Started(3);
            Side? ended = null;
            var endedCount = 0;
            session.Ended += winner =>
            {
                ended = winner;
                endedCount++;
            };

            KillTimes(session, 2, 0, _scoreLimit - 1);
            session.ReportKill(1, 0);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));

            KillTimes(session, 2, 1, 1);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(2)));
            Assert.That(ended, Is.EqualTo((Side?)new Side(2)));
            Assert.That(endedCount, Is.EqualTo(1));
        }

        [Test]
        public void TimeUpGivesTheWinToTheSoleTopScorer()
        {
            var session = Started(3);
            session.ReportKill(2, 1);
            session.Tick(_respawn);
            session.ReportKill(1, 0);
            session.ReportKill(1, 2);

            session.Tick(_timeLimit);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(1)));
        }

        [Test]
        public void TimeUpWithTiedTopScorersIsADraw()
        {
            var session = Started(4);
            session.ReportKill(1, 0);
            session.ReportKill(2, 3);

            session.Tick(_timeLimit);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
        }

        [Test]
        public void TimeUpWithoutAnyKillIsADraw()
        {
            var session = Started(4);

            session.Tick(_timeLimit);

            Assert.That(session.Winner, Is.Null);
        }

        [Test]
        public void CreateRejectsBadSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllMode.Create(0f, _scoreLimit, _respawn));
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllMode.Create(_timeLimit, 0, _respawn));
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllMode.Create(_timeLimit, _scoreLimit, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllMode.Create(_timeLimit, _scoreLimit, float.PositiveInfinity));
        }

        [Test]
        public void TheCreatedSessionWaitsWithNoPlayers()
        {
            var session = FreeForAllMode.Create(_timeLimit, _scoreLimit, _respawn);

            Assert.That(session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(session.PlayerCount, Is.Zero);
            Assert.That(session.ScoreLimit, Is.EqualTo((double?)_scoreLimit));
            Assert.That(session.TimeLimitSeconds, Is.EqualTo(_timeLimit));
            Assert.That(session.TracksDowns, Is.True);
        }

        [Test]
        public void StandingRanksTiesTogether()
        {
            var session = Started(4);
            KillTimes(session, 0, 3, 2);
            session.ReportKill(1, 3);
            session.Tick(_respawn);
            session.ReportKill(2, 3);

            Assert.That(FreeForAllMode.Standing(session, new Side(0)), Is.EqualTo(new FreeForAllStanding(1, 4, 2, 1)));
            Assert.That(FreeForAllMode.Standing(session, new Side(1)), Is.EqualTo(new FreeForAllStanding(2, 4, 1, -1)));
            Assert.That(FreeForAllMode.Standing(session, new Side(2)), Is.EqualTo(new FreeForAllStanding(2, 4, 1, -1)));
            Assert.That(FreeForAllMode.Standing(session, new Side(3)), Is.EqualTo(new FreeForAllStanding(4, 4, 0, -2)));
            Assert.That(FreeForAllMode.Standing(session, new Side(0)).IsSoleLeader, Is.True);
            Assert.That(FreeForAllMode.Standing(session, new Side(1)).IsSoleLeader, Is.False);
        }

        [Test]
        public void StandingShowsATieForFirst()
        {
            var session = Started(4);
            session.ReportKill(0, 2);
            session.ReportKill(1, 3);

            var standing = FreeForAllMode.Standing(session, new Side(1));

            Assert.That(standing.Rank, Is.EqualTo(1));
            Assert.That(standing.Margin, Is.EqualTo((int?)0));
            Assert.That(standing.IsSoleLeader, Is.False);
        }

        [Test]
        public void StandingOfALonePlayerHasNoMargin()
        {
            var session = Started(1);

            var standing = FreeForAllMode.Standing(session, new Side(0));

            Assert.That(standing, Is.EqualTo(new FreeForAllStanding(1, 1, 0, null)));
            Assert.That(standing.IsSoleLeader, Is.True);
        }

        [Test]
        public void StandingRejectsASideThatHasNotJoined()
        {
            var session = Started(2);

            Assert.Throws<ArgumentNullException>(() => FreeForAllMode.Standing(null, new Side(0)));
            Assert.Throws<ArgumentException>(() => FreeForAllMode.Standing(session, new Side(5)));
        }

        [Test]
        public void RankTextNamesTheRankAndTheGap()
        {
            Assert.That(FreeForAllMode.RankText(new FreeForAllStanding(3, 6, 1, -4)), Is.EqualTo("順位 3/6  1位と -4"));
            Assert.That(FreeForAllMode.RankText(new FreeForAllStanding(1, 6, 5, 2)), Is.EqualTo("順位 1/6  2位と +2"));
            Assert.That(FreeForAllMode.RankText(new FreeForAllStanding(1, 6, 5, 0)), Is.EqualTo("順位 1/6  1位と同点"));
            Assert.That(FreeForAllMode.RankText(new FreeForAllStanding(1, 1, 0, null)), Is.EqualTo("順位 1/1"));
        }

        [Test]
        public void RankTextReadsTheSession()
        {
            var session = Started(3);
            session.ReportKill(2, 0);

            Assert.That(FreeForAllMode.RankText(session, new Side(0)), Is.EqualTo("順位 2/3  1位と -1"));
            Assert.That(FreeForAllMode.RankText(session, new Side(2)), Is.EqualTo("順位 1/3  2位と +1"));
        }

        [Test]
        public void StandingRejectsAnImpossibleRank()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreeForAllStanding(0, 3, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreeForAllStanding(4, 3, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FreeForAllStanding(1, 0, 0, null));
        }

        private static ModeSession Create() => FreeForAllMode.Create(_timeLimit, _scoreLimit, _respawn);

        /// <summary>count 人を参加順に陣営 0, 1, ... へ入れて始める。プレイヤーの ID は陣営の ID と同じ。</summary>
        private static ModeSession Started(int count)
        {
            var session = Create();
            for (var id = 0; id < count; id++)
            {
                session.Join(id);
            }

            session.Start();
            return session;
        }

        /// <summary>killer が victim を times 回倒す。倒すたびに戻るまで時間を進める。</summary>
        private static void KillTimes(ModeSession session, int killer, int victim, int times)
        {
            for (var i = 0; i < times; i++)
            {
                session.ReportKill(killer, victim);
                session.Tick(_respawn);
            }
        }
    }
}
