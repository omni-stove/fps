using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DominationTests
    {
        private const int _alpha1 = 1;
        private const int _alpha2 = 2;
        private const int _bravo1 = 11;
        private const int _bravo2 = 12;
        private const float _captureSeconds = 5f;
        private const float _pointsPerSecond = 1f;

        private static readonly GameModeState[] _expectedStatesToEnd = { GameModeState.Playing, GameModeState.Ended };

        private static readonly string[] _expectedCapturedAlpha1 = { "1 Alpha" };
        private static readonly string[] _expectedNeutralizedAlpha2 = { "2 Alpha" };

        private static Domination CreateStarted(int pointCount = 3, int scoreLimit = 100, float timeLimitSeconds = 600f, int maxCapturers = 1)
        {
            var mode = new Domination(new DominationSpec(
                pointCount, _captureSeconds, _pointsPerSecond, scoreLimit, timeLimitSeconds, maxCapturers));
            mode.AddPlayer(_alpha1, Team.Alpha);
            mode.AddPlayer(_alpha2, Team.Alpha);
            mode.AddPlayer(_bravo1, Team.Bravo);
            mode.AddPlayer(_bravo2, Team.Bravo);
            mode.Start();
            return mode;
        }

        private static void TickMany(Domination mode, float seconds, float step = 0.1f)
        {
            var steps = (int)Math.Round(seconds / step);
            for (var i = 0; i < steps; i++)
            {
                mode.Tick(step);
            }
        }

        [Test]
        public void OnePlayerCapturesPointAfterCaptureSeconds()
        {
            var mode = CreateStarted();
            var captured = new List<string>();
            mode.PointCaptured += (index, team) => captured.Add($"{index} {team}");
            mode.SetPlayerPoint(_alpha1, 1);

            TickMany(mode, _captureSeconds - 0.1f);
            Assert.That(mode.GetOwner(1), Is.Null);

            mode.Tick(0.1f);

            Assert.That(mode.GetOwner(1), Is.EqualTo(Team.Alpha));
            Assert.That(mode.GetOwner(0), Is.Null);
            Assert.That(mode.GetOwner(2), Is.Null);
            Assert.That(mode.CountOwned(Team.Alpha), Is.EqualTo(1));
            Assert.That(captured, Is.EqualTo(_expectedCapturedAlpha1));
        }

        [Test]
        public void BothTeamsOnPointStopsProgress()
        {
            var mode = CreateStarted();
            mode.SetPlayerPoint(_alpha1, 0);
            mode.Tick(2f);
            var before = mode.GetControl(0);

            mode.SetPlayerPoint(_bravo1, 0);
            mode.Tick(_captureSeconds * 2f);

            Assert.That(mode.IsContested(0), Is.True);
            Assert.That(mode.GetControl(0), Is.EqualTo(before));
            Assert.That(mode.GetOwner(0), Is.Null);

            mode.SetPlayerPoint(_bravo1, null);
            mode.Tick(3f);

            Assert.That(mode.IsContested(0), Is.False);
            Assert.That(mode.GetOwner(0), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void TeamHoldingTwoOfThreePointsScoresTwiceAsFast()
        {
            var mode = CreateStarted();
            mode.SetPlayerPoint(_alpha1, 0);
            mode.SetPlayerPoint(_alpha2, 1);
            mode.SetPlayerPoint(_bravo1, 2);
            mode.Tick(_captureSeconds);
            Assert.That(mode.CountOwned(Team.Alpha), Is.EqualTo(2));
            Assert.That(mode.CountOwned(Team.Bravo), Is.EqualTo(1));
            var alphaBefore = mode.GetScore(Team.Alpha);
            var bravoBefore = mode.GetScore(Team.Bravo);

            TickMany(mode, 10f);

            var alphaGain = mode.GetScore(Team.Alpha) - alphaBefore;
            var bravoGain = mode.GetScore(Team.Bravo) - bravoBefore;
            Assert.That(bravoGain, Is.EqualTo(10f * _pointsPerSecond).Within(1e-3f));
            Assert.That(alphaGain, Is.EqualTo(2f * bravoGain).Within(1e-3f));
        }

        [Test]
        public void ReachingScoreLimitEndsWithThatTeam()
        {
            var mode = CreateStarted(pointCount: 1, scoreLimit: 10);
            var states = new List<GameModeState>();
            var ended = new List<Team?>();
            mode.StateChanged += states.Add;
            mode.Ended += ended.Add;
            mode.SetPlayerPoint(_bravo1, 0);
            mode.Tick(_captureSeconds);

            TickMany(mode, 9.9f);
            Assert.That(mode.State, Is.EqualTo(GameModeState.Playing));

            mode.Tick(0.1f);

            Assert.That(mode.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(mode.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(mode.GetScore(Team.Bravo), Is.EqualTo(10f));
            Assert.That(states, Is.EqualTo(new[] { GameModeState.Ended }));
            Assert.That(ended, Is.EqualTo(new Team?[] { Team.Bravo }));
        }

        [Test]
        public void LongTickStopsAtScoreLimit()
        {
            var mode = CreateStarted(pointCount: 1, scoreLimit: 10, timeLimitSeconds: 600f);
            mode.SetPlayerPoint(_alpha1, 0);
            mode.Tick(_captureSeconds);

            mode.Tick(1000f);

            Assert.That(mode.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(mode.GetScore(Team.Alpha), Is.EqualTo(10f));
            Assert.That(mode.RemainingSeconds, Is.EqualTo(600f - _captureSeconds - 10f).Within(1e-3f));
        }

        [Test]
        public void TeamReachingLimitFirstWithinOneTickWins()
        {
            var mode = CreateStarted(pointCount: 3, scoreLimit: 20);
            mode.SetPlayerPoint(_alpha1, 0);
            mode.SetPlayerPoint(_bravo1, 1);
            mode.SetPlayerPoint(_bravo2, 2);
            mode.Tick(_captureSeconds);

            // Alpha は 1 拠点で 1 点/秒、Bravo は 2 拠点で 2 点/秒なので Bravo が先に届く。
            mode.Tick(100f);

            Assert.That(mode.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(mode.GetScore(Team.Bravo), Is.EqualTo(20f));
            Assert.That(mode.GetScore(Team.Alpha), Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void TimeUpEndsWithLeadingTeamOrDraw()
        {
            var leading = CreateStarted(pointCount: 1, timeLimitSeconds: 20f);
            leading.SetPlayerPoint(_alpha1, 0);
            leading.Tick(_captureSeconds);
            leading.Tick(20f);

            Assert.That(leading.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(leading.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(leading.RemainingSeconds, Is.Zero);

            var draw = CreateStarted(pointCount: 1, timeLimitSeconds: 20f);
            draw.Tick(20f);

            Assert.That(draw.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(draw.Winner, Is.Null);
        }

        [Test]
        public void KillsAreCountedButDoNotScore()
        {
            var mode = CreateStarted(scoreLimit: 1);

            mode.ReportKill(_alpha1, _bravo1);
            mode.ReportKill(_alpha1, _bravo2);
            mode.ReportKill(_alpha1, _alpha2);
            mode.ReportDeath(_bravo1);

            Assert.That(mode.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(mode.GetKills(_alpha1), Is.EqualTo(2));
            Assert.That(mode.GetDeaths(_bravo1), Is.EqualTo(2));
            Assert.That(mode.GetDeaths(_bravo2), Is.EqualTo(1));
            Assert.That(mode.GetDeaths(_alpha2), Is.EqualTo(1));
            Assert.That(mode.GetScore(Team.Alpha), Is.Zero);
            Assert.That(mode.GetScore(Team.Bravo), Is.Zero);
        }

        [Test]
        public void DeathRemovesPlayerFromPoint()
        {
            var mode = CreateStarted();
            mode.SetPlayerPoint(_alpha1, 0);
            mode.SetPlayerPoint(_bravo1, 0);

            mode.ReportKill(_alpha1, _bravo1);
            mode.Tick(_captureSeconds);

            Assert.That(mode.GetPlayerPoint(_bravo1), Is.Null);
            Assert.That(mode.GetOwner(0), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void RemovedPlayerNoLongerHoldsPoint()
        {
            var mode = CreateStarted();
            mode.SetPlayerPoint(_alpha1, 0);
            mode.SetPlayerPoint(_bravo1, 0);

            mode.RemovePlayer(_bravo1);
            mode.Tick(_captureSeconds);

            Assert.That(mode.GetOwner(0), Is.EqualTo(Team.Alpha));
            Assert.That(mode.HasPlayer(_bravo1), Is.False);
        }

        [Test]
        public void EnemyNeutralizesOwnedPoint()
        {
            var mode = CreateStarted();
            var neutralized = new List<string>();
            mode.PointNeutralized += (index, team) => neutralized.Add($"{index} {team}");
            mode.SetPlayerPoint(_alpha1, 2);
            mode.Tick(_captureSeconds);
            mode.SetPlayerPoint(_alpha1, null);
            mode.SetPlayerPoint(_bravo1, 2);

            mode.Tick(_captureSeconds);

            Assert.That(mode.GetOwner(2), Is.Null);
            Assert.That(neutralized, Is.EqualTo(_expectedNeutralizedAlpha2));
        }

        [Test]
        public void WaitingAndEndedIgnoreTickAndReports()
        {
            var mode = new Domination(new DominationSpec(1, _captureSeconds, _pointsPerSecond, 1, 60f));
            mode.AddPlayer(_alpha1, Team.Alpha);
            mode.AddPlayer(_bravo1, Team.Bravo);
            mode.SetPlayerPoint(_alpha1, 0);

            mode.Tick(100f);
            mode.ReportKill(_alpha1, _bravo1);

            Assert.That(mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(mode.GetOwner(0), Is.Null);
            Assert.That(mode.GetKills(_alpha1), Is.Zero);
            Assert.That(mode.RemainingSeconds, Is.EqualTo(60f));

            var states = new List<GameModeState>();
            mode.StateChanged += states.Add;
            mode.Start();
            mode.Tick(_captureSeconds);
            mode.Tick(2f);
            Assert.That(mode.State, Is.EqualTo(GameModeState.Ended));

            var remaining = mode.RemainingSeconds;
            mode.Tick(100f);
            mode.ReportKill(_alpha1, _bravo1);

            Assert.That(mode.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(mode.GetKills(_alpha1), Is.Zero);
            Assert.That(states, Is.EqualTo(_expectedStatesToEnd));
            Assert.Throws<InvalidOperationException>(() => mode.Start());
        }

        [Test]
        public void ResetClearsScoresPointsAndPresence()
        {
            var mode = CreateStarted(pointCount: 1, scoreLimit: 5);
            mode.SetPlayerPoint(_alpha1, 0);
            mode.ReportKill(_alpha1, _bravo1);
            mode.Tick(_captureSeconds);
            mode.Tick(100f);
            Assert.That(mode.State, Is.EqualTo(GameModeState.Ended));

            mode.Reset();

            Assert.That(mode.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(mode.Winner, Is.Null);
            Assert.That(mode.GetScore(Team.Alpha), Is.Zero);
            Assert.That(mode.GetOwner(0), Is.Null);
            Assert.That(mode.GetControl(0), Is.Zero);
            Assert.That(mode.GetPlayerPoint(_alpha1), Is.Null);
            Assert.That(mode.GetKills(_alpha1), Is.Zero);
            Assert.That(mode.GetDeaths(_bravo1), Is.Zero);
            Assert.That(mode.RemainingSeconds, Is.EqualTo(600f));
            Assert.That(mode.PlayerCount, Is.EqualTo(4));

            mode.Start();
            Assert.That(mode.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new Domination(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DominationSpec(0, 5f, 1f, 100, 600f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DominationSpec(3, 0f, 1f, 100, 600f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DominationSpec(3, 5f, float.NaN, 100, 600f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DominationSpec(3, 5f, 1f, 0, 600f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DominationSpec(3, 5f, 1f, 100, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DominationSpec(3, 5f, 1f, 100, 600f, 0));

            var mode = CreateStarted();
            Assert.Throws<ArgumentException>(() => mode.AddPlayer(_alpha1, Team.Bravo));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.AddPlayer(99, (Team)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.SetPlayerPoint(99, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.SetPlayerPoint(_alpha1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.SetPlayerPoint(_alpha1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.GetOwner(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.ReportKill(_alpha1, 99));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.RemovePlayer(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => mode.Tick(float.NaN));
        }
    }
}
