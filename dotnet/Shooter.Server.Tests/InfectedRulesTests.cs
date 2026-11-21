using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 試合を通した流れは MatchRulesInfectedTests で確かめ、ここは Session の無いときの値と引数を確かめる
    public sealed class InfectedRulesTests
    {
        private StringWriter _log;
        private LogHost _host;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _host = new LogHost(_log);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void TheObjectiveKeepsItsScoreAndKeepsTheSessionFromEndingFirst()
        {
            var settings = new NetMatchSettings(respawnSeconds: 9f, infected: new InfectedSpec(startDelaySeconds: 4f, roundSeconds: 120f, infectedRespawnSeconds: 2f));
            var rules = new InfectedRules(settings, _host);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Infected));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.RemainingSeconds, Is.EqualTo(120f));
            Assert.That(rules.Tick(false, 1f), Is.False, "the round does not start before the match plays");
            Assert.That(rules.Infected.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(rules.DecideWinner(Team.Bravo), Is.EqualTo(Team.Bravo), "before the round ends the kill winner stands");
            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(2f), "the infected respawn, not the team deathmatch respawn");
        }

        [Test]
        public void BeforeTheOutbreakTheSnapshotCountsDownFromAtLeastOneSecond()
        {
            var rules = new InfectedRules(new NetMatchSettings(infected: new InfectedSpec(startDelaySeconds: 4.2f)), _host);
            var snapshot = new MatchSnapshot { AlphaScore = 7, BravoScore = 8, HasActivePoint = false };

            rules.FillMatch(ref snapshot);

            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Infected));
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(5));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.AlphaScore, Is.Zero, "the kill score is replaced by the infected score");
            Assert.That(snapshot.BravoScore, Is.Zero);

            var immediate = new InfectedRules(new NetMatchSettings(infected: new InfectedSpec(startDelaySeconds: 0f)), _host);
            immediate.FillMatch(ref snapshot);
            Assert.That(snapshot.ObjectiveRemainingSeconds, Is.EqualTo(1), "0 is kept for after the outbreak");
        }

        [Test]
        public void PlayersOutsideTheObjectiveAreJudgedByTheirMatchTeam()
        {
            var rules = new InfectedRules(NetMatchSettings.Default, _host);

            Assert.That(rules.AreHostile(1, Team.Alpha, 2, Team.Bravo), Is.True);
            Assert.That(rules.AreHostile(1, Team.Alpha, 3, Team.Alpha), Is.False);
            Assert.That(rules.AreHostile(1, Team.Alpha, 1, Team.Bravo), Is.False);
            Assert.That(rules.IsWinningPlayer(1, Team.Alpha, Team.Alpha), Is.True);
            Assert.That(rules.IsWinningPlayer(1, Team.Alpha, null), Is.False);

            var row = new PlayerSnapshot { PlayerId = 1, Team = Team.Bravo };
            rules.FillPlayer(1, ref row);
            Assert.That(row.Team, Is.EqualTo(Team.Bravo), "a row of someone the objective does not know keeps its team");
        }

        [Test]
        public void EventsBeforeAnyoneJoinsAreIgnored()
        {
            var rules = new InfectedRules(NetMatchSettings.Default, _host);

            Assert.DoesNotThrow(() => rules.PlayerJoined(1), "there is no Session yet");
            Assert.DoesNotThrow(() => rules.PlayerKilled(1, 2));
            Assert.DoesNotThrow(() => rules.PlayerLeft(2));
            Assert.That(rules.Infected.Session.PlayerCount, Is.Zero);
        }

        [Test]
        public void StartingLogsTheTuningAndTheInfectedBody()
        {
            var settings = new NetMatchSettings(infected: new InfectedSpec(2, 5f, 200f, 175, 1.25f, 4f, 2, 6, "claws"));
            var rules = new InfectedRules(settings, _host);

            rules.Start();
            rules.Start();
            rules.Stop();

            Assert.That(_log.ToString(), Is.EqualTo(
                "[Infected] net initialInfected=2 startDelay=5 roundSeconds=200 respawn=4 scorePerInfection=2 scorePerSurvivedRound=6" + Environment.NewLine
                + "[Infected] body maxHealth=175 speedMultiplier=1.25 weapon=claws" + Environment.NewLine));
        }

        [Test]
        public void AnEmptyWeaponIdIsLoggedAsKeepingTheLoadout()
        {
            var rules = new InfectedRules(NetMatchSettings.Default, _host);

            rules.Start();

            Assert.That(_log.ToString(), Does.Contain("[Infected] body maxHealth=150 speedMultiplier=1.15 weapon=loadout"));
        }

        [Test]
        public void NoOneIsInfectedBeforeJoiningTheObjective()
        {
            var rules = new InfectedRules(NetMatchSettings.Default, _host);

            Assert.That(rules.IsInfected(1), Is.False);
        }

        [Test]
        public void TheSettingsAndTheHostAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new InfectedRules(null, _host));
            Assert.Throws<ArgumentNullException>(() => new InfectedRules(NetMatchSettings.Default, null));
            var rules = new InfectedRules(NetMatchSettings.Default, _host);
            Assert.Throws<ArgumentNullException>(() => rules.CreateSessionSpec(GameRules.Default, null));
            Assert.Throws<ArgumentNullException>(() => rules.FillControlPoints(null));
            Assert.Throws<ArgumentNullException>(() => rules.FillObjectiveItems(null));
        }

        private sealed class LogHost : IMatchObjectiveHost
        {
            public LogHost(TextWriter log)
            {
                Log = log;
            }

            public TeamDeathmatchSession Session => null;

            public ServerBodies Bodies => null;

            public TextWriter Log { get; }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }
        }
    }
}
