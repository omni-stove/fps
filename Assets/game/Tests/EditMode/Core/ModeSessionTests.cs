using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ModeSessionTests
    {
        [Test]
        public void JoinAssignsSidesInJoinOrder()
        {
            var session = Create(new TwoTeams());

            Assert.That(session.NextSide, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(session.Join(10), Is.EqualTo(Side.Alpha));
            Assert.That(session.Join(11), Is.EqualTo(Side.Bravo));
            session.RemovePlayer(11);
            Assert.That(session.Join(12), Is.EqualTo(Side.Alpha));

            Assert.That(session.GetSide(10), Is.EqualTo(Side.Alpha));
            Assert.That(session.CountMembers(Side.Alpha), Is.EqualTo(2));
            Assert.That(session.CountMembers(Side.Bravo), Is.Zero);
            Assert.That(session.GetPlayerIds(), Is.EqualTo(new[] { 10, 12 }));
            Assert.That(session.Sides, Is.EqualTo(new[] { Side.Alpha, Side.Bravo }));
        }

        [Test]
        public void FreeForAllAddsASidePerPlayer()
        {
            var session = Create(new FreeForAll());

            Assert.That(session.Sides, Is.Empty);
            session.Join(10);
            session.Join(11);

            Assert.That(session.Sides, Is.EqualTo(new[] { new Side(0), new Side(1) }));
            Assert.That(session.GetScore(new Side(1)), Is.Zero);
            Assert.That(session.GetScore(new Side(9)), Is.Zero);
        }

        [Test]
        public void FullSquadsRejectMorePlayers()
        {
            var session = Create(new Squads(2, 1));
            session.Join(10);
            session.Join(11);

            Assert.That(session.NextSide, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Join(12));
            Assert.Throws<InvalidOperationException>(() => session.AddPlayer(12, new Side(0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AddPlayer(12, new Side(2)));
            Assert.That(session.HasPlayer(12), Is.False);

            session.RemovePlayer(10);
            Assert.That(session.Join(12), Is.EqualTo(new Side(0)));
        }

        [Test]
        public void KillOfAnotherSideScoresForTheKillerSide()
        {
            var session = Create(new FreeForAll(), scoreRules: new IScoreRule[] { new KillScore(2d) }, respawn: new IndividualRespawn(1f));
            session.Join(10);
            session.Join(11);
            session.Start();

            session.ReportKill(10, 11);

            Assert.That(session.GetScore(new Side(0)), Is.EqualTo(2d));
            Assert.That(session.GetKills(10), Is.EqualTo(1));
            Assert.That(session.GetDeaths(11), Is.EqualTo(1));
            Assert.That(session.IsAwaitingRespawn(11), Is.True);
        }

        [Test]
        public void TeamKillAndSuicideOnlyCountTheDeath()
        {
            var session = Create(new TwoTeams(), scoreRules: new IScoreRule[] { new KillScore() });
            session.AddPlayer(10, Side.Alpha);
            session.AddPlayer(11, Side.Alpha);
            session.Start();

            session.ReportKill(10, 11);
            session.ReportKill(10, 10);

            Assert.That(session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(session.GetKills(10), Is.Zero);
            Assert.That(session.GetDeaths(10), Is.EqualTo(1));
            Assert.That(session.GetDeaths(11), Is.EqualTo(1));
        }

        [Test]
        public void ReachingTheScoreLimitEndsWithTheScorer()
        {
            var session = Create(
                new TwoTeams(),
                scoreRules: new IScoreRule[] { new KillScore() },
                winConditions: new IWinCondition[] { new ScoreLimitWin(2d), new TimeLimitWin() });
            var events = new List<string>();
            session.StateChanged += state => events.Add(state + " " + session.Winner);
            session.Ended += winner => events.Add("Ended " + winner);
            session.Join(10);
            session.Join(11);
            session.Start();

            session.ReportKill(10, 11);
            session.ReportKill(10, 11);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(events, Is.EqualTo(new[] { "Playing ", "Ended Side 0", "Ended Side 0" }));
        }

        [Test]
        public void AddScoreWorksOnlyWhilePlayingAndCanEndTheMatch()
        {
            var session = Create(new TwoTeams(), winConditions: new IWinCondition[] { new ScoreLimitWin(5d) });
            session.Join(10);
            session.Join(11);

            session.AddScore(Side.Bravo, 3d);
            Assert.That(session.GetScore(Side.Bravo), Is.Zero);

            session.Start();
            session.AddScore(Side.Bravo, 3d);
            Assert.That(session.GetScore(Side.Bravo), Is.EqualTo(3d));

            session.AddScore(Side.Bravo, 2d);
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AddScore(new Side(5), 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AddScore(Side.Alpha, -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AddScore(Side.Alpha, double.NaN));
        }

        [Test]
        public void TimedPointsUseTheRateAtTheStartOfTheTick()
        {
            var rule = new FixedRateScore();
            rule.Rates[Side.Alpha] = 1d;
            var session = Create(new TwoTeams(), scoreRules: new IScoreRule[] { rule });
            session.Join(10);
            session.Join(11);
            session.Start();
            rule.OnAdvance = () => rule.Rates[Side.Bravo] = 2d;

            session.Tick(3f);

            Assert.That(rule.Advanced, Is.EqualTo(new[] { 3f }));
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(3d));
            Assert.That(session.GetScore(Side.Bravo), Is.Zero);

            rule.OnAdvance = null;
            session.Tick(1f);
            Assert.That(session.GetScore(Side.Bravo), Is.EqualTo(2d));
        }

        [Test]
        public void LongTickStopsAtTheScoreLimit()
        {
            var rule = new FixedRateScore();
            rule.Rates[Side.Alpha] = 2d;
            rule.Rates[Side.Bravo] = 1d;
            var session = Create(
                new TwoTeams(),
                timeLimitSeconds: 60f,
                scoreRules: new IScoreRule[] { rule },
                winConditions: new IWinCondition[] { new ScoreLimitWin(10d), new TimeLimitWin() });
            session.Join(10);
            session.Join(11);
            session.Start();

            session.Tick(30f);

            Assert.That(rule.Advanced, Is.EqualTo(new[] { 5f }));
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(10d));
            Assert.That(session.GetScore(Side.Bravo), Is.EqualTo(5d));
            Assert.That(session.RemainingSeconds, Is.EqualTo(55f));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(5d));
        }

        [Test]
        public void SidesReachingTheLimitAtTheSameTimeDraw()
        {
            var rule = new FixedRateScore();
            rule.Rates[Side.Alpha] = 1d;
            rule.Rates[Side.Bravo] = 1d;
            var session = Create(
                new TwoTeams(),
                scoreRules: new IScoreRule[] { rule },
                winConditions: new IWinCondition[] { new ScoreLimitWin(4d) });
            session.Join(10);
            session.Join(11);
            session.Start();

            session.Tick(10f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(4d));
            Assert.That(session.GetScore(Side.Bravo), Is.EqualTo(4d));
        }

        [Test]
        public void ScoreRuleReturningBadPointsThrows()
        {
            var rule = new FixedRateScore();
            rule.Rates[Side.Alpha] = -1d;
            var session = Create(new TwoTeams(), scoreRules: new IScoreRule[] { rule });
            session.Start();

            Assert.Throws<InvalidOperationException>(() => session.Tick(1f));
        }

        [Test]
        public void TimeUpUsesTheFirstDecidingConditionOrDraws()
        {
            var session = Create(new TwoTeams(), timeLimitSeconds: 10f, winConditions: new IWinCondition[] { new TimeLimitWin() });
            session.Join(10);
            session.Join(11);
            session.Start();
            session.AddScore(Side.Bravo, 1d);

            session.Tick(4f);
            Assert.That(session.RemainingSeconds, Is.EqualTo(6f));

            session.Tick(20f);
            Assert.That(session.RemainingSeconds, Is.Zero);
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Bravo));

            var noConditions = Create(new TwoTeams(), timeLimitSeconds: 1f);
            noConditions.Start();
            noConditions.Tick(1f);
            Assert.That(noConditions.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(noConditions.Winner, Is.Null);
        }

        [Test]
        public void InfiniteTimeLimitNeverRunsOut()
        {
            var session = Create(new TwoTeams(), timeLimitSeconds: float.PositiveInfinity, winConditions: new IWinCondition[] { new TimeLimitWin() });
            session.Start();

            session.Tick(1e6f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(1e6d));
        }

        [Test]
        public void RespawnReadyFiresAfterTheWait()
        {
            var session = Create(new TwoTeams(), respawn: new IndividualRespawn(2f));
            var respawned = new List<int>();
            session.RespawnReady += respawned.Add;
            session.Join(10);
            session.Join(11);
            session.Start();

            session.ReportKill(10, 11);
            session.ReportKill(10, 11);
            Assert.That(session.GetDeaths(11), Is.EqualTo(1));
            Assert.That(session.GetRespawnRemaining(11), Is.EqualTo(2f));

            session.Tick(2f);

            Assert.That(respawned, Is.EqualTo(new[] { 11 }));
            Assert.That(session.IsDown(11), Is.False);
        }

        [Test]
        public void WithoutARespawnPolicyDeathsAreOnlyCounted()
        {
            var session = Create(new TwoTeams());
            session.Join(10);
            session.Join(11);
            session.Start();

            session.ReportKill(10, 11);
            session.ReportKill(10, 11);
            session.ReportDeath(11);

            Assert.That(session.TracksDowns, Is.False);
            Assert.That(session.GetDeaths(11), Is.EqualTo(3));
            Assert.That(session.GetKills(10), Is.EqualTo(2));
            Assert.That(session.IsDown(11), Is.False);
            Assert.That(session.IsEliminated(11), Is.False);
            Assert.That(session.IsAwaitingRespawn(11), Is.False);
            Assert.That(session.GetRespawnRemaining(11), Is.Null);
            Assert.That(session.SecondsUntilNextWave, Is.Null);
        }

        [Test]
        public void EliminatedFiresOncePerSideBeforeTheMatchEnds()
        {
            var session = Create(
                new Squads(3, 2),
                respawn: new NoRespawn(),
                winConditions: new IWinCondition[] { new LastSideStandingWin() });
            var events = new List<string>();
            session.Eliminated += side => events.Add("Eliminated " + side.Id);
            session.Ended += winner => events.Add("Ended " + winner?.Id);
            session.Join(10);
            session.Join(11);
            session.Join(12);
            session.Start();

            session.ReportKill(10, 11);
            session.Tick(1f);
            session.Tick(1f);
            Assert.That(events, Is.EqualTo(new[] { "Eliminated 1" }));
            Assert.That(session.IsSideEliminated(new Side(1)), Is.True);

            session.ReportKill(10, 12);
            session.Tick(1f);

            Assert.That(events, Is.EqualTo(new[] { "Eliminated 1", "Eliminated 2", "Ended 0" }));
        }

        [Test]
        public void ResetInAnEliminatedHandlerStopsTheTick()
        {
            var session = Create(
                new TwoTeams(),
                respawn: new NoRespawn(),
                winConditions: new IWinCondition[] { new LastSideStandingWin() });
            session.Join(10);
            session.Join(11);
            session.Start();
            session.Eliminated += _ => session.Reset();

            session.ReportDeath(11);
            session.Tick(1f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(session.Winner, Is.Null);
            Assert.That(session.IsEliminated(11), Is.False);
        }

        [Test]
        public void EndRevivesEveryoneAndCancelsWaits()
        {
            var session = Create(
                new TwoTeams(),
                scoreRules: new IScoreRule[] { new KillScore() },
                respawn: new IndividualRespawn(5f),
                winConditions: new IWinCondition[] { new ScoreLimitWin(2d) });
            var respawned = new List<int>();
            session.RespawnReady += respawned.Add;
            session.Join(10);
            session.Join(11);
            session.Join(12);
            session.Start();

            session.ReportKill(11, 12);
            session.ReportKill(11, 10);
            session.Tick(10f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.IsDown(12), Is.False);
            Assert.That(session.GetRespawnRemaining(12), Is.Null);
            Assert.That(respawned, Is.Empty);
        }

        [Test]
        public void ResetRestoresScoresAndKeepsPlayersAndSides()
        {
            var session = Create(new FreeForAll(), scoreRules: new IScoreRule[] { new KillScore() }, respawn: new IndividualRespawn(5f));
            var states = new List<GameModeState>();
            session.StateChanged += states.Add;
            session.Join(10);
            session.Join(11);
            session.Start();
            session.ReportKill(10, 11);
            session.Tick(3f);

            session.Reset();
            session.Reset();

            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing, GameModeState.Waiting }));
            Assert.That(session.GetScore(new Side(0)), Is.Zero);
            Assert.That(session.GetKills(10), Is.Zero);
            Assert.That(session.GetDeaths(11), Is.Zero);
            Assert.That(session.IsDown(11), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.GetSide(11), Is.EqualTo(new Side(1)));
            Assert.That(session.Join(12), Is.EqualTo(new Side(2)));
        }

        [Test]
        public void NothingChangesOutsidePlaying()
        {
            var session = Create(new TwoTeams(), scoreRules: new IScoreRule[] { new KillScore() }, respawn: new IndividualRespawn(1f));
            session.Join(10);
            session.Join(11);

            session.ReportKill(10, 11);
            session.ReportDeath(10);
            session.Tick(5f);

            Assert.That(session.GetDeaths(11), Is.Zero);
            Assert.That(session.GetDeaths(10), Is.Zero);
            Assert.That(session.RemainingSeconds, Is.EqualTo(60f));

            session.Start();
            Assert.Throws<InvalidOperationException>(() => session.Start());
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ModeSession(null, 60f, Array.Empty<IScoreRule>(), null, Array.Empty<IWinCondition>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModeSession(new TwoTeams(), 0f, Array.Empty<IScoreRule>(), null, Array.Empty<IWinCondition>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModeSession(new TwoTeams(), float.NaN, Array.Empty<IScoreRule>(), null, Array.Empty<IWinCondition>()));
            Assert.Throws<ArgumentNullException>(() => new ModeSession(new TwoTeams(), 60f, null, null, Array.Empty<IWinCondition>()));
            Assert.Throws<ArgumentNullException>(() => new ModeSession(new TwoTeams(), 60f, Array.Empty<IScoreRule>(), null, null));
            Assert.Throws<ArgumentException>(() => new ModeSession(new TwoTeams(), 60f, new IScoreRule[] { null }, null, Array.Empty<IWinCondition>()));

            var session = Create(new TwoTeams());
            session.Join(10);
            Assert.Throws<ArgumentException>(() => session.Join(10));
            Assert.Throws<ArgumentException>(() => session.AddPlayer(10, Side.Bravo));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.GetSide(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.RemovePlayer(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportKill(99, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.GetScore(new Side(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(float.NaN));
        }

        [Test]
        public void ScoreLimitIsTheLowestAmongConditions()
        {
            var session = Create(
                new TwoTeams(),
                winConditions: new IWinCondition[] { new ScoreLimitWin(30d), new TimeLimitWin(), new ScoreLimitWin(20d) });

            Assert.That(session.ScoreLimit, Is.EqualTo(20d));
            Assert.That(Create(new TwoTeams()).ScoreLimit, Is.Null);
        }

        private static ModeSession Create(
            ITeamLayout layout,
            float timeLimitSeconds = 60f,
            IScoreRule[] scoreRules = null,
            IRespawnPolicy respawn = null,
            IWinCondition[] winConditions = null)
        {
            return new ModeSession(
                layout,
                timeLimitSeconds,
                scoreRules ?? Array.Empty<IScoreRule>(),
                respawn,
                winConditions ?? Array.Empty<IWinCondition>());
        }

        private sealed class FixedRateScore : IScoreRule
        {
            public Dictionary<Side, double> Rates { get; } = new Dictionary<Side, double>();
            public List<float> Advanced { get; } = new List<float>();
            public Action OnAdvance { get; set; }

            public double PointsForKill(ModeSession session, int killerId, int victimId) => 0d;

            public double PointsPerSecond(ModeSession session, Side side) => Rates.TryGetValue(side, out var rate) ? rate : 0d;

            public void Advance(ModeSession session, float seconds)
            {
                Advanced.Add(seconds);
                OnAdvance?.Invoke();
            }
        }
    }
}
