using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RoundSessionTests
    {
        private const int _alphaPlayer = 10;
        private const int _bravoPlayer = 11;

        private static readonly int[] _expectedRoundOneStarted = { 1 };
        private static readonly int[] _expectedRespawnedBravo = { _bravoPlayer };

        private static readonly string[] _expectedFirstToTwo =
        {
            "Preparation 1", "Started 1", "RoundEnded Side 0",
            "Preparation 2", "Started 2", "RoundEnded Side 1",
            "Preparation 3", "Started 3", "RoundEnded Side 0", "Ended Side 0",
        };

        private static readonly string[] _expectedDrawThenNext =
        {
            "Preparation 1", "Started 1", "RoundEnded draw",
            "Preparation 2", "Started 2",
        };

        private static readonly string[] _expectedSwapOrder =
        {
            "Preparation 1", "Started 1 attacker Side 0", "RoundEnded Side 0",
            "Preparation 2", "Started 2 attacker Side 0", "RoundEnded Side 1", "Swapped attacker Side 1",
            "Preparation 3", "Started 3 attacker Side 1", "RoundEnded Side 0",
            "Preparation 4", "Started 4 attacker Side 1", "RoundEnded Side 1", "Swapped attacker Side 0",
            "Preparation 5", "Started 5 attacker Side 0", "RoundEnded Side 0", "Ended Side 0",
        };

        private static readonly string[] _expectedResetInRoundEnded = { "RoundEnded Side 0" };

        [Test]
        public void RoundGoesLiveAfterThePreparationWait()
        {
            var rounds = Create(preparationSeconds: 5f);
            var started = new List<int>();
            rounds.RoundStarted += started.Add;

            rounds.Start();

            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(rounds.RoundNumber, Is.EqualTo(1));
            Assert.That(rounds.PreparationRemaining, Is.EqualTo(5f));
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Waiting));

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            Assert.That(rounds.Mode.GetKills(_alphaPlayer), Is.Zero);
            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.Zero);

            rounds.Tick(3f);
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(rounds.PreparationRemaining, Is.EqualTo(2f));
            Assert.That(started, Is.Empty);

            rounds.Tick(2.5f);
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(rounds.PreparationRemaining, Is.Zero);
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(rounds.Mode.ElapsedSeconds, Is.EqualTo(0.5d));
            Assert.That(started, Is.EqualTo(_expectedRoundOneStarted));
        }

        [Test]
        public void ZeroPreparationGoesLiveOnStart()
        {
            var rounds = Create(preparationSeconds: 0f);

            rounds.Start();

            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void FirstSideToReachWinsToWinWinsTheMatch()
        {
            var rounds = Create(winsToWin: 2);
            var log = Record(rounds);
            rounds.Start();

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Live));

            rounds.ReportKill(_bravoPlayer, _alphaPlayer);
            rounds.ReportKill(_alphaPlayer, _bravoPlayer);

            Assert.That(log, Is.EqualTo(_expectedFirstToTwo));
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(rounds.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.EqualTo(2));
            Assert.That(rounds.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(rounds.RoundNumber, Is.EqualTo(3));

            // 試合が終わった後は ModeSession も最後のラウンドの結果のまま残り、報告も時間も効かない。
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Ended));
            rounds.ReportKill(_bravoPlayer, _alphaPlayer);
            rounds.Tick(100f);
            Assert.That(rounds.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(rounds.RoundNumber, Is.EqualTo(3));
            Assert.That(log, Has.Count.EqualTo(_expectedFirstToTwo.Length));
        }

        [Test]
        public void DrawnRoundCountsForNeitherSideAndMovesOn()
        {
            var rounds = Create(winsToWin: 1, timeLimitSeconds: 10f);
            var log = Record(rounds);
            rounds.Start();

            rounds.Tick(10f);

            Assert.That(log, Is.EqualTo(_expectedDrawThenNext));
            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(rounds.GetRoundWins(Side.Bravo), Is.Zero);
            Assert.That(rounds.RoundNumber, Is.EqualTo(2));
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(rounds.Winner, Is.Null);
            Assert.That(rounds.Mode.RemainingSeconds, Is.EqualTo(10f));
        }

        [Test]
        public void SidesSwapEveryNRoundsButNotAfterTheLastRound()
        {
            var rounds = Create(winsToWin: 3, swapEveryRounds: 2);
            var log = Record(rounds, withAttacker: true);
            rounds.Start();

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            rounds.ReportKill(_bravoPlayer, _alphaPlayer);
            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            rounds.ReportKill(_bravoPlayer, _alphaPlayer);
            rounds.ReportKill(_alphaPlayer, _bravoPlayer);

            Assert.That(log, Is.EqualTo(_expectedSwapOrder));
            Assert.That(rounds.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(rounds.Defender, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void DrawnRoundsCountTowardsTheSwap()
        {
            var rounds = Create(timeLimitSeconds: 10f, swapEveryRounds: 1);
            rounds.Start();

            rounds.Tick(10f);
            Assert.That(rounds.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(rounds.Defender, Is.EqualTo(Side.Alpha));

            rounds.Tick(10f);
            Assert.That(rounds.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(rounds.Defender, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void NoSwapWhenSwapEveryRoundsIsZero()
        {
            var rounds = Create(winsToWin: 5, swapEveryRounds: 0);
            var swaps = 0;
            rounds.SidesSwapped += () => swaps++;
            rounds.Start();

            for (var i = 0; i < 4; i++)
            {
                rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            }

            Assert.That(swaps, Is.Zero);
            Assert.That(rounds.Attacker, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void NextRoundDoesNotCarryOverScoresKillsOrRespawnWaits()
        {
            var rounds = Create(scoreLimit: 2d, preparationSeconds: 1f, respawn: new IndividualRespawn(3f));
            rounds.Start();
            rounds.Tick(1f);

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            Assert.That(rounds.Mode.GetScore(Side.Alpha), Is.EqualTo(1d));
            Assert.That(rounds.Mode.IsAwaitingRespawn(_bravoPlayer), Is.True);

            rounds.AddScore(Side.Bravo, 2d);

            Assert.That(rounds.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(rounds.Mode.GetScore(Side.Alpha), Is.Zero);
            Assert.That(rounds.Mode.GetScore(Side.Bravo), Is.Zero);
            Assert.That(rounds.Mode.GetKills(_alphaPlayer), Is.Zero);
            Assert.That(rounds.Mode.GetDeaths(_bravoPlayer), Is.Zero);
            Assert.That(rounds.Mode.IsDown(_bravoPlayer), Is.False);
            Assert.That(rounds.Mode.GetRespawnRemaining(_bravoPlayer), Is.Null);
            Assert.That(rounds.Mode.PlayerCount, Is.EqualTo(2));
            Assert.That(rounds.Mode.GetSide(_bravoPlayer), Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void RespawnWithinARoundIsLeftToTheRespawnPolicy()
        {
            var rounds = Create(scoreLimit: 5d, respawn: new IndividualRespawn(2f));
            var respawned = new List<int>();
            rounds.Mode.RespawnReady += respawned.Add;
            rounds.Start();

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            rounds.Tick(2f);

            Assert.That(respawned, Is.EqualTo(_expectedRespawnedBravo));
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(rounds.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void EliminationEndsTheRoundOnTheNextTick()
        {
            var mode = new ModeSession(
                new TwoTeams(),
                60f,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new LastSideStandingWin() });
            mode.AddPlayer(_alphaPlayer, Side.Alpha);
            mode.AddPlayer(_bravoPlayer, Side.Bravo);
            var rounds = new RoundSession(mode, 2, 0f, 0);
            rounds.Start();

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            Assert.That(rounds.RoundNumber, Is.EqualTo(1));

            rounds.Tick(0.1f);

            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(rounds.RoundNumber, Is.EqualTo(2));
            Assert.That(rounds.Mode.IsEliminated(_bravoPlayer), Is.False);
        }

        [Test]
        public void RoundEndedThroughTheModeSessionIsHandledOnTheNextTick()
        {
            var rounds = Create();
            rounds.Start();

            rounds.Mode.ReportKill(_alphaPlayer, _bravoPlayer);
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(rounds.RoundNumber, Is.EqualTo(1));
            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.Zero);

            rounds.Tick(0f);

            Assert.That(rounds.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(rounds.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void ResetReturnsToWaitingAndKeepsThePlayers()
        {
            var rounds = Create(winsToWin: 1, swapEveryRounds: 1, timeLimitSeconds: 10f);
            rounds.Start();
            rounds.Tick(10f);
            rounds.ReportKill(_bravoPlayer, _alphaPlayer);
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Ended));

            rounds.Reset();

            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(rounds.RoundNumber, Is.Zero);
            Assert.That(rounds.Winner, Is.Null);
            Assert.That(rounds.GetRoundWins(Side.Bravo), Is.Zero);
            Assert.That(rounds.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(rounds.Defender, Is.EqualTo(Side.Bravo));
            Assert.That(rounds.Mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(rounds.Mode.PlayerCount, Is.EqualTo(2));

            rounds.Start();
            rounds.ReportKill(_alphaPlayer, _bravoPlayer);
            Assert.That(rounds.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void ResetInsideRoundEndedStopsTheRestOfTheEvents()
        {
            var rounds = Create(winsToWin: 1);
            var log = new List<string>();
            rounds.RoundEnded += winner =>
            {
                log.Add("RoundEnded " + winner);
                rounds.Reset();
            };
            rounds.Ended += winner => log.Add("Ended " + winner);
            rounds.Start();

            rounds.ReportKill(_alphaPlayer, _bravoPlayer);

            Assert.That(log, Is.EqualTo(_expectedResetInRoundEnded));
            Assert.That(rounds.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(rounds.Winner, Is.Null);
        }

        [Test]
        public void StartAndTickRejectInvalidUse()
        {
            var rounds = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => rounds.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => rounds.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => rounds.Tick(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => rounds.GetRoundWins(new Side(2)));

            rounds.Start();
            Assert.Throws<InvalidOperationException>(() => rounds.Start());
        }

        [Test]
        public void ConstructorRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new RoundSession(null, 1, 0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundSession(CreateMode(), 0, 0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundSession(CreateMode(), 1, -1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundSession(CreateMode(), 1, float.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundSession(CreateMode(), 1, float.PositiveInfinity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundSession(CreateMode(), 1, 0f, -1));

            var started = CreateMode();
            started.Start();
            Assert.Throws<ArgumentException>(() => new RoundSession(started, 1, 0f, 0));

            var freeForAll = new ModeSession(new FreeForAll(), 60f, Array.Empty<IScoreRule>(), null, Array.Empty<IWinCondition>());
            Assert.Throws<ArgumentException>(() => new RoundSession(freeForAll, 1, 0f, 0));
        }

        private static RoundSession Create(
            int winsToWin = 4,
            float preparationSeconds = 0f,
            int swapEveryRounds = 0,
            float timeLimitSeconds = 60f,
            double scoreLimit = 1d,
            IRespawnPolicy respawn = null)
        {
            var mode = CreateMode(timeLimitSeconds, scoreLimit, respawn);
            mode.AddPlayer(_alphaPlayer, Side.Alpha);
            mode.AddPlayer(_bravoPlayer, Side.Bravo);
            return new RoundSession(mode, winsToWin, preparationSeconds, swapEveryRounds);
        }

        private static ModeSession CreateMode(float timeLimitSeconds = 60f, double scoreLimit = 1d, IRespawnPolicy respawn = null)
        {
            return new ModeSession(
                new TwoTeams(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                respawn,
                new IWinCondition[] { new ScoreLimitWin(scoreLimit) });
        }

        private static List<string> Record(RoundSession rounds, bool withAttacker = false)
        {
            var log = new List<string>();
            rounds.PreparationStarted += round => log.Add("Preparation " + round);
            rounds.RoundStarted += round => log.Add(withAttacker ? $"Started {round} attacker {rounds.Attacker}" : "Started " + round);
            rounds.RoundEnded += winner => log.Add("RoundEnded " + (winner.HasValue ? winner.Value.ToString() : "draw"));
            rounds.SidesSwapped += () => log.Add("Swapped attacker " + rounds.Attacker);
            rounds.Ended += winner => log.Add("Ended " + winner);
            return log;
        }
    }
}
