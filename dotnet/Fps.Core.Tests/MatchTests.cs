using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MatchTests
    {
        private const int _player = 1;
        private const int _target = 100;
        private const int _otherTarget = 101;

        private static Match StartedMatch(float timeLimitSeconds = 10f, int killsToWin = 3)
        {
            var match = new Match(timeLimitSeconds, killsToWin, _player);
            match.Start();
            return match;
        }

        private static void Kill(Match match)
        {
            match.ReportKill(_player, _target);
        }

        private static void PlayerDies(Match match)
        {
            match.ReportDeath(_player);
        }

        [Test]
        public void StartsWaitingWithFullTimeAndNoKills()
        {
            var match = new Match(30f, 5, _player);

            Assert.That(match.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(match.TimeLimitSeconds, Is.EqualTo(30f));
            Assert.That(match.KillsToWin, Is.EqualTo(5));
            Assert.That(match.PlayerId, Is.EqualTo(_player));
            Assert.That(match.RemainingSeconds, Is.EqualTo(30f));
            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.Result, Is.Null);
            Assert.That(match.IsOver, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidTimeLimit(float timeLimitSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Match(timeLimitSeconds, 1, _player));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveKillsToWin(int killsToWin)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Match(10f, killsToWin, _player));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        public void RejectsInvalidDelta(float deltaSeconds)
        {
            var match = StartedMatch();

            Assert.Throws<ArgumentOutOfRangeException>(() => match.Tick(deltaSeconds));
        }

        [Test]
        public void StartMovesToPlaying()
        {
            var match = new Match(10f, 3, _player);

            match.Start();

            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void WaitingIgnoresTickKillAndDeath()
        {
            var match = new Match(10f, 1, _player);

            match.Tick(100f);
            Kill(match);
            PlayerDies(match);

            Assert.That(match.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(match.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.Result, Is.Null);
        }

        [Test]
        public void TickReducesRemainingTime()
        {
            var match = StartedMatch(10f);

            match.Tick(2.5f);

            Assert.That(match.RemainingSeconds, Is.EqualTo(7.5f));
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void RemainingSecondsChangedFiresOnEveryTickThatReducesTime()
        {
            var match = new Match(10f, 1, _player);
            var reported = new List<float>();
            match.RemainingSecondsChanged += reported.Add;

            match.Tick(1f);
            match.Start();
            match.Tick(2.5f);
            match.Tick(0f);
            match.Tick(100f);
            match.Tick(1f);

            Assert.That(reported, Is.EqualTo(new[] { 7.5f, 0f }));
        }

        [Test]
        public void RemainingSecondsChangedFiresBeforeTheTimeUpEnd()
        {
            var match = StartedMatch(1f);
            var order = new List<string>();
            match.RemainingSecondsChanged += _ => order.Add("remaining");
            match.StateChanged += state => order.Add(state.ToString());

            match.Tick(1f);

            Assert.That(order, Is.EqualTo(new[] { "remaining", "Ended" }));
        }

        [Test]
        public void TimeUpLoses()
        {
            var match = StartedMatch(10f);

            match.Tick(4f);
            match.Tick(6f);

            Assert.That(match.RemainingSeconds, Is.Zero);
            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
        }

        [Test]
        public void OvershootingTimeClampsToZero()
        {
            var match = StartedMatch(10f);

            match.Tick(25f);

            Assert.That(match.RemainingSeconds, Is.Zero);
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
        }

        [Test]
        public void ReachingKillsToWinExactlyWins()
        {
            var match = StartedMatch(killsToWin: 3);

            match.ReportKill(_player, _target);
            match.ReportKill(_player, _otherTarget);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(match.Result, Is.Null);

            match.ReportKill(_player, _target);

            Assert.That(match.Kills, Is.EqualTo(3));
            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
        }

        [Test]
        public void PlayerDeathLoses()
        {
            var match = StartedMatch();

            PlayerDies(match);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
        }

        [Test]
        public void PlayerKilledByTargetLoses()
        {
            var match = StartedMatch();

            match.ReportKill(_target, _player);

            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
        }

        [Test]
        public void PlayerSuicideLosesWithoutCountingKill()
        {
            var match = StartedMatch(killsToWin: 1);

            match.ReportKill(_player, _player);

            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
        }

        [Test]
        public void KillsAndDeathsNotInvolvingPlayerAreIgnored()
        {
            var match = StartedMatch(killsToWin: 1);

            match.ReportKill(_target, _otherTarget);
            match.ReportDeath(_target);

            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(match.Result, Is.Null);
        }

        [Test]
        public void TickThenKillInSameFrameLosesWhenTimeRunsOut()
        {
            var match = StartedMatch(1f, 1);

            match.Tick(1f);
            Kill(match);

            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
            Assert.That(match.Kills, Is.Zero);
        }

        [Test]
        public void KillThenTickInSameFrameWinsBeforeTimeRunsOut()
        {
            var match = StartedMatch(1f, 1);

            Kill(match);
            match.Tick(1f);

            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
            Assert.That(match.RemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void NothingChangesAfterWin()
        {
            var match = StartedMatch(10f, 1);
            Kill(match);

            match.Start();
            match.Tick(100f);
            Kill(match);
            PlayerDies(match);
            match.ReportKill(_target, _player);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
            Assert.That(match.Kills, Is.EqualTo(1));
            Assert.That(match.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(match.IsOver, Is.True);
        }

        [Test]
        public void NothingChangesAfterLoss()
        {
            var match = StartedMatch(10f, 2);
            match.Tick(3f);
            PlayerDies(match);

            match.Start();
            match.Tick(1f);
            Kill(match);
            Kill(match);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(7f));
            Assert.That(match.IsOver, Is.True);
        }

        [Test]
        public void StartTwiceDoesNotRaiseAgain()
        {
            var match = new Match(10f, 3, _player);
            var states = new List<GameModeState>();
            match.StateChanged += states.Add;

            match.Start();
            match.Start();

            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing }));
        }

        [Test]
        public void StateChangedFiresOncePerTransitionUntilWin()
        {
            var match = new Match(10f, 2, _player);
            var states = new List<GameModeState>();
            match.StateChanged += states.Add;

            match.Tick(1f);
            Kill(match);
            match.Start();
            match.Tick(1f);
            Kill(match);
            Kill(match);
            Kill(match);
            match.Tick(100f);
            PlayerDies(match);

            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing, GameModeState.Ended }));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
        }

        [Test]
        public void StateChangedFiresOncePerTransitionUntilLoss()
        {
            var match = new Match(5f, 2, _player);
            var states = new List<GameModeState>();
            match.StateChanged += states.Add;

            match.Start();
            match.Tick(5f);
            match.Tick(5f);
            PlayerDies(match);
            Kill(match);

            Assert.That(states, Is.EqualTo(new[] { GameModeState.Playing, GameModeState.Ended }));
            Assert.That(match.Result, Is.EqualTo(MatchResult.Lost));
        }

        [Test]
        public void StateAndResultAreUpdatedBeforeStateChangedFires()
        {
            var match = new Match(10f, 1, _player);
            var observed = new List<(GameModeState, MatchResult?)>();
            match.StateChanged += _ => observed.Add((match.State, match.Result));

            match.Start();
            Kill(match);

            Assert.That(observed, Is.EqualTo(new (GameModeState, MatchResult?)[]
            {
                (GameModeState.Playing, null),
                (GameModeState.Ended, MatchResult.Won),
            }));
        }
    }
}
