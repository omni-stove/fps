using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TeamDeathmatchTests
    {
        private static readonly string[] _expectedStateChangedEvents =
            { "state Playing winner ", "state Ended winner Bravo", "ended Bravo" };

        private static readonly Team?[] _expectedWinnersAcrossReset = { Team.Alpha, Team.Bravo };

        private static readonly GameModeState[] _expectedStatesAcrossReset =
            { GameModeState.Playing, GameModeState.Ended, GameModeState.Waiting };

        private const int _alpha1 = 1;
        private const int _alpha2 = 2;
        private const int _bravo1 = 11;
        private const int _bravo2 = 12;

        private static GameRules RulesWith(RespawnMode respawnMode, float waveIntervalSeconds = GameRules.DefaultWaveIntervalSeconds)
        {
            return new GameRules(
                FriendlyFire.Off, respawnMode, waveIntervalSeconds, 0f, 0f, DeathBody.Vanish, DeathCamera.None, false, 1f, Destruction.Off);
        }

        private static TeamDeathmatch CreateStarted(
            int scoreLimit = 10, float timeLimitSeconds = 60f, float respawnSeconds = 3f, GameRules rules = null)
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(scoreLimit, timeLimitSeconds, respawnSeconds, rules));
            match.AddPlayer(_alpha1, Team.Alpha);
            match.AddPlayer(_alpha2, Team.Alpha);
            match.AddPlayer(_bravo1, Team.Bravo);
            match.AddPlayer(_bravo2, Team.Bravo);
            match.Start();
            return match;
        }

        [Test]
        public void StartsWaitingThenPlaying()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f));

            Assert.That(match.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(match.RemainingSeconds, Is.EqualTo(60f));

            match.Start();

            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.Throws<InvalidOperationException>(() => match.Start());
        }

        [Test]
        public void KillingEnemyScoresForKillerTeam()
        {
            var match = CreateStarted();

            match.ReportKill(_alpha1, _bravo1);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(_alpha1), Is.EqualTo(1));
            Assert.That(match.GetDeaths(_alpha1), Is.Zero);
            Assert.That(match.GetKills(_bravo1), Is.Zero);
            Assert.That(match.GetDeaths(_bravo1), Is.EqualTo(1));
        }

        [Test]
        public void TeamKillOnlyCountsDeath()
        {
            var match = CreateStarted();

            match.ReportKill(_alpha1, _alpha2);

            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(_alpha1), Is.Zero);
            Assert.That(match.GetDeaths(_alpha2), Is.EqualTo(1));
        }

        [Test]
        public void SuicideOnlyCountsDeath()
        {
            var match = CreateStarted();

            match.ReportKill(_bravo1, _bravo1);

            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(_bravo1), Is.Zero);
            Assert.That(match.GetDeaths(_bravo1), Is.EqualTo(1));
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.True);
        }

        [Test]
        public void ReachingScoreLimitWins()
        {
            var match = CreateStarted(scoreLimit: 2, respawnSeconds: 0f);
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);

            match.ReportKill(_bravo1, _alpha1);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));

            match.ReportKill(_bravo2, _alpha2);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(ended, Is.EqualTo(new Team?[] { Team.Bravo }));
        }

        [Test]
        public void TimeUpWinsForHigherScore()
        {
            var match = CreateStarted(timeLimitSeconds: 10f);
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);
            match.ReportKill(_alpha1, _bravo1);

            match.Tick(9f);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(match.RemainingSeconds, Is.EqualTo(1f));

            match.Tick(5f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
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
            match.ReportKill(_alpha1, _bravo1);
            match.ReportKill(_bravo2, _alpha2);

            match.Tick(10f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.Null);
            Assert.That(ended, Is.EqualTo(new Team?[] { null }));
        }

        [Test]
        public void RespawnReadyFiresAfterRespawnSeconds()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(_alpha1, _bravo1);

            match.Tick(1f);
            match.Tick(1.5f);
            Assert.That(respawned, Is.Empty);
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.True);

            match.Tick(0.5f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.False);

            match.Tick(10f);
            Assert.That(respawned, Has.Count.EqualTo(1));
        }

        [Test]
        public void RespawnReadyFiresInOrderOfDeath()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(_alpha1, _bravo2);
            match.Tick(1f);
            match.ReportKill(_alpha1, _bravo1);

            match.Tick(5f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo2, _bravo1 }));
        }

        [Test]
        public void ZeroRespawnSecondsFiresImmediately()
        {
            var match = CreateStarted(respawnSeconds: 0f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.ReportKill(_alpha1, _bravo1);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
        }

        [Test]
        public void ReportDeathCountsDeathAndWaitsForRespawnWithoutScoring()
        {
            var match = CreateStarted(respawnSeconds: 2f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.ReportDeath(_alpha1);
            match.ReportDeath(_alpha1);

            Assert.That(match.GetDeaths(_alpha1), Is.EqualTo(1));
            Assert.That(match.GetKills(_alpha1), Is.Zero);
            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.IsAwaitingRespawn(_alpha1), Is.True);

            match.ReportKill(_bravo1, _alpha1);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);

            match.Tick(2f);

            Assert.That(respawned, Is.EqualTo(new[] { _alpha1 }));
        }

        [Test]
        public void ReportDeathIsIgnoredWhileWaitingAndAfterEnd()
        {
            var waiting = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f));
            waiting.AddPlayer(_alpha1, Team.Alpha);

            waiting.ReportDeath(_alpha1);

            Assert.That(waiting.GetDeaths(_alpha1), Is.Zero);

            var ended = CreateStarted(scoreLimit: 1);
            ended.ReportKill(_alpha1, _bravo1);

            ended.ReportDeath(_alpha2);

            Assert.That(ended.GetDeaths(_alpha2), Is.Zero);
            Assert.That(ended.IsAwaitingRespawn(_alpha2), Is.False);
        }

        [Test]
        public void ReportDeathOfUnknownPlayerThrows()
        {
            var match = CreateStarted();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportDeath(99));
            Assert.That(ex.ParamName, Is.EqualTo("playerId"));
        }

        [Test]
        public void StateChangedFiresOnStartAndEndBeforeEnded()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(1, 60f, 3f));
            match.AddPlayer(_alpha1, Team.Alpha);
            match.AddPlayer(_bravo1, Team.Bravo);
            var events = new List<string>();
            match.StateChanged += state => events.Add($"state {state} winner {match.Winner}");
            match.Ended += winner => events.Add($"ended {winner}");

            match.Start();
            match.ReportKill(_bravo1, _alpha1);
            match.Tick(100f);

            Assert.That(events, Is.EqualTo(_expectedStateChangedEvents));
        }

        [Test]
        public void KillOfPlayerAwaitingRespawnIsIgnored()
        {
            var match = CreateStarted();
            match.ReportKill(_alpha1, _bravo1);

            match.ReportKill(_alpha2, _bravo1);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetKills(_alpha2), Is.Zero);
            Assert.That(match.GetDeaths(_bravo1), Is.EqualTo(1));
        }

        [Test]
        public void NothingChangesAfterEnd()
        {
            var match = CreateStarted(scoreLimit: 1, timeLimitSeconds: 10f, respawnSeconds: 1f);
            var ended = 0;
            var respawned = 0;
            match.Ended += _ => ended++;
            match.RespawnReady += _ => respawned++;
            match.ReportKill(_alpha1, _bravo1);
            match.Tick(1f);

            match.ReportKill(_alpha1, _bravo2);
            match.ReportKill(_bravo1, _alpha1);
            match.Tick(1f);
            match.Tick(100f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.GetKills(_alpha1), Is.EqualTo(1));
            Assert.That(match.GetDeaths(_bravo2), Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(respawned, Is.Zero);
        }

        [Test]
        public void ResetAfterEndRestoresInitialScoresKillsAndTime()
        {
            var match = CreateStarted(scoreLimit: 2, timeLimitSeconds: 30f, respawnSeconds: 1f);
            match.Tick(4f);
            match.ReportKill(_alpha1, _bravo1);
            match.ReportKill(_bravo2, _alpha2);
            match.ReportKill(_alpha2, _bravo2);
            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));

            match.Reset();

            Assert.That(match.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(match.Winner, Is.Null);
            Assert.That(match.RemainingSeconds, Is.EqualTo(30f));
            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            foreach (var id in new[] { _alpha1, _alpha2, _bravo1, _bravo2 })
            {
                Assert.That(match.GetKills(id), Is.Zero, "kills of " + id);
                Assert.That(match.GetDeaths(id), Is.Zero, "deaths of " + id);
                Assert.That(match.IsAwaitingRespawn(id), Is.False, "respawn of " + id);
            }
        }

        [Test]
        public void ResetKeepsPlayersAndTeams()
        {
            var match = CreateStarted(scoreLimit: 1);
            match.ReportKill(_bravo1, _alpha1);

            match.Reset();

            Assert.That(match.PlayerCount, Is.EqualTo(4));
            Assert.That(match.GetTeam(_alpha1), Is.EqualTo(Team.Alpha));
            Assert.That(match.GetTeam(_alpha2), Is.EqualTo(Team.Alpha));
            Assert.That(match.GetTeam(_bravo1), Is.EqualTo(Team.Bravo));
            Assert.That(match.GetTeam(_bravo2), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void ResetMatchCanBePlayedAndEndedAgain()
        {
            var match = CreateStarted(scoreLimit: 1, timeLimitSeconds: 10f);
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);
            match.ReportKill(_alpha1, _bravo1);

            match.Reset();
            match.Start();
            match.ReportKill(_bravo1, _alpha1);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetScore(Team.Bravo), Is.EqualTo(1));
            Assert.That(ended, Is.EqualTo(_expectedWinnersAcrossReset));
        }

        [Test]
        public void ResetCancelsPendingRespawnWhilePlaying()
        {
            var match = CreateStarted(respawnSeconds: 2f);
            var respawned = 0;
            match.RespawnReady += _ => respawned++;
            match.ReportKill(_alpha1, _bravo1);

            match.Reset();
            match.Start();
            match.Tick(5f);

            Assert.That(respawned, Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(55f));
        }

        [Test]
        public void ResetFiresStateChangedOnlyWhenStateChanges()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(1, 10f, 0f));
            match.AddPlayer(_alpha1, Team.Alpha);
            match.AddPlayer(_bravo1, Team.Bravo);
            var states = new List<GameModeState>();
            match.StateChanged += states.Add;

            match.Reset();
            Assert.That(states, Is.Empty);

            match.Start();
            match.ReportKill(_alpha1, _bravo1);
            match.Reset();

            Assert.That(states, Is.EqualTo(_expectedStatesAcrossReset));
        }

        [Test]
        public void KillsWhileWaitingAreIgnored()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f));
            match.AddPlayer(_alpha1, Team.Alpha);
            match.AddPlayer(_bravo1, Team.Bravo);

            match.ReportKill(_alpha1, _bravo1);
            match.Tick(100f);

            Assert.That(match.GetScore(Team.Alpha), Is.Zero);
            Assert.That(match.GetDeaths(_bravo1), Is.Zero);
            Assert.That(match.RemainingSeconds, Is.EqualTo(60f));
            Assert.That(match.State, Is.EqualTo(GameModeState.Waiting));
        }

        [Test]
        public void RemovedPlayerDoesNotRespawn()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(_alpha1, _bravo1);
            match.ReportKill(_alpha1, _bravo2);
            match.Tick(1f);

            match.RemovePlayer(_bravo1);
            match.Tick(5f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo2 }));
            Assert.That(match.HasPlayer(_bravo1), Is.False);
            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(2));
        }

        [Test]
        public void RejoinedPlayerDoesNotInheritRespawn()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(_alpha1, _bravo1);

            match.RemovePlayer(_bravo1);
            match.AddPlayer(_bravo1, Team.Bravo);
            match.Tick(5f);

            Assert.That(respawned, Is.Empty);
            Assert.That(match.GetDeaths(_bravo1), Is.Zero);
        }

        [Test]
        public void PlayerRemovedInRespawnHandlerDoesNotRespawn()
        {
            var match = CreateStarted(respawnSeconds: 3f);
            var respawned = new List<int>();
            match.RespawnReady += id =>
            {
                respawned.Add(id);
                if (id == _bravo1)
                {
                    match.RemovePlayer(_bravo2);
                }
            };
            match.ReportKill(_alpha1, _bravo1);
            match.ReportKill(_alpha1, _bravo2);

            match.Tick(3f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
        }

        [Test]
        public void DefaultRulesUseIndividualRespawn()
        {
            var match = CreateStarted();

            Assert.That(match.Spec.Rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(match.SecondsUntilNextWave, Is.Null);
        }

        [Test]
        public void IndividualRespawnRemainingCountsDownFromDeath()
        {
            var match = CreateStarted(respawnSeconds: 3f);

            Assert.That(match.GetRespawnRemaining(_bravo1), Is.Null);

            match.ReportKill(_alpha1, _bravo1);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(3f));

            match.Tick(2f);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(1f));

            match.Tick(1f);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.Null);
        }

        [Test]
        public void WaveRespawnsEveryoneReadyAtTheWaveTogether()
        {
            var match = CreateStarted(respawnSeconds: 3f, rules: RulesWith(RespawnMode.Wave, 10f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.Tick(2f);
            match.ReportKill(_alpha1, _bravo1);
            match.Tick(3f);
            match.ReportKill(_alpha1, _bravo2);

            match.Tick(4f);
            Assert.That(respawned, Is.Empty);
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.True);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(1f));
            Assert.That(match.GetRespawnRemaining(_bravo2), Is.EqualTo(1f));

            match.Tick(1f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo1, _bravo2 }));
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.False);
            Assert.That(match.IsAwaitingRespawn(_bravo2), Is.False);
        }

        [Test]
        public void WaveSkipsPlayersWhoHaveNotWaitedRespawnSeconds()
        {
            var match = CreateStarted(respawnSeconds: 3f, rules: RulesWith(RespawnMode.Wave, 10f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.Tick(8f);
            match.ReportKill(_alpha1, _bravo1);

            match.Tick(2f);
            Assert.That(respawned, Is.Empty);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(10f));

            match.Tick(9f);
            Assert.That(respawned, Is.Empty);

            match.Tick(1f);
            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
        }

        [Test]
        public void WaveRespawnsPlayerWhoseWaitEndsExactlyAtTheWave()
        {
            var match = CreateStarted(respawnSeconds: 3f, rules: RulesWith(RespawnMode.Wave, 10f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.Tick(7f);
            match.ReportKill(_alpha1, _bravo1);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(3f));

            match.Tick(3f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
        }

        [Test]
        public void WaveKillAtTheWaveTimeWaitsForTheNextWave()
        {
            var match = CreateStarted(respawnSeconds: 0f, rules: RulesWith(RespawnMode.Wave, 10f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.Tick(10f);
            match.ReportKill(_alpha1, _bravo1);

            Assert.That(respawned, Is.Empty);
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.True);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(10f));

            match.Tick(9f);
            Assert.That(respawned, Is.Empty);

            match.Tick(1f);
            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
        }

        [Test]
        public void WaveWithZeroRespawnSecondsStillWaitsForTheWave()
        {
            var match = CreateStarted(respawnSeconds: 0f, rules: RulesWith(RespawnMode.Wave, 10f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.ReportKill(_alpha1, _bravo1);

            Assert.That(respawned, Is.Empty);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(10f));
        }

        [Test]
        public void WaveLongTickReleasesAtTheFirstWaveTheWaitHasEnded()
        {
            var match = CreateStarted(respawnSeconds: 7f, rules: RulesWith(RespawnMode.Wave, 5f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.ReportKill(_alpha1, _bravo1);

            match.Tick(12f);

            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
            Assert.That(match.SecondsUntilNextWave, Is.EqualTo(3f));
        }

        [Test]
        public void WavePlayerKilledInRespawnHandlerWaitsFromTheEndOfTheTick()
        {
            var match = CreateStarted(respawnSeconds: 3f, rules: RulesWith(RespawnMode.Wave, 5f));
            var respawned = new List<int>();
            match.RespawnReady += id =>
            {
                respawned.Add(id);
                if (respawned.Count == 1)
                {
                    match.ReportKill(_alpha1, id);
                }
            };
            match.ReportKill(_alpha1, _bravo1);

            // 5 秒の波で戻ってすぐ倒され、10 秒の波は待ちが足りずに見送る
            match.Tick(12f);
            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.EqualTo(3f));

            match.Tick(3f);
            Assert.That(respawned, Is.EqualTo(new[] { _bravo1, _bravo1 }));
        }

        [Test]
        public void SecondsUntilNextWaveCountsFromMatchStart()
        {
            var match = new TeamDeathmatch(new TeamDeathmatchSpec(10, 60f, 3f, RulesWith(RespawnMode.Wave, 10f)));
            match.AddPlayer(_alpha1, Team.Alpha);
            match.AddPlayer(_bravo1, Team.Bravo);

            Assert.That(match.SecondsUntilNextWave, Is.Null);

            match.Start();
            Assert.That(match.SecondsUntilNextWave, Is.EqualTo(10f));

            match.Tick(3f);
            Assert.That(match.SecondsUntilNextWave, Is.EqualTo(7f));

            match.Tick(7f);
            Assert.That(match.SecondsUntilNextWave, Is.EqualTo(10f));
        }

        [Test]
        public void ResetRestartsWaveTimes()
        {
            var match = CreateStarted(respawnSeconds: 0f, rules: RulesWith(RespawnMode.Wave, 10f));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);
            match.Tick(8f);
            match.ReportKill(_alpha1, _bravo1);

            match.Reset();
            match.Start();

            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.False);
            Assert.That(match.SecondsUntilNextWave, Is.EqualTo(10f));

            match.ReportKill(_alpha1, _bravo1);
            match.Tick(2f);
            Assert.That(respawned, Is.Empty);

            match.Tick(8f);
            Assert.That(respawned, Is.EqualTo(new[] { _bravo1 }));
        }

        [Test]
        public void NoneNeverRespawnsDuringTheMatch()
        {
            var match = CreateStarted(respawnSeconds: 0f, rules: RulesWith(RespawnMode.None));
            var respawned = new List<int>();
            match.RespawnReady += id => respawned.Add(id);

            match.ReportKill(_alpha1, _bravo1);
            match.Tick(30f);

            Assert.That(respawned, Is.Empty);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(match.IsEliminated(_bravo1), Is.True);
            Assert.That(match.IsAwaitingRespawn(_bravo1), Is.False);
            Assert.That(match.GetRespawnRemaining(_bravo1), Is.Null);
            Assert.That(match.IsEliminated(_bravo2), Is.False);

            match.ReportKill(_alpha1, _bravo1);
            match.ReportDeath(_bravo1);
            Assert.That(match.GetDeaths(_bravo1), Is.EqualTo(1));
            Assert.That(match.GetKills(_alpha1), Is.EqualTo(1));
        }

        [Test]
        public void NoneWipeOutEndsAtTheNextTickWithOpponentWinning()
        {
            var match = CreateStarted(rules: RulesWith(RespawnMode.None));
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);

            match.ReportKill(_bravo1, _alpha1);
            match.ReportDeath(_alpha2);
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));

            match.Tick(0f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(ended, Is.EqualTo(new Team?[] { Team.Bravo }));
            Assert.That(match.IsEliminated(_alpha1), Is.False);
        }

        [Test]
        public void NoneWipeOutWinsEvenIfTheOpponentHasMorePoints()
        {
            var match = CreateStarted(rules: RulesWith(RespawnMode.None));
            match.ReportKill(_alpha1, _bravo1);
            match.ReportDeath(_alpha1);
            match.ReportDeath(_alpha2);

            match.Tick(1f);

            Assert.That(match.GetScore(Team.Alpha), Is.EqualTo(1));
            Assert.That(match.GetScore(Team.Bravo), Is.Zero);
            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void NoneSimultaneousWipeOutIsDraw()
        {
            var match = CreateStarted(rules: RulesWith(RespawnMode.None));
            var ended = new List<Team?>();
            match.Ended += winner => ended.Add(winner);

            // 最後の 2 人が同じ Tick の間に相打ちになった
            match.ReportKill(_alpha1, _bravo1);
            match.ReportKill(_bravo2, _alpha2);
            match.ReportKill(_alpha1, _bravo2);
            match.ReportKill(_bravo2, _alpha1);

            match.Tick(0.5f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.Null);
            Assert.That(ended, Is.EqualTo(new Team?[] { null }));
        }

        [Test]
        public void NoneWipeOutIsCheckedBeforeTimeUp()
        {
            var match = CreateStarted(timeLimitSeconds: 60f, rules: RulesWith(RespawnMode.None));
            match.ReportDeath(_bravo1);
            match.ReportDeath(_bravo2);

            match.Tick(60f);

            Assert.That(match.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(match.RemainingSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void NoneLeavingLastSurvivorWipesOutTheTeam()
        {
            var match = CreateStarted(rules: RulesWith(RespawnMode.None));
            match.ReportKill(_alpha1, _bravo1);

            match.RemovePlayer(_bravo2);
            match.Tick(0f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void NoneEmptyTeamIsNotWipedOut()
        {
            var match = CreateStarted(rules: RulesWith(RespawnMode.None));

            match.RemovePlayer(_bravo1);
            match.RemovePlayer(_bravo2);
            match.Tick(1f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));

            match.ReportDeath(_alpha1);
            match.ReportDeath(_alpha2);
            match.Tick(0f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.Winner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void NoneResetRevivesEliminatedPlayers()
        {
            var match = CreateStarted(rules: RulesWith(RespawnMode.None));
            match.ReportDeath(_bravo1);
            match.ReportDeath(_bravo2);
            match.Tick(0f);

            match.Reset();
            match.Start();
            match.Tick(1f);

            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(match.IsEliminated(_bravo1), Is.False);
            Assert.That(match.IsEliminated(_bravo2), Is.False);
        }

        [Test]
        public void UnknownPlayerThrows()
        {
            var match = CreateStarted();

            Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportKill(99, _bravo1));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportKill(_alpha1, 99));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.RemovePlayer(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.GetKills(99));
        }

        [Test]
        public void UnknownPlayerThrowsEvenAfterEnd()
        {
            var match = CreateStarted(scoreLimit: 1);
            match.ReportKill(_alpha1, _bravo1);

            Assert.Throws<ArgumentOutOfRangeException>(() => match.ReportKill(99, _bravo1));
        }

        [Test]
        public void RejectsDuplicateAndInvalidPlayers()
        {
            var match = CreateStarted();

            Assert.Throws<ArgumentException>(() => match.AddPlayer(_alpha1, Team.Bravo));
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
