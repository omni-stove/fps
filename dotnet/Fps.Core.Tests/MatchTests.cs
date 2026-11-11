using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MatchTests
    {
        private static Match StartedMatch(float timeLimitSeconds = 10f, int killsToWin = 3)
        {
            var match = new Match(timeLimitSeconds, killsToWin);
            match.Start();
            return match;
        }

        [Test]
        public void StartsWaitingWithFullTimeAndNoKills()
        {
            var match = new Match(30f, 5);

            Assert.That(match.State, Is.EqualTo(MatchState.Waiting));
            Assert.That(match.TimeLimitSeconds, Is.EqualTo(30f));
            Assert.That(match.KillsToWin, Is.EqualTo(5));
            Assert.That(match.RemainingSeconds, Is.EqualTo(30f));
            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.IsOver, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidTimeLimit(float timeLimitSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Match(timeLimitSeconds, 1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveKillsToWin(int killsToWin)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Match(10f, killsToWin));
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
            var match = new Match(10f, 3);

            match.Start();

            Assert.That(match.State, Is.EqualTo(MatchState.Playing));
        }

        [Test]
        public void WaitingIgnoresTickKillAndDeath()
        {
            var match = new Match(10f, 1);

            match.Tick(100f);
            match.RegisterKill();
            match.PlayerDied();

            Assert.That(match.State, Is.EqualTo(MatchState.Waiting));
            Assert.That(match.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(match.Kills, Is.Zero);
        }

        [Test]
        public void TickReducesRemainingTime()
        {
            var match = StartedMatch(10f);

            match.Tick(2.5f);

            Assert.That(match.RemainingSeconds, Is.EqualTo(7.5f));
            Assert.That(match.State, Is.EqualTo(MatchState.Playing));
        }

        [Test]
        public void TimeUpLoses()
        {
            var match = StartedMatch(10f);

            match.Tick(4f);
            match.Tick(6f);

            Assert.That(match.RemainingSeconds, Is.Zero);
            Assert.That(match.State, Is.EqualTo(MatchState.Lost));
        }

        [Test]
        public void OvershootingTimeClampsToZero()
        {
            var match = StartedMatch(10f);

            match.Tick(25f);

            Assert.That(match.RemainingSeconds, Is.Zero);
            Assert.That(match.State, Is.EqualTo(MatchState.Lost));
        }

        [Test]
        public void ReachingKillsToWinExactlyWins()
        {
            var match = StartedMatch(killsToWin: 3);

            match.RegisterKill();
            match.RegisterKill();
            Assert.That(match.State, Is.EqualTo(MatchState.Playing));

            match.RegisterKill();

            Assert.That(match.Kills, Is.EqualTo(3));
            Assert.That(match.State, Is.EqualTo(MatchState.Won));
        }

        [Test]
        public void PlayerDeathLoses()
        {
            var match = StartedMatch();

            match.PlayerDied();

            Assert.That(match.State, Is.EqualTo(MatchState.Lost));
        }

        [Test]
        public void TickThenKillInSameFrameLosesWhenTimeRunsOut()
        {
            var match = StartedMatch(1f, 1);

            match.Tick(1f);
            match.RegisterKill();

            Assert.That(match.State, Is.EqualTo(MatchState.Lost));
            Assert.That(match.Kills, Is.Zero);
        }

        [Test]
        public void KillThenTickInSameFrameWinsBeforeTimeRunsOut()
        {
            var match = StartedMatch(1f, 1);

            match.RegisterKill();
            match.Tick(1f);

            Assert.That(match.State, Is.EqualTo(MatchState.Won));
            Assert.That(match.RemainingSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void NothingChangesAfterWin()
        {
            var match = StartedMatch(10f, 1);
            match.RegisterKill();

            match.Start();
            match.Tick(100f);
            match.RegisterKill();
            match.PlayerDied();

            Assert.That(match.State, Is.EqualTo(MatchState.Won));
            Assert.That(match.Kills, Is.EqualTo(1));
            Assert.That(match.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(match.IsOver, Is.True);
        }

        [Test]
        public void NothingChangesAfterLoss()
        {
            var match = StartedMatch(10f, 2);
            match.Tick(3f);
            match.PlayerDied();

            match.Start();
            match.Tick(1f);
            match.RegisterKill();
            match.RegisterKill();

            Assert.That(match.State, Is.EqualTo(MatchState.Lost));
            Assert.That(match.Kills, Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(7f));
            Assert.That(match.IsOver, Is.True);
        }

        [Test]
        public void StartTwiceDoesNotRaiseAgain()
        {
            var match = new Match(10f, 3);
            var states = new List<MatchState>();
            match.StateChanged += states.Add;

            match.Start();
            match.Start();

            Assert.That(states, Is.EqualTo(new[] { MatchState.Playing }));
        }

        [Test]
        public void StateChangedFiresOncePerTransitionUntilWin()
        {
            var match = new Match(10f, 2);
            var states = new List<MatchState>();
            match.StateChanged += states.Add;

            match.Tick(1f);
            match.RegisterKill();
            match.Start();
            match.Tick(1f);
            match.RegisterKill();
            match.RegisterKill();
            match.RegisterKill();
            match.Tick(100f);
            match.PlayerDied();

            Assert.That(states, Is.EqualTo(new[] { MatchState.Playing, MatchState.Won }));
        }

        [Test]
        public void StateChangedFiresOncePerTransitionUntilLoss()
        {
            var match = new Match(5f, 2);
            var states = new List<MatchState>();
            match.StateChanged += states.Add;

            match.Start();
            match.Tick(5f);
            match.Tick(5f);
            match.PlayerDied();
            match.RegisterKill();

            Assert.That(states, Is.EqualTo(new[] { MatchState.Playing, MatchState.Lost }));
        }

        [Test]
        public void StateIsUpdatedBeforeStateChangedFires()
        {
            var match = new Match(10f, 1);
            var observed = new List<MatchState>();
            match.StateChanged += _ => observed.Add(match.State);

            match.Start();
            match.RegisterKill();

            Assert.That(observed, Is.EqualTo(new[] { MatchState.Playing, MatchState.Won }));
        }
    }
}
