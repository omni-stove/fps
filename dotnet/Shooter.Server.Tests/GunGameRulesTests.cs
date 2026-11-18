using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class GunGameRulesTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _knifeId = "3e9a7c41-5b2d-4f08-a6c3-9d1e8b7f2a54";

        private StringWriter _log;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void AGunGameMatchRunsUntilSomeoneClimbsTheLadderAndTheirTeamIsSentAsTheWinner()
        {
            var match = new GunGameMatch(Settings(), Rules(FriendlyFire.On), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Join(3, Team.Alpha);
            Assert.That(_log.ToString(), Does.Contain(
                "[Mode] gungame net tiers=2 timeLimit=600s respawn=0.5s melee=1 demotesOnMelee=true friendlyFire=On"));
            match.Step();
            Assert.That(match.Session.State, Is.EqualTo(GameModeState.Playing));

            Assert.That(match.Kill(1, 2, TestRules.HandgunId), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(match.Rules.GunGame.GetTier(1), Is.Zero, "a kill with a later tier's weapon does not climb");
            Assert.That(match.Rules.WeaponIdFor(1), Is.EqualTo(TestRules.RifleId));

            match.Advance(0.6f);
            Assert.That(match.Kill(1, 2, TestRules.RifleId), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(match.Rules.GunGame.GetTier(1), Is.EqualTo(1));
            Assert.That(match.Rules.WeaponIdFor(1), Is.EqualTo(TestRules.HandgunId));
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame promote player=1 tier=2/2 weapon=" + TestRules.HandgunId));

            match.Advance(0.6f);
            Assert.That(match.Kill(1, 3, TestRules.HandgunId), Is.EqualTo(DamageOutcome.Killed), "everyone is on their own side");
            Assert.That(match.Rules.GunGame.FinisherId, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame finish player=1 weapon=" + TestRules.HandgunId));
            Assert.That(match.Session.State, Is.EqualTo(GameModeState.Playing), "the match ends on the next tick");

            match.Step();

            Assert.That(match.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.EndedWith, Is.EqualTo(Team.Alpha));
            Assert.That(match.Rules.DecideWinner(Team.Bravo), Is.EqualTo(Team.Alpha), "the kill count does not decide");
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame ended by=finish winner=1 team=Alpha"));

            var snapshot = match.Snapshot(1, 2, 3);
            Assert.That(snapshot.Match.Objective, Is.EqualTo(MatchObjective.GunGame));
            Assert.That(snapshot.Match.AlphaPoints, Is.Zero);
            Assert.That(snapshot.Match.BravoPoints, Is.Zero);
            Assert.That(snapshot.Players[0].GunGameTier, Is.EqualTo(2), "the finisher is sent at the tier count");
            Assert.That(snapshot.Players[1].GunGameTier, Is.Zero);
            Assert.That(snapshot.Players[2].GunGameTier, Is.Zero);
            Assert.That(snapshot.ObjectiveItems.Count, Is.Zero, "a gun game has no objective items");
        }

        [Test]
        public void OnlyTheFinisherCountsAsTheWinnerNotTheirWholeTeam()
        {
            var match = new GunGameMatch(Settings(), Rules(FriendlyFire.On), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Join(3, Team.Alpha);
            match.Step();

            Assert.That(match.Kill(1, 2, TestRules.RifleId), Is.EqualTo(DamageOutcome.Killed));
            match.Advance(0.6f);
            Assert.That(match.Kill(1, 3, TestRules.HandgunId), Is.EqualTo(DamageOutcome.Killed));
            match.Step();

            Assert.That(match.EndedWith, Is.EqualTo(Team.Alpha));
            IMatchObjectiveRules rules = match.Rules;
            Assert.That(rules.IsWinningPlayer(1, Team.Alpha, Team.Alpha), Is.True);
            Assert.That(rules.IsWinningPlayer(3, Team.Alpha, Team.Alpha), Is.False, "a teammate of the finisher did not win");
            Assert.That(rules.IsWinningPlayer(2, Team.Bravo, Team.Alpha), Is.False);
        }

        [Test]
        public void TheNextMatchStartsEveryoneFromTheFirstTier()
        {
            var match = new GunGameMatch(Settings(), Rules(FriendlyFire.Off), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Step();
            match.Kill(1, 2, TestRules.RifleId);
            match.Advance(0.6f);
            match.Kill(1, 2, TestRules.HandgunId);
            match.Step();
            Assert.That(match.Session.State, Is.EqualTo(GameModeState.Ended));

            match.Session.Restart();
            match.Step();

            Assert.That(match.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(match.Rules.GunGame.GetTier(1), Is.Zero);
            Assert.That(match.Rules.GunGame.FinisherId, Is.Null);
            Assert.That(match.Rules.DecideWinner(null), Is.Null, "nobody leads a fresh ladder");
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame restart"));

            match.Kill(2, 1, TestRules.RifleId);
            Assert.That(match.Rules.GunGame.GetTier(2), Is.EqualTo(1), "kills climb again in the next match");
        }

        [Test]
        public void AMeleeKillSendsTheVictimDownATier()
        {
            var match = new GunGameMatch(Settings(), Rules(FriendlyFire.Off), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Step();
            match.Kill(2, 1, TestRules.RifleId);
            match.Advance(0.6f);
            Assert.That(match.Rules.GunGame.GetTier(2), Is.EqualTo(1));

            match.Kill(1, 2, _knifeId);

            Assert.That(match.Rules.GunGame.GetTier(2), Is.Zero);
            Assert.That(match.Rules.GunGame.GetTier(1), Is.Zero, "the knife is not on the ladder");
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame demote player=2 tier=1/2 weapon=" + TestRules.RifleId));
        }

        [Test]
        public void AtTimeUpTheHighestTierWinsAndATieIsADraw()
        {
            var match = new GunGameMatch(Settings(timeLimitSeconds: 2f), Rules(FriendlyFire.Off), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Step();
            match.Kill(2, 1, TestRules.RifleId);

            match.Advance(2.1f);

            Assert.That(match.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(match.EndedWith, Is.EqualTo(Team.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame ended by=time winner=2 team=Bravo"));

            var draw = new GunGameMatch(Settings(timeLimitSeconds: 2f), Rules(FriendlyFire.Off), _log);
            draw.Join(1, Team.Alpha);
            draw.Join(2, Team.Bravo);
            draw.Advance(2.1f);

            Assert.That(draw.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(draw.EndedWith, Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Mode] gungame ended by=time winner=draw team=-"));
        }

        [Test]
        public void TheWinnerStaysTheSameWhenTheyLeaveAfterTheMatchEnded()
        {
            var match = new GunGameMatch(Settings(timeLimitSeconds: 2f), Rules(FriendlyFire.Off), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Join(3, Team.Alpha);
            match.Step();
            match.Kill(2, 1, TestRules.RifleId);
            match.Advance(2.1f);
            Assert.That(match.EndedWith, Is.EqualTo(Team.Bravo));

            match.Leave(2);

            Assert.That(match.Rules.DecideWinner(Team.Alpha), Is.Null, "the winner left, so no team is sent as the winner");
        }

        [Test]
        public void KillsOfPlayersTheRulesDoNotKnowAreIgnored()
        {
            var match = new GunGameMatch(Settings(), Rules(FriendlyFire.Off), _log);
            match.Join(1, Team.Alpha);
            match.Join(2, Team.Bravo);
            match.Step();

            match.Rules.CreditKill(new KillFeedEntry(1, 9, TestRules.RifleId, false, false));
            match.Rules.CreditKill(new KillFeedEntry(1, 1, TestRules.RifleId, false, false));

            Assert.That(match.Rules.GunGame.GetTier(1), Is.Zero);
        }

        [Test]
        public void TheRulesTakeTheTimeAndRespawnFromTheGunGameAndNeverEndOnKills()
        {
            var settings = Settings(timeLimitSeconds: 480f);
            var rules = new GunGameRules(settings, new GunGameMatch.NoSessionHost(_log));

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.GunGame));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.False);
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.RemainingSeconds, Is.Null);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.Tick(false, 1f), Is.False);
            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(480f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.RespawnsByRound, Is.False);
            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Empty", TestRules.Points()), TestRules.Points()));
            Assert.That(rules.DecideWinner(Team.Alpha), Is.Null, "without a session there is no team to send");
            var points = new SnapshotList<ControlPointSnapshot>(4, "control points");
            points.Add();
            rules.FillControlPoints(points);
            Assert.That(points.Count, Is.Zero);
        }

        [Test]
        public void AGunGameWithoutTiersIsRefused()
        {
            var e = Assert.Throws<InvalidOperationException>(
                () => new GunGameRules(new NetMatchSettings(), new GunGameMatch.NoSessionHost(_log)));

            Assert.That(e.Message, Is.EqualTo("cannot start the GunGame match: the gun game spec has no tier weapon ids"));
            Assert.Throws<ArgumentNullException>(() => new GunGameRules(null, new GunGameMatch.NoSessionHost(_log)));
            Assert.Throws<ArgumentNullException>(() => new GunGameRules(Settings(), null));
        }

        private static NetMatchSettings Settings(float timeLimitSeconds = 600f)
        {
            return new NetMatchSettings(
                gunGame: new GunGameSpec(
                    timeLimitSeconds, 0.5f, new[] { TestRules.RifleId, TestRules.HandgunId }, new[] { _knifeId }, demotesOnMelee: true));
        }

        private static GameRules Rules(FriendlyFire friendlyFire)
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithFriendlyFire(friendlyFire)
                .WithObjective(MatchObjective.GunGame);
        }

        // MatchRulesModule と同じ順で Session と目的をつなぐ。ServerObjectiveFactory にまだ登録していないので、MatchRulesModule は使えない
        private sealed class GunGameMatch : IMatchObjectiveHost
        {
            private string _weaponId = string.Empty;

            public GunGameMatch(NetMatchSettings settings, GameRules rules, TextWriter log)
            {
                Log = log;
                Rules = new GunGameRules(settings, this);
                var points = TestRules.Points();
                Rules.UseMap(TestMaps.Data("NetTest", points), points);
                Session = new TeamDeathmatchSession(
                    Rules.CreateSessionSpec(rules, settings),
                    settings.MaxHealth,
                    points.SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    _ => Vector3.Zero);
                Session.Restarted += Rules.MatchRestarted;
                Session.Killed += (killerId, victimId, assisterIds) =>
                {
                    Rules.PlayerKilled(killerId, victimId);
                    var friendly = killerId != victimId && Session.GetTeam(killerId) == Session.GetTeam(victimId);
                    Rules.CreditKill(new KillFeedEntry(killerId, victimId, _weaponId, false, friendly, assisterIds));
                };
                Session.Ended += winner =>
                {
                    EndedWith = Rules.DecideWinner(winner);
                    Rules.MatchEnded();
                };
                Rules.Start();
            }

            public GunGameRules Rules { get; }

            public TeamDeathmatchSession Session { get; }

            public ServerBodies Bodies => null;

            public TextWriter Log { get; }

            public Team? EndedWith { get; private set; }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }

            public void Join(int playerId, Team team)
            {
                Session.Join(playerId, null, team);
                Rules.PlayerJoined(playerId);
            }

            public void Leave(int playerId)
            {
                Rules.PlayerLeft(playerId);
                Session.Leave(playerId);
            }

            public DamageOutcome Kill(int killerId, int victimId, string weaponId)
            {
                _weaponId = weaponId;
                try
                {
                    return Session.ApplyDamage(killerId, victimId, Session.MaxHealth);
                }
                finally
                {
                    _weaponId = string.Empty;
                }
            }

            public void Step()
            {
                Session.Tick(_tickSeconds);
                if (Rules.Tick(Session.State == GameModeState.Playing, _tickSeconds))
                {
                    Session.Finish(Rules.DecideWinner(Session.Winner));
                }
            }

            public void Advance(float seconds)
            {
                for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
                {
                    Step();
                }
            }

            public SnapshotMessage Snapshot(params int[] playerIds)
            {
                var snapshot = new SnapshotMessage();
                foreach (var playerId in playerIds)
                {
                    ref var row = ref snapshot.Players.Add();
                    row.PlayerId = playerId;
                }

                Rules.FillMatch(ref snapshot.Match);
                Rules.Contribute(snapshot, null);
                return snapshot;
            }

            // Session を作る前の目的だけを確かめる
            internal sealed class NoSessionHost : IMatchObjectiveHost
            {
                public NoSessionHost(TextWriter log)
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
}
