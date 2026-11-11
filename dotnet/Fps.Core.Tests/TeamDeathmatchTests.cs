using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TeamDeathmatchTests
    {
        private const int Alpha1 = 1;
        private const int Alpha2 = 2;
        private const int Bravo1 = 11;
        private const int Bravo2 = 12;

        private static TeamDeathmatch CreateStarted(int scoreLimit = 10, float timeLimitSeconds = 60f, float respawnSeconds = 3f)
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(scoreLimit, timeLimitSeconds, respawnSeconds));
            match.AddPlayer(Alpha1, Team.Alpha);
            match.AddPlayer(Alpha2, Team.Alpha);
            match.AddPlayer(Bravo1, Team.Bravo);
            match.AddPlayer(Bravo2, Team.Bravo);
            match.Start();
            return match;
        }

        [Test]
        public void StartsWaitingThenPlaying()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f));

            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Waiting));
            Assert.That(match.RemainingSeconds, Is.EqualTo(60f));

            match.Start();

            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Playing));
            Assert.Throws<InvalidOperationException>(() => match.Start());
        }

        [Test]
        public void KillingEnemyScoresForKillerTeam()
        {
            var match = CreateStarted();

            match.ReportKill(Alpha1, Bravo1);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(Alpha1), Is.EqualTo(1));
            Assert.That(match.GetDeaths(Alpha1), Is.Zero);
            Assert.That(match.GetKills(Bravo1), Is.Zero);
            Assert.That(match.GetDeaths(Bravo1), Is.EqualTo(1));
        }

        [Test]
        public void TeamKillOnlyCountsDeath()
        {
            var match = CreateStarted();

            match.ReportKill(Alpha1, Alpha2);

            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(Alpha1), Is.Zero);
            Assert.That(match.GetDeaths(Alpha2), Is.EqualTo(1));
        }

        [Test]
        public void SuicideOnlyCountsDeath()
        {
            var match = CreateStarted();

            match.ReportKill(Bravo1, Bravo1);

            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(Bravo1), Is.Zero);
            Assert.That(match.GetDeaths(Bravo1), Is.EqualTo(1));
            Assert.That(match.IsAwaitingRespawn(Bravo1), Is.True);
        }

        [Test]
        public void ReachingScoreLimitWins()
        {
            var match = CreateStarted(scoreLimit: 2, respawnSeconds: 0f);
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);

            match.ReportKill(Bravo1, Alpha1);
            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Playing));

            match.ReportKill(Bravo2, Alpha2);

            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(ended, Is.EqualTo(new Team?[] { Team.Bravo }));
        }

        [Test]
        public void TimeUpWinsForHigherScore()
        {
            var match = CreateStarted(timeLimitSeconds: 10f);
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);
            match.ReportKill(Alpha1, Bravo1);

            match.Tick(9f);
            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Playing));
            Assert.That(match.RemainingSeconds, Is.EqualTo(1f));

            match.Tick(5f);

            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Ended));
            Assert.That(match.RemainingSeconds, Is.Zero);
            Assert.That(match.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(ended, Is.EqualTo(new Team?[] { Team.Alpha }));
        }

        [Test]
        public void TimeUpWithTiedScoreIsDraw()
        {
            var match = CreateStarted(timeLimitSeconds: 10f);
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);
            match.ReportKill(Alpha1, Bravo1);
            match.ReportKill(Bravo2, Alpha2);

            match.Tick(10f);

            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Ended));
            Assert.That(match.Winner, Is.Null);
            Assert.That(ended, Is.EqualTo(new Team?[] { null }));
        }

        [Test]
        public void RespawnReadyFiresAfterRespawnSeconds()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(Alpha1, Bravo1);

            match.Tick(1f);
            match.Tick(1.5f);
            Assert.That(respawned, Is.Empty);
            Assert.That(match.IsAwaitingRespawn(Bravo1), Is.True);

            match.Tick(0.5f);

            Assert.That(respawned, Is.EqualTo(new[] { Bravo1 }));
            Assert.That(match.IsAwaitingRespawn(Bravo1), Is.False);

            match.Tick(10f);
            Assert.That(respawned, Has.Count.EqualTo(1));
        }

        [Test]
        public void RespawnReadyFiresInOrderOfDeath()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(Alpha1, Bravo2);
            match.Tick(1f);
            match.ReportKill(Alpha1, Bravo1);

            match.Tick(5f);

            Assert.That(respawned, Is.EqualTo(new[] { Bravo2, Bravo1 }));
        }

        [Test]
        public void ZeroRespawnSecondsFiresImmediately()
        {
            var match = CreateStarted(respawnSeconds: 0f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.ReportKill(Alpha1, Bravo1);

            Assert.That(respawned, Is.EqualTo(new[] { Bravo1 }));
        }

        [Test]
        public void KillOfPlayerAwaitingRespawnIsIgnored()
        {
            var match = CreateStarted();
            match.ReportKill(Alpha1, Bravo1);

            match.ReportKill(Alpha2, Bravo1);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetKills(Alpha2), Is.Zero);
            Assert.That(match.GetDeaths(Bravo1), Is.EqualTo(1));
        }

        [Test]
        public void NothingChangesAfterEnd()
        {
            var match = CreateStarted(scoreLimit: 1, timeLimitSeconds: 10f, respawnSeconds: 1f);
            var ended = 0;
            var respawned = 0;
            match.Ended += _ => ended++;
            match.RespawnReady += _ => respawned++;
            match.ReportKill(Alpha1, Bravo1);
            match.Tick(1f);

            match.ReportKill(Alpha1, Bravo2);
            match.ReportKill(Bravo1, Alpha1);
            match.Tick(1f);
            match.Tick(100f);

            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(Alpha1), Is.EqualTo(1));
            Assert.That(match.GetDeaths(Bravo2), Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(respawned, Is.Zero);
        }

        [Test]
        public void KillsWhileWaitingAreIgnored()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f));
            match.AddPlayer(Alpha1, Team.Alpha);
            match.AddPlayer(Bravo1, Team.Bravo);

            match.ReportKill(Alpha1, Bravo1);
            match.Tick(100f);

            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetDeaths(Bravo1), Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(match.State, Is.EqualTo(TeamDeathmatchState.Waiting));
        }

        [Test]
        public void RemovedPlayerDoesNotRespawn()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(Alpha1, Bravo1);
            match.ReportKill(Alpha1, Bravo2);
            match.Tick(1f);

            match.RemovePlayer(Bravo1);
            match.Tick(5f);

            Assert.That(respawned, Is.EqualTo(new[] { Bravo2 }));
            Assert.That(match.HasPlayer(Bravo1), Is.False);
            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(2));
        }

        [Test]
        public void RejoinedPlayerDoesNotInheritRespawn()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(Alpha1, Bravo1);

            match.RemovePlayer(Bravo1);
            match.AddPlayer(Bravo1, Team.Bravo);
            match.Tick(5f);

            Assert.That(respawned, Is.Empty);
            Assert.That(match.GetDeaths(Bravo1), Is.Zero);
        }

        [Test]
        public void PlayerRemovedInRespawnHandlerDoesNotRespawn()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id =>
            {
                respawned.Add(id);
                if (id == Bravo1)
                {
                    match.RemovePlayer(Bravo2);
                }
            };
            match.ReportKill(Alpha1, Bravo1);
            match.ReportKill(Alpha1, Bravo2);

            match.Tick(3f);

            Assert.That(respawned, Is.EqualTo(new[] { Bravo1 }));
        }

        [Test]
        public void UnknownPlayerThrows()
        {
            var match = CreateStarted();

            Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportKill(99, Bravo1));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportKill(Alpha1, 99));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.RemovePlayer(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.GetKills(99));
        }

        [Test]
        public void UnknownPlayerThrowsEvenAfterEnd()
        {
            var match = CreateStarted(scoreLimit: 1);
            match.ReportKill(Alpha1, Bravo1);

            Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportKill(99, Bravo1));
        }

        [Test]
        public void RejectsDuplicateAndInvalidPlayers()
        {
            var match = CreateStarted();

            Assert.Throws<ArgumentException>(() => match.AddPlayer(Alpha1, Team.Bravo));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.AddPlayer(50, (Team)7));
        }

        [Test]
        public void RejectsInvalidTick()
        {
            var match = CreateStarted();

            Assert.Throws<ArgumentOutOfRangeException>(() => match.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Tick(float.NaN));
        }

        [Test]
        public void RejectsNullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new TeamDeathmatch(null));
        }

        [TestCase(0, 60f, 3f)]
        [TestCase(-1, 60f, 3f)]
        [TestCase(10, 0f, 3f)]
        [TestCase(10, -1f, 3f)]
        [TestCase(10, float.NaN, 3f)]
        [TestCase(10, float.PositiveInfinity, 3f)]
        [TestCase(10, 60f, -0.5f)]
        [TestCase(10, 60f, float.NaN)]
        [TestCase(10, 60f, float.PositiveInfinity)]
        public void SpecRejectsInvalidValues(int scoreLimit, float timeLimitSeconds, float respawnSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamDeathmatchSpec(scoreLimit, timeLimitSeconds, respawnSeconds));
        }
    }
}
